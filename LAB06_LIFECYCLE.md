# Laboratorio 06: ciclo de vida y propiedad del cofre

## Flujo implementado

1. Antes de iniciar la red, `GameManager` activa `ConnectionApproval` y registra `ConnectionApprovalCallback`.
2. El primer cliente aprobado recibe el prefab Guardián mediante `PlayerPrefabHash`. Los siguientes reciben el prefab Explorador. Netcode crea el avatar como `PlayerObject`.
3. El Guardián pulsa C. El RPC de su propio `PlayerInteraction` llega al servidor con permiso Owner y el servidor crea `NetworkChest` mediante `InstantiateAndSpawn` frente al avatar.
4. Cualquier jugador puede mantener E para abrir el cofre. El servidor conserva el bloqueo exclusivo del Lab 5.
5. Al abrirse, cada cubo nace con `SpawnWithOwnership`, asignado al cliente que completó la apertura.
6. El material se repinta usando el dueño lógico replicado. `OnOwnershipChanged`, `OnGainedOwnership` y `OnLostOwnership` registran las transiciones.
7. El dueño agarra su cubo con F. G lo suelta y transfiere al jugador más cercano mediante `ChangeOwnership`.
8. Q consume el cubo con `Despawn(true)`.
9. Si el dueño se desconecta, el servidor desparenta el cubo, activa su física, ejecuta `RemoveOwnership` y lo deja gris en el suelo. `DontDestroyWithOwner` permanece activado en el prefab.

## Controles

| Control | Acción |
|---|---|
| WASD | Mover el avatar propio |
| C | Crear el cofre, solo Guardián |
| Mantener E | Abrir el cofre |
| F | Agarrar o soltar cubo propio/abandonado |
| G | Regalar cubo sostenido al jugador más cercano |
| Q | Consumir cubo sostenido |
| Clic izquierdo | Lanzar cubo sostenido |
| Espacio | Ataque del Lab 4 |

## Evidencia sugerida

1. Mostrar que Host aparece como Guardián y Cliente como Explorador.
2. Intentar C con Explorador y confirmar que no aparece un cofre.
3. Pulsar C con Guardián y mostrar el cofre delante del personaje.
4. Abrirlo con el Explorador y comprobar que los cuatro cubos nacen con su color de dueño.
5. Agarrar un cubo, acercarse al Guardián y pulsar G. Mostrar el cambio de color y los mensajes de ownership en Consola.
6. Consumir otro cubo con Q y comprobar que desaparece para todos.
7. Desconectar al dueño de un cubo. Confirmar que el cubo no se destruye, cae al suelo y cambia a gris.

## Decisiones de autoridad

- El cliente solo solicita crear, abrir, agarrar, regalar o consumir.
- El servidor valida rol, distancia, dueño y objetivo antes de cambiar el mundo.
- `NetworkObject` mantiene la propiedad real. `LootOwnerId` permite representar claramente el estado abandonado incluso en Host, donde el ID del servidor y del jugador Host coinciden.
- La propiedad no controla la física: el `NetworkTransform` de los cubos continúa con autoridad Server para mantener un único resultado físico.
