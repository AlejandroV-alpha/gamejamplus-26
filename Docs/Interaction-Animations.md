# Animaciones de recogida y reparación

PlayerInteractionAction está añadido a los cinco prefabs Fusion del astronauta. Crea gestos procedurales sobre columna y brazo derecho del rig actual; no son clips externos ni una nueva máquina de locomoción. La columna se inclina hacia delante y el brazo alcanza el objetivo. Al reparar se añade movimiento repetido de trabajo y pequeñas chispas. El objeto del arma se asigna explícitamente en Weapon Visual (K-39), evitando ocultar al astronauta.

- E: PlayerCollector encuentra el objeto cercano y comienza la acción de 0,8 s. MaterialPickup.RequestCollection solo se llama al terminar. La autoridad existente decide el ganador y confirma la entrega; las partículas se envían después de confirmar y el objeto se despawnea 0,2 s más tarde para permitir la notificación.
- F: PlayerShipRepair busca la estación y comprueba que hay tres recetas terminadas. Inicia 2,4 s de trabajo. RequestRepair se ejecuta al terminar y vuelve a validar vida, posición y recetas. La presentación de piezas se mantiene ligada al estado Repaired, no al comienzo de la animación.
- Fusion replica tipo de acción, objetivo y temporizador; cada cliente presenta el gesto localmente. Las partículas no son objetos de red.
- La barra indica progreso. Se bloquea el control y se suspende el apuntado durante la acción. Daño, muerte, tablet abierta, pérdida del objetivo o distancia excesiva cancelan sin solicitar recogida/reparación. No se pueden encadenar acciones simultáneas. Se requiere estar apoyado en el suelo al comenzar cuando existe el controlador KCC.
- La recogida no reserva el objeto durante la animación: si dos jugadores lo intentan, gana la primera solicitud válida al terminar. El otro no recibe materiales ni efecto de éxito.

Pickup Seconds, Repair Seconds, Feedback Material y Weapon Visual son configurables por prefab. Las fuentes directas RequestCollection/RequestRepair siguen disponibles como API de bajo nivel; los controles normales pasan por la acción animada.

Validación local en j_MarsFusion: la recogida empezó ocupada, con el recurso todavía disponible y sin ingreso inmediato; al terminar se recibió el circuito. Interrupción determinista por daño: acción cancelada, cantidad antes/después 0, objeto disponible. Reparación: Repaired=false inmediatamente después de comenzar, true al terminar, mallas de nave restauradas. Para aislar el tiempo de reparación, las tres recetas se marcaron completas temporalmente durante Play. Revisión de pose con cámara renderizada y sin errores de consola. Las pruebas no guardaron cantidades ni duraciones temporales.

Asignados efectos en ambas escenas principales. Pendiente validar la representación remota y las carreras de recogida en una partida real con dos clientes. Los gestos procedurales pueden sustituirse posteriormente por clips animados de un artista manteniendo el protocolo de confirmación al final.
