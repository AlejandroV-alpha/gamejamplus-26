# Mesa de Marte y reparacion incremental

Mars reutiliza la mesa animada y el CraftingBox de b_NeptuneExploration, conservando el planeta Mars y sus tres recetas. Se reemplazo la mesa graybox, manteniendo el ID de CraftingTable para las referencias existentes.

La estacion sigue siendo un NetworkObject de escena para conservar identidad y autoridad; su marcador visual desaparece. El jugador interactua con Q desde la superficie de la nave real. Se usa la distancia a sus colliders activos; para MeshColliders no convexos se usa bounds, evitando una consulta ClosestPoint no soportada. La animacion de soldadura y las chispas se ubican junto al jugador, no en el marcador oculto.

Cada accion instala un componente fabricado aun no instalado. CraftingTable conserva la fabricacion compartida y sus costes; ShipRepairStation conserva un bit de instalacion por receta. La autoridad de la estacion decide y escribe el bit, evitando instalar o contar dos veces la misma pieza. Al completar tres instalaciones se activa el retorno al lobby existente. No hace falta fabricar tres componentes antes de instalar el primero.

ShipRepairPresentation relaciona los meshes de las piezas ausentes con los prefabs de ingredientes de cada receta, para reponerlos por componente. Los objetos sin coincidencia usan un grupo determinista entre los tres componentes. Los que entren tarde leen la misma mascara replicada.

Prueba en Unity pendiente: fabricar solo el propulsor, usar Q en distintos lados de la nave, ver 1/3 instalado y 2 pendientes; repetir para los otros dos; interrumpir soldadura por dano o alejamiento; intentar instalar la misma pieza desde dos clientes; verificar las piezas correctas y la apertura/cierre de la mesa en Marte. Reiniciar los clientes tras importar: cambio el esquema de red de ShipRepairStation. No se utilizo MCP.
