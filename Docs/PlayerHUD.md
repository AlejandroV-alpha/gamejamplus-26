# Interfaz del jugador

La rama `codex/player-ui` crea el HUD local al aparecer el `MarsFusionAvatar` propietario. No requiere modificar escenas ni agregar componentes de red a los prefabs. Se elimina junto con el avatar.

## Presentación y controles

- Arriba a la derecha: acceso a tablet con **Tab**. Arriba a la izquierda: hasta tres avisos de incremento de materiales, con desvanecimiento. La primera lectura del inventario se toma como referencia y no genera avisos por objetos que ya existían.
- Abajo a la izquierda: cabeza con silueta angular de casco, visor oscuro, laterales y mentonera, junto a ambiente, vida y estamina. `PlayerHelmetGraphic` dibuja la silueta con geometría uGUI y toma el color del traje. Es un retrato 2D, no una captura del modelo 3D. Sobre el visor se copian los sprites de ojos y boca del avatar: incluye parpadeo, habla, daño y eliminación, sin otra cámara ni RenderTexture.
- Al centro del lado derecho: indicador de arma presente o manos libres y acceso al inventario con **I**. La ventana del inventario también queda centrada verticalmente y alineada a la derecha.
- **I** abre/cierra el inventario; **Esc** lo cierra antes de abrir pausa. Mientras está abierto se bloquea el movimiento y se libera el cursor. Tablet, fabricación, muerte y transiciones cierran el inventario.
- El inventario muestra los materiales presentes, cantidades y descripción del material seleccionado. Lee el inventario replicado; no permite equipar minerales como herramientas ni altera cantidades.
- La barra de progreso de recolección/reparación reemplaza el indicador IMGUI cuando existe este HUD.

## Idiomas y datos

Todos los textos nuevos usan `GameLocalization`: español, inglés y portugués de Brasil. Los rótulos usan `LocalizedText`; cantidades, avisos y detalles se actualizan con `OnLanguageChanged`. Las traducciones de HUD y de los ocho materiales están en `GameLocalization.PlayerUI.cs`, dentro del mismo sistema centralizado. Las teclas son prefijos independientes del texto traducido.

Las barras leen `IPlayerHealth`, `MarsFusionAvatar.Stamina` y `SurvivalAvatar.MaximumStamina`. En Neptuno se muestra `PlayerCold.GetColdPercentage()`. Marte aún no tiene simulación de calor: aparece la etiqueta de calor con **Sin sensor**, sin inventar valores ni daño. Los materiales muestran su `MaterialData.icon` cuando está asignado y conservan nombre/cantidad cuando no tienen icono. El equipo usa el `weaponVisual` existente; todavía no existe un catálogo de equipo con nombres e iconos individuales.

El HUD separa inventario y estado en canvases hijos, respeta el área segura y escala desde 1920×1080. Consulta cambios de inventario a 5 Hz y estado a 10 Hz; las expresiones siguen los sprites cada frame.

## Validación de esta entrega

- Compilación C# con las referencias Unity/Fusion existentes y los dos archivos nuevos incluidos mediante un targets temporal de MSBuild.
- Prueba ejecutada del diccionario real: 114 traducciones, nombres y descripciones de los ocho materiales, marcadores de formato, fallback y cambio de idioma. PlayerPrefs/Application se sustituyeron por stubs en memoria en esa prueba; no verifica persistencia en el motor.
- `git diff --check`.
- Pendiente revisión visual en Play Mode y prueba con dos clientes. El intento de Unity batch terminó con código 1 antes de importar/compilar y no permitió verificar el motor. La compilación de C# no sustituye el tejido de Fusion ni las pruebas de ejecución.

Para revisar: entrar en una escena de exploración con el jugador local; recoger y consumir materiales; abrir **I**, seleccionar materiales y cerrar con **Esc**; cambiar idioma desde pausa; comprobar daño/voz/parpadeo y frío de Neptuno; salir y volver a entrar para verificar que no quedan HUD duplicados.

## Corrección del retrato y ciclo de red

Se corrigió el centro de triangulación de cada polígono del casco. Los paneles laterales y la mentonera usaban el centro del retrato y extendían triángulos sobre la cara. Una prueba ejecutada sobre `PlayerHelmetGraphic` con stubs de uGUI comprobó los 77 vértices y 68 triángulos, incluidos los límites de los cuatro paneles.

El registro del Editor también mostraba accesos a `PlayerHealth.IsAlive`, `PlayerHealth.CurrentHealth`, `MarsFusionAvatar.IdentityInitialized` y `PlayerInteractionAction.ActionKind` sin estado de red disponible. El avatar ahora expone `IsNetworkReady`, filtra fuentes de vida sin buffer y evita leer el color antes de tiempo; HUD, identidad, animaciones de daño/muerte, recolección y acciones comprueban disponibilidad antes de acceder. La compilación C# posterior pasó sin errores. Pendiente confirmar en Play Mode la salida/entrada a una partida y las recargas de scripts.
