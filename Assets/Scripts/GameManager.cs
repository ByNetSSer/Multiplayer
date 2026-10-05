using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : NetworkBehaviour
{
    private static GameManager instance;

    [SerializeField] private Transform playerPrefab;
    private readonly HashSet<ulong> spawnedPlayers = new();
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

        if (!IsClient) return;
        RequestPlayerSpawnRpc();
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;

        // Permite iniciar otra sesión en la misma ejecución sin conservar IDs antiguos.
        spawnedPlayers.Clear();
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (IsServer)
            spawnedPlayers.Remove(clientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlayerSpawnRpc(RpcParams rpcParams = default)
    {
        ulong ownerId = rpcParams.Receive.SenderClientId;
        if (!spawnedPlayers.Add(ownerId)) return;

        Vector3 spawnPosition = new Vector3((ownerId % 4) * 2.2f - 3.3f, 1f, 0f);
        Transform player = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        player.GetComponent<NetworkObject>().SpawnWithOwnership(ownerId, true);
    }

    void Update()
    {
        
    }
    public static GameManager Instance => instance;

    private void OnGUI()
    {
        EnsureGuiStyles();
        float width = Mathf.Min(430f, Screen.width - 32f);
        GUILayout.BeginArea(new Rect(16f, 16f, width, 245f), GUI.skin.box);
        GUILayout.Label("LAB 04 · COMBATE EN RED", titleStyle);

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            GUILayout.Label("Inicia una instancia como Host y otra como Cliente.", bodyStyle);
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
            string role = NetworkManager.Singleton.IsHost ? "HOST" :
                NetworkManager.Singleton.IsServer ? "SERVIDOR" : "CLIENTE";
            GUILayout.Label($"Conectado como {role} · ID {NetworkManager.Singleton.LocalClientId}", bodyStyle);
            GUILayout.Label("WASD: mover    ESPACIO: atacar al rival cercano", bodyStyle);
            GUILayout.Label("El servidor valida rango, cooldown y daño.", bodyStyle);
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
            normal = { textColor = new Color(0.25f, 0.85f, 1f) },
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
