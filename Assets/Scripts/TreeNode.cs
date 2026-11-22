using Unity.Netcode;
using UnityEngine;

public class TreeNode : NetworkBehaviour
{
    [SerializeField] private int hitsToHarvest = 3;
    [SerializeField] private float harvestRange = 3.5f;
    [SerializeField] private ushort woodPerTree = 3;

    public readonly NetworkVariable<int> RemainingHits = new(3,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Renderer[] renderers;
    private MaterialPropertyBlock propertyBlock;
    private TextMesh statusText;

    public override void OnNetworkSpawn()
    {
        CacheVisuals();
        RemainingHits.OnValueChanged += OnHitsChanged;
        if (IsServer) RemainingHits.Value = hitsToHarvest;
        ApplyState(RemainingHits.Value);
    }

    public override void OnNetworkDespawn()
    {
        RemainingHits.OnValueChanged -= OnHitsChanged;
        ResetPooledState();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HarvestRpc(RpcParams rpcParams = default)
    {
        if (!IsSpawned || RemainingHits.Value <= 0) return;
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.ConnectedClients.TryGetValue(sender, out NetworkClient client) ||
            client.PlayerObject == null ||
            Vector3.Distance(transform.position, client.PlayerObject.transform.position) > harvestRange) return;

        RemainingHits.Value--;
        if (RemainingHits.Value > 0) return;
        PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.AddItem(ItemType.Wood, woodPerTree)) return;
        Lab07ForestManager.Instance?.RecycleTree(this);
    }

    private void CacheVisuals()
    {
        renderers ??= GetComponentsInChildren<Renderer>(true);
        propertyBlock ??= new MaterialPropertyBlock();
        if (statusText != null) return;
        GameObject label = new("ResourceStatus");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        statusText = label.AddComponent<TextMesh>();
        statusText.anchor = TextAnchor.MiddleCenter;
        statusText.alignment = TextAlignment.Center;
        statusText.fontSize = 38;
        statusText.characterSize = 0.08f;
        statusText.color = Color.white;
        label.AddComponent<BillboardToCamera>();
    }

    private void OnHitsChanged(int previous, int current) => ApplyState(current);

    private void ApplyState(int hits)
    {
        CacheVisuals();
        float health = hitsToHarvest > 0 ? Mathf.Clamp01((float)hits / hitsToHarvest) : 0f;
        Color color = Color.Lerp(new Color(0.35f, 0.18f, 0.05f), new Color(0.18f, 0.65f, 0.2f), health);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        foreach (Renderer currentRenderer in renderers) currentRenderer.SetPropertyBlock(propertyBlock);
        if (statusText != null) statusText.text = $"ÁRBOL {hits}/{hitsToHarvest}\nH: talar";
    }

    private void ResetPooledState()
    {
        if (IsServer) RemainingHits.Value = hitsToHarvest;
        ApplyState(hitsToHarvest);
    }
}
