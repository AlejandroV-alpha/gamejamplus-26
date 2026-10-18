# Derribo de tormenta

- PlayerSandstorm decide el derribo desde StateAuthority cuando la tormenta alcanza al jugador en el aire.
- downState se replica: 0 libre, 1 tendido, 2 levantandose. El KCC conserva gravedad y colisiones; no se activa un ragdoll fisico.
- El jugador queda inmovil mientras haya tormenta. Al terminar y tocar suelo reproduce Stand Up completo antes de devolver controles.
- Clip: Assets/07Edu_Animations/Astronaut/FixedAnimations/RIGGED_ASTRONAUT.v2.0@Stand Up.fbx. Humanoid, Copy From Other Avatar: Assets/06Edu_Models/RIGGED_ASTRONAUT.v2.0.fbx. Take mixamo.com, 0-248 a 30 fps, sin bucle ni desplazamiento de root motion.
- Un PlayableGraph local por jugador derribado mantiene el primer fotograma acostado y reproduce la recuperacion segun TickTimer replicado. Se libera al terminar, desactivar o morir.
- PlayerHurtReaction se evalua despues de la pose acostada: mantiene sangre y reaccion de dolor reduciendo movimiento del cuerpo para no levantarlo. La vida y el dano no se bloquean.
- Se asigno el clip en A_FusionAstronaut, A_SurvivalAstronaut y A_MarsSandstormAstronaut. Otros prefabs que incorporen PlayerSandstorm deberan asignarlo tambien.

Validacion pendiente en Unity: importacion Humanoid del FBX y referencia del clip; verificar caida sobre pendiente, dolor tendido, muerte durante tormenta, recuperacion completa y visualizacion desde un segundo cliente. Compilacion C# offline correcta; no se utilizo MCP.
