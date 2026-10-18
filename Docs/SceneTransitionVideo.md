# Secuencia de salida del lobby

La secuencia actual permanece en `j_LobyDeEspera` hasta que la tripulación cruza
la compuerta. No carga Marte mientras se reproduce la cinemática.

1. Cada jugador usa el monitor izquierdo para marcar o cancelar `LISTO`.
2. Cuando todos están listos, la autoridad de `LobbyAlert` inicia automáticamente
   cinco segundos de luces rojas, sirena sintetizada y turbulencia suave.
3. Cada cliente reproduce el video de ocho segundos en el lobby. Se bloquea el
   movimiento durante el video y se mantienen los cierres por fallo o bloqueo.
4. La autoridad espera las confirmaciones de fin de los clientes activos. Hay
   un límite de 40 segundos para evitar que un reproductor defectuoso bloquee
   al grupo. Después se produce un impacto de cámara de 1.25 segundos.
5. La puerta A se eleva durante 1.4 segundos y revela un portal animado. Cada
   jugador se acerca y cruza la zona delante de la compuerta.
6. El jugador que entra queda esperando al resto. La autoridad valida la
   proximidad del avatar, cuenta los jugadores activos que entraron y carga
   Marte para todos cuando el último haya cruzado.

Al entrar, `PortalWhiteTransition` funde a blanco durante 0.65 segundos. La
confirmación del portal se envía solo cuando la cubierta es completamente
opaca. Esta cubierta persiste entre escenas y mantiene el blanco mientras
se espera al grupo y durante la carga. Tras aparecer la escena de destino,
terminar el gestor de escenas de Fusion y existir el jugador y la cámara,
espera 0.3 segundos y revela el entorno durante 1.3 segundos. Los tiempos
usan reloj real y curvas SmoothStep. Los controles se bloquean hasta terminar.
Si la conexión se cierra o la carga detectada supera 90 segundos, se retira la
cubierta y se libera la espera local; hay que salir del portal antes de reintentar.

Fusion usa una escena compartida: no se simula un lobby y una misión distintos
por participante. Las fases y sus temporizadores están replicados en
`LobbyAlert`; las confirmaciones se envían mediante RPC a su autoridad. Las
salidas de jugadores se descuentan al comprobar los participantes activos.
Los fotogramas del video se reproducen localmente; el impacto y la apertura
esperan al grupo, en vez de exigir que todas las máquinas decodifiquen igual.

`LobbyDepartureEffects` crea una sirena mono de dos segundos en memoria, de
volumen moderado, que se atenúa durante el video. No depende de una descarga de
audio. Aplica el movimiento de las hojas de la puerta y el portal usando un
material unlit. `Player.LateUpdate` añade la sacudida después de colocar la
cámara local: no modifica posiciones de red ni la dirección de movimiento.

La puerta configurada es `Puerta_A_*`, centrada en X=-3, Z=6.4 en este lobby.
Si se mueve el modelo del lobby habrá que actualizar la ubicación en
`LobbyDepartureEffects`. La zona acepta el paso desde el interior sin necesidad
de atravesar las paredes del modelo. La puerta B no se modifica.

Los logs `[Lobby departure]` muestran `alarm`, `cinematic`, `impact`,
`portal-open` y `travel`. Los logs `[Transition video]` muestran la preparación,
reproducción y motivo de cierre del clip. La textura y los eventos se liberan
al terminar. El video conserva su comportamiento sin audio; la alarma se
reproduce por separado.
