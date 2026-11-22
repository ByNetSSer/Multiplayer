# Laboratorio 9 · La mochila que vuelve

Escena: `Assets/Scenes/Lab09_ReturningBackpack.unity`.

## Qué se reconstruye y qué se descarta

| Estado | Dónde vive | Al reconectar | Entre sesiones |
|---|---|---|---|
| Ranuras, tipo y cantidad | `NetworkList<InventorySlot>` | Se reconstruye desde el Guardarropa del servidor | Se carga desde Cloud Save |
| Relación PlayerId → ClientId | `Lab09ReconnectCoordinator` | Se crea durante `ConnectionApprovalCallback` | Se vuelve a crear al entrar |
| Rol Guardián/Explorador | Diccionario por `PlayerId` | Se conserva mientras vive el servidor | Se decide de nuevo en una sesión nueva |
| Cofre abierto y progreso | `NetworkVariable` del `ChestController` | Ya se replica a quien entra tarde en `OnNetworkSpawn` | Se descarta al terminar la partida |
| Cubos, árboles y física | Objetos de mundo de red | El mundo actual se replica, no se serializa como mochila | Se descarta al terminar la partida |

## Prueba sugerida

1. Abre `Lab09_ReturningBackpack`, espera el aviso de Authentication/Cloud Save y usa **Iniciar Host**.
2. Talar un árbol tres veces con `H`; el panel muestra la madera apilada.
3. Cerrar la instancia del jugador o usar **Desconectar**. El servidor registra el `PlayerId` y las casillas en el Guardarropa.
4. Volver a entrar con la misma instalación. Se envía el mismo `PlayerId` en `ConnectionData`, se asigna el nuevo `ClientId`, se restauran las ranuras y se elimina la copia del Guardarropa.
5. Repetir el reingreso: no aparecen objetos ni cantidades duplicadas.
6. Reiniciar la sesión. El dueño carga `lab09_backpack` desde Cloud Save y solicita una única restauración al servidor.

## Configuración de Unity Services

El proyecto requiere estar vinculado en **Project Settings > Services** a un proyecto de Unity Services. El paquete `com.unity.services.cloudsave` instala Authentication como dependencia. Sin vínculo o conexión, el Guardarropa de la sesión continúa funcionando y la consola avisa que Cloud Save no está disponible.
