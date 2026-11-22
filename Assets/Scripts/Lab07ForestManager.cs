using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Lab07ForestManager : MonoBehaviour
{
    [SerializeField] private NetworkObjectPool treePool;
    [SerializeField] private float respawnDelay = 4f;
    public static Lab07ForestManager Instance { get; private set; }

    private static readonly Vector3[] SpawnPoints =
    {
        new(-6f, 0f, 4f), new(-2f, 0f, 6f), new(3f, 0f, 5f),
        new(7f, 0f, 3f), new(-5f, 0f, -3f), new(5f, 0f, -4f)
    };

    private void Awake() => Instance = this;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnInitialForest;
        if (NetworkManager.Singleton.IsServer) SpawnInitialForest();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= SpawnInitialForest;
    }

    public void RecycleTree(TreeNode tree)
    {
        if (!NetworkManager.Singleton.IsServer || tree == null || !tree.IsSpawned) return;
        Vector3 position = tree.transform.position;
        NetworkObject networkObject = tree.NetworkObject;
        networkObject.Despawn(false);
        treePool.ReturnNetworkObject(networkObject);
        StartCoroutine(RespawnTree(position));
    }

    private void SpawnInitialForest()
    {
        if (!NetworkManager.Singleton.IsServer || treePool == null) return;
        foreach (Vector3 position in SpawnPoints) SpawnTree(position);
    }

    private IEnumerator RespawnTree(Vector3 position)
    {
        yield return new WaitForSeconds(respawnDelay);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer) SpawnTree(position);
    }

    private void SpawnTree(Vector3 position)
    {
        NetworkObject tree = treePool.GetNetworkObject(position, Quaternion.identity);
        tree.Spawn(true);
    }
}
