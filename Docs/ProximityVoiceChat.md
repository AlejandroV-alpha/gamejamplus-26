# Voz de proximidad (Photon Voice + Fusion)

El paquete `com.photonengine.voice-fusion` se instala desde Fusion Hub > Addons > Voice.
La configuración se aplica al runner compartido por Marte y el lobby y a los prefabs
`A_FusionAstronaut` y `j_FusionAstronaut`.

## Configuración privada en cada computadora

Crear un App ID de tipo **Voice** en Photon, distinto al de Fusion. Ejecutar desde
PowerShell `./Tools/Configure-PhotonLocal.ps1 -Service Voice` y pegarlo en la entrada
oculta. Se cifra con Windows DPAPI para ese usuario en
`%LOCALAPPDATA%/GameJamPlus26/Photon/voice-appid.dpapi`.

No pegar IDs en scripts, escenas ni prefabs. `MarsProximityVoiceClient` lee la copia
cifrada al crear el runner y configura Voice solo en memoria. La copia de respaldo
en `.local/Photon` también está excluida de Git. Cada compañero configura su propia
copia; los archivos DPAPI no son portables entre cuentas de Windows.

## Componentes y comportamiento

- **MarsProximityVoiceClient**, en `j_FusionRunner`: usa la integración oficial
  `FusionVoiceClient` para seguir sala, región y desconexión de Fusion.
- **VoiceNetworkObject**, en el jugador: asocia el flujo de audio con la identidad
  de red del astronauta y registra el micrófono del jugador local.
- **Recorder**: detección de voz, umbral 0.015, audio mono a 16 kHz, Opus de 20 ms,
  24 kb/s y eco de prueba desactivado.
- **Proximity Voice Speaker**: fuente 3D a la altura de la cabeza, volumen completo
  cerca de 2 m y caída lineal hasta 22 m. Fuera del alcance se silencia la fuente.
- **MarsProximityVoice**: enlaza la amplitud del micrófono con las expresiones del
  visor. `Push To Talk` está activado: mantener **V** para transmitir y animar la boca,
  incluso durante el silencio; soltarla detiene el envío y la expresión de habla.
  El estado de la cara se replica mediante `MarsFusionAvatar.VoiceLevel`.

La distancia modifica la reproducción local: no se implementaron grupos de interés
para limitar el transporte de audio a jugadores cercanos.

## Prueba

1. Configurar App IDs de Fusion y Voice en ambas computadoras.
2. Iniciar **Crear / entrar a sala online** con el mismo nombre de sala y la misma
   versión del proyecto. La prueba Single no comprueba comunicación entre clientes.
3. Usar auriculares, verificar el micrófono predeterminado y hablar cerca del otro
   jugador. Alejarse más de 22 m para comprobar el silencio y volver a acercarse.
4. Comprobar voz direccional, expresión del visor, desconexión y regreso a la sala.

Si el micrófono no funciona, revisar los permisos de Windows para aplicaciones de
escritorio. Si Voice no conecta, revisar que el App ID sea de tipo Voice y que el
log `[Proximity voice]` confirme la lectura cifrada, sin imprimir el identificador.

Para volver a aplicar los componentes, con Play detenido usar
**GameJam > Photon > Configure proximity voice**. Este comando conserva los objetos
de la escena y configura únicamente los prefabs de runner y astronautas.

## Validación realizada

Compilación del runtime y del Editor correcta. Ambos prefabs tienen el componente
de voz registrado en Fusion; el altavoz usa audio 3D y alcance de 22 m. Se verificó
el cifrado, la lectura en Unity y la exclusión de Git del App ID. Pendiente validar
captura de micrófono y comunicación audible entre dos clientes online.

## Fallo de replicación de Fusion 2.1.1

### Jugador virtual: manifest ausente y configuración no disponible

Si el log muestra `appIdPresent=true appIdFormatValid=true`, el App ID sí se
leyó: no es necesario volver a configurarlo. `FusionInstaller` intentaba leer
`Library/VP/.../Packages/manifest.json`, que no existe en el proyecto virtual.
Se añadió una condición para que el instalador solo se ejecute en el Editor
principal, igual que la protección del Hub. Los jugadores virtuales usan los
paquetes y la configuración importada por ese Editor.

Se encontró además que `FusionCustomDependency.InitializeEager` conservaba en
`SessionState` una marca de inicialización anterior a la actualización. Faltaban
los comandos persistentes `Fusion.ScriptOrderDependency` y
`Fusion.PrefabsDependency` en `Temp/FusionMppm`, necesarios para sincronizar la
configuración importada. Ahora se publican las dependencias en cada recarga del
dominio del Editor principal; los jugadores virtuales reciben esos hashes y no
calculan hashes propios. Se comprobó que ambos comandos vuelven a generarse y
que el Editor principal sigue cargando `NetworkProjectConfigAsset` sin errores.
Después de reimportar el asset desde el Editor principal, se verificó también en
el proceso Player 2: `resourceLoaded=True assetDatabaseLoaded=True globalLoaded=True`.
El menú `GameJam > Photon > Validate virtual player configuration`, fuera de
Play, repite la comprobación en el Editor y los jugadores virtuales conectados.
Sus resultados, sin credenciales, quedan en `Temp/MarsFusionVirtualConfig`.

Después de actualizar el SDK, detén Play y reinicia el jugador virtual desde
Multiplayer Play Mode para que reciba las importaciones nuevas. Si continúa
`Asset Database is set to Read Only` junto con `Failed to load
NetworkProjectConfigAsset`, la copia virtual aún no está sincronizada. No copies
un manifest a mano ni reemplaces la configuración por una vacía. Comprueba
primero que el Editor principal haya terminado de importar. El usuario confirmó
que la prueba volvió a funcionar después de aplicar la corrección. El aviso de
Account API de Unity AI pertenece a otro servicio.

El log de la prueba del 18/10/2026 muestra el fallo
`AssertException: tick >= sc._latestTickAcknowledged` en
`Fusion.Simulation.StateReplicator.OnPacketDelivered`, seguido de desconexión de la
sesión. La versión instalada cuando ocurrió el fallo era Fusion **2.1.1, build 2177**.

Photon documenta exactamente esa aserción como incorrecta y corregida en
**2.1.2, build 2279**; podía dispararse tras un reinicio del tiempo o pérdida extrema
de paquetes. La versión estable posterior disponible es **2.1.3, build 2390**.
Fuente: [notas oficiales de Fusion 2.1](https://doc.photonengine.com/fusion/v2/getting-started/release-notes/release-notes-2-1).

Se instaló **Fusion 2.1.3, build 2390** desde el paquete oficial descargado por el
usuario. Se conservaron `NetworkProjectConfig.fusion`, los App IDs locales y las
adaptaciones del Hub para jugadores virtuales. Unity completó la recompilación
sin errores de C#. Se verificó dentro del Editor que la versión cargada es
2.1.3.2390, los dos App IDs se leen correctamente y ambos prefabs conservan Voice
y push to talk con V. La prueba Fusion Single creó el jugador y el EventSystem
y se desconectó sin errores de consola. Falta repetir la prueba online con todos los participantes
usando la misma revisión del proyecto y del SDK; esa prueba es necesaria para
confirmar que el fallo ya no reaparece en esta partida.

Los avisos anteriores tienen otro significado:

- `TransmitEnabled is false`: estado inicial esperado al usar push to talk.
- Micrófono a 48 kHz y codificador a 16 kHz: el SDK está adaptando la frecuencia.
  Ese aviso no contiene el fallo de replicación identificado.
- `Leader joined room, Voice client is busy`: el cliente Voice aún está conectando;
  en el log después aparece la petición de entrada a la sala de voz.
- `DisconnectedByPluginLogic` / código 104 aparece después de la aserción. El log
  no permite atribuir por separado la razón interna del rechazo del servidor.
