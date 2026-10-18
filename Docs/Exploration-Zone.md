# Zona explorable

Configurada en `j_MarsFusion` y `b_NeptuneExploration` mediante el objeto **Exploration Zone**.

- `Transform.position`: centro del radio horizontal; la altura no interviene.
- `Radius`: Marte 460 m, Neptuno 450 m. Neptuno se centra en (120, 0, 382).
- `Return Seconds`: 10 segundos para regresar.
- `Terrain` y `Terrain Edge Margin`: excluyen espacio fuera del terreno rectangular, sin modificar su geometría. Margen inicial 2 m.
- Seleccionar el objeto muestra el radio en Scene mediante Gizmos.

`PlayerExplorationBoundary` está en los prefabs A_FusionAstronaut, j_FusionAstronaut y A_NeptuneAstronaut. El propietario de estado Fusion mantiene el TickTimer; la UI solo aparece para ese jugador. Volver al área cancela el contador y una nueva salida comienza con el tiempo completo. Al vencer, el avatar deriva el daño letal a PlayerHealth, que replica vida y muerte. La UI no captura controles. Sin zona en una escena (por ejemplo lobby), no hay penalización.

Se enlazó PlayerHealth también en las variantes que no tenían vida, para que el daño letal no dependa de un componente ausente. Si se reemplaza el sistema de vida, mantener IPlayerHealth y enlazar healthSource en MarsFusionAvatar.

Validación local Fusion Single: salida inicia contador; reentrada cancela sin daño; segunda salida con duración temporal de 1 s lleva vida a 0, IsAlive=false y oculta advertencia. Sin errores de consola. Spawn y punto distante de Neptuno comprobados dentro de zona; coordenada fuera del terreno rechazada. Los valores temporales de prueba no se guardaron. Pendiente comprobación de la misma secuencia con dos clientes online.
