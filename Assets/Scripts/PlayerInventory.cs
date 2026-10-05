using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public NetworkVariable<int> woodCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public void AddWood(int amount)
    {
        if (!IsServer) return;
        woodCount.Value += amount;
        Debug.Log($"Server: Player {OwnerClientId} now has {woodCount.Value} wood");
    }

    public override void OnNetworkSpawn()
    {
        woodCount.OnValueChanged += OnWoodChanged;
    }

    public override void OnNetworkDespawn()
    {
        woodCount.OnValueChanged -= OnWoodChanged;
    }

    private void OnWoodChanged(int previousValue, int newValue)
    {
        Debug.Log($"Inventory updated: {previousValue} -> {newValue} wood");
    }
}
