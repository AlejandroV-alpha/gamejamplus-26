# Integración de Photon Fusion

Rama: `codex/photon-fusion-integration`.

## Configurar una copia nueva del proyecto

1. Cambiar a esta rama y abrir el proyecto con Unity **6000.5.6f1**. Esperar a que terminen la resolución de paquetes, importación y compilación, antes de iniciar los jugadores virtuales.
2. Obtener un App ID de tipo **Fusion** en el [dashboard de Photon](https://dashboard.photonengine.com/). Para compartir sala, el equipo debe utilizar la misma aplicación. El App ID identifica la aplicación cliente; este proyecto lo mantiene fuera de Git por decisión del equipo. No sustituye autenticación ni validación de gameplay.
3. Abrir una terminal normal de Windows PowerShell en la raíz del proyecto y ejecutar:

   ```powershell
   powershell -NoProfile -File .\Tools\Configure-PhotonLocal.ps1
   ```

   Pegar el identificador en la entrada oculta. El script valida el formato y guarda únicamente una copia cifrada en el perfil local de Windows. No pasar el valor como argumento de terminal ni colocarlo en escenas, scripts, capturas o commits. Si la política local bloquea scripts, revisar la política de ejecución del equipo; no deshabilitarla globalmente para esta configuración.
4. Abrir `Assets/00_Scenes/j_MarsFusion.unity`. Comprobar que `j_PhotonFusionSession` tenga asignados `playerPrefab`, `runnerPrefab` y `spawnPoint`. La escena ya está registrada en Build Settings.
5. Hacer primero una prueba local. Después activar Player 2 en **Window > Multiplayer > Multiplayer Play Mode** y hacer la prueba online descrita abajo.

No copiar ProjectSettings ni Packages del proyecto descargado de Photon sobre este proyecto. El SDK y las dependencias necesarias ya están versionados en esta rama.

### Prueba con dos jugadores

1. Detener Play. Si Player 2 conserva un estado anterior a la compilación, desactivarlo, esperar a que cierre y activarlo otra vez.
2. Entrar en Play y comprobar en ambas consolas `[Photon local config] Configuración cifrada cargada.`. Desactivar Pausa si está encendida.
3. Escribir `Mars-Coop` en ambas ventanas. Pulsar **Crear / entrar a sala online** en cada una. **Prueba local** inicia sesiones Single separadas: nunca conecta a los dos jugadores.
4. Esperar los logs `start-result ok=True`, `player-spawned` (o `player-already-spawned`) y `session-ready` en cada proceso. Comprobar visualmente que cada jugador ve al otro moverse.
5. Abrir la tablet con Tab: solo el propietario debe ver la interfaz; el otro observa la tablet equipada. Probar V/H y comprobar las expresiones desde el otro cliente.
6. Desconectar, volver a entrar y verificar que no aparecen jugadores duplicados. La recarga de escena al desconectar es intencional.

Entre computadoras, configurar el archivo cifrado independientemente bajo cada usuario. Para un build Windows, incluir `j_MarsFusion` en el perfil de build y configurar ese mismo usuario de Windows con el script. La lectura en builds está implementada, pero el build todavía no se ha validado. No distribuir el archivo DPAPI de otra persona ni asumir que funcionará en otra cuenta o máquina.

## Cómo se relaciona con Netcode de Unity

Fusion reemplaza la red de la **nueva escena**, no funciona como un transporte añadido debajo de Netcode. Las escenas anteriores siguen utilizando NGO y sirven de referencia. No arrancar `GameNetworkManager` y `NetworkRunner` para controlar al mismo jugador.

| Template anterior — NGO | Integración nueva — Fusion | Diferencia práctica |
| --- | --- | --- |
| `GameNetworkManager` / `NetworkManager` | `MarsFusionSession` crea un `NetworkRunner` | Arranca, entra en sala y cierra la sesión. El runner cerrado no se reutiliza. |
| `Unity.Netcode.NetworkObject` | `Fusion.NetworkObject` | Son tipos distintos; un prefab NGO no queda migrado al añadir Fusion encima. |
| `Unity.Netcode.NetworkBehaviour` | `Fusion.NetworkBehaviour` | Cambian los callbacks, los permisos y la sincronización. |
| `OnNetworkSpawn()` | `Spawned()` | Inicialización una vez que el objeto pertenece a la simulación de red. |
| `OnNetworkDespawn()` | `Despawned(runner, hasState)` | Liberar UI y objetos locales; no asumir que el estado sigue accesible. |
| `NetworkVariable<T>` | Propiedad `[Networked]` | Representa estado actual replicado; la autoridad de estado realiza las escrituras. |
| `IsOwner` | `HasStateAuthority` en este controlador Shared | Aquí cada jugador controla el objeto que crea. No es una equivalencia general para todas las topologías. |
| `ClientId` / `OwnerClientId` | `PlayerRef` / autoridades del `NetworkObject` | No trasladar identificadores numéricos directamente entre sistemas. |
| `NetworkObject.Spawn()` | `Runner.Spawn(...)` | El spawner de escena y la sesión coordinan la aparición; `SetPlayerObject` asocia el avatar al jugador. |
| `ServerRpc` / `ClientRpc` | `[Rpc(sources, targets)]` | Un RPC comunica un evento o petición; el estado persistente debe quedar en propiedades replicadas. |
| Controlador anterior y `NetworkTransform` | `SimpleKCC`, `Player.FixedUpdateNetwork()` | Movimiento con el tick de Fusion y `Runner.DeltaTime`; KCC se ocupa de su estado de movimiento. |
| Presentación / `NetworkAnimator` anterior | `Player.Render()` y `MarsFusionAvatar.Render()` | Derivan animación, sprites y visuales a partir del estado recibido. |
| UI local filtrada por propietario | UI local filtrada por autoridad Shared | La tablet y la cámara no deben crearse ni controlarse como interfaz local de los observadores. |

Shared distribuye la autoridad entre clientes; el master client no equivale a un servidor dedicado que valida todo. Esta integración no implementa la topología Host ni convierte automáticamente el gameplay en autoritativo de servidor. `RPC_ApplyDamage` admite solicitudes de otros clientes y solo limita sus valores: antes de producción faltan reglas de validación del combate. Cambiar a Host exige revisar input, predicción y autoridad, no solo cambiar el enum del modo.

La división entre `Spawned`, `FixedUpdateNetwork`, propiedades `[Networked]` y `Render` sigue el modelo de [NetworkBehaviour de Fusion](https://doc.photonengine.com/fusion/v2/manual/network-behaviour). El procesamiento de inputs y la autoridad usada aquí proceden de [Fusion Starter Shared](https://doc.photonengine.com/fusion/v2/game-samples/fusion-starter/architecture-shared-authority).

## Responsabilidad de cada archivo

| Archivo / asset | Responsabilidad |
| --- | --- |
| `Assets/PhotonStarter/PhotonLocalSettings.cs` | Leer DPAPI, validar formato del App ID, construir settings independientes con `FixedRegion` no nulo. Nunca mostrar el ID. |
| `Assets/PhotonStarter/MarsFusionSession.cs` | Menú, inicio Single/Shared, configuración del runner, sala, logs, aparición local y desconexión. |
| `Assets/PhotonStarter/MarsFusionSpawner.cs` | Solicitar aparición después de que el objeto de escena se adjunte al runner y termine la carga. La asociación existente evita duplicados. |
| `Assets/PhotonStarter/PlayerInput.cs` | Acumular movimiento, mirada y salto entre ticks; ignorar jugadores remotos y controles bloqueados. |
| `Assets/PhotonStarter/Player.cs` | Movimiento con Simple KCC, gravedad, orientación, animación de salto y cámara local con colisión. |
| `Assets/PhotonStarter/MarsFusionAvatar.cs` | Salud, energía, tablet abierta, voz cosmética y expresión; UI local y tablet en mano. |
| `Assets/PhotonStarter/j_FusionRunner.prefab` | Runner y eventos configurados del paquete original. No sustituirlo por un GameObject vacío. |
| `Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab` | Avatar Fusion, KCC, referencias al modelo, animador, hueso de mano y sprites. |
| `Assets/Editor/MarsFusionMigration.cs` | Generador inicial de escena y prefab; evita sobrescribir una escena Fusion ya creada. No se ejecuta en un build. |
| `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` | Configuración del SDK, ticks y assemblies que procesa el weaver. No contiene el App ID privado. |
| `Assets/Photon/Fusion/Editor/FusionEditorHubWindow.cs` | SDK con guardas locales para no abrir el Hub en clones MPPM. Revisar al actualizar Photon. |

## Troubleshooting

| Síntoma / último log | Qué comprobar |
| --- | --- |
| `config-failed` / archivo cifrado ausente | Ejecutar el script de configuración bajo el usuario que ejecuta Unity. Revisar el perfil Windows; el archivo no viene del repositorio. |
| Código de descifrado de Windows | Archivo corrupto o protección DPAPI incompatible con la cuenta/máquina. Volver a configurarlo localmente con el script. No copiar el cifrado de un compañero. |
| Configuración cargada y luego `SetupAppSettings` / `NullReferenceException` | No es falta de App ID. La versión 2.1.1 utiliza `FixedRegion.ToLower()`; mantener la inicialización a cadena vacía o a una región válida. Reiniciar clones después de compilar el arreglo. |
| `start-rejected` | El log indica si hay un intento en curso, un runner existente o referencias de prefab ausentes. Esperar o desconectar; revisar el inspector. |
| `connecting` y luego `OperationTimeout` | Primero confirmar lectura de configuración. Después revisar acceso a Photon, red/VPN/firewall y estado del servicio. Timeout no demuestra que falte el ID. |
| Rechazo de autenticación o aplicación | Comprobar en dashboard que el identificador corresponde a Fusion y que la aplicación está habilitada. No registrar el valor en el reporte. |
| Ambos conectados, pero no se ven | Mismo App ID, `AppVersion` (`MarsCoop-v1`), sala, región y versión del proyecto. La región actual es automática: si difiere entre clientes, configurar la misma región para ambos. Verificar que ninguno use Single. |
| `session-ready` sin avatar | Buscar `player-spawned`, revisar prefab, referencias de la sesión, configuración de objetos de red, escena en Build Settings y ausencia de errores de compilación. |
| Avatar no se mueve / conexión no progresa | Revisar Pausa, foco de la ventana, menú abierto, tablet abierta y salud. La sesión habilita ejecución en segundo plano. |
| Dos cámaras o dos jugadores locales | Evitar arrancar NGO en la escena Fusion; revisar autoridades y aparición duplicada. No conservar un jugador colocado manualmente además del que crea el runner. |
| `FallbackEditorWindow` y `EditorWindow.Close` en Player 2 | Problema de ventanas del Editor en el clon; no prueba falta de App ID. Detener Play, desactivar/reactivar Player 2 tras compilar. No abrir el Hub de Photon en el clon. La guarda evita aperturas del Hub; el error interno de Unity puede persistir. |
| Saltos que arrancan con retraso | Mantener la entrada a `Airborne` en tiempo normalizado 0,34 para el clip del astronauta; no reactivar el ragdoll del template. |
| Materiales o terreno no aparecen igual | La red no replica assets ni materiales. Ambos clientes necesitan la misma revisión del proyecto. |

### Qué enviar al reportar un fallo

Copiar las líneas `[Photon local config]` y `[Mars Fusion pid=... attempt=...]` desde el inicio hasta `start-failed` o `session-ready`, indicar si fue Editor principal, Player 2 o build, modo elegido y versión/commit. Los PID distinguen procesos; `attempt` distingue intentos dentro de la sesión del menú. No incluir el App ID, el archivo DPAPI ni un volcado completo de configuración.

Orden normal: `start-requested → config-ready → runner-created → connecting → start-result → player-spawned/player-already-spawned → session-ready`. El spawner también puede registrar la aparición antes de `start-result`; la verificación de jugador existente es intencional.

## Escena de trabajo

Abrir `Assets/00_Scenes/j_MarsFusion.unity` y entrar en Play:

- **Prueba local** inicia Fusion Single sin conexión.
- **Crear / entrar a sala online** inicia Fusion Shared. Los participantes deben utilizar el mismo App ID, versión, región y nombre de sala.
- WASD, ratón, Espacio y Shift controlan al astronauta. Tab abre la tablet. Esc abre el menú de conexión. V y H prueban las expresiones en Editor/Development Build.
- Al desconectar se recarga la escena, como en Fusion Starter, para restaurar los objetos de escena que Fusion retira durante el cierre.

La escena usa Simple KCC y un prefab nuevo (`Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab`) con el modelo y las animaciones existentes. El movimiento, la cámara y la acumulación de input proceden del ejemplo ThirdPersonCharacter, adaptados a autoridad Shared, bloqueo de controles de la tablet y al inicio del salto del astronauta. La cámara comprueba colisiones con el entorno. El collider del personaje mide 2,05 m.

La tablet, la salud, la energía y el nivel de voz de las caras se replican mediante Fusion. La interfaz sigue siendo local y reutiliza el catálogo y el diseño actuales. El chat de proximidad no está implementado: el proveedor de voz debe alimentar `MarsFusionAvatar.SetVoiceLevel(float)` en el jugador local. `RPC_ApplyDamage` es la entrada de daño del prototipo. No hay respawn ni inventario general nuevos.

## Configuración privada

El App ID se almacena en `%LOCALAPPDATA%/GameJamPlus26/Photon/appid.dpapi`, cifrado con Windows DPAPI para el usuario actual. El lector de ejecución lo descifra únicamente en memoria y entrega una configuración a StartGame. Esta ruta es compartida por el Editor principal, los jugadores virtuales de Multiplayer Play Mode y un ejecutable Windows del mismo usuario. No se serializa en la escena ni en el prefab. La copia anterior en `.local/Photon/` y el asset local `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` y su meta están excluidos de Git.

El archivo cifrado no es portable a otro usuario de Windows. Cada compañero necesita configurar su copia local; no se distribuye dentro del build. No publicar el archivo cifrado ni introducir el identificador en código versionado.

## Alcance de la migración

La integración de `feature/mars-environment` conserva su historial mediante el merge 53. El commit 54 lleva a `j_MarsFusion` el rover, su ruta de cuatro puntos, ambas antenas y el polvo ambiental. El asset de terreno compartido incorpora los cambios de esa rama. Las referencias del rover y del polvo apuntan al terreno y a los objetos de la escena Fusion; no se copian los managers NGO. El polvo es visual local y el rover conserva su patrulla local original: su movimiento todavía no se replica por Fusion.

Se importó el SDK Fusion **2.1.1 Stable 2177**, Simple KCC y los scripts necesarios del proyecto local `Photon Fusion Starter - Multiplayer`. Se conserva la estructura y los metadatos del SDK. No se copiaron Library, Temp, ProjectSettings, los escenarios de demostración ni sus modelos y texturas.

`Player.cs`, `PlayerInput.cs`, `InputActions.cs`, `AnimatorId.cs` y el prefab Runner proceden de Photon Fusion Starter. Las adaptaciones del juego están en `MarsFusionSession`, `MarsFusionSpawner` y `MarsFusionAvatar`. La instalación añade las definiciones del SDK, habilita unsafe y declara Mono.Cecil para su generador de código.

La escena y el prefab nuevos no incluyen objetos o comportamientos de Netcode de Unity. Las escenas anteriores y sus paquetes permanecen para permitir comparación y para el trabajo de los compañeros. Su eliminación del repositorio y la migración de los demás modos, interacciones y sistemas del template no forman parte de esta primera escena migrada.

## Validación

- Compilación del SDK y de los scripts de integración sin errores.
- Sesión local con astronauta, autoridad local, salud 100 y contacto con terreno confirmado por KCC.
- Tablet local y estado abierto; daño de 5 reduce salud a 95 y activa expresión Hurt.
- Se estableció conexión Shared y se comprobó un jugador con autoridad y contacto con terreno tras una aparición manual de diagnóstico.
- La aparición automática Shared y la reconexión todavía requieren repetir la prueba final; una conexión posterior terminó en OperationTimeout. No se ha validado una partida con dos clientes ni un build.
- Se comprobó la exclusión del archivo cifrado. La revisión detectó y eliminó una copia del identificador en el asset local de Photon; ese asset también quedó excluido.
- Corregida la lectura intermitente en jugadores virtuales: lectura directa en ejecución, comprobación de plataforma en runtime y copia cifrada local de respaldo. El registro de Player 2 confirmó `Configuración cifrada cargada` al iniciar Play, igual que el Editor principal. El menú muestra ahora diagnósticos diferenciados sin revelar el identificador.
- El Hub del SDK tiene una adaptación local para omitir apertura automática y navegación de páginas en clones MPPM. Evita intentar abrir ventanas de configuración no admitidas allí; una actualización del SDK debe conservar esta adaptación.
- Corregido el fallo `SetupAppSettings`: Fusion 2.1.1 llama a `FixedRegion.ToLower()` sin comprobar null. La configuración privada ahora inicializa `FixedRegion` a cadena vacía (región automática). El App ID ya se descifraba correctamente; esta excepción ocurría después, antes de conectarse a la nube.
- Logs sin App ID: `[Photon local config]` registra origen, validación y PID; `[Mars Fusion pid=... attempt=...]` registra solicitud, configuración, creación del runner, conexión, resultado, aparición del jugador y cierre. `config-failed` y `start-failed` distinguen lectura y conexión.

Referencia del comportamiento original: [Fusion Starter, arquitectura Shared](https://doc.photonengine.com/fusion/v2/game-samples/fusion-starter/architecture-shared-authority).
