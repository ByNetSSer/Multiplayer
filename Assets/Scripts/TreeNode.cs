using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class TreeNode : NetworkBehaviour
{
    // Sistema de tala y aparicion de troncos (tema posterior).
    // Se conserva comentado para esta tarea, que solo pide reclamar el recurso.
    // [SerializeField] private int maxLogs = 5;
    // [SerializeField] private GameObject logPrefab;
    //
    // private NetworkVariable<int> currentLogs = new NetworkVariable<int>(
    //     5,
    //     NetworkVariableReadPermission.Everyone,
    //     NetworkVariableWritePermission.Server
    // );

    private NetworkVariable<Color> treeColor = new NetworkVariable<Color>(
        Color.gray,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private NetworkVariable<FixedString32Bytes> ownerName =
        new NetworkVariable<FixedString32Bytes>(
            new FixedString32Bytes("Libre"),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private TextMesh ownerText;

    public override void OnNetworkSpawn()
    {
        // currentLogs.OnValueChanged += OnLogsChanged;
        CreateOwnerText();

        treeColor.OnValueChanged += OnColorChanged;
        ownerName.OnValueChanged += OnOwnerNameChanged;

        PaintTree(treeColor.Value);
        ShowOwnerName(ownerName.Value);
    }

    public override void OnNetworkDespawn()
    {
        // currentLogs.OnValueChanged -= OnLogsChanged;
        treeColor.OnValueChanged -= OnColorChanged;
        ownerName.OnValueChanged -= OnOwnerNameChanged;
    }

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
                    InteractServerRpc();
                }
            }
        }
    }

    [Rpc(SendTo.Server)]
    public void InteractServerRpc(RpcParams rpcParams = default)
    {
        ulong who = rpcParams.Receive.SenderClientId;

        if (treeColor.Value == Color.gray)
        {
            treeColor.Value = Color.red;

            FixedString32Bytes newOwnerName = new FixedString32Bytes("Jugador ");
            newOwnerName.Append(who);
            ownerName.Value = newOwnerName;

            NetworkObject.ChangeOwnership(who);
            Debug.Log("Server: El arbol ahora le pertenece a: " + who);
            return;
        }

        Debug.Log("Server: Te ganaron, este arbol ya tiene dueno");

        // Sistema de tala y aparicion de troncos (tema posterior).
        // if (currentLogs.Value <= 0)
        // {
        //     SpawnLogsServerRpc();
        //     NetworkObject.Despawn(true);
        //     Debug.Log("Server: Arbol destruido, logs spawned");
        //     return;
        // }
        //
        // currentLogs.Value -= 1;
        // Debug.Log("Server: Talo! Logs restantes: " + currentLogs.Value);
    }

    // [Rpc(SendTo.Server)]
    // private void SpawnLogsServerRpc()
    // {
    //     for (int i = 0; i < maxLogs; i++)
    //     {
    //         Vector3 offset = new Vector3(
    //             Random.Range(-1.5f, 1.5f),
    //             0.5f,
    //             Random.Range(-1.5f, 1.5f)
    //         );
    //
    //         GameObject log = Instantiate(logPrefab, transform.position + offset, Quaternion.identity);
    //         log.GetComponent<NetworkObject>().Spawn(true);
    //     }
    // }

    private void OnColorChanged(Color previousColor, Color newColor)
    {
        PaintTree(newColor);
    }

    // private void OnLogsChanged(int previous, int current)
    // {
    //     ChangeColor();
    // }

    private void PaintTree(Color newColor)
    {
        GetComponent<Renderer>().material.color = newColor;
    }

    private void OnOwnerNameChanged(FixedString32Bytes previousName, FixedString32Bytes newName)
    {
        ShowOwnerName(newName);
    }

    private void ShowOwnerName(FixedString32Bytes newName)
    {
        ownerText.text = newName.ToString();
    }

    private void CreateOwnerText()
    {
        if (ownerText != null) return;

        GameObject textObject = new GameObject("OwnerName");
        textObject.transform.SetParent(transform);
        textObject.transform.localPosition = new Vector3(0, 3, 0);
        textObject.transform.localRotation = Quaternion.Euler(0, 180, 0);

        ownerText = textObject.AddComponent<TextMesh>();
        ownerText.anchor = TextAnchor.MiddleCenter;
        ownerText.alignment = TextAlignment.Center;
        ownerText.fontSize = 40;
        ownerText.characterSize = 0.1f;
        ownerText.color = Color.white;
    }
}
