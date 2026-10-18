# Salud del astronauta

`j_MarsFusion` utiliza `Assets/02_Prefabs/Fusion/A_FusionAstronaut.prefab`.

`MarsFusionAvatar` controla tablet, expresiones, voz y energía. No almacena vida,
no inicializa una cantidad de salud y no calcula daño ni muerte.

El futuro componente de salud debe implementar `GameJam.Combat.IPlayerHealth`:

- `CurrentHealth` y `MaxHealth`: valores consultables para el HUD y las expresiones.
- `IsAlive`: estado que habilita controles, interacción y selección de objetivos.
- `TakeDamage(int damage)`: implementación de la interfaz existente `ITakeDamage`.

Agregar ese componente al objeto raíz de `A_FusionAstronaut` y asignarlo al campo
**Health Source** del avatar. Si no se asigna, el avatar busca un componente que
implemente la interfaz en ese mismo objeto al aparecer en la sesión.

El componente nuevo será responsable de almacenar y replicar la vida con Fusion,
inicializarla y aplicar las reglas de daño. El avatar reenvía los golpes mediante
RPC hacia su autoridad y luego llama al componente; no vuelve a descontar vida.

Hasta implementar ese componente, el jugador puede explorar e interactuar. El HUD
no muestra una vida ficticia y los golpes producen un único aviso por avatar sin
modificar salud. La tecla H sigue previsualizando la expresión de dolor.

Los clientes deben usar la misma versión del prefab y los scripts: retirar el campo
de vida replicado cambia el esquema de red de `MarsFusionAvatar`.
