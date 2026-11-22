using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Laboratorio 10: el servidor calcula el movimiento; el propietario predice
/// únicamente su representación visual y concilia con el último tick confirmado.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PredictedPlayerMovement : NetworkBehaviour
{
    private struct MoveInput : INetworkSerializable
    {
        public uint Tick;
        public float Horizontal;
        public float Vertical;

        public MoveInput(uint tick, float horizontal, float vertical)
        {
            Tick = tick;
            Horizontal = horizontal;
            Vertical = vertical;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref Horizontal);
            serializer.SerializeValue(ref Vertical);
        }
    }

    private struct AuthoritativeState : INetworkSerializable
    {
        public Vector3 Position;
        public uint LastProcessedTick;

        public AuthoritativeState(Vector3 position, uint lastProcessedTick)
        {
            Position = position;
            LastProcessedTick = lastProcessedTick;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref LastProcessedTick);
        }
    }

    [Header("Predicción y reconciliación")]
    [SerializeField] private float speed = 5f;
    [SerializeField, Min(10f)] private float tickRate = 30f;
    [SerializeField] private float correctionSmoothing = 14f;
    [SerializeField] private float errorThreshold = 0.02f;
    [SerializeField] private bool showServerGhost = true;
    [SerializeField] private bool forceDesyncZone = true;
    [SerializeField] private float desyncWallX = 4f;

    private readonly NetworkVariable<AuthoritativeState> serverState = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly List<MoveInput> pendingInputs = new();

    private uint localTick;
    private float tickAccumulator;
    private bool localPredictionEnabled = true;
    private Vector3 predictedPosition;
    private Vector3 visualVelocity;
    private GameObject predictedVisual;
    private Renderer rootRenderer;
    private NetworkTransform networkTransform;

    private void Awake()
    {
        rootRenderer = GetComponent<Renderer>();
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        // Esta práctica transmite la posición exclusivamente mediante serverState.
        if (networkTransform != null) networkTransform.enabled = false;
        serverState.OnValueChanged += OnServerStateChanged;

        if (IsServer)
            serverState.Value = new AuthoritativeState(transform.position, 0);

        if (IsOwner)
        {
            predictedPosition = serverState.Value.Position;
            CreatePredictionVisual();
        }
        else
        {
            transform.position = serverState.Value.Position;
        }
    }

    public override void OnNetworkDespawn()
    {
        serverState.OnValueChanged -= OnServerStateChanged;
        if (predictedVisual != null) Destroy(predictedVisual);
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            if (Input.GetKeyDown(KeyCode.P))
                localPredictionEnabled = !localPredictionEnabled;
            // Cada entrada se genera a una cadencia fija y lleva su número de tick.
            // Así cliente y servidor ejecutan exactamente la misma simulación.
            float tickDuration = 1f / tickRate;
            tickAccumulator += Time.deltaTime;
            while (tickAccumulator >= tickDuration)
            {
                tickAccumulator -= tickDuration;
                SimulateOwnerTick();
            }
            UpdatePredictedVisual();
        }
        else if (!IsServer)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, serverState.Value.Position, ref visualVelocity, 0.08f);
        }
    }

    private void SimulateOwnerTick()
    {
        localTick++;
        MoveInput input = new(localTick, Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        pendingInputs.Add(input);

        if (localPredictionEnabled)
            predictedPosition = Simulate(predictedPosition, input);

        SubmitInputRpc(input.Tick, input.Horizontal, input.Vertical);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitInputRpc(uint tick, float horizontal, float vertical)
    {
        MoveInput input = new(tick, Mathf.Clamp(horizontal, -1f, 1f), Mathf.Clamp(vertical, -1f, 1f));
        AuthoritativeState current = serverState.Value;
        if (tick <= current.LastProcessedTick) return;

        Vector3 nextPosition = Simulate(current.Position, input);
        // Zona de prueba: produce una corrección visible sin confiar en el cliente.
        if (forceDesyncZone && nextPosition.x > desyncWallX)
            nextPosition.x = desyncWallX;

        serverState.Value = new AuthoritativeState(nextPosition, tick);
        transform.position = nextPosition;
    }

    private Vector3 Simulate(Vector3 position, MoveInput input)
    {
        Vector3 direction = new(input.Horizontal, 0f, input.Vertical);
        if (direction.sqrMagnitude > 1f) direction.Normalize();
        return position + direction * (speed / tickRate);
    }

    private void OnServerStateChanged(AuthoritativeState previous, AuthoritativeState current)
    {
        if (!IsOwner) return;

        // El objeto raíz es la copia autoritativa. El cubo visual es la predicción local.
        transform.position = current.Position;

        int removeCount = 0;
        while (removeCount < pendingInputs.Count && pendingInputs[removeCount].Tick <= current.LastProcessedTick)
            removeCount++;
        if (removeCount > 0) pendingInputs.RemoveRange(0, removeCount);

        Vector3 reconciled = current.Position;
        foreach (MoveInput pending in pendingInputs)
            reconciled = Simulate(reconciled, pending);

        float error = Vector3.Distance(predictedPosition, reconciled);
        predictedPosition = reconciled;
        if (error > errorThreshold)
            Debug.Log($"[Lab 10] Reconciliación tick {current.LastProcessedTick}: error {error:F3}, re-simuladas {pendingInputs.Count} entradas.");
    }

    private void CreatePredictionVisual()
    {
        if (predictedVisual != null) return;
        if (rootRenderer != null) rootRenderer.enabled = showServerGhost;

        predictedVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        predictedVisual.name = "VisualPredicho_Local";
        predictedVisual.transform.position = predictedPosition;
        predictedVisual.transform.localScale = transform.localScale;
        Collider collider = predictedVisual.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = predictedVisual.GetComponent<Renderer>();
        renderer.material.color = new Color(0.2f, 0.85f, 1f);
    }

    private void UpdatePredictedVisual()
    {
        if (predictedVisual == null) return;
        Vector3 target = localPredictionEnabled ? predictedPosition : serverState.Value.Position;
        predictedVisual.transform.position = Vector3.SmoothDamp(
            predictedVisual.transform.position, target, ref visualVelocity,
            localPredictionEnabled ? 1f / correctionSmoothing : 0.12f);
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned) return;
        string mode = localPredictionEnabled ? "PREDICCIÓN ACTIVADA" : "SIN PREDICCIÓN";
        string zone = forceDesyncZone ? $"Zona de corrección: X > {desyncWallX:F0}" : "Zona de corrección desactivada";
        GUIStyle style = new(GUI.skin.box)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = localPredictionEnabled ? new Color(0.3f, 0.9f, 1f) : Color.yellow }
        };
        GUI.Box(new Rect(Screen.width - 325f, 84f, 305f, 88f),
            $"LAB 10 · {mode}\nP: comparar modos\n{zone}\nPendientes: {pendingInputs.Count}", style);
    }
}
