# Consola táctil del lobby

Escena: `Assets/00_Scenes/j_LobyDeEspera.unity`.

- Pantalla grande: material `j_DestinationLED`, gráfico plano de Marte y texto
  enrasado al panel. El planeta 3D y su órbita anteriores quedan desactivados.
- Monitor izquierdo: iniciar partida. Conserva la comprobación de autoridad de
  escena, la alerta y la transición existentes.
- Monitor derecho: abandonar lobby y desconectar al jugador local.
- Monitor central: apagado, sin controles.

Acércate a menos de 3 metros, apunta al monitor y pulsa **E** o clic izquierdo.
Con el cursor bloqueado se apunta desde el centro de la pantalla; con cursor
libre se usa su posición. El raycast respeta los obstáculos e ignora los
colliders del jugador local. Las acciones se bloquean al abrir el menú o la
tablet y cuando el jugador está muerto. El rótulo añade `[E]` cuando el monitor
está seleccionado. Las geometrías de los botones antiguos quedan desactivadas.

`LobbyConsoleButton.touchSurface` asigna el collider del monitor. Sin esa
referencia el componente conserva su comportamiento anterior por proximidad.
El monitor izquierdo marca `LobbyReady` en el avatar local. Otra pulsación
cancela la confirmación mientras se espera al resto. Cuando todos están listos,
la autoridad inicia automáticamente la secuencia de alarma, cinemática, impacto
y portal. Durante la secuencia el monitor muestra `SECUENCIA ACTIVA`.

La pantalla de tripulación muestra cuatro plazas con un sprite renderizado del
modelo del astronauta, teñido con su color, nombre y estado `LISTO` o `ESPERANDO`.
Las plazas vacías dicen `PLAZA LIBRE`. Se actualiza cada 0.25 segundos usando los
jugadores activos de Fusion. Los nombres no interpretan etiquetas de rich text.
La sesión se limita visualmente a cuatro plazas, como la configuración actual.
`LobbyReady` se replica desde la autoridad de cada avatar; la comprobación de
todos listos se hace de nuevo antes de iniciar la alerta en la autoridad de escena.
El flujo completo de la compuerta se describe en [SceneTransitionVideo.md](SceneTransitionVideo.md).

Validado: compilación sin errores, captura de la consola, selección de ambos
monitores en Play, ausencia de activación al apuntar al central y ejecución de
la acción de abandonar en Single. No se inició una misión online durante esta
validación. También se comprobó la lista en Single, el estado inicial pendiente,
el cambio a listo mediante el control y la actualización visual del rótulo.
Falta validar la confirmación y la cancelación con varios clientes online.
