using Unity.Netcode;
using UnityEngine;

public enum LabPlayerRole
{
    Guardian,
    Explorer
}

/// <summary>
/// Identidad definida por el prefab elegido durante ConnectionApprovalCallback.
/// </summary>
public class PlayerRole : NetworkBehaviour
{
    [SerializeField] private LabPlayerRole role = LabPlayerRole.Explorer;

    public LabPlayerRole Role => role;
    public bool IsGuardian => role == LabPlayerRole.Guardian;
    public string DisplayName => IsGuardian ? "GUARDIÁN" : "EXPLORADOR";

    public override void OnNetworkSpawn()
    {
        GameObject textObject = new("RoleLabel");
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, 2.15f, 0f);

        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = DisplayName;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 46;
        text.characterSize = 0.045f;
        text.color = IsGuardian
            ? new Color(1f, 0.75f, 0.1f)
            : new Color(0.25f, 0.85f, 1f);

        textObject.AddComponent<BillboardToCamera>();
    }
}
