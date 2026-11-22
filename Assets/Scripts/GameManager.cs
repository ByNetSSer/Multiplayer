using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Arranque de red del Lab 6: aprobación, selección de prefab por rol y ciclo de desconexión.
/// </summary>
public class GameManager : MonoBehaviour
{
    private static GameManager instance;

    [Header("Prefabs registrados en NetworkManager")]
    [SerializeField] private NetworkObject guardianPrefab;
    [SerializeField] private NetworkObject explorerPrefab;
    [SerializeField] private NetworkObject chestPrefab;
    [SerializeField] private bool enableLab06Chest = true;

    private NetworkManager networkManager;
    private ulong guardianClientId = ulong.MaxValue;
    private NetworkObject activeChest;
    private bool disconnectHandlerRegistered;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    public static GameManager Instance => instance;
    public bool ChestLabEnabled => enableLab06Chest;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        ConfigureConnectionApproval();
    }

    private void Update()
    {
        RegisterServerCallbacksWhenReady();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (networkManager != null && disconnectHandlerRegistered)
            networkManager.ConnectionApprovalCallback = null;

        if (networkManager != null && disconnectHandlerRegistered)
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void ConfigureConnectionApproval()
    {
        networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("Lab 6: no se encontró NetworkManager en la escena.");
            return;
        }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApproveConnection;
    }

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        bool assignGuardian = guardianClientId == ulong.MaxValue;
        NetworkObject selectedPrefab = assignGuardian ? guardianPrefab : explorerPrefab;

        if (selectedPrefab == null)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = "El servidor no tiene configurados los prefabs Guardián/Explorador.";
            return;
        }

        if (assignGuardian)
            guardianClientId = request.ClientNetworkId;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = selectedPrefab.PrefabIdHash;
        response.Position = GetSpawnPosition(request.ClientNetworkId);
        response.Rotation = Quaternion.identity;
        response.Pending = false;

        Debug.Log($"Lab 6: cliente {request.ClientNetworkId} aprobado como " +
                  (assignGuardian ? "GUARDIÁN" : "EXPLORADOR"));
    }

    private void RegisterServerCallbacksWhenReady()
    {
        if (networkManager == null)
            ConfigureConnectionApproval();

        if (networkManager != null && networkManager.IsServer && !disconnectHandlerRegistered)
        {
            networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
            disconnectHandlerRegistered = true;
        }
        else if (networkManager != null && !networkManager.IsServer && disconnectHandlerRegistered)
        {
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
            disconnectHandlerRegistered = false;
            guardianClientId = ulong.MaxValue;
            activeChest = null;
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager == null || !networkManager.IsServer)
            return;

        List<NetworkObject> spawnedObjects = new(networkManager.SpawnManager.SpawnedObjectsList);
        foreach (NetworkObject networkObject in spawnedObjects)
        {
            GrabbableCube cube = networkObject != null
                ? networkObject.GetComponent<GrabbableCube>()
                : null;

            if (cube != null && cube.LogicalOwnerId == clientId)
                cube.HandleOwnerDisconnected();
        }

        if (guardianClientId == clientId)
            guardianClientId = ulong.MaxValue;
    }

    public bool TrySpawnGuardianChest(NetworkObject requester)
    {
        if (!enableLab06Chest || networkManager == null || !networkManager.IsServer || requester == null || chestPrefab == null)
            return false;

        PlayerRole role = requester.GetComponent<PlayerRole>();
        if (role == null || !role.IsGuardian || requester.OwnerClientId != guardianClientId)
            return false;

        if (activeChest != null && activeChest.IsSpawned)
            return false;

        // Queda dentro del rango de apertura (3 m) incluso considerando la diferencia vertical.
        Vector3 spawnPosition = requester.transform.position + requester.transform.forward * 2.2f;
        spawnPosition.y = 0.75f;

        activeChest = chestPrefab.InstantiateAndSpawn(
            networkManager,
            NetworkManager.ServerClientId,
            destroyWithScene: true,
            isPlayerObject: false,
            forceOverride: false,
            position: spawnPosition,
            rotation: requester.transform.rotation);

        return activeChest != null;
    }

    private static Vector3 GetSpawnPosition(ulong clientId)
    {
        return new Vector3((clientId % 4) * 2.2f - 3.3f, 1f, 0f);
    }

    private void OnGUI()
    {
        EnsureGuiStyles();
        float width = Mathf.Min(455f, Screen.width - 32f);
        GUILayout.BeginArea(new Rect(16f, 16f, width, 285f), GUI.skin.box);
        GUILayout.Label(enableLab06Chest
            ? "LAB 06 · COFRE DEL GUARDIÁN"
            : "LAB 07 · INVENTARIO Y POOL DE RED", titleStyle);

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            GUILayout.Label("El primer jugador será Guardián; los siguientes, Exploradores.", bodyStyle);
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("INICIAR HOST", buttonStyle, GUILayout.Height(42f)))
                NetworkManager.Singleton.StartHost();
            if (GUILayout.Button("INICIAR CLIENTE", buttonStyle, GUILayout.Height(42f)))
                NetworkManager.Singleton.StartClient();
            GUILayout.EndHorizontal();
        }
        else
        {
            string roleName = "CONECTANDO";
            NetworkObject localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (localPlayer != null && localPlayer.TryGetComponent(out PlayerRole role))
                roleName = role.DisplayName;

            GUILayout.Label($"Rol: {roleName} · ID {NetworkManager.Singleton.LocalClientId}", bodyStyle);
            if (enableLab06Chest)
            {
                GUILayout.Label("WASD mover · C crear cofre (Guardián) · E abrir", bodyStyle);
                GUILayout.Label("F agarrar/soltar · G regalar · Q consumir", bodyStyle);
                GUILayout.Label("Clic izquierdo lanzar · Espacio atacar", bodyStyle);
            }
            else
            {
                GUILayout.Label("WASD mover · H talar árbol cercano", bodyStyle);
                GUILayout.Label("La madera se apila; el árbol reaparece desde el pool", bodyStyle);
            }
            GUILayout.Space(8f);
            if (GUILayout.Button("DESCONECTAR", buttonStyle, GUILayout.Height(34f)))
                NetworkManager.Singleton.Shutdown();
        }

        GUILayout.EndArea();
    }

    private void EnsureGuiStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.78f, 0.2f) },
            margin = new RectOffset(10, 10, 8, 8)
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = Color.white },
            margin = new RectOffset(10, 10, 4, 4)
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(8, 8, 4, 4)
        };
    }
}
