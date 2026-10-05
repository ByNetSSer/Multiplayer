using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Cofre con bloqueo exclusivo. Mantener E reserva el cofre en el servidor.
/// </summary>
public class ChestController : NetworkBehaviour
{
    [SerializeField] private NetworkObject cubePrefab;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float useRange = 3f;
    [SerializeField] private int cubeCount = 4;

    public readonly NetworkVariable<ulong> CurrentOpener = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<float> OpenProgress = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<bool> IsOpen = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Transform lid;

    public override void OnNetworkSpawn()
    {
        BuildVisuals();
        IsOpen.OnValueChanged += OnOpenChanged;
        OpenProgress.OnValueChanged += OnProgressChanged;
        ApplyVisualState();
    }

    public override void OnNetworkDespawn()
    {
        IsOpen.OnValueChanged -= OnOpenChanged;
        OpenProgress.OnValueChanged -= OnProgressChanged;
    }

    private void Update()
    {
        if (!IsServer || IsOpen.Value || CurrentOpener.Value == ulong.MaxValue)
            return;

        if (!TryGetPlayer(CurrentOpener.Value, out NetworkObject player) ||
            Vector3.Distance(transform.position, player.transform.position) > useRange)
        {
            ClearLock();
            return;
        }

        OpenProgress.Value = Mathf.Clamp01(OpenProgress.Value + Time.deltaTime / holdDuration);
        if (OpenProgress.Value >= 1f)
        {
            IsOpen.Value = true;
            CurrentOpener.Value = ulong.MaxValue;
            SpawnLoot();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void BeginOpeningRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (IsOpen.Value ||
            (CurrentOpener.Value != ulong.MaxValue && CurrentOpener.Value != sender) ||
            !TryGetPlayer(sender, out NetworkObject player) ||
            Vector3.Distance(transform.position, player.transform.position) > useRange)
            return;

        CurrentOpener.Value = sender;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void CancelOpeningRpc(RpcParams rpcParams = default)
    {
        if (!IsOpen.Value && CurrentOpener.Value == rpcParams.Receive.SenderClientId)
            ClearLock();
    }

    private bool TryGetPlayer(ulong clientId, out NetworkObject player)
    {
        player = null;
        if (NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            (player = client.PlayerObject) != null)
            return true;

        // Respaldo para escenas antiguas que hayan generado avatares solo con ownership.
        foreach (NetworkObject candidate in NetworkManager.SpawnManager.SpawnedObjectsList)
        {
            if (candidate.OwnerClientId == clientId && candidate.GetComponent<PlayerInteraction>() != null)
            {
                player = candidate;
                return true;
            }
        }

        return false;
    }

    private void ClearLock()
    {
        CurrentOpener.Value = ulong.MaxValue;
        OpenProgress.Value = 0f;
    }

    private void SpawnLoot()
    {
        if (cubePrefab == null)
            return;

        for (int i = 0; i < cubeCount; i++)
        {
            float angle = i * Mathf.PI * 2f / cubeCount;
            Vector3 offset = new(Mathf.Cos(angle) * 1.35f, 1.4f, Mathf.Sin(angle) * 1.35f);
            NetworkObject cube = Instantiate(cubePrefab, transform.position + offset, Quaternion.identity);
            cube.Spawn(true);
            cube.GetComponent<GrabbableCube>().InitializeColor(i);
        }
    }

    private void BuildVisuals()
    {
        if (lid != null) return;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "ChestBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        body.transform.localScale = new Vector3(2.4f, 0.8f, 1.5f);
        body.GetComponent<Renderer>().material.color = new Color(0.35f, 0.16f, 0.04f);
        Destroy(body.GetComponent<Collider>());

        GameObject lidObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lidObject.name = "ChestLid";
        lidObject.transform.SetParent(transform, false);
        lidObject.transform.localPosition = new Vector3(0f, 0.95f, 0.55f);
        lidObject.transform.localScale = new Vector3(2.5f, 0.32f, 1.55f);
        lidObject.GetComponent<Renderer>().material.color = new Color(0.95f, 0.62f, 0.08f);
        Destroy(lidObject.GetComponent<Collider>());
        lid = lidObject.transform;

        GameObject textObject = new("ChestLabel");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = "COFRE";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 48;
        text.characterSize = 0.06f;
        text.color = new Color(1f, 0.82f, 0.2f);
        textObject.AddComponent<BillboardToCamera>();
    }

    private void OnOpenChanged(bool previous, bool current) => ApplyVisualState();
    private void OnProgressChanged(float previous, float current) => ApplyVisualState();

    private void ApplyVisualState()
    {
        if (lid == null) return;
        float angle = IsOpen.Value ? -105f : -55f * OpenProgress.Value;
        lid.localRotation = Quaternion.Euler(angle, 0f, 0f);
    }
}
