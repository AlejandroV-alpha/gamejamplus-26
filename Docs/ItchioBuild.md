# Build de itch.io: Photon Fusion y Voice sin configuración del jugador

La build incluye automáticamente los App IDs de Fusion y Voice. El jugador no necesita el proyecto, PowerShell, archivos DPAPI ni una cuenta de Photon.

## Generar el entregable

1. En el Windows del desarrollador deben estar configurados ambos App IDs locales. Ya están guardados cifrados en este equipo. Si generas el juego desde otra PC, ejecuta `Tools/Configure-PhotonLocal.ps1` una vez con `-Service Fusion` y otra con `-Service Voice`.
2. Abre File > Build Profiles y selecciona Windows, Intel 64-bit. Desactiva Development Build, Script Debugging y Autoconnect Profiler para la entrega.
3. Deja como primera escena `Assets/00_Scenes/j_LobyDeEspera.unity`; incluye también `Assets/00_Scenes/Mars.unity` y `Assets/00_Scenes/b_NeptuneExploration.unity`.
4. Genera una build nueva. `[Photon build]` confirma en Console que Fusion y Voice se validaron. Si falta uno de los IDs, se detiene la generación con un mensaje explicativo.
5. Comprime toda la carpeta del ejecutable y súbela a itch.io como descarga para Windows. No incluyas `.local`, archivos DPAPI ni el repositorio.

## Funcionamiento

`PhotonDistributionBuild` valida la configuración cifrada local y agrega `PhotonBuildConfiguration` a las copias de las escenas procesadas durante el build. No guarda IDs en las escenas originales ni genera assets o código con credenciales.

En el ejecutable, `PhotonLocalSettings` usa exclusivamente la configuración incluida. En el Editor conserva la configuración cifrada del desarrollador. Fusion mantiene la versión de sala `MarsCoop-v1` y Voice conserva el cliente de proximidad y push to talk con V.

Los App IDs quedan incluidos en los datos distribuidos del juego y son extraíbles del ejecutable. No son contraseñas de una cuenta Photon. Permanecen fuera de los archivos fuente y del repositorio, pero no se pueden mantener secretos frente al usuario que recibe el juego. No incluir claves administrativas ni secretos de autenticación.

## Validar antes de publicar

Abre la misma build en dos PC Windows, idealmente una sin configuración local de Photon. Entra con ambos jugadores al mismo nombre de sala y región, comprueba movimiento y mantén V para hablar. Windows debe permitir el uso del micrófono para aplicaciones de escritorio y ambos equipos deben tener conexión a Internet. La capacidad de conexiones depende del plan de Photon.

La comprobación de compilación de scripts no sustituye esta prueba de conexión y audio en el ejecutable final.
