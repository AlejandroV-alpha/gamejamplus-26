# Marte: zonas de interes defendidas

Las piezas y minerales se agrupan junto a hitos ya presentes en Mars. No se modifico Neptuno. Se conservaron 18 minerales (6 metal, 6 circuitos, 6 mineral rojo), tres piezas unicas y sus scripts/identidad Fusion.

| Zona | Hito existente | Centro X/Z | Recompensa | Defensores |
|---|---|---|---|---|
| POI 1 - Campamento abandonado | j_AbandonedCamp | -217, 176 | DamagedPropulsionPickup | 2 Reksai |
| POI 2 - Refugio de Marte | j_MarsHouse1_Review | 151, 29 | DamagedIntakePickup | 2 Reksai |
| POI 3 - Cuenca del gusano | j_WormBasin | -22, -221 | DamagedPanelPickup | 1 Reksai + gusano de la cuenca |
| POI 4 - Yacimiento arco oeste | j_RockArch_2 | -142, -111 | 2 metal + 2 circuitos + 2 mineral rojo | 2 Reksai |
| POI 5 - Yacimiento arco este | j_RockArch | 196, 120 | 2 metal + 2 circuitos + 2 mineral rojo | 2 Reksai |
| POI 6 - Yacimiento arco norte | j_RockArch_3 | 116, 184 | 2 metal + 2 circuitos + 2 mineral rojo | 2 Reksai |

Los grupos de minerales quedan a unos 12-20 m de sus arcos. Las piezas estan junto al campamento, refugio y borde de cuenca, conservando su humo de localizacion. Todos los recolectables estan dentro del radio explorable. No se bloquea E artificialmente: la dificultad es el combate con guardianes antes de acercarse a recoger.

Se reutilizaron los cuatro spawners existentes y se agregaron siete. Maximo 11 Reksai mas un gusano; activacion de Reksai a 110 m, con comprobacion cada 0,5 s solo hasta crearlos. DidSpawn impide reapariciones continuas. MarsInterestZone ajusta los objetos al Terrain antes de jugar en cada cliente; el resto del spawn y dano sigue bajo autoridad Fusion. No hay busquedas de minerales ni consultas de suelo cada frame.

Validado offline: compilacion de juego y Editor; identificadores y referencias de escena; cantidades conservadas; separacion de recursos y pertenencia a la zona explorable. Pendiente en Unity: altura/colisiones junto a estructuras, llegada de dos clientes, guardianes antes de la recogida y rendimiento con varias zonas activadas. No se utilizo MCP.
