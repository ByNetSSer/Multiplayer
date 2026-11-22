using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Entrada del dueño. Todas las operaciones sobre objetos se validan en el servidor.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private float giftRange = 5f;
    [SerializeField] private float throwForce = 9f;

    public readonly NetworkVariable<ulong> HeldCubeId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private ChestController activeChest;
    private PlayerRole playerRole;

    private void Awake()
    {
        playerRole = GetComponent<PlayerRole>();
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        if (Input.GetKeyDown(KeyCode.C) && GameManager.Instance != null &&
            GameManager.Instance.ChestLabEnabled && playerRole != null && playerRole.IsGuardian)
            RequestGuardianChestRpc();

        if (Input.GetKeyDown(KeyCode.H))
        {
            TreeNode tree = FindNearest<TreeNode>(interactionRange + 0.5f);
            if (tree != null)
                tree.HarvestRpc();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            activeChest = FindNearestInteractableChest(interactionRange);
            if (activeChest != null)
                activeChest.BeginOpeningRpc();
        }

        if (Input.GetKeyUp(KeyCode.E) && activeChest != null)
        {
            activeChest.CancelOpeningRpc();
            activeChest = null;
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (HeldCubeId.Value == ulong.MaxValue)
            {
                GrabbableCube cube = FindNearest<GrabbableCube>(interactionRange);
                if (cube != null)
                    RequestGrabRpc(cube.NetworkObjectId);
            }
            else
            {
                RequestReleaseRpc(false);
            }
        }

        if (Input.GetMouseButtonDown(0) && HeldCubeId.Value != ulong.MaxValue)
            RequestReleaseRpc(true);

        if (Input.GetKeyDown(KeyCode.G) && HeldCubeId.Value != ulong.MaxValue)
            RequestGiftRpc();

        if (Input.GetKeyDown(KeyCode.Q) && HeldCubeId.Value != ulong.MaxValue)
            RequestConsumeRpc();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGuardianChestRpc()
    {
        GameManager.Instance?.TrySpawnGuardianChest(NetworkObject);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGrabRpc(ulong cubeId)
    {
        if (HeldCubeId.Value != ulong.MaxValue || !TryGetCube(cubeId, out GrabbableCube cube))
            return;

        if (cube.IsHeld || Vector3.Distance(transform.position, cube.transform.position) > interactionRange)
            return;

        if (cube.LogicalOwnerId == ulong.MaxValue)
            cube.AssignOwner(OwnerClientId);
        else if (cube.LogicalOwnerId != OwnerClientId)
            return;

        if (cube.TryGrab(NetworkObject))
            HeldCubeId.Value = cubeId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestReleaseRpc(bool launch)
    {
        if (!TryGetHeldCube(out GrabbableCube cube))
            return;

        Vector3 direction = transform.forward + Vector3.up * 0.22f;
        cube.Release(launch ? direction.normalized * throwForce : Vector3.zero);
        HeldCubeId.Value = ulong.MaxValue;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGiftRpc()
    {
        if (!TryGetHeldCube(out GrabbableCube cube) || cube.LogicalOwnerId != OwnerClientId)
            return;

        NetworkObject receiver = FindNearestOtherPlayerServer();
        if (receiver == null)
            return;

        cube.Release(Vector3.zero);
        cube.AssignOwner(receiver.OwnerClientId);
        HeldCubeId.Value = ulong.MaxValue;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestConsumeRpc()
    {
        if (!TryGetHeldCube(out GrabbableCube cube) || cube.LogicalOwnerId != OwnerClientId)
            return;

        HeldCubeId.Value = ulong.MaxValue;
        cube.NetworkObject.Despawn(true);
    }

    private bool TryGetHeldCube(out GrabbableCube cube)
    {
        cube = null;
        if (HeldCubeId.Value == ulong.MaxValue)
            return false;

        if (!TryGetCube(HeldCubeId.Value, out cube))
        {
            HeldCubeId.Value = ulong.MaxValue;
            return false;
        }

        return true;
    }

    private bool TryGetCube(ulong cubeId, out GrabbableCube cube)
    {
        cube = null;
        return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(cubeId, out NetworkObject cubeObject) &&
               cubeObject.TryGetComponent(out cube);
    }

    private NetworkObject FindNearestOtherPlayerServer()
    {
        NetworkObject nearest = null;
        float bestDistance = giftRange;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            NetworkObject candidate = client.PlayerObject;
            if (candidate == null || candidate == NetworkObject)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance <= bestDistance)
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    private T FindNearest<T>(float range) where T : NetworkBehaviour
    {
        T nearest = null;
        float bestDistance = range;
        foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!candidate.IsSpawned)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance <= bestDistance)
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }
        return nearest;
    }

    private ChestController FindNearestInteractableChest(float range)
    {
        ChestController nearest = null;
        float bestDistance = range;

        foreach (ChestController candidate in FindObjectsByType<ChestController>(FindObjectsSortMode.None))
        {
            if (!candidate.CanInteract)
                continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            if (distance <= bestDistance)
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }

        return nearest;
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned)
            return;

        string message;
        if (HeldCubeId.Value != ulong.MaxValue)
            message = "F soltar · Clic lanzar · G regalar · Q consumir";
        else if (FindNearestInteractableChest(interactionRange) != null)
            message = "Mantén E para abrir el cofre";
        else if (FindNearest<GrabbableCube>(interactionRange) != null)
            message = "F: agarrar cubo propio o abandonado";
        else if (FindNearest<TreeNode>(interactionRange + 0.5f) != null)
            message = "H: talar árbol · la madera se apila en NetworkList";
        else if (GameManager.Instance != null && GameManager.Instance.ChestLabEnabled &&
                 playerRole != null && playerRole.IsGuardian)
            message = "C: crear el cofre frente al Guardián";
        else
            return;

        GUIStyle style = new(GUI.skin.box)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 235f, Screen.height - 70f, 470f, 44f), message, style);
    }
}
