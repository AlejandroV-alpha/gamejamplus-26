# Identidad visual del jugador

`MarsFusionAvatar` replica `SuitColorIndex` y `PlayerName` con Fusion. La autoridad
asigna la identidad al aparecer: un color aleatorio entre ocho colores vivos,
priorizando colores que no usan los astronautas ya presentes, y un nombre
automático `Astronauta N` basado en el PlayerRef. Dos entradas simultáneas pueden
coincidir en color; la selección no es una reserva centralizada. No hay selector
de color ni formulario de nombre en esta entrega.

`MarsPlayerIdentityView` se crea desde el avatar de ambos prefabs, tanto en lobby
como en Marte. Aplica el tinte a los SkinnedMeshRenderers mediante un
MaterialPropertyBlock, conservando las texturas y los materiales compartidos.
Los sprites de expresión del visor y la tablet no se tiñen.

El nombre usa texto 3D sobre el astronauta, mira hacia la cámara y se oculta a
más de 40 metros y para el propietario local. Respeta la profundidad de la escena.
El texto no interpreta etiquetas de rich text.

La futura pantalla de nombres puede llamar `SetPlayerName(string)` en el avatar
local. Solo la autoridad puede modificarlo; se ignoran nombres vacíos, se
eliminan saltos de línea y se limita el nombre a 24 caracteres. No se guarda
todavía una preferencia entre partidas.

Validación: compilación y tejido de Fusion, aparición en Single, asignación de
color, conservación del material compartido, cambio de nombre y ocultación del
rótulo propio. Falta comprobar visualmente los rótulos y la sincronización de
identidades con dos clientes online.
