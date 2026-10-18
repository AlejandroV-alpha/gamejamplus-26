# Integración: jugador, peligros y Neptuno

Rama de integración: `codex/integrate-player-hazards-neptune`.
Base: `feature/proximity-voice-chat`, commit `0c68252`.

## Orden de integración

1. Commit 78: `origin/feature/player-item-animations` en `21eea86`.
2. Commit 79: `origin/feature/planet-hazards` en `bee8346`.
3. Commit 80: `origin/codex/neptune-level` en `310f931`.

Se conservaron las pantallas táctiles, el estado de listo, la secuencia del
portal, el fundido blanco y Photon Voice de la rama base. El controlador nuevo
usa Cinemachine para el apuntado; la turbulencia se aplica después desde
`LobbyDepartureEffects.LateUpdate`, evitando una segunda cámara manual.

El conflicto de `Gun.cs` conservó la versión más reciente del apuntado y el
retroceso. El prefab `A_FusionAstronaut` conserva tanto la pistola como los
componentes de voz. Se combinaron las referencias raíz de la escena de Marte y
los campos `groundProbeHeight` y `planetLabel` de la sesión. Neptuno se añadió
al final de la lista de escenas, conservando los índices anteriores.

Se eliminó la copia duplicada de `GunTracer.mat` en la carpeta de prefabs. La
copia canónica está en `Assets/05_Materials/Gun`, con el GUID original para
conservar las referencias. La herramienta de configuración de voz ahora cubre
todos los prefabs Fusion que contienen `MarsFusionAvatar`, incluidos combate,
tormenta de arena y frío de Neptuno.

## Escenas y alcance

- `A_PlayerCombat`: pruebas de combate.
- `A_MarsSandstorm`: pruebas de tormenta y controlador específico de viento.
- `A_NeptuneFusion`: pruebas de frío y fuentes de calor.
- `b_NeptuneExploration`: nivel de exploración de Neptuno.
- `j_LobyDeEspera` y `j_MarsFusion`: flujo actual de la partida.

Las escenas de pruebas de peligros y la escena artística de Neptuno mantienen
la configuración que traían sus ramas. Esta integración no añade un selector
de destino al lobby: su portal sigue apuntando a Marte.

## Comprobaciones

Los 295 archivos incorporados no presentan marcadores de conflicto ni IDs
YAML duplicados. Unity compiló combate y peligros sin errores. Se resolvieron
229 GUIDs referenciados por escenas, prefabs y materiales incorporados, sin
referencias ausentes. Los cinco astronautas tienen cero scripts perdidos,
VoiceNetworkObject registrado en Fusion y push to talk activado.
La prueba Single en el lobby hizo aparecer el `A_FusionAstronaut` combinado,
con pistola, voz y las dos cámaras virtuales de Cinemachine, sin errores de
consola. El cambio de prefab de esa prueba solo se hizo en Play.

La validación no sustituye una prueba online de combate, peligros y cambio de
planeta entre varios clientes.

## Corrección de selección del jugador en el lobby

La escena del lobby heredaba `j_FusionAstronaut`, sin pistola. Ese jugador
persiste al viajar a Marte, aunque la sesión de Marte apunte a otro prefab.
Se cambió el lobby a `A_FusionAstronaut`, que contiene la pistola y las
referencias más recientes de rig y cámara al hombro. `A_CombatAstronaut`
conserva su uso como variante de prueba y no sustituye al rig más reciente.

La mira anterior existía como `CrosshairCanvas` solo en Marte. Ahora
`Player.CrosshairPrefab` crea una mira del propietario local, con el mismo
sprite `Crosshair_04`, que persiste entre escenas y se destruye al despawn.
Se asignó a `A_FusionAstronaut` y `A_CombatAstronaut`. Solo se muestra durante
el apuntado y con los controles habilitados. La búsqueda del canvas antiguo
se conserva como compatibilidad cuando no hay prefab asignado.

Se comprobó el spawn real del lobby con `A_FusionAstronaut`, las mallas de la
pistola activas, la mira visible al apuntar y un disparo que consume munición,
sin errores de consola.

El `Gun` de `A_FusionAstronaut` también tenía `tracer` sin asignar. Se añadió
el hijo `Shot Tracer` con LineRenderer, el material canónico `GunTracer` y una
duración de 0.12 segundos. Usa el RPC visual existente y conserva el efecto de
impacto. En Single se comprobó que un disparo consume munición, habilita la
línea con sus extremos y la deshabilita después, sin errores.
