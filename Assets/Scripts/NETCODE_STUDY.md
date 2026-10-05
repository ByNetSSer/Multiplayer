# Netcode para GameObjects - Guia de Estudio

## Conceptos Clave

### NetworkBehaviour
- Todos los scripts que usan netcode deben heredar de `NetworkBehaviour` en vez de `MonoBehaviour`.
- Provee acceso a `IsOwner`, `IsServer`, `IsSpawned`, `OwnerClientId`, etc.

### NetworkObject
- Todo prefab que se instancia en red necesita un componente `NetworkObject`.
- `Spawn()` - Instancia el objeto en la red (solo en el servidor).
- `Despawn()` - Elimina el objeto de la red.
- `ChangeOwnership(ulong clientId)` - Cambia quién es el dueño del objeto.

---

## RPCs (Remote Procedure Calls)

### ¿Qué son?
Las RPCs son funciones que se llaman en un lado (cliente o servidor) y se ejecutan en el otro. Son la base de la comunicación en Netcode.

### Estilo nuevo (Netcode 2.x)
```csharp
// El cliente llama esto, se ejecuta en el servidor
[Rpc(SendTo.Server)]
public void MiFuncionServerRpc(int datos)
{
    // Aqui el SERVIDOR recibe y ejecuta
}

// El servidor llama esto, se ejecuta en todos los clientes menos el dueño
[Rpc(SendTo.NotOwner)]
public void MiFuncionClientRpc(int datos)
{
    // Aqui los CLIENTES reciben y ejecutan
}
```

### Estilo viejo (que vio el profe)
```csharp
[ServerRpc]  // Se llama desde cliente -> ejecuta en servidor
public void MiFuncionServerRpc(int datos) { }

[ClientRpc]  // Se llama desde servidor -> ejecuta en todos los clientes
public void MiFuncionClientRpc(int datos) { }
```

**Ambos hacen lo mismo**, el nuevo es mas flexible.

---

## Flujo del Juego (The Forest Mini)

```
1. Jugador hace click en arbol libre
   → ClaimServerRpc() → Server cambia ownership al jugador
   → Arbol cambia de color ( verde = libre, rojo = ocupado )

2. Jugador hace click en arbol que le pertenece
   → ChopServerRpc() → Server resta 1 log
   → Cuando logs = 0:
     → SpawnLogsServerRpc() → Server instancia 5 LogItem prefabs
     → NetworkObject.Despawn() → Arbol desaparece

3. Jugador hace click en un LogItem en el suelo
   → PickUpServerRpc() → Server busca el inventario del jugador
   → PlayerInventory.AddWood(1) → Server suma 1 al NetworkVariable
   → NetworkObject.Despawn() → LogItem desaparece
```

---

## NetworkVariable

Variable sincronizada automaticamente entre servidor y clientes:

```csharp
private NetworkVariable<int> myVar = new NetworkVariable<int>(
    defaultValue,
    NetworkVariableReadPermission.Everyone,    // Quien puede leer
    NetworkVariableWritePermission.Server      // Quien puede escribir
);
```

- **Solo el servidor puede modificar el valor.**
- Los clientes ven el valor actualizado automaticamente.
- Se puede escuchar cambios con `OnValueChanged`.

---

## Patron "Server Authoritative"

El servidor es la autoridad:
1. El cliente **pide** hacer algo (via RPC).
2. El servidor **valida** y **ejecuta**.
3. El servidor **notifica** a los clientes del cambio (via RPC o NetworkVariable).

Esto evita trampas porque el cliente nunca modifica el estado directamente.

---

## Flujo visual

```
CLIENTE                    SERVIDOR                    CLIENTE
  |                          |                          |
  |-- MoveServerRpc(pos) --> |                          |
  |                          |-- MoveClientRpc(pos) --> |
  |                          |                          |
  |-- ClaimServerRpc() ----> |                          |
  |                          |-- ChangeOwnership() --->  |
  |                          |                          |
  |-- ChopServerRpc() -----> |                          |
  |                          |-- currentLogs.Value--    |
  |                          |-- SpawnLogsServerRpc()   |
  |                          |-- Despawn(arbol)         |
  |                          |                          |
  |-- PickUpServerRpc() ---> |                          |
  |                          |-- AddWood(1)             |
  |                          |-- Despawn(log)           |
  |                          |                          |
```
