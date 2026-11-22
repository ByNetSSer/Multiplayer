using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;

/// <summary>
/// Serialización compacta de las ranuras que sí sobreviven a una desconexión.
/// No se guardan GameObjects, física ni objetos de mundo.
/// </summary>
public static class Lab09BackpackSerializer
{
    [Serializable]
    private class Snapshot { public List<Slot> slots = new(); }

    [Serializable]
    private class Slot { public byte type; public ushort amount; }

    public static string Serialize(IReadOnlyList<InventorySlot> source)
    {
        Snapshot snapshot = new();
        for (int index = 0; index < source.Count; index++)
        {
            InventorySlot current = source[index];
            snapshot.slots.Add(new Slot { type = (byte)current.Type, amount = current.Amount });
        }
        return JsonUtility.ToJson(snapshot);
    }

    public static bool TryDeserialize(string json, List<InventorySlot> destination)
    {
        destination.Clear();
        if (string.IsNullOrWhiteSpace(json)) return true;

        try
        {
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(json);
            if (snapshot?.slots == null) return false;
            foreach (Slot slot in snapshot.slots)
            {
                if (slot.amount > 0)
                    destination.Add(new InventorySlot((ItemType)slot.type, slot.amount));
            }
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// Identifica a un jugador por PlayerId, conserva su mochila durante una reconexión
/// y la consume una sola vez cuando el nuevo ClientId recibe su PlayerObject.
/// </summary>
public class Lab09ReconnectCoordinator : MonoBehaviour
{
    private const string ScenePrefix = "Lab09";
    private static Lab09ReconnectCoordinator instance;
    private static readonly Dictionary<string, List<InventorySlot>> Wardrobe = new();
    private static readonly Dictionary<ulong, string> PlayerIdByClientId = new();
    private static readonly Dictionary<string, LabPlayerRole> RoleByPlayerId = new();
    private static readonly HashSet<ulong> CloudRestoreApplied = new();

    public static bool IsActive => instance != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForLab09()
    {
        if (!SceneManager.GetActiveScene().name.StartsWith(ScenePrefix, StringComparison.Ordinal) || instance != null)
            return;

        GameObject services = new("Lab09_BackpackServices");
        DontDestroyOnLoad(services);
        services.AddComponent<Lab09ReconnectCoordinator>();
        services.AddComponent<Lab09CloudBackpack>();
    }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        if (instance == this) instance = null;
    }

    public static bool TryApprove(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response,
        NetworkObject guardianPrefab,
        NetworkObject explorerPrefab)
    {
        if (!IsActive || guardianPrefab == null || explorerPrefab == null) return false;

        string playerId = Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>());
        if (string.IsNullOrWhiteSpace(playerId))
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = "Lab 9: falta PlayerId en ConnectionData.";
            return true;
        }

        if (!RoleByPlayerId.TryGetValue(playerId, out LabPlayerRole role))
        {
            role = RoleByPlayerId.ContainsValue(LabPlayerRole.Guardian)
                ? LabPlayerRole.Explorer
                : LabPlayerRole.Guardian;
            RoleByPlayerId[playerId] = role;
        }

        PlayerIdByClientId[request.ClientNetworkId] = playerId;
        NetworkObject prefab = role == LabPlayerRole.Guardian ? guardianPrefab : explorerPrefab;
        response.Approved = true;
        response.CreatePlayerObject = true;
        response.PlayerPrefabHash = prefab.PrefabIdHash;
        response.Position = new Vector3((request.ClientNetworkId % 4) * 2.2f - 3.3f, 1f, 0f);
        response.Rotation = Quaternion.identity;
        response.Pending = false;
        Debug.Log($"[Lab 9] PlayerId {playerId} asociado al ClientId {request.ClientNetworkId} como {role}.");
        return true;
    }

    public static bool IsGuardian(ulong clientId) =>
        PlayerIdByClientId.TryGetValue(clientId, out string playerId) &&
        RoleByPlayerId.TryGetValue(playerId, out LabPlayerRole role) &&
        role == LabPlayerRole.Guardian;

    public static void RecordInventoryDespawn(ulong clientId, IReadOnlyList<InventorySlot> slots)
    {
        if (!IsActive || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        SaveWardrobe(clientId, slots, "OnNetworkDespawn");
    }

    public static void HandleDisconnect(ulong clientId, PlayerInventory inventory)
    {
        if (!IsActive || inventory == null) return;
        SaveWardrobe(clientId, inventory.CopySlots(), "ClientDisconnected");
    }

    public static bool TryConsumeCloudRestore(ulong clientId)
    {
        if (!IsActive || !PlayerIdByClientId.ContainsKey(clientId) || !CloudRestoreApplied.Add(clientId))
            return false;
        return true;
    }

    private static void SaveWardrobe(ulong clientId, IReadOnlyList<InventorySlot> slots, string source)
    {
        if (!PlayerIdByClientId.TryGetValue(clientId, out string playerId)) return;
        Wardrobe[playerId] = new List<InventorySlot>(slots);
        Debug.Log($"[Lab 9] {source}: {playerId} salió con {slots.Count} casilla(s); mochila guardada.");
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer || !PlayerIdByClientId.TryGetValue(clientId, out string playerId)) return;
        StartCoroutine(RestoreAfterPlayerSpawn(clientId, playerId));
    }

    private System.Collections.IEnumerator RestoreAfterPlayerSpawn(ulong clientId, string playerId)
    {
        yield return null;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) ||
            client.PlayerObject == null || !Wardrobe.TryGetValue(playerId, out List<InventorySlot> saved))
            yield break;

        PlayerInventory inventory = client.PlayerObject.GetComponent<PlayerInventory>();
        if (inventory == null) yield break;
        inventory.ReplaceSlotsFromServer(saved);
        Wardrobe.Remove(playerId);
        Debug.Log($"[Lab 9] Mochila de {playerId} restaurada y eliminada del Guardarropa: {saved.Count} casilla(s).");
    }
}

/// <summary>
/// Usa Authentication PlayerId para ConnectionData y Cloud Save para reconstruir
/// la mochila al comenzar una sesión distinta. Si UGS no está vinculado, la
/// reconexión de la misma sesión sigue funcionando con el Guardarropa.
/// </summary>
public class Lab09CloudBackpack : MonoBehaviour
{
    private const string CloudKey = "lab09_backpack";
    private static Lab09CloudBackpack instance;
    private string playerId;
    private bool ready;
    private bool cloudAvailable;

    public static bool CanStart => instance != null && instance.ready;
    public static string PlayerId => instance?.playerId ?? string.Empty;

    private async void Awake()
    {
        instance = this;
        playerId = PlayerPrefs.GetString("Lab09FallbackPlayerId", Guid.NewGuid().ToString("N"));
        PlayerPrefs.SetString("Lab09FallbackPlayerId", playerId);
        PlayerPrefs.Save();

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            playerId = AuthenticationService.Instance.PlayerId;
            cloudAvailable = true;
            Debug.Log($"[Lab 9] Authentication listo: {playerId}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Lab 9] UGS no disponible; se usará identidad local para la reconexión. {exception.Message}");
        }
        finally
        {
            ready = true;
        }
    }

    public static void PutIdentityInConnectionData(NetworkManager networkManager)
    {
        if (networkManager == null || string.IsNullOrWhiteSpace(PlayerId)) return;
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(PlayerId);
    }

    public static void RegisterLocalInventory(PlayerInventory inventory)
    {
        if (instance != null && inventory != null) instance.LoadIntoNewSessionAsync(inventory);
    }

    public static void SaveOwnedInventory(IReadOnlyList<InventorySlot> slots)
    {
        if (instance != null) instance.SaveAsync(slots);
    }

    private async void SaveAsync(IReadOnlyList<InventorySlot> slots)
    {
        if (!ready || !cloudAvailable) return;
        try
        {
            Dictionary<string, object> data = new() { { CloudKey, Lab09BackpackSerializer.Serialize(slots) } };
            await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            Debug.Log($"[Lab 9] Cloud Save guardó {slots.Count} casilla(s) para {playerId}.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Lab 9] No se pudo guardar Cloud Save: {exception.Message}");
        }
    }

    private async void LoadIntoNewSessionAsync(PlayerInventory inventory)
    {
        while (!ready) await System.Threading.Tasks.Task.Yield();
        if (!cloudAvailable || inventory == null || !inventory.IsOwner) return;
        try
        {
            HashSet<string> keys = new() { CloudKey };
            var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);
            if (!data.TryGetValue(CloudKey, out var item)) return;

            string snapshot = item.Value.GetAs<string>();
            inventory.RequestCloudRestore(snapshot);
            Debug.Log("[Lab 9] Cloud Save cargó la mochila y solicitó su restauración al servidor.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Lab 9] No se pudo cargar Cloud Save: {exception.Message}");
        }
    }
}
