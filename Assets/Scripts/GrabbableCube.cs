using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Cubo con autoridad del servidor. NetworkRigidbody deja los clientes cinemáticos.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(NetworkTransform))]
public class GrabbableCube : NetworkBehaviour
{
    [SerializeField] private float recoveryHeight = -3f;
    [SerializeField] private Vector3 heldLocalPosition = new(0f, 0.65f, 1.15f);

    public readonly NetworkVariable<int> ColorIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsHeld => transform.parent != null;

    private Rigidbody body;
    private NetworkTransform networkTransform;
    private Vector3 spawnPosition;

    private static readonly Color[] Colors =
    {
        new(0.15f, 0.7f, 1f),
        new(1f, 0.25f, 0.22f),
        new(0.35f, 0.95f, 0.38f),
        new(0.85f, 0.3f, 1f)
    };

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void OnNetworkSpawn()
    {
        spawnPosition = transform.position;
        ColorIndex.OnValueChanged += OnColorChanged;
        Paint(ColorIndex.Value);
    }

    public override void OnNetworkDespawn()
    {
        ColorIndex.OnValueChanged -= OnColorChanged;
    }

    private void Update()
    {
        if (IsServer && !IsHeld && transform.position.y < recoveryHeight)
            Recover();
    }

    private void LateUpdate()
    {
        if (!IsHeld)
            return;

        // Mientras está emparentado, la posición es estado local derivado del avatar.
        // Se aplica después de NetworkTransform para evitar el efecto de objeto rezagado.
        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.identity;
    }

    public void InitializeColor(int index)
    {
        if (!IsServer) return;
        ColorIndex.Value = index % Colors.Length;
        Paint(ColorIndex.Value);
        spawnPosition = transform.position;
    }

    public bool TryGrab(NetworkObject player)
    {
        if (!IsServer || IsHeld || player == null)
            return false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        networkTransform.Interpolate = false;
        if (!NetworkObject.TrySetParent(player, false))
        {
            networkTransform.Interpolate = true;
            return false;
        }

        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.identity;
        return true;
    }

    public void Release(Vector3 velocity)
    {
        if (!IsServer || !IsHeld)
            return;

        NetworkObject.TryRemoveParent(true);
        networkTransform.Interpolate = true;
        body.isKinematic = false;
        body.linearVelocity = velocity;
        body.angularVelocity = Random.insideUnitSphere * 5f;
    }

    private void Recover()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        networkTransform.Teleport(spawnPosition, Quaternion.identity, transform.localScale);
    }

    private void OnColorChanged(int previous, int current) => Paint(current);

    private void Paint(int index)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = Colors[Mathf.Abs(index) % Colors.Length];
    }
}
