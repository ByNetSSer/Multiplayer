using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkObjectPool : MonoBehaviour, INetworkPrefabInstanceHandler
{
    [SerializeField] private NetworkObject prefab;
    [SerializeField] private int prewarmCount = 8;
    private readonly Queue<NetworkObject> available = new();
    private readonly HashSet<NetworkObject> pooled = new();
    private NetworkManager networkManager;
    private bool registered;

    public NetworkObject Prefab => prefab;

    private void Start()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null || prefab == null) return;
        registered = networkManager.PrefabHandler.AddHandler(prefab, this);
        for (int i = 0; i < prewarmCount; i++) CreatePooledObject();
    }

    private void OnDestroy()
    {
        if (registered && networkManager != null) networkManager.PrefabHandler.RemoveHandler(prefab);
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        => GetNetworkObject(position, rotation);

    public void Destroy(NetworkObject networkObject) => ReturnNetworkObject(networkObject);

    public NetworkObject GetNetworkObject(Vector3 position, Quaternion rotation)
    {
        NetworkObject instance = available.Count > 0
            ? available.Dequeue()
            : Instantiate(prefab, transform);
        pooled.Remove(instance);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);
        return instance;
    }

    public void ReturnNetworkObject(NetworkObject instance)
    {
        if (instance == null || !pooled.Add(instance)) return;
        instance.gameObject.SetActive(false);
        available.Enqueue(instance);
    }

    private NetworkObject CreatePooledObject()
    {
        NetworkObject instance = Instantiate(prefab);
        instance.gameObject.SetActive(false);
        pooled.Add(instance);
        available.Enqueue(instance);
        return instance;
    }
}
