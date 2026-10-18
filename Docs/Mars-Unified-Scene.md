# Marte unificado: j_MarsFusion

La escena principal de Marte es `Assets/00_Scenes/j_MarsFusion.unity`. Conserva terreno, nave, mesa, recursos, reparación, tablet, voz y controles actuales. Usa `A_FusionAstronaut` también cuando se llega desde el lobby.

## Integración

- El astronauta conserva apuntado, arma, crosshair y cámaras actuales. Ahora incorpora `PlayerHealth`, enlazado al avatar mediante `IPlayerHealth`, y `PlayerSandstorm`.
- El movimiento pasa por `PlayerSandstorm.ModifyMovementVelocity` antes del único `KCC.Move`; no se añade un segundo controlador.
- La tormenta de Mars Sandstorm conserva su ciclo sincronizado: espera aleatoria de 45–75 segundos, tormenta de 20 segundos, movimiento al 50 % y ráfagas. `MarsStormAtmosphere` reduce progresivamente la visibilidad y restaura la niebla al terminar. Se oculta el cilindro de graybox.
- Los cuatro puntos de Reksai usan `ReksaiFusionAreaSpawner` y el nuevo prefab `Reksai_Fusion`. Cada punto crea un enemigo una sola vez desde su autoridad. No se ejecutan los spawners Netcode anteriores en esta escena.
- `ReksaiFusionEnemy` sincroniza vida, posición, rotación y estado de animación. La autoridad simula persecución, ataques, enterramiento y muerte. El arma solicita el daño a esa autoridad; el enemigo aplica daño al componente de vida del jugador. Los NPC alienígenas decorativos se conservan.

## Prueba realizada

En Play, partida local Fusion Single:

- Astronauta con 100 de vida, cuatro enemigos válidos y una tormenta registrada.
- Ruta de impacto del arma: vida del enemigo 160 → 159 (se conserva el daño configurado del arma).
- Ataque del enemigo: jugador 100 → 82.
- Daño letal: enemigo pasa a 0 y estado Death.
- Tormenta activada temporalmente durante la prueba: velocidad solicitada 4 → 2. Los valores temporales no se guardaron.
- Sin errores de Unity durante estas comprobaciones.

Esto valida rutas de daño y simulación local; falta comprobar con dos equipos la replicación, entrada tardía y salida del cliente que posee enemigos. No equivale a una prueba manual completa de puntería o de navegación por todo el terreno.

## Prueba con amigos

Abrir `j_MarsFusion`, iniciar online con la misma sala, región y versiones. Verificar cuatro enemigos compartidos, disparar a uno y observar la misma vida/muerte, recibir un ataque y esperar al ciclo de tormenta en ambos clientes. Desde lobby se mantiene el mismo prefab equipado y con vida.

Las escenas de combate y tormenta se conservan como referencias aisladas hasta validar este recorrido multicliente. No se borran niveles de otros planetas ni escenas con contenido único.
