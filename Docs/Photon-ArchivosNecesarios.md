# Archivos necesarios: multiplayer y voz de proximidad

Para que un compañero juegue desde esta rama, debe descargar la misma revisión
del proyecto, ejecutar `git lfs pull` y configurar sus IDs locales. No necesita
copiar manualmente DLLs o importar otra versión de Photon encima.

## SDK y configuración compartida

| Ruta | Para qué sirve |
| --- | --- |
| `Assets/Photon/Fusion/` | Runtime, editor, weaver y plugins de Fusion 2.1.3. Incluye las correcciones para jugadores virtuales. |
| `Assets/Photon/PhotonRealtime/` | Conexión y salas de Photon. |
| `Assets/Photon/PhotonLibs/` | Bibliotecas del SDK. |
| `Assets/Photon/FusionAddons/` | Incluye Simple KCC, utilizado por el controlador actual. |
| `Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion` y `.meta` | Configuración de red, registro de prefabs y assemblies que teje Fusion. |
| `Packages/manifest.json` y `Packages/packages-lock.json` | Restauran las dependencias y la revisión resuelta de Photon Voice. |
| `ProjectSettings/ProjectSettings.asset` | Símbolos de compilación y ajustes del proyecto. |
| `ProjectSettings/EditorBuildSettings.asset` y perfil activo, si sobrescribe escenas | Lista e índices de las escenas de red. Deben coincidir entre clientes. |

La dependencia de Voice en el manifest es:

```json
"com.photonengine.voice-fusion": "https://github.com/Photon-Server/Photon-UPM.git#fusion/v2/voice-for-fusion"
```

Unity descarga el paquete automáticamente. No compartir `Library/PackageCache`.
El proyecto también utiliza Input System y Mono Cecil; conserva las entradas
existentes del manifest. No reemplaces el manifest completo por este fragmento.

## Integración del juego

Todos estos scripts están en `Assets/PhotonStarter/`:

| Archivos | Función |
| --- | --- |
| `PhotonLocalSettings.cs` | Lee y descifra los IDs de Fusion y Voice. |
| `MarsFusionSession.cs`, `MarsFusionSpawner.cs` | Crear/entrar a sala, runner, escenas y aparición del jugador. |
| `Player.cs`, `PlayerInput.cs`, `InputActions.cs`, `AnimatorId.cs` | Controlador de tercera persona, input, cámara y animaciones. |
| `MarsFusionAvatar.cs` | Estado del astronauta, expresiones y conexión con tablet, identidad y salud. |
| `MarsProximityVoiceClient.cs` | Cliente Voice asociado a la sesión de Fusion. |
| `MarsProximityVoice.cs` | Push to talk con V, volumen espacial y alcance de 22 m. |
| `MarsPlayerIdentityView.cs` | Colores y nombres del avatar actual. |
| `SceneTransitionVideo.cs`, `PortalWhiteTransition.cs`, `LobbyDepartureEffects.cs` | Referencias que utiliza actualmente el avatar/controlador/sesión para bloquear controles y gestionar las transiciones. |

Estos archivos forman parte de la integración actual: no son un SDK mínimo
independiente. El avatar también referencia las clases de tablet, salud y
presentación del proyecto. Para mantener esta implementación sin refactorizar,
conserva sus scripts y los assets referenciados por el prefab. Copiar únicamente
los dos scripts de voz a un proyecto vacío no basta.

## Prefabs preparados

- `Assets/PhotonStarter/j_FusionRunner.prefab`: NetworkRunner, eventos, gestor
  de escenas y `MarsProximityVoiceClient`.
- `Assets/02_Prefabs/Fusion/j_FusionAstronaut.prefab`: jugador del lobby.
- `Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab`: jugador de Marte.
- Sus dependencias: modelo, materiales, Animator Controller, clips, sprites,
  tablet y scripts asociados. Conserva los `.meta` para mantener los GUIDs.
- `Assets/Editor/MarsProximityVoiceSetup.cs`: herramienta para volver a aplicar
  componentes de voz; no se ejecuta dentro del juego compilado.

Los jugadores necesitan `NetworkObject`, `VoiceNetworkObject`, `Recorder`,
`Speaker`, AudioSource y los componentes del controlador. Esos componentes ya
están configurados en los prefabs de esta rama; no añadir duplicados.

Fusion debe tejer `Assembly-CSharp`, `Assembly-CSharp-firstpass` y
`PhotonVoice.Fusion`, como indica el NetworkProjectConfig existente.

Escenas preparadas: `Assets/00_Scenes/j_LobyDeEspera.unity` y
`Assets/00_Scenes/j_MarsFusion.unity`. Para copiar una escena a otro proyecto hay
que incluir sus dependencias; esta lista no es un paquete exportado de escenas.

## Configuración privada en cada PC

Compartir el script **`Tools/Configure-PhotonLocal.ps1`**, pero no los IDs ni
los archivos cifrados. Ejecutar desde la raíz del proyecto:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Configure-PhotonLocal.ps1 -Service Fusion
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Configure-PhotonLocal.ps1 -Service Voice
```

Introducir en las entradas ocultas los mismos App IDs del equipo: uno de tipo
Fusion y otro de tipo Voice. Se guardan para el usuario Windows local en:

```text
%LOCALAPPDATA%\GameJamPlus26\Photon\appid.dpapi
%LOCALAPPDATA%\GameJamPlus26\Photon\voice-appid.dpapi
```

No compartir esos `.dpapi` entre PCs: están cifrados para su usuario/máquina.
No incluir `.local/Photon`, `PhotonAppSettings.asset` privado, `Library`,
`Temp`, logs de sesiones ni crash dumps en un paquete para compañeros.

Guía de puesta en marcha: [Multiplayer-Guia-Equipo.md](Multiplayer-Guia-Equipo.md).
Detalles de voz: [ProximityVoiceChat.md](ProximityVoiceChat.md).
