# Contexto para el proyecto final — Unity Netcode for GameObjects

> Este documento está pensado para pegarse o adjuntarse en otro chat antes de pedir cambios sobre el proyecto final. Resume qué está enseñando el profesor, cómo se aplicó en los laboratorios y qué criterios debe respetar cualquier implementación futura.

## 1. Contexto técnico del proyecto

- Motor: Unity 6 (`6000.3.9f1`).
- Red: Netcode for GameObjects (`2.13.2`).
- Transporte: Unity Transport.
- Modelo actual: Host/Client con servidor autoritativo.
- Entrada actual: teclado mediante `Input.GetKey...`.
- Cada laboratorio tiene una escena independiente.
- Los objetos de red deben estar registrados en `DefaultNetworkPrefabs`.
- El primer cliente aprobado funciona como Guardián y los siguientes como Exploradores.

Escenas actuales:

- `Assets/Scenes/SampleScene.unity`: trabajo anterior/base.
- `Assets/Scenes/Lab06_GuardianChest.unity`: propiedad y ciclo de vida de objetos.
- `Assets/Scenes/Lab07_InventoryPool.unity`: inventario apilable y pool de red.

Antes de modificar el proyecto se debe inspeccionar la escena, los prefabs, la consola y los scripts reales. No asumir que una solución genérica encaja sin revisar la autoridad y el ciclo de vida existentes.

---

## 2. Objetivo pedagógico general

El profesor no está evaluando solamente que “funcione”. Los laboratorios buscan demostrar que el grupo comprende:

1. Quién tiene autoridad sobre cada dato o acción.
2. La diferencia entre estado persistente y evento puntual.
3. Qué debe validar el servidor.
4. Cómo evitar datos de red redundantes.
5. Cómo transferir propiedad correctamente.
6. Cómo controlar spawn, despawn y desconexión.
7. Cómo sincronizar datos compactos con `NetworkList`.
8. Cómo evitar `Instantiate`/`Destroy` repetitivos usando un pool de red.
9. Cómo reiniciar correctamente los objetos reutilizados.

Para el proyecto final, cada mecánica debe poder explicarse con estas preguntas:

- ¿Quién puede solicitarla?
- ¿Quién la valida?
- ¿Quién modifica el estado real?
- ¿Qué se replica como estado?
- ¿Qué se envía como evento?
- ¿Qué ocurre con un cliente tardío?
- ¿Qué ocurre al desconectarse el dueño?
- ¿Qué debe reiniciarse si el objeto reaparece o vuelve al pool?

---

## 3. Laboratorio 4 — Estado, eventos y autoridad del combate

### Conceptos enseñados

- La vida es estado persistente y autoritativo del servidor.
- Una solicitud de ataque es un evento del cliente hacia el servidor.
- El servidor valida alcance, objetivo, reglas y daño.
- Los clientes no restan vida directamente.
- La muerte puede ser un estado derivado: `IsDead => Health.Value <= 0`.
- Sonido, partículas y daño flotante son eventos de acompañamiento.
- El feedback puramente visual puede enviarse como no fiable cuando perder un paquete no rompe el juego.

### Patrón esperado

```text
Dueño presiona atacar
        ↓
RPC de solicitud al servidor
        ↓
Servidor valida impacto y reglas
        ↓
Servidor modifica NetworkVariable de vida
        ↓
Todos observan el nuevo estado
        ↓
RPC cosmético reproduce sonido/VFX
```

### Criterios para el proyecto final

- Nunca confiar en daño, posición de impacto o recompensa calculados exclusivamente por el cliente.
- No crear una `NetworkVariable<bool> IsDead` si puede derivarse de la vida.
- Suscribirse y desuscribirse simétricamente de `OnValueChanged`.
- La UI observa el estado; no es la autoridad del estado.

---

## 4. Laboratorio 5 — NetworkTransform, latencia y objetos agarrables

### Conceptos enseñados

- Configurar únicamente los ejes que realmente deben sincronizarse.
- Elegir correctamente `AuthorityMode`.
- Comparar interpolación bajo aproximadamente 200 ms de latencia.
- Usar `Use Unreliable Deltas` y `Half Float Precision` cuando la pérdida de precisión sea aceptable.
- Usar parenting de red para objetos agarrados.
- Activar el cambio de espacio del transform cuando el objeto se convierte en hijo.
- Mantener la física autoritativa en el servidor.
- Usar `Teleport` para recuperaciones o cambios instantáneos que no deben interpolarse atravesando el mapa.

### Error importante ya encontrado

Un objeto agarrado puede rotar con el jugador pero quedarse atrás si solamente se sincroniza la rotación o si el parenting/espacio del `NetworkTransform` está mal configurado. Para objetos sostenidos se debe comprobar:

- Parenting de red válido.
- `Switch Transform Space When Parented`.
- Autoridad coherente entre `NetworkTransform` y `NetworkRigidbody`.
- Posición local correcta respecto al punto de agarre.
- No escribir el transform simultáneamente desde servidor y cliente.

### Criterios para el proyecto final

- No sincronizar ejes que nunca cambian.
- No utilizar interpolación para respawns o recuperación fuera del mapa.
- En clientes no autoritativos, la física normalmente debe permanecer cinemática.
- Probar movimientos y objetos agarrados con latencia simulada, no solamente como host.

---

## 5. Laboratorio 6 — Roles, propiedad, transferencia y ciclo de vida

Escena: `Assets/Scenes/Lab06_GuardianChest.unity`.

### Conceptos enseñados

- `ConnectionApprovalCallback` decide si entra un cliente y qué prefab de jugador recibe.
- `PlayerPrefabHash` permite seleccionar Guardián o Explorador por código.
- El servidor crea objetos de red ante una solicitud autorizada.
- El botín puede nacer con dueño mediante `SpawnWithOwnership`.
- La propiedad puede transferirse mediante `ChangeOwnership`.
- `OnOwnershipChanged`, `OnGainedOwnership` y `OnLostOwnership` permiten reaccionar al cambio.
- Un objeto puede consumirse con `Despawn(true)`.
- Con `DontDestroyWithOwner = true`, el objeto no tiene que desaparecer con su dueño.
- Al desconectarse un dueño, el servidor puede usar `RemoveOwnership` para dejar el objeto abandonado.

### Implementación actual

- Primer jugador: Guardián.
- Siguientes jugadores: Exploradores.
- `C`: el Guardián solicita crear un cofre.
- Mantener `E`: apertura exclusiva del cofre.
- El cofre genera cuatro cubos pertenecientes a quien lo abrió.
- `F`: agarrar o soltar cubo.
- Clic izquierdo: lanzar.
- `G`: regalar al jugador más cercano.
- `Q`: consumir.
- Si el dueño se desconecta, el cubo queda en el suelo sin dueño.

### Correcciones importantes realizadas

- Un cofre abierto deja de ser interactuable tanto en el servidor como en el HUD local.
- Cada cofre nuevo reinicia bloqueo, progreso y estado abierto.
- La tapa usa una bisagra real; no rota desde el centro del modelo.
- La apertura visual interpola suavemente el progreso replicado.

### Criterios para el proyecto final

- El cliente solicita; el servidor instancia, valida y asigna propiedad.
- No permitir que un cliente transfiera objetos que no posee.
- La desconexión debe tener una política explícita: destruir, transferir, abandonar o conservar.
- No confundir `OwnerClientId == 0` con “sin dueño”: el host normalmente utiliza el ID 0.
- Para representar “sin dueño” puede usarse un valor explícito como `ulong.MaxValue`.

---

## 6. Laboratorio 7 — NetworkList, inventario apilable y pool de red

Escena: `Assets/Scenes/Lab07_InventoryPool.unity`.

### Struct de inventario

`InventorySlot` contiene:

- Tipo de objeto.
- Cantidad.
- `INetworkSerializable` para viajar por red.
- `IEquatable<InventorySlot>` para que `NetworkList` compare correctamente sus valores.

El struct debe ser compacto y contener datos, no referencias a `GameObject`, componentes o materiales.

### NetworkList y UI

- El servidor tiene permiso de escritura.
- El dueño tiene permiso de lectura.
- `OnListChanged` actualiza el HUD únicamente cuando cambia la lista.
- El `EventType` permite distinguir `Add`, `RemoveAt`, `Value`, `Clear`, etc.
- No se debe reconstruir la UI de inventario cada frame.

### Guardado de datos y despawn

Cuando se tala un árbol:

1. El cliente solicita talar.
2. El servidor valida jugador, distancia y golpes restantes.
3. El servidor agrega madera a la `NetworkList`.
4. El árbol desaparece del mundo.
5. El inventario conserva el dato descriptivo, no el `GameObject` del árbol.

### Pool de red

`NetworkObjectPool` implementa `INetworkPrefabInstanceHandler`:

- Se registra mediante `NetworkManager.PrefabHandler.AddHandler`.
- Cada peer mantiene su propio pool local para representar los mismos objetos de red.
- El servidor decide cuándo hacer spawn y despawn.
- El handler entrega una instancia reutilizada en vez de crear una nueva cada vez.
- Al despawnear, la instancia se desactiva y vuelve a la cola del pool.

No se debe reparentar un `NetworkObject` despawneado solo para organizar el pool: Netcode puede advertir que el objeto únicamente puede reparentarse después del spawn. Guardar la referencia y desactivar el objeto es suficiente.

### Reinicio obligatorio

Un objeto reciclado no debe conservar su pasado. El árbol reinicia:

- Golpes restantes.
- Color y texto visual.
- Posición y rotación al obtenerse del pool.
- Suscripciones a eventos.
- Cualquier velocidad o estado físico, si lo tuviera.

El patrón debe ser:

```text
OnNetworkSpawn
  → inicializar estado válido
  → suscribirse
  → aplicar presentación

OnNetworkDespawn
  → desuscribirse
  → limpiar estado temporal
  → dejar la instancia lista para reutilizar
```

### Implementación actual

- Seis árboles aparecen desde el pool.
- `H`: talar árbol cercano.
- Cada árbol necesita tres golpes.
- Otorga tres unidades de madera.
- La madera se apila en el inventario.
- El árbol hace `Despawn(false)` y regresa al pool.
- Reaparece cuatro segundos después con `3/3` golpes.
- Los jugadores del Lab 7 usan prefabs separados para no agregar el HUD de inventario al Lab 6.

---

## 7. Matriz de autoridad recomendada para el proyecto final

| Elemento | Tipo | Autoridad | Replicación/acción |
|---|---|---|---|
| Vida | Estado | Servidor | `NetworkVariable` |
| Ataque solicitado | Evento | Dueño → servidor | RPC con validación |
| Resultado del daño | Estado | Servidor | Modificación de vida |
| Sonido/partículas | Evento cosmético | Servidor → clientes | RPC, no fiable si procede |
| Movimiento de avatar | Estado continuo | Según diseño, normalmente dueño | `NetworkTransform` configurado |
| Física de objetos lanzados | Estado continuo | Servidor | `NetworkRigidbody` |
| Dueño de botín | Estado nativo | Servidor | Ownership de `NetworkObject` |
| Inventario | Estado persistente | Servidor; lectura del dueño | `NetworkList` |
| Solicitud de tala | Evento | Dueño → servidor | RPC |
| Golpes del árbol | Estado | Servidor | `NetworkVariable<int>` |
| Spawn/despawn de árbol | Ciclo de vida | Servidor | `Spawn`/`Despawn(false)` |
| Instancia visual reciclada | Recurso local | Cada peer | `INetworkPrefabInstanceHandler` |
| Muerte | Estado derivado | Todos | `Health <= 0` |

---

## 8. Arquitectura recomendada para integrar todo al proyecto final

No copiar todos los laboratorios como sistemas aislados dentro de una sola escena. Integrarlos por responsabilidad:

```text
NetworkBootstrap
├── aprobación de conexiones
├── asignación de prefab/rol
└── eventos de conexión y desconexión

Player
├── PlayerInput / PlayerInteraction
├── PlayerHealth
├── PlayerCombat
├── PlayerInventory
├── PlayerRole
└── NetworkTransform

WorldResources
├── ResourceNode / TreeNode
├── NetworkObjectPool
└── ResourceSpawnCoordinator

WorldItems
├── pickup
├── ownership
├── physics
└── consumo/transferencia

Presentation
├── HUD de vida
├── HUD de inventario
├── mensajes de interacción
└── sonido/VFX
```

### Reglas de integración

- Mantener una sola fuente autoritativa por dato.
- Separar reglas de juego de la presentación.
- Evitar un `GameManager` gigantesco con todas las mecánicas.
- No usar nombres de objetos de escena como API si puede existir una referencia explícita.
- No usar búsquedas globales en cada frame.
- Los RPC deben expresar intención: solicitar ataque, talar, recoger, regalar o consumir.
- El servidor debe volver a comprobar las condiciones aunque el cliente ya las haya comprobado.
- La UI nunca debe modificar directamente los datos de red.
- Los clientes tardíos deben recibir el estado actual mediante `NetworkVariable`/`NetworkList`, no depender de haber visto eventos pasados.

---

## 9. Errores que deben evitarse

### Configuración diferente entre host y cliente

El mensaje `NetworkConfig mismatch` suele indicar que host y cliente no tienen la misma configuración o catálogo de prefabs. Comprobar:

- Misma versión/build del proyecto.
- Mismos prefabs registrados y en el mismo estado.
- Misma configuración del `NetworkManager`.
- No ejecutar un build antiguo contra un Editor modificado.

### Escritura desde el cliente

No permitir que el cliente:

- Reste vida.
- Aumente inventario.
- Decida recompensas.
- Haga spawn/despawn autoritativo.
- Cambie propiedad sin validación.

### Estados redundantes

No sincronizar datos que pueden calcularse de otro estado. Ejemplo:

```csharp
public bool IsDead => Health.Value <= 0;
```

### Suscripciones duplicadas

Toda suscripción en `OnNetworkSpawn` debe retirarse en `OnNetworkDespawn`.

### Pool sin reset

No basta con desactivar y reactivar. Reiniciar:

- Contadores.
- Velocidad.
- Parenting.
- Dueño lógico si corresponde.
- Temporizadores.
- Animaciones.
- Materiales/colores.
- Eventos y callbacks.

### Validar solamente como host

El host reúne servidor y cliente en un proceso y puede ocultar errores. La entrega debe probar también:

- Cliente remoto.
- Observador no dueño.
- Transferencia de ownership.
- Desconexión.
- Cliente tardío.
- Latencia simulada.

---

## 10. Lista de validación antes de entregar el proyecto final

### Compilación y escena

- [ ] Unity compila sin errores.
- [ ] Consola sin errores o advertencias nuevas relacionadas.
- [ ] No existen scripts perdidos ni prefabs rotos.
- [ ] Todas las escenas necesarias están en Build Settings.
- [ ] Los prefabs de red están registrados.

### Autoridad

- [ ] Cada acción del cliente se valida en el servidor.
- [ ] Solo el servidor escribe vida, inventario y recursos.
- [ ] Las escrituras del dueño están justificadas y limitadas.
- [ ] Los RPC usan permisos apropiados.

### Ciclo de vida

- [ ] Spawn y despawn funcionan repetidamente.
- [ ] La desconexión tiene una política definida.
- [ ] Los objetos pooled reinician todo su estado.
- [ ] Las suscripciones se eliminan correctamente.
- [ ] Un cliente tardío recibe el estado correcto.

### Jugabilidad

- [ ] Host puede jugar.
- [ ] Cliente puede entrar y controlar solo su avatar.
- [ ] Dos clientes ven el mismo resultado.
- [ ] No se pueden duplicar recompensas enviando varias solicitudes inválidas.
- [ ] La UI del inventario corresponde al dueño correcto.
- [ ] Los objetos transferidos cambian realmente de ownership.
- [ ] Los objetos agarrados no se quedan atrás ni se desincronizan.

### Rendimiento

- [ ] No se usa `Instantiate/Destroy` repetidamente para recursos frecuentes.
- [ ] No se reconstruye UI cada frame.
- [ ] No existen RPC por frame sin necesidad.
- [ ] Solo se sincronizan ejes y datos necesarios.

---

## 11. Evidencias recomendadas para el video

1. Mostrar la escena del proyecto final.
2. Iniciar Host y luego uno o dos clientes.
3. Mostrar roles o ownership.
4. Ejecutar combate y evidenciar vida autoritativa.
5. Mostrar un objeto agarrado/lanzado bajo latencia.
6. Abrir/generar botín y mostrar propiedad.
7. Regalar y consumir un objeto.
8. Desconectar al dueño y mostrar la política elegida.
9. Talar varios recursos y mostrar el inventario apilándose.
10. Mostrar que un recurso desaparece y reaparece desde el pool con estado limpio.
11. Mostrar la consola sin errores.

La explicación oral debe indicar qué observaron, qué concluyeron y por qué eligieron cada autoridad, variable, RPC o estrategia de pooling.

---

## 12. Instrucción sugerida para otro chat

Se puede iniciar otro chat con este texto:

> Lee completamente `PROJECT_FINAL_NETWORKING_GUIDE.md` antes de modificar el proyecto. Inspecciona primero la arquitectura, escenas, prefabs, configuración de Netcode y consola actuales. Integra los sistemas al proyecto final respetando servidor autoritativo, separación entre estado y eventos, ownership, desconexión, `NetworkList` y pooling. No copies los laboratorios como managers paralelos si pueden integrarse por responsabilidad. No modifiques escenas de laboratorios existentes salvo que se solicite. Después de cada cambio, compila, revisa la consola y valida Host más al menos un cliente cuando sea posible. Informa con precisión qué fue probado y qué queda pendiente.

