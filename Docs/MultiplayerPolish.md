# Multiplayer polish — rama codex/multiplayer-polish

## Alcance y compatibilidad

La base de esta rama es `4dbc89a`. No se cambian inventario, recetas, mecánica del arma ni lógica de movimiento salvo sensibilidad y bindings locales. La barra limitada a 4–5 objetos y la radio se reservan para otra fase.

- Fusion sigue en Shared. No se ha migrado a Host/Server ni añadido servidores dedicados.
- El protocolo de sala usa `expedition-2` más una huella de las propiedades Networked y la versión de SDK. También hay que incrementar ContentProtocol si cambia un prefab/capacidad/layout sin cambiar tipos de propiedades. Esto reduce mezclas incompatibles; no es un sistema anticheat.
- Todos deben regenerar la build. Las builds anteriores usan otra versión de Photon y no comparten salas con esta rama.
- Región inicial: SA (Sudamérica), configurable antes de conectar. Todos deben elegir la misma región y sala. AUTO se ofrece con advertencia: no garantiza que amigos de regiones distintas se encuentren.
- Las confirmaciones de salida del lobby pasan a estado replicado. Los enemigos Fusion siguen al Master Client y no se destruyen con la salida de su autoridad.
- Durante la salida del lobby se cierra la entrada de nuevos jugadores. La entrada a mitad de misión y la reconexión con restauración del inventario siguen pendientes de validación transaccional.

## Uso

Botón SYSTEMS / AJUSTES del inicio o del menú de pausa: pantalla, voz, controles y conexión. Los textos del nuevo panel tienen ES/EN/PT.

Pantalla: resolución, modo ventana/sin bordes, confirmación con rollback a los 15 s, presets locales bajo/medio/alto, VSync, límite FPS y postprocesado. Los presets usan un clon del asset URP y cambian resolución de render, sombras, MSAA y LOD. No reducen el polvo de la tormenta ni alteran distancias de combate. Preview de resolución solo en ejecutable.

Voz: selección de micrófono, reinicio de captura, loopback local explícito, nivel de entrada, PTT reasignable, micrófono abierto con umbral, volumen y mute por jugador. La prueba local detiene el Recorder de red antes de abrir su propia captura y lo restaura al terminar; usar auriculares. La selección concreta de dispositivo usa el backend Unity. Sin selección se conserva el backend existente y su fallback. Desconexión/reconexión de dispositivo se revisa cada 2 s. No se graba ni guarda audio.

Controles: sensibilidad, invertir Y y reasignación de teclado para Move/Jump/Sprint con cancelación y detección de colisiones. Los controles directos E/Q/R/Tab/I/números permanecen reservados. No está completada la reasignación de todas las acciones del juego.

Diagnóstico: F8 alterna el panel; exporta a `Application.persistentDataPath/Diagnostics`. FPS/frame, RTT a Photon y su variación, región, topología, Voice, protocolo y escena. CPU/GPU muestran n/a si no hay frame timing disponible. No se inventa un porcentaje de paquetes perdidos: todavía no se muestrea. El archivo omite IDs de aplicación, micrófonos, nombres de jugadores/sala y audio.

La identidad de instalación se crea localmente para preferencias de volumen/mute por usuario Photon. No representa autenticación verificada. Editor y ejecutables simultáneos se aíslan para permitir pruebas en una PC.

## Riesgos encontrados en la auditoría

1. Los cinco prefabs de enemigos Fusion estaban marcados DestroyWhenStateAuthorityLeaves. Se cambian a MasterClientObject sin destrucción al salir. Debe probarse salida normal y forzada en una misión real.
2. LobbyAlert usaba HashSets locales para acknowledgements. Se sustituye por NetworkDictionary replicado para que una nueva autoridad conserve esos datos.
3. MineralEnemySpawner conservaba población y respawn solo en listas locales del dueño inicial. Se replica el registro de guardianes y sus tiempos para continuidad con cambio de autoridad.
4. MaterialPickup ya tiene reserva replicada por ganador y validación de distancia. No modificar su protocolo sin probar dos recogidas simultáneas.
5. CraftingTable cobra en la autoridad del inventario y confirma a la mesa. Si se desconecta entre cobro y confirmación, queda una operación incierta bloqueada deliberadamente para evitar duplicación. Liberarla automáticamente podría duplicar recursos. Requiere diseño de recibos/idempotencia antes de ofrecer reconexión con inventario completo.
6. Daño de enemigos acepta RPC de clientes y limita cantidades, pero no es validación completa de disparos del servidor. Shared confía parcialmente en los clientes. No afirmar protección anticheat.
7. Migración de autoridad es continuidad Shared, no Host Migration de la topología Host. No usar ambas expresiones como equivalentes.

## Validación

- Compilación offline de C# Editor/runtime y variante Player: correcta.
- Revisión visual real en Unity: apertura de ajustes, pestañas pantalla/voz/conexión, scroll y cierre por X correctos. Primera prueba detectó ancho doble del contenido y fue corregido.
- Validación ejecutada dentro de Unity: PASS de políticas de autoridad de cinco enemigos y dos jugadores.
- Conexión Shared real del Editor en SA: correcta, jugador registrado y menú de inicio retirado.
- Build de Windows de validación: correcta, 1.804.332.232 bytes en 2 min 40 s. Ejecutable arrancado y conectado a Fusion con configuración de distribución. Mismo protocolo registrado en Editor y ejecutable.
- Preview de 640×480 en ventana y rollback automático a pantalla completa: comprobados en ejecutable. El feedback del formulario se mejoró después de esa prueba; confirmar en build regenerada.
- Encontrado espacio final en productName que causaba fallos de CreateDirectory en Windows. Se quitó; puede cambiar la ubicación/identificación de ajustes guardados.
- La primera build precede los ajustes finales de los commits 129–130: regenerar antes de distribuir.
- Pendiente: build instalada en otra PC, loopback/permiso real de micrófono, 2–4 jugadores, salida del Master Client en cada fase, pérdida/latencia y soak de 30–60 min. Atajos/rebinds de teclado no se dan por probados: la herramienta de inyección no produjo evidencia fiable en esta sesión.

## Matriz de aceptación para release

- Dos builds idénticas se encuentran con región SA y mismo código; otra versión queda aislada.
- Recogida simultánea concede exactamente una recompensa; crafteo y reparación no duplican cobros ni progreso.
- Cerrar quien controla la sesión en lobby/cinemática/Marte/Neptuno no elimina enemigos, reinicia sus vidas ni duplica guardianes.
- Cambio de mapa conserva una sola cámara/listener y un Recorder local; Voice vuelve a la sala correcta.
- Probar 0/100/200 ms de latencia añadida y 0/2/5% de pérdida en un entorno de prueba; registrar desconexiones y diferencias, sin cambiar la red del usuario silenciosamente.
- Confirmar/revertir pantalla y cerrar ajustes durante la cuenta de confirmación.
- Rebinding: cancelar con Escape, rechazar conflictos y persistir al recrear el jugador.
- Medir CPU/GPU con la misma PC, calidad, escena y número de jugadores. No declarar una mejora de latencia antes de medirla.

## Puntos que permanecen abiertos

No se implementaron restauración transaccional del inventario al reconectar, reingreso a mitad de misión, mezclador separado para música/efectos, reasignación de todas las acciones directas, selección de monitor ni ajuste de codec/buffers/AOI/tick rate basado en medidas. Las casillas generales no se marcan terminadas por haber implementado solamente parte de su alcance. Esta rama deja una base verificable y documenta esos pendientes.

La propuesta de radio/barra está únicamente en el vault del juego y permanece aplazada.

## Commits de esta entrega

- 126: configuración de red, huella de compatibilidad e instrumentación.
- 127: gráficos, voz, controles y panel de sistemas.
- 128: continuidad de autoridad, población y acknowledgements; validadores y build local.
- 129: limpieza de conexiones, feedback de resolución y nombre de producto válido en Windows.
- 130: conservar mensajes útiles si se corta la conexión durante el inicio.

El ejecutable de prueba está en `.local/MenuValidation/Windows/ExpeditionValidation.exe`. No es una entrega de itch.io: es Development Build y requiere regenerarse con el último commit antes de probar los fixes finales. Generar desde GameJam > Diagnostics > Build multiplayer validation (Windows), fuera de Play. Para publicar usar Build Profiles sin Development Build.

### Prueba Multiplayer Play Mode — 2026-10-25

Main Editor (Player 1) y Player 2 entraron a la misma sala en SA con protocolo expedition-2-A96C6F6139242851; ambos registraron player-spawned valid=True y el personaje remoto fue visible. Player 2 leyó correctamente la configuración cifrada local. Ambos solicitaron la sala Voice correspondiente.

Se encontró el Editor en pausa al inicio; al reanudar se reconectó. Durante la prueba posterior volvió a aparecer SendOutgoingCommands >5 s y el principal terminó con DisconnectedByPluginLogic. runInBackground ya está habilitado en proyecto, runner y sesión: no se atribuye el segundo corte solamente al foco sin más evidencia. Conexión inicial de dos clientes confirmada; estabilidad sostenida, Voice Joined, transmisión, escucha, mute y volumen aún no validados. El micrófono informó captura a 48 kHz con remuestreo del encoder de 16 kHz; es un aviso de adaptación, no evidencia de fallo de audio.

## Revisión de UX e idioma — 2026-10-25

Inicio y pausa comparten un único panel de ajustes. Resolución, modo de pantalla, calidad, FPS, idioma, micrófono, modo de voz y región tienen listas de elección directa; no requieren recorrer todos los valores con clics. Los valores continuos conservan sliders con lectura numérica. Pausa tiene acciones mayores, borde visible y cierre ESC sin glifos faltantes.

Se corrigieron textos de ajustes de inicio, estados habituales de conexión, tripulación/monitores del lobby, ayudas y estados de fabricación, estados/ayudas de misión de tablet y títulos finales. Las cadenas de datos de recetas conservan fallback al contenido original si falta su traducción; mensajes históricos del diario y errores técnicos aún pueden conservar el idioma de origen. No se declara traducido el 100% del contenido.

Validación: compilación C# Editor y variante Player sin errores, diff sin errores de espacios y validador de assets multiplayer PASS. Visualmente se confirmó el inicio en portugués incluyendo ajustes y estado de conexión. La automatización no abrió fiablemente el panel durante la última sesión: selección, cierre y teclado de las listas y nuevo aspecto de pausa requieren revisión interactiva. No se generó una build nueva en esta revisión.
