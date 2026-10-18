# Piezas perdidas y controles

Las piezas estan bajo **Lost Ship Parts** en j_MarsFusion y b_NeptuneExploration. Coordenadas X/Z:

| Pieza | Marte | Neptuno |
|---|---|---|
| Turbina | -170 / -165 | -80 / 180 |
| Toma de aire | 105 / -210 | 255 / 330 |
| Panel exterior | -65 / 165 | 70 / 570 |

Cada pieza tiene una rotacion de caida diferente y esta apoyada sobre el terrain. Se conserva su collider y un ancla de interaccion a nivel del suelo. No se simula un rigidbody rodando por la pendiente, para mantener las ubicaciones y la recogida consistentes entre clientes.

DamagedPartSmoke genera una columna vertical de unos 100 m, con deriva lateral. Es un efecto local ligado a la disponibilidad replicada del recolectable. Desaparece cuando la pieza se reclama y no emite en minerales normales. Material suave sin sombras ni colisiones, limite de 80 particulas por pieza (240 para tres columnas). El material no aplica la niebla del terrain para conservar la referencia durante la tormenta; la geometria del terreno si puede ocultarla. No garantiza visibilidad desde cualquier punto ni atraviesa montanas.

**F repara; R recarga el arma; E recoge.** Junto a la estacion aparece F | Reparar nave. La reparacion mantiene el gesto procedural de trabajo, chispas y barra de 2,4 s; la nave se restaura al finalizar y confirmar la reparacion. Recibir dano o alejarse cancela la accion.

Prueba local: las tres columnas alcanzaron 75 particulas cada una, con orientacion vertical independiente de la pieza. Al reclamar una pieza paso a 0 particulas. Se renderizo una vista distante y las columnas se distinguian en el horizonte. La ruta de PlayerShipRepair inicio la accion con Repaired=false y termino con Repaired=true. Sin errores de consola. Los movimientos y recetas temporales de validacion no se guardaron. Pendiente prueba entre dos clientes.
