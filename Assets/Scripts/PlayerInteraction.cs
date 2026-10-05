using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Entrada del dueño. El cliente solo solicita interactuar; el servidor valida distancia y estado.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private float throwForce = 9f;

    public readonly NetworkVariable<ulong> HeldCubeId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private ChestController activeChest;

    private void Update()
    {
        if (!IsOwner || !IsSpawned)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            activeChest = FindNearest<ChestController>(interactionRange);
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
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGrabRpc(ulong cubeId)
    {
        if (HeldCubeId.Value != ulong.MaxValue ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(cubeId, out NetworkObject cubeObject))
            return;

        GrabbableCube cube = cubeObject.GetComponent<GrabbableCube>();
        if (cube == null || cube.IsHeld || Vector3.Distance(transform.position, cube.transform.position) > interactionRange)
            return;

        if (cube.TryGrab(NetworkObject))
            HeldCubeId.Value = cubeId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestReleaseRpc(bool launch)
    {
        if (HeldCubeId.Value == ulong.MaxValue ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(HeldCubeId.Value, out NetworkObject cubeObject))
        {
            HeldCubeId.Value = ulong.MaxValue;
            return;
        }

        GrabbableCube cube = cubeObject.GetComponent<GrabbableCube>();
        if (cube != null)
        {
            Vector3 direction = transform.forward + Vector3.up * 0.22f;
            cube.Release(launch ? direction.normalized * throwForce : Vector3.zero);
        }

        HeldCubeId.Value = ulong.MaxValue;
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

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned)
            return;

        string message;
        if (HeldCubeId.Value != ulong.MaxValue)
            message = "F: soltar cubo    Clic izquierdo: lanzar";
        else if (FindNearest<ChestController>(interactionRange) != null)
            message = "Mantén E para abrir el cofre";
        else if (FindNearest<GrabbableCube>(interactionRange) != null)
            message = "F: agarrar cubo";
        else
            return;

        GUIStyle style = new(GUI.skin.box)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 70f, 380f, 44f), message, style);
    }
}
