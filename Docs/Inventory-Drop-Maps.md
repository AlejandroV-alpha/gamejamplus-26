# Dropeo de inventario en Marte y Neptuno

Se integro feature/inventory-drop (33bca10 y ce87d43). El prefab A_SurvivalAstronaut usado por Mars ya incluye PlayerMaterialDrop; A_FusionAstronaut usado por b_NeptuneExploration tambien lo incluye y esta registrado en NetworkedBehaviours del NetworkObject raiz. No se agregan NetworkBehaviours al jugador en ejecucion.

Abrir inventario con I, seleccionar material y pulsar Soltar 1. Aparece un MaterialPickup de una unidad delante del jugador, sobre suelo valido y con espacio libre. Solo se descuenta la unidad si Fusion confirma la creacion; si falla quitarla del inventario se retira el objeto creado. Otro jugador puede recogerlo con E usando la autoridad del pickup.

Soporta los ocho materiales actuales, incluidas las piezas de nave. No se transformaron las bolsas de agua en inventario/consumibles guardables: ese trabajo sigue pendiente de autorizacion del usuario.

La integracion bloquea la accion durante tormenta/derribo, asistencia, recuperacion, emotes, dano, tablet, crafting y transiciones, y exige que la conexion Shared este lista mediante CanSpawnNow. El inventario abierto sigue permitiendo soltar aunque bloquee el movimiento del jugador.

Validado offline: compilacion de juego y Editor, prefabs activos de ambas escenas, componente raiz y lista Fusion, ocho pickups funcionales. Pendiente Unity: dos clientes, soltar una unidad y recogerla con el otro, intentar con inventario vacio, colocacion ocupada y desconexion. Reiniciar ambos clientes tras importar cambios de prefabs. No se utilizo MCP.
