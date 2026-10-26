# Inventario limitado a la barra rápida

El arma principal y el walkie-talkie ocupan los espacios 1 y 2 al aparecer; quedan cuatro libres. Su selección y canal se describen en [Equipment-And-Radio.md](Equipment-And-Radio.md).

La barra rápida es toda la capacidad del jugador: seis espacios por defecto, una unidad por espacio y sin pilas. Materiales repetidos ocupan espacios distintos. No existe mochila adicional ni ventana con I.

- Números 1–6 y rueda del ratón: seleccionar espacio.
- G: soltar exactamente la unidad del espacio seleccionado. El espacio queda libre y los otros objetos conservan su posición.
- E: recoger. Cuando no cabe la cantidad completa del recolectable, permanece en el mundo y aparece feedback en el HUD.
- E junto a la caja: abrir fabricación. Los antiguos atajos 4–6 ya no fabrican, porque seleccionan espacios.

En el componente `Inventory` del prefab del jugador, `Slot Count` configura entre 1 y 10 espacios. Se aplica al aparecer el jugador y se replica desde su autoridad. El décimo espacio se selecciona con 0. Las recetas actuales necesitan hasta seis unidades; reducir la capacidad por debajo de seis requiere revisar esas recetas.

`GetContents`, `GetMaterialAmount`, `HasMaterials` y `TryRemoveMaterials` siguen ofreciendo cantidades agregadas para la tablet y la fabricación. La fuente de verdad son los espacios replicados: no existe otro contador con capacidad ilimitada. La fabricación valida todos los ingredientes antes de vaciar ningún espacio. Recogidas fallidas no eliminan el recurso; los controles de autoridad y el protocolo de recogida existente siguen aplicándose.

La muerte y el reinicio de misión usan los mismos flujos existentes para recuperar objetos y crear un inventario vacío. El protocolo de contenido cambia a `expedition-4-equipment-radio` para separar clientes con la mochila anterior.

## Validación

Compilación C# correcta y prueba en Unity con Fusion Single: seis unidades en espacios distintos, rechazo de desbordamiento, reutilización del hueco sin mover otros objetos, consumo atómico, costes de las nueve recetas, recogida real por red con barra llena y tras liberar espacio, y soltar la unidad de un espacio concreto. HUD renderizado a 1920×1080. No se ha realizado una prueba con dos clientes online.
