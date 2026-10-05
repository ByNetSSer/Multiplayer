# PlayerMovement - Analisis

## Codigo del profe (estilo viejo)
```csharp
[ServerRpc]
public void MoveServerRpc(Vector3 pos)
{
    MoveClientRpc(pos);
}

[ClientRpc]
public void MoveClientRpc(Vector3 pos)
{
    if (IsOwner) return;
    transform.position = pos;
}
```

## Version implementada (estilo nuevo)
```csharp
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
```

## Diferencias
1. `SendTo.NotOwner` reemplaza el `if (IsOwner) return;` - mas limpio.
2. El `[Rpc(SendTo.X)]` es mas explicito sobre a quien va el mensaje.
3. Funcionan igual: cliente envia posicion -> servidor reenvia a todos.

## Flujo
```
Jugador A presiona W
  → Movement() calcula nueva posicion
  → transform.position += dir (mover localmente)
  → MoveServerRpc(posicion) → servidor recibe
  → MoveClientRpc(posicion) → todos los demas clientes reciben
  → Jugadores B, C, etc. mueven al jugador A a esa posicion
```
