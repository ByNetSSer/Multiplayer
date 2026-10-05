# Laboratorio 05: cofre, agarre y lanzamiento con NetworkTransform

## Configuración del avatar

| Ajuste | Valor | Justificación |
|---|---:|---|
| Authority Mode | Owner | El jugador aplica su input sin esperar el viaje al servidor. Se acepta temporalmente el riesgo de confiar en su posición. |
| Position X/Z | Activado | El juego se desplaza sobre el plano. |
| Position Y | Desactivado | El avatar no salta y no necesita enviar altura. |
| Rotation Y | Activado | Replica la dirección hacia la que camina. |
| Rotation X/Z | Desactivado | La cápsula no se inclina. |
| Scale X/Y/Z | Desactivado | La escala no cambia durante el juego. |
| Interpolation Type | Lerp | Mantiene un coste bajo y adapta el buffer a la latencia medida. |
| Use Unreliable Deltas | Activado | Una posición perdida se reemplaza con la siguiente actualización. |
| Half Float Precision | Activado | Reduce el tamaño del estado continuo; la pérdida de precisión resulta imperceptible en esta arena pequeña. |

El movimiento anterior por RPC fue eliminado. `PlayerMovement` modifica únicamente el transform del dueño y `NetworkTransform` replica X/Z y el giro Y.

## Prueba de interpolación con 200 ms

El proyecto queda configurado con `Debug Simulator > Packet Delay MS = 200` y 25 ms de jitter en `Unity Transport`. Mantener el mismo recorrido en cada prueba y grabar el jugador remoto.

| Tipo | Qué observar | Conclusión esperada |
|---|---|---|
| LegacyLerp | Movimiento lineal con más tirones cuando llegan estados a ráfagas. | Es barato, pero no adapta el buffer a la latencia medida. |
| Lerp | Movimiento uniforme con un pequeño retraso visual. | Mejor equilibrio para este proyecto. |
| SmoothDampening | Entradas y giros más suaves, con sensación más amortiguada. | Mayor suavidad y mayor coste de procesamiento. |

El prefab queda configurado en `Lerp`. Durante Play se puede pulsar 1 para `LegacyLerp`, 2 para `Lerp` y 3 para `SmoothDampening`. La esquina superior derecha muestra el modo actual y permite grabar las tres comparaciones en una sola sesión.

## Cofre y autoridad

`ChestController.CurrentOpener` guarda el ID del cliente que mantiene E. El servidor acepta la primera solicitud dentro del rango y rechaza a los demás hasta que el jugador suelta E, se aleja o termina la apertura. `OpenProgress` y `IsOpen` también los escribe exclusivamente el servidor.

Al completar 1.5 segundos, el servidor abre la tapa y crea cuatro cubos. Cada cubo conserva autoridad del servidor. El jugador solicita agarrarlo con F y el servidor valida distancia y disponibilidad antes de ejecutar `TrySetParent`.

## Agarre, lanzamiento y recuperación

- `Switch Transform Space When Parented` está activado en el cubo. Al agarrarlo, su posición se sincroniza en espacio local respecto del avatar.
- `NetworkRigidbody` mantiene la física autoritativa en el servidor y deja las copias remotas cinemáticas.
- F suelta el cubo sin impulso.
- Clic izquierdo lo desparenta y aplica velocidad desde el servidor.
- Si cae por debajo de Y = -3, el servidor llama a `NetworkTransform.Teleport` y lo devuelve a su punto de aparición sin animar un vuelo desde el vacío.

## Guion de evidencia

1. Mostrar en el inspector los ejes del avatar, `Authority Mode = Owner`, `Lerp`, `Use Unreliable Deltas` y `Half Float Precision`.
2. Configurar 200 ms de latencia y comparar brevemente LegacyLerp, Lerp y SmoothDampening.
3. Acercar dos jugadores al cofre. Ambos mantienen E, pero solo uno aumenta el progreso y lo abre.
4. Agarrar un cubo con F, caminar y comprobar que permanece pegado al avatar en ambas ventanas.
5. Soltarlo con F y volver a agarrarlo.
6. Lanzarlo con clic izquierdo.
7. Lanzarlo fuera del mapa y mostrar su recuperación instantánea mediante `Teleport`.

## Controles

- WASD: movimiento.
- E mantenida: abrir el cofre.
- F: agarrar o soltar un cubo.
- Clic izquierdo: lanzar el cubo sostenido.
- Espacio: ataque del Laboratorio 4.
- 1, 2 y 3: comparar LegacyLerp, Lerp y SmoothDampening.
