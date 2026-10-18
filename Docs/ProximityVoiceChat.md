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
