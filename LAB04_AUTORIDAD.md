# Laboratorio 04: primer combate y matriz de autoridad

## Matriz de autoridad

| Entidad | Variable o suceso | Tipo de red | Quién decide | Quién replica | Quién visualiza | Fiabilidad y justificación |
|---|---|---|---|---|---|---|
| PlayerCombat | `Health` | Estado, `NetworkVariable<int>` | Servidor | Servidor a todos | Todos | Persistente. Un jugador que entra tarde necesita conocer la vida actual. |
| PlayerCombat | Solicitud de ataque | Evento, RPC | Dueño solicita; servidor valida | Dueño al servidor | Servidor | Reliable. La intención debe llegar para que el servidor pueda evaluarla. |
| PlayerCombat | Golpe confirmado | Evento, RPC | Servidor | Servidor a todos | Todos | Reliable. Importa cada ocurrencia y transporta daño y vida restante. |
| PlayerCombat | Partículas de impacto | Evento, RPC | Servidor | Servidor a todos | Todos | Unreliable. Es decoración; perder una partícula no cambia el juego. |
| PlayerCombat | `IsDead` | Estado derivado local | Cálculo `Health <= 0` | No se replica | Todos | Evita una segunda variable que podría contradecir la vida. |
| PlayerCombat | Color de muerto | Visual derivado | Cada cliente desde `IsDead` | No se replica | Todos | El material gris se reconstruye desde la vida replicada. |
| PlayerMovement | Input bloqueado | Comportamiento derivado | Cliente dueño desde `IsDead` | No se replica | Dueño | Un jugador con vida 0 no procesa movimiento ni ataque. |
| PlayerCombat | Barra de vida | UI derivada | Cada cliente desde `Health` | No se replica | Todos | `OnValueChanged` actualiza el valor y color sin tráfico adicional. |

## Decisiones tomadas

Observamos que la vida describe cómo está el mundo y debe estar disponible para clientes que se conecten tarde. Por eso el servidor es el único escritor de `Health`. El cliente no envía una vida final ni un daño aplicado; solo envía el identificador del objetivo que quiere atacar.

El servidor comprueba que quien llama sea dueño del atacante, que ambos jugadores estén vivos, que el objetivo exista, que esté dentro del rango y que haya pasado el enfriamiento. Solo entonces resta vida. Esto evita que un cliente alterado decida su daño o golpee desde cualquier distancia.

El PPT muestra `RequireOwnership = true`. En Netcode for GameObjects 2.13 esa propiedad aparece obsoleta; el proyecto usa su reemplazo actual, `InvokePermission = RpcInvokePermission.Owner`, con el mismo propósito.

El impacto se comunica aparte porque importa cuántas veces ocurrió. El RPC fiable incluye el daño y la vida restante para que el número flotante no dependa del orden de llegada de la `NetworkVariable`. Las partículas viajan como evento no fiable porque son cosméticas. La muerte, el color gris y el bloqueo de controles se deducen de `Health <= 0`, sin replicar un booleano redundante.

## Evidencia sugerida

1. Ejecutar dos instancias con Multiplayer Play Mode o una compilación y el Editor.
2. Iniciar una como **Host** y la otra como **Cliente**.
3. Acercar ambos jugadores con WASD y atacar con Espacio.
4. Capturar en ambas ventanas la misma vida, el daño flotante, las partículas y el cambio gris al llegar a 0.
5. Como evidencia de autoridad, mostrar la consola del servidor y este documento junto al código de `RequestAttackRpc`.

## Correspondencia con la rúbrica

- Matriz de autoridad: tabla anterior con responsable, tipo y fiabilidad.
- Estado: `Health`, permiso de escritura del servidor y `OnValueChanged`.
- Evento: `RequestAttackRpc` envía intención y el servidor valida el impacto.
- Estado derivado: `IsDead`, material gris y bloqueo de input.
- Acompañamiento: sonido y daño flotante fiables; partículas no fiables.
