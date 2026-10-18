# Sprites de materiales y recetas

Se conservaron las cinco imágenes y asignaciones existentes de circuito, metal, mineral rojo, cristal de hielo y cristal energético. Se generaron doce PNG nuevos mediante la herramienta integrada ImageGen, con canal alpha real, y se asignaron a los campos `icon` que estaban vacíos.

Los archivos están en `Assets/03_Sprites/Items/`. Su importador está configurado como Sprite (Single), con transparencia, sin mipmaps, máximo de 512 px y sin geometría física automática. Se conservan los PNG originales de mayor resolución en esa misma carpeta; el límite de 512 se aplica al importar en Unity.

| Sprite nuevo | ScriptableObject en `Assets/_SO/` |
| --- | --- |
| toma_aire_danada.png | Materials/DamagedIntake.asset |
| panel_casco_danado.png | Materials/DamagedPanel.asset |
| turbina_danada.png | Materials/DamagedPropulsion.asset |
| filtro_aire.png | Recipes/Mars/FiltroDeAire.asset |
| propulsor.png | Recipes/Mars/Propulsor.asset |
| radar.png | Recipes/Mars/Radar.asset |
| blindaje.png | Recipes/Neptune/Blindaje.asset |
| motor.png | Recipes/Neptune/Motor.asset |
| navegador.png | Recipes/Neptune/Navegador.asset |
| estabilizador.png | Recipes/Saturn/Estabilizador.asset |
| hiperpropulsor.png | Recipes/Saturn/Hiperpropulsor.asset |
| nucleo_energetico.png | Recipes/Saturn/NucleoEnergetico.asset |

## Referencias revisadas

Los prefabs `DamagedIntakePickup`, `DamagedPanelPickup` y `DamagedPropulsionPickup` usan mallas extraídas de `Nave_Puerta_Animada_Corregida/Nave_Puerta_Animada_Corregida/Nave_Puerta_Animada.fbx`: rejillas frontales, placas del techo y anillos/collares de turbina, respectivamente.

Se usó como referencia visual `Assets/06j_Models/Nave_Puerta_Animada_Unity/Nave_Puerta_Animada/Puerta_Cerrada.png`, junto al sprite existente `metal_recuperado.png`. Los iconos son ilustraciones generadas inspiradas en estas referencias; no son capturas exactas de las mallas. Las recetas no tienen un campo de modelo/prefab de resultado y sus componentes se diseñaron con la misma construcción y paleta de la nave.

El prompt completo de cada imagen, sus referencias y su asignación están en [ItemSprites.prompts.json](ItemSprites.prompts.json).

## Comprobaciones

- Los ocho materiales y las nueve recetas tienen un sprite con GUID resoluble a un PNG del proyecto.
- Los doce PNG nuevos contienen píxeles transparentes y visibles; tienen importador de Sprite y máximo de 512 px.
- Se conservaron los GUID de las cinco asignaciones anteriores.
- `git diff --check` pasó.
- Pendiente comprobar el aspecto final en las ventanas de inventario y fabricación de Unity.
