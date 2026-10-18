# Materiales, piezas recuperadas y reparación

## Lógica actual

1. MaterialPickup solicita la recogida a su autoridad Fusion. Se valida propietario, vida, distancia (2,5 m), inventario y disponibilidad. Solo un jugador gana. Tras confirmar el ingreso se despawnea el objeto para todos.
2. Inventory mantiene contadores replicados individuales, identificados mediante MaterialData.id. Ahora admite ocho tipos, en el mismo orden en todos los prefabs: Metal, Circuit, IceCrystal, EnergyCrystal, RedMineral, DamagedPropulsion, DamagedIntake, DamagedPanel. MaterialData.prefab apunta al recolectable funcional; icon sigue pendiente de arte.
3. Cerca de una CraftingTable, 4/5/6 solicitan sus tres recetas. Se comprueban ingredientes completos, se aprueba un jugador, su inventario consume el coste y confirma. Cada receta se completa una vez para todo el equipo. No se crea una pieza física fabricada ni un objeto equipable: el resultado es progreso compartido en esa mesa.
4. F junto a ShipRepairStation comprueba que la mesa enlazada tiene 3/3. Repaired pasa a true una vez. ShipRepairPresentation lee ese estado y reactiva las mallas ausentes de la nave en todos los clientes, incluidos los que entran tarde.
5. Al morir, InventoryDeathRecovery genera un recolectable por tipo con la cantidad acumulada y vacía ese saldo. MaterialPickup.DroppedAmount replica esa cantidad. Usa la última posición dentro de la zona explorable, para que salir y morir no deje una pieza indispensable fuera del mapa.

No hay minería con golpes, durabilidad de yacimientos, intercambio, almacén compartido ni inventario visual completo. E recoge el mineral entero. La tablet sigue siendo una guía informativa con catálogo propio; se actualizaron sus textos de costes, pero no consulta cantidades ni progreso real automáticamente. No se agregó regeneración de salud.

## Modelos conectados

| Dato | Modelo visual reutilizado |
|---|---|
| Metal | Mars/Resources_mars/Metal_Model |
| Circuit | Mars/Resources_mars/circuit_Model |
| RedMineral | Mars/Resources_mars/crimsom_red_Model |
| IceCrystal | Neptune/resources_neptune/azure_crystal_model |
| EnergyCrystal | Neptune/resources_neptune/crystal_violet |

Los prefabs funcionales siguen bajo Assets/02_Prefabs/Interactables/Materials. Se conservaron MaterialPickup, NetworkObject y collider; se sustituyeron sus cubos por los modelos, centrados y con tamaño legible. La distribución activa original se convirtió en recolectables independientes; sus copias decorativas se desactivaron para evitar superposición. Los puntos fuera de Exploration Zone no generan recursos necesarios. Neptuno recibió además 12 metales y 12 circuitos distribuidos como suministros recuperables.

## Piezas de la nave

Se reutilizan 17 objetos del FBX Nave_Puerta_Animada, agrupados en tres recolectables:

- DamagedPropulsion: anillo, collares y luz de una turbina.
- DamagedIntake: conjunto de rejillas del modelo, o marcos e interior de la toma como alternativa.
- DamagedPanel: cuatro placas de armadura exterior.

Las copias recuperables usan esas mismas mallas y materiales, con tamaño máximo de 3,4 m para interacción. No se modificó el FBX. Las piezas originales se desactivan en la nave de cada mundo y se restauran al terminar la reparación. El lobby conserva la nave completa. Hay una unidad de cada conjunto repartida por planeta bajo Lost Ship Parts.

Marte:
- Propulsor: 2 metal + 2 mineral rojo + 1 turbina dañada.
- Filtro: 2 mineral rojo + 1 circuito + 1 toma de aire dañada.
- Radar: 2 circuitos + 1 metal + 1 panel de casco dañado.

Neptuno:
- Motor: 3 metal + 2 cristal de hielo + 1 turbina dañada.
- Navegador: 2 circuitos + 1 cristal de hielo + 1 toma de aire dañada.
- Blindaje: 3 metal + 1 circuito + 1 panel de casco dañado.

La asociación de la carcasa de toma al navegador es una primera asignación funcional con las mallas disponibles; el arte puede sustituirse por un módulo de navegación dedicado. Las recetas de Saturno no se modificaron.

Neptuno no tenía nave, mesa ni estación funcionales en su escena principal: se añadieron una copia de la nave actual y estaciones enlazadas con las recetas de Neptuno. Las mesas y estaciones conservan su aspecto de graybox; este cambio reemplaza el arte de materiales y piezas, no todos los interactuables.

## Validación

Marte, Fusion Single: recogida real mediante RequestCollection de tres piezas y un mineral, comprobando su entrada en inventario. Para aislar la fabricación se añadieron cantidades de prueba de minerales; las recetas progresaron 1/3, 2/3, 3/3 y consumieron las piezas. RequestRepair terminó correctamente y se reactivaron los 17 objetos originales. Una muerte posterior vació el inventario y generó pilas replicadas de metal x7, circuito x7, mineral rojo x7 y una pieza x1. Sin errores de consola. Los movimientos y cantidades de prueba no se guardaron.

Neptuno, Fusion Single: recolectables registrados, jugador con collector, mesa Neptune, estación válida y 17 objetos ausentes antes de reparar. Sin errores de consola. La prueba completa de reparación se realizó en Marte.

Pendiente: concurrencia real entre dos equipos, migración de autoridad y desconexión del portador de piezas. La recuperación añadida cubre muerte, no desconexión. CraftingTable ya conserva un bloqueo si el jugador aprobado se desconecta antes de confirmar un cobro incierto; falta una política de recuperación para ese caso. No hay persistencia de inventario entre recreaciones de avatar/cambio de planeta.

Herramienta de configuración: Assets/Editor/ResourceGameplayIntegration.cs. PrepareAssets actualiza datos y prefabs; ConfigureScenes trabaja con ambas escenas abiertas. Se conservaron las escenas existentes y no se realizaron commits en este paso.
