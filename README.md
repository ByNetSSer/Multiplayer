# Laboratorios de Networking

Proyecto de prácticas de Unity Netcode for GameObjects.

## Semana 10 · Predicción y reconciliación

Escena: `Assets/Scenes/Lab10_PredictionReconciliation.unity`.

Se implementó movimiento autoritativo de servidor con predicción local y reconciliación:

- El cliente envía por RPC una entrada `(tick, eje horizontal, eje vertical)`.
- Servidor y cliente usan la misma función `Simulate`.
- El dueño guarda entradas pendientes y mueve un **visual local separado** de su `NetworkObject`.
- El servidor replica posición y último tick procesado mediante `NetworkVariable`.
- Cuando llega el estado autoritativo, el cliente descarta entradas confirmadas, vuelve a esa posición y re-simula las pendientes.
- La tecla `P` permite comparar predicción activa contra seguimiento del servidor; la escena usa 250 ms de demora simulada.
- Al cruzar `X = 4`, el servidor limita la posición para provocar y observar una reconciliación controlada.

| Comportamiento | Sin predicción (`P`) | Con predicción |
|---|---|---|
| Respuesta visual al WASD con 250 ms | Espera la respuesta del servidor | Inmediata en el visual local |
| Fuente de verdad | Servidor | Servidor |
| Llegada de estado | El visual sigue al servidor | Se compara, corrige y re-simulan entradas pendientes |
| Zona X > 4 | Se ve el límite al llegar | Se observa la corrección suavizada |

Para demostrarlo, ejecuta Host y Client desde la misma escena, mueve el Client con y sin predicción y revisa en consola los mensajes `[Lab 10] Reconciliación`.
