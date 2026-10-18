# Caras del visor — prototipo

El prefab `Assets/02_Prefabs/Mars/j_MarsPreviewPlayer.prefab` incorpora `PlayerVisorFace` y dos SpriteRenderer unidos al hueso Head del astronauta. El jugador de la tablet hereda este cambio. No se modifica el FBX ni el controlador de animaciones.

En `j_TabletPrototype`, iniciar Play y Start Host si no arranca automáticamente. Girar la cámara para ver la cara del personaje:

- Mantener **V**: simular habla.
- Pulsar **H**: mostrar dolor durante 0,7 segundos sin quitar salud.
- Al recibir daño real, la cara de dolor aparece automáticamente.
- Al llegar a cero de salud, aparecen ojos en X.
- En reposo, parpadea cada 2,5–5 segundos.

Las teclas de prueba solo funcionan en Editor o Development Build y pueden deshabilitarse con `Enable Prototype Keys`. La prioridad es eliminado > dolor > habla > reposo. El prototipo no captura el micrófono ni incorpora chat de voz.

## Integración con voz

El sistema de chat debe llamar continuamente a `PlayerVisorFace.SetVoiceLevel(float)` sobre el jugador propietario, pasando amplitud normalizada entre 0 y 1. Por debajo de 0,04 se considera silencio. Si deja de llegar señal durante 0,25 segundos, la boca se cierra. Conviene que la fuente aplique su filtro de ruido y respete mute/PTT antes de llamar a esta API.

Solo se replica un nivel cuantizado de voz, como máximo a 10 Hz y únicamente cuando cambia. La animación de boca corre localmente a 9 Hz, sin enviar cada sprite por red. El dolor utiliza la salud ya replicada; la tecla de prueba replica un contador cosmético. Ambos clientes necesitan la misma versión del prefab por su nuevo NetworkBehaviour.

## Arte y coste

Ojos y boca son sprites separados de 256×256, originales y regenerables mediante `Tools > GameJam > Rebuild Visor Sprites`. Se pueden sustituir desde el componente. Usan material compartido unlit transparente, profundidad normal y sin sombras, sin luces adicionales ni materiales instanciados por jugador. Son dos renderers extra por astronauta; el coste total multijugador aún requiere medición.

Validado en Host dentro del Editor: habla, dolor al reducir salud, cierre por timeout y eliminación; sin errores de ejecución. Pendiente validación con dos clientes y con el proveedor real de voz. Las capturas de revisión se obtuvieron con cámara temporal, evitando la captura compuesta del MCP que antes provocó recursión del PlayerLoop.
