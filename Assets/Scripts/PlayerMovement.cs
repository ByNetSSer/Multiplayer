using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 5f;

    private PlayerCombat combat;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
    }

    void Update()
    {
        if (!IsOwner || (combat != null && combat.IsDead)) return;
        Movement();
    }

    public void Movement()
    {
        float y = Input.GetAxisRaw("Vertical");
        float x = Input.GetAxisRaw("Horizontal");

        if (y == 0 && x == 0) return;

        Vector3 dir = new Vector3(x, 0, y).normalized;
        transform.position += dir * speed * Time.deltaTime;

        MoveServerRpc(transform.position);
    }

    [Rpc(SendTo.Server)]
    public void MoveServerRpc(Vector3 pos)
    {
        MoveClientRpc(pos);
    }

    [Rpc(SendTo.NotOwner)]
    public void MoveClientRpc(Vector3 pos)
    {
        transform.position = pos;
    }
}
