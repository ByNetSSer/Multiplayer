# TreeNode + LogItem + PlayerInventory - Analisis

## TreeNode (Arbol)

### Estados
- **Libre**: No tiene dueno (OwnedByServer = true) → Color verde
- **Ocupado**: Un jugador lo reclamo → Color rojo
- **Destruido**: Se acabaron los logs → Despawneado, spawnean LogItems

### Metodos importantes

| Metodo | Que hace |
|--------|----------|
| `ClaimServerRpc()` | Jugador reclama el arbol. Server cambia ownership. |
| `ChopServerRpc()` | Jugador talaga. Server resta 1 log. |
| `SpawnLogsServerRpc()` | Server instancia 5 LogItem en posiciones random. |
| `ChangeColor()` | Cambia color segun ownership. |

### Flujo de talar un arbol
```
1. Click en arbol libre
   → ClaimServerRpc() → ChangeOwnership(jugadorId)
   → ChangeColor() se ejecuta en todos (OnOwnershipChanged)

2. Click en arbol propio (logs > 0)
   → ChopServerRpc() → currentLogs.Value -= 1
   → OnValueChanged se ejecuta en todos los clientes

3. Click en arbol propio (logs = 0)
   → ChopServerRpc() detecta logs <= 0
   → SpawnLogsServerRpc() → instancia 5 LogItems
   → NetworkObject.Despawn(true) → elimina el arbol
```

---

## LogItem (Tronco en el suelo)

### Que hace
- Aparece cuando un arbol se destruye.
- Se puede clickear para recogerlo.
- Al recogerlo, se suma 1 al inventario del jugador y se destruye.

### Flujo
```
Jugador hace click en LogItem
  → Es el dueño? (IsOwner)
  → Si: PickUpServerRpc(jugadorId)
  → Server busca el PlayerObject del jugador
  → Server busca el componente PlayerInventory
  → Server suma 1 a woodCount (NetworkVariable)
  → Server destruye el LogItem (Despawn)
```

### ¿Por que IsOwner?
El LogItem se spawnea sin ownership (el server lo instancia).
Necesitamos que CUALQUIER jugador pueda recogerlo.
La validacion de ownership esta en PickUpServerRpc usando el OwnerClientId.

---

## PlayerInventory (Inventario)

### NetworkVariable
```csharp
public NetworkVariable<int> woodCount = new NetworkVariable<int>(
    0,                                  // valor inicial
    NetworkVariableReadPermission.Everyone,  // todos pueden leer
    NetworkVariableWritePermission.Server    // solo server puede escribir
);
```

### ¿Por que Server write permission?
- Solo el servidor modifica el estado del juego.
- Los clientes **piden** (AddWood → RequestAddWoodServerRpc) pero no modifican directamente.
- Esto es **server authoritative** - evita trampas.

### Flujo completo de recoger madera
```
CLIENTE                      SERVIDOR
  |                            |
  |-- Click en LogItem ------->|
  |   (IsOwner = true)         |
  |                            |
  |-- PickUpServerRpc(id) ---->|
  |                            |-- Busca PlayerObject del id
  |                            |-- woodCount.Value += 1
  |                            |-- Despawn(LogItem)
  |                            |
  |<-- woodCount se actualiza -|  (automatico via NetworkVariable)
  |                            |
  |  OnValueChanged se ejecuta |
  |  en todos los clientes     |
```

---

## Prefabs necesarios en Unity

### TreeNode
- `TreeNode.cs`
- `NetworkObject`
- `MeshRenderer` (para el color)
- Collider (para OnMouseDown)
- **Marcar como Network Prefab**

### LogItem
- `LogItem.cs`
- `NetworkObject`
- Collider (para OnMouseDown)
- **Marcar como Network Prefab**
- Asignar al campo `logPrefab` en TreeNode

### Player
- `SimplePlayer.cs` o `PlayerMovement.cs`
- `PlayerInventory.cs`
- `NetworkObject`
- **Marcar como Network Prefab**
