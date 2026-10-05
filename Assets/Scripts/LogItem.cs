using Unity.Netcode;
using UnityEngine;

public class LogItem : NetworkBehaviour
{
    private void Update()
    {
        if (!IsSpawned) return;

        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    PickUpServerRpc();
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void PickUpServerRpc(RpcParams rpcParams = default)
    {
        ulong pickerId = rpcParams.Receive.SenderClientId;
        NetworkObject playerObj = NetworkManager.Singleton.ConnectedClients[pickerId].PlayerObject;

        if (playerObj != null)
        {
            PlayerInventory inventory = playerObj.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddWood(1);
                Debug.Log($"Server: Player {pickerId} picked up a log");
            }
        }

        NetworkObject.Despawn(true);
    }
}
