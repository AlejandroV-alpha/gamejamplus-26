# Colores de jugador por sesion

Paleta fija de seis colores:

| Color | Hex |
|---|---|
| Rojo coral | #FF3840 |
| Azul | #2D8CFF |
| Verde | #39E675 |
| Amarillo | #FFD43B |
| Violeta | #B36BFF |
| Turquesa | #27D9CF |

En Fusion Shared el avatar del Master Client coordina las reservas. Cada jugador solicita un color al coordinador; este reserva un indice libre antes de publicar la confirmacion por RPC. No se escoge un color libre de forma independiente en cada computadora. Las solicitudes repetidas del mismo PlayerRef devuelven su reserva previa.

El registro local de cada peer se mantiene por NetworkRunner y observa las identidades replicadas; tambien recibe las confirmaciones del coordinador. La reserva sobrevive a cambios de escena y recreaciones del avatar dentro de la misma sesion. Cambiar de mundo no sortea otro color. Una nueva sala/runner comienza con un registro nuevo. Al salir un jugador se libera su reserva sin cambiar los colores de quienes siguen jugando. No es una identidad permanente entre desconexiones y nuevas sesiones.

Mientras llega la confirmacion, el traje y la tarjeta del lobby muestran gris neutro; no muestran el rojo del indice por defecto. Los indices pendientes no se convierten mediante modulo en otro color.

Hay seis colores como capacidad maxima del registro; nunca se reutiliza un color ocupado al llenarse. StartGame limita PlayerCount a un maximo de seis, conservando la configuracion actual si era menor (por ejemplo cuatro). Esto no aumenta las plazas de las pantallas del lobby: el lobby actual sigue configurado para cuatro.

Archivos: MarsFusionAvatar (solicitud/confirmacion y persistencia por runner), SessionColorAssignments (reservas), MarsPlayerIdentityView (paleta y presentacion), LobbyPlayerCountScreen (estado pendiente), MarsFusionSession (limite de capacidad).

Validacion sin MCP: SessionColorAssignments compilado mediante Add-Type y ejercitado en 500 rondas con entradas aleatorias de seis jugadores; sin colores duplicados, reservas estables, septimo jugador rechazado y reutilizacion del hueco de un jugador que sale. API GetMasterClient verificada en la documentacion XML del SDK instalado. No se ejecuto Unity ni se uso MCP: falta compilacion/weaving y comprobacion multicliente, cambio de mundo y cambio de Master Client. Reiniciar la partida con todos usando esta misma version antes de esa prueba; se agregaron RPCs al avatar.
