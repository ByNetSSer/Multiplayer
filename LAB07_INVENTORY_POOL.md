# Laboratorio 7 — Inventario apilable y pool de red

Escena: `Assets/Scenes/Lab07_InventoryPool.unity`

## Flujo demostrable

1. Iniciar Host. El bosque aparece con seis árboles obtenidos del pool.
2. Acercarse a un árbol y presionar `H` tres veces.
3. El servidor valida distancia y golpes; agrega `Wood x3` al inventario.
4. `NetworkList<InventorySlot>` emite eventos `Add` o `Value` y actualiza el HUD del dueño.
5. El árbol hace `Despawn(false)`, vuelve al pool y reaparece cuatro segundos después.
6. Al reaparecer recupera `3/3` golpes y sus eventos/visuales quedan reiniciados.

## Correspondencia con la rúbrica

- `InventorySlot`: struct unmanaged con `INetworkSerializable` e `IEquatable`.
- `PlayerInventory`: `NetworkList`, apilado hasta 99 y `OnListChanged` para logs/HUD.
- `TreeNode`: el servidor guarda madera como dato y despawnea el recurso visual.
- `NetworkObjectPool`: implementa `INetworkPrefabInstanceHandler` y se registra en cada peer.
- `TreeNode.OnNetworkSpawn/OnNetworkDespawn`: reinicia golpes, visuales y suscripciones.

## Autoridad

- El cliente solo solicita talar.
- El servidor valida jugador, rango, golpes y modificación del inventario.
- El dueño recibe su inventario; los observadores no necesitan esos datos privados.
- El servidor decide cuándo despawnear, devolver al pool y reaparecer el árbol.
