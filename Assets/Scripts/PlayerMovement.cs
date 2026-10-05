using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    private PlayerCombat combat;
    private NetworkTransform networkTransform;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        networkTransform = GetComponent<NetworkTransform>();
    }

    void Update()
    {
        if (!IsOwner || (combat != null && combat.IsDead)) return;
        UpdateInterpolationDemo();
        Movement();
    }

    private void UpdateInterpolationDemo()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
            SetInterpolation(NetworkTransform.InterpolationTypes.LegacyLerp);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            SetInterpolation(NetworkTransform.InterpolationTypes.Lerp);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            SetInterpolation(NetworkTransform.InterpolationTypes.SmoothDampening);
    }

    private void SetInterpolation(NetworkTransform.InterpolationTypes type)
    {
        networkTransform.PositionInterpolationType = type;
        networkTransform.RotationInterpolationType = type;
    }

    public void Movement()
    {
        float y = Input.GetAxisRaw("Vertical");
        float x = Input.GetAxisRaw("Horizontal");

        if (y == 0 && x == 0) return;

        Vector3 dir = new Vector3(x, 0, y).normalized;
        transform.position += dir * speed * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            12f * Time.deltaTime);
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsSpawned || networkTransform == null)
            return;

        GUIStyle style = new(GUI.skin.box)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.75f, 0.9f, 1f) }
        };
        string mode = networkTransform.PositionInterpolationType.ToString();
        GUI.Box(new Rect(Screen.width - 285f, 16f, 265f, 58f),
            $"Interpolación: {mode}\n1 Legacy · 2 Lerp · 3 Smooth", style);
    }
}
