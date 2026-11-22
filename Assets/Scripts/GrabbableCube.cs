using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Botín con dueño lógico y NetworkObject ownership. La física sigue siendo autoritativa del servidor.
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

    public readonly NetworkVariable<ulong> LootOwnerId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsHeld => transform.parent != null;
    public ulong LogicalOwnerId => LootOwnerId.Value;

    private Rigidbody body;
    private NetworkTransform networkTransform;
    private Vector3 spawnPosition;

    private static readonly Color[] OwnerColors =
    {
        new(1f, 0.7f, 0.12f),
        new(0.15f, 0.72f, 1f),
        new(0.95f, 0.25f, 0.42f),
        new(0.35f, 0.95f, 0.42f)
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
        LootOwnerId.OnValueChanged += OnLootOwnerChanged;
        PaintForOwner(LootOwnerId.Value);
    }

    public override void OnNetworkDespawn()
    {
        ColorIndex.OnValueChanged -= OnColorChanged;
        LootOwnerId.OnValueChanged -= OnLootOwnerChanged;
    }

    protected override void OnOwnershipChanged(ulong previous, ulong current)
    {
        base.OnOwnershipChanged(previous, current);
        PaintForOwner(LootOwnerId.Value);
        Debug.Log($"Cubo {NetworkObjectId}: ownership {previous} -> {current}");
    }

    public override void OnGainedOwnership()
    {
        Debug.Log($"Cliente {OwnerClientId} recibió el cubo {NetworkObjectId}.");
    }

    public override void OnLostOwnership()
    {
        Debug.Log($"Se perdió la propiedad local del cubo {NetworkObjectId}.");
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

        transform.localPosition = heldLocalPosition;
        transform.localRotation = Quaternion.identity;
    }

    public void InitializeLoot(int index, ulong ownerClientId)
    {
        if (!IsServer) return;
        ColorIndex.Value = index;
        LootOwnerId.Value = ownerClientId;
        spawnPosition = transform.position;
        PaintForOwner(ownerClientId);
    }

    public void AssignOwner(ulong newOwnerClientId)
    {
        if (!IsServer || !NetworkManager.ConnectedClients.ContainsKey(newOwnerClientId))
            return;

        NetworkObject.ChangeOwnership(newOwnerClientId);
        LootOwnerId.Value = newOwnerClientId;
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

    public void HandleOwnerDisconnected()
    {
        if (!IsServer)
            return;

        if (IsHeld)
            NetworkObject.TryRemoveParent(true);

        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        NetworkObject.RemoveOwnership();
        LootOwnerId.Value = ulong.MaxValue;
    }

    private void Recover()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        networkTransform.Teleport(spawnPosition, Quaternion.identity, transform.localScale);
    }

    private void OnColorChanged(int previous, int current) => PaintForOwner(LootOwnerId.Value);
    private void OnLootOwnerChanged(ulong previous, ulong current) => PaintForOwner(current);

    private void PaintForOwner(ulong ownerId)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
            return;

        if (ownerId == ulong.MaxValue)
        {
            renderer.material.color = new Color(0.5f, 0.52f, 0.56f);
            return;
        }

        Color baseColor = OwnerColors[(int)(ownerId % (ulong)OwnerColors.Length)];
        float brightness = Mathf.Lerp(0.78f, 1.08f, (Mathf.Abs(ColorIndex.Value) % 4) / 3f);
        renderer.material.color = baseColor * brightness;
    }
}
