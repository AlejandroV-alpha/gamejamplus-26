# Equipación y radio

Cada jugador aparece con arma principal y walkie-talkie en los espacios 1 y 2. Ambos ocupan una unidad de la barra de seis espacios: quedan cuatro libres. Se pueden soltar y recoger con los mismos flujos de objetos de red. Para una receta de seis unidades hay que liberar los espacios de equipamiento.

## Uso

- Números o rueda: seleccionar el objeto que se equipa. Solo el espacio seleccionado equipa un objeto; llevar un arma en otro espacio no permite disparar.
- Arma seleccionada: clic derecho alterna apuntado, clic izquierdo dispara y R recarga. Cambiar de espacio cancela una recarga pendiente, retira el modelo y bloquea apuntado, disparos y sus efectos. Cambiar de arma no repone munición.
- Radio seleccionada: clic izquierdo abre o cierra el canal. Mientras esté abierto se transmite voz a cualquier distancia a jugadores vivos de la misma partida que lleven radio en su barra, aunque tengan otro objeto seleccionado.
- Cambiar de espacio, soltar la radio, morir, abrir la tablet o bloquear los controles cierra el canal. Al volver a equiparla hay que abrirlo de nuevo.
- Sin canal de radio abierto se conserva la voz de proximidad y sus preferencias de pulsar V / micrófono abierto. Sin radio no se recibe el canal a distancia.

Las preferencias individuales de silenciar y volumen se respetan tanto en proximidad como en radio. El canal de radio usa reproducción sin atenuación por distancia; la proximidad conserva el límite y audio espacial anteriores. Se reutiliza Photon Voice, no se crea otra conexión de voz ni se guardan credenciales en estos documentos.

## Edición

- Datos e iconos: `Assets/_SO/Equipment/PrimaryWeapon.asset` y `WalkieTalkie.asset`.
- Objetos de suelo: `Assets/02_Prefabs/Interactables/Equipment/PrimaryWeaponPickup.prefab` y `WalkieTalkiePickup.prefab`.
- Modelo en mano: `Assets/Resources/Equipment/WalkieTalkieVisual.prefab`.
- Sonidos generados de equipar y abrir/cerrar: `Assets/Resources/Audio/Radio/`.
- `Inventory.startingItems`: equipamiento inicial; sigue consumiendo la capacidad normal.
- `PlayerEquipment`: clips y volumen de feedback local.
- `EquipmentPresentation`: agarre, orientación del walkie y mezcla de la pose de llamada. La pose es procedural sobre el brazo derecho; las animaciones de locomoción y reanimación conservan su prioridad.

El menú `Tools/GameJam/Equipment/Configure weapon and radio loadout` reconstruye los recursos de equipamiento y configura los prefabs de astronauta existentes. Se ejecutó para la configuración inicial; no se ejecuta automáticamente en cada partida.

El protocolo de contenido pasa a `expedition-4-equipment-radio`: todos los participantes necesitan esta misma versión.

## Validación realizada

Compilación correcta en Unity y con el compilador C#. Prueba Fusion Single con dos avatares simulados: equipamiento inicial 2/6, apuntar y disparar con arma seleccionada, bloqueo y cancelación de recarga al cambiar de espacio, modelo del arma sin desactivar al jugador, canal de radio, recepción con radio guardada, corte al perderla, audio 2D a distancia y vuelta al límite espacial de proximidad. Soltar y recuperar arma y radio usa los prefabs de red y no repone munición. Muerte y cambio de espacio cierran el canal. Iconos y pose de radio inspeccionados en una captura de 1600×900.

Pendiente: prueba de audio real con dos clientes online y dos micrófonos. Las pruebas simuladas verifican estados y filtros de reproducción, no la calidad de audio ni la entrega entre dos equipos.

También se comprobó el inicio y cierre del runner después de la integración: sin excepciones de ejecución de equipamiento/voz en la última prueba.

El agarre del walkie usa posición, rotación y escala **locales de la mano derecha**, sin sobrescribir la orientación mundial del objeto. Calibración: posición `(-0.092, 0.413, -0.036)`, rotación `(25.626, -67.976, 53.75)`, escala `(1.666664, 1.666664, 1.666664)`. El configurador conserva los ajustes de agarre de componentes existentes.

La mano al hablar tiene un objetivo separado del agarre del objeto: `talkingHandOffset = (0.32, -0.42, 0.30)` en metros respecto a la cabeza y los ejes del jugador. Mantiene la mano frente al pecho, con el codo bajo, sin cambiar la calibración local del walkie. `carryingHandOffset` conserva la pose de transporte con el canal cerrado. Ambos se pueden editar en `EquipmentPresentation`.

La pose de radio dirige el codo hacia abajo, junto al torso, y limita el alcance al 78 % de la longitud total del brazo. El objetivo se limita antes de resolver ambos huesos, para que la mano no vuelva a estirar el codo. Comprobado en Play de frente y de perfil: codo flexionado (72° en la captura) y por debajo del hombro.
