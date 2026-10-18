# Jugar con el equipo: configuración de Fusion y voz

Esta guía corresponde a `feature/proximity-voice-chat`, con las correcciones
73 y 74. Usa Windows: la configuración privada actual se cifra con Windows DPAPI.

## 1. Descargar la misma revisión

Desde la carpeta del repositorio, con tus cambios propios guardados:

```powershell
git fetch origin
git switch feature/proximity-voice-chat
git pull --ff-only
git lfs install
git lfs pull
git log -1 --oneline
```

El responsable debe publicar primero la rama y sus commits. Todos deben tener
la misma revisión para esta prueba, incluidos los commits 73 y 74. Si Git indica
cambios locales o conflictos, resuélvelos sin borrar tu trabajo.

Abre el proyecto con **Unity 6000.5.6f1**, la versión indicada en
`ProjectSettings/ProjectVersion.txt`. Espera a que terminen las importaciones,
la restauración de paquetes y la compilación. La rama ya incluye Fusion
**2.1.3, build 2390**, y la dependencia de Photon Voice para Fusion.
No vuelvas a importar el proyecto Starter encima ni instales otro SDK.

## 2. Configurar los dos App IDs en cada PC

Pide al responsable del equipo, por un canal privado:

- El App ID **Fusion** del equipo.
- El App ID **Voice** del equipo, distinto al de Fusion.

**Todos usan el mismo ID de Fusion y el mismo ID de Voice.** No crees una
aplicación distinta por jugador: sus salas quedarían separadas. No necesitas
iniciar sesión con la cuenta Photon del responsable para ejecutar el juego.

Abre PowerShell en la raíz del proyecto y ejecuta estos comandos por separado:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Configure-PhotonLocal.ps1 -Service Fusion
```

Pega el ID de Fusion cuando lo solicite y pulsa Enter. La entrada está oculta.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Configure-PhotonLocal.ps1 -Service Voice
```

Ahora pega el ID de Voice. La opción de ejecución solo aplica a ese proceso.
Los comandos guardan la configuración cifrada para tu usuario de Windows en:

```text
%LOCALAPPDATA%\GameJamPlus26\Photon\appid.dpapi
%LOCALAPPDATA%\GameJamPlus26\Photon\voice-appid.dpapi
```

No copies los `.dpapi` de otro compañero: están ligados a su usuario y PC.
No pegues IDs en scripts, prefabs, escenas ni en Git. No necesitas rellenar
PhotonAppSettings ni marcar manualmente las casillas del Fusion Hub: el código
lee estos archivos y configura Fusion y Voice en memoria al iniciar.
Si Unity estaba en Play, detén Play y vuelve a iniciarlo tras configurar los IDs.

## 3. Abrir la escena preparada

Para probar el lobby, abre **`Assets/00_Scenes/j_LobyDeEspera.unity`**.
Para probar directamente Marte, abre **`Assets/00_Scenes/j_MarsFusion.unity`**.
Todos deben empezar en la misma escena.

En **File > Build Profiles**, comprueba que la escena elegida esté habilitada
en la lista de escenas del perfil activo. Usa la misma lista y orden que el
resto del equipo: Fusion identifica las escenas por índice. Si el perfil
sobrescribe la lista global, revisa esa lista específica. La lista global
versionada incluye Marte; verifica expresamente el lobby antes de probarlo.

En la escena, el componente **Mars Fusion Session** ya debe tener:

| Campo | Lobby | Marte |
| --- | --- | --- |
| Player Prefab | `j_FusionAstronaut` | `A_FusionAstronaut` |
| Runner Prefab | `j_FusionRunner` | `j_FusionRunner` |
| Spawn Point | Transform asignado | Transform asignado |
| Max Players | Máximo de participantes previsto | Máximo de participantes previsto |
| Ground Probe Height | `1.5`, bajo el techo | `100`, exterior |

No coloques otro jugador o runner manualmente: la sesión los crea. No añadas
un NetworkManager de Netcode a estas escenas de Fusion. El runner compartido
y ambos prefabs de astronauta ya tienen los componentes de voz configurados.
Si las referencias están asignadas, no hace falta reconstruir la escena.

## 4. Entrar a la misma partida

1. Inicia Play en cada PC.
2. Escribe exactamente el mismo nombre de sala, por ejemplo `Equipo-Prueba-01`.
3. Pulsa **Crear / entrar a sala online** en todos los clientes. El primero crea
   la sala; los siguientes entran si hay plazas y coinciden aplicación y región.
4. Espera a que aparezca el astronauta. Debes ver a tus compañeros moverse.

**Prueba local · sin App ID** usa Fusion Single: sirve para probar un jugador,
pero no conecta con tus amigos. No hace falta abrir puertos del router para
esta prueba mediante Photon Cloud. Si Windows solicita acceso de red, permite
Unity para la red desde la que estás probando.

La región se selecciona automáticamente. Si no os encontráis pese a usar la
misma escena, revisión, App IDs y sala, revisad la región efectiva de Fusion:
los clientes deben coincidir. No cambiéis una región solo en una PC sin
coordinarla con el responsable.

## 5. Probar la voz

- Configura el micrófono predeterminado de Windows y permite su uso por
  aplicaciones de escritorio en los ajustes de privacidad de Windows.
- Usa auriculares para evitar realimentación.
- Acércate al otro astronauta y **mantén V** para hablar; suelta V para dejar
  de transmitir. La boca del visor se anima mientras mantienes la tecla.
- La voz es espacial: compruébala cerca y después aléjate más de **22 metros**,
  donde debe silenciarse. La animación de la boca por sí sola no demuestra
  que el micrófono esté transmitiendo audio.
- **Tab** abre la tablet y **Esc** abre el menú de conexión.

## 6. Si algo falla

| Síntoma | Qué revisar |
| --- | --- |
| Falta App ID o no se puede descifrar | Ejecuta el configurador en esa PC y con el mismo usuario de Windows que ejecuta Unity. |
| `appIdPresent=true appIdFormatValid=true` | El ID se leyó y tiene formato válido; esto todavía no confirma que Photon lo acepte o que sea del tipo correcto. |
| `scene-config-failed` o índice negativo | Agrega y habilita la escena en el Build Profile activo. |
| Ambos conectan pero no se ven | Misma revisión, aplicación Fusion, región, nombre de sala, escena e índices de escenas; verifica también que la sala tenga plazas. |
| Hay movimiento pero no voz | Mismo App ID Voice, lectura cifrada correcta, permisos y micrófono; mantén V y prueba a menos de 22 m. |
| Fusion Hub muestra pasos sin completar | Las casillas no sustituyen la configuración real de los prefabs. Consulta los logs y esta guía antes de añadir componentes duplicados. |
| `NetworkProjectConfigAsset` o Asset Database de solo lectura en Player 2 | Sigue el procedimiento para jugadores virtuales indicado abajo. |
| Aviso de Unity AI / Account API | Pertenece a otro servicio; no demuestra un fallo del App ID de Photon. |

### Player 2 en la misma PC

Solo hace falta Multiplayer Play Mode para abrir un segundo cliente dentro del
Editor; tus amigos en otras PCs no necesitan activar Player 2.

Con Play detenido y la importación terminada, ejecuta
**GameJam > Photon > Validate virtual player configuration**. El diagnóstico
consulta el Editor principal y los jugadores virtuales conectados. Los archivos
en `Temp/MarsFusionVirtualConfig` deben mostrar:

```text
resourceLoaded=True assetDatabaseLoaded=True globalLoaded=True
```

La rama corrige la lectura del manifest en el clon y vuelve a publicar las
dependencias de Fusion en cada recarga. Si un clon sigue desactualizado,
desactívalo y actívalo después de que el Editor principal termine de importar.
No copies un `manifest.json` a mano ni reemplaces la configuración de Fusion
por una vacía. Si persiste, comparte el log completo y los diagnósticos para
revisarlo; no incluyas los App IDs.

Más detalles: [voz y troubleshooting de Fusion](ProximityVoiceChat.md).
