# Golem, emotes y reanimacion

## Integracion

Se integro origin/feature/Giant-Golem con los commits 92 y 93, conservando las correcciones de recarga, empuje, tormenta y spawn. GiantGolemNeptune queda en b_NeptuneExploration. Gun acepta su ITakeDamage y el golem envia el dano a StateAuthority; su muerte replica el estado Death y usa Dying. No se amplio el permiso de dano a cualquier NetworkBehaviour desconocido.

## Controles

- 7: saludar (Waving).
- 8: pulgar arriba (Standing Thumbs Up).
- 9: presumir/fuerza (Mutant Flexing Muscles).
- F mantenida junto a un aliado muerto: reanimacion existente, con pose agachada y movimiento de brazos del rescatador.
- Al recuperar vida, el aliado reproduce Stand Up completo antes de recuperar controles. La salud y posibilidad de recibir dano siguen activas.

Los emotes solo empiezan estando quieto y apoyado; movimiento, salto o apuntado/disparo los cancelan. La tablet, el dano, la muerte y el derribo de tormenta tienen prioridad. Durante asistencia, emotes o recuperacion se impide disparar e iniciar recogida/reparacion. Soltar F o perder la reserva cancela la asistencia.

## Implementacion

SurvivalRevive replica Emote, EmoteDeadline y GetUpDeadline, ademas de su reserva existente. PlayerSocialAnimation es un MonoBehaviour visual creado por Spawned del componente existente: no agrega NetworkBehaviours al prefab en ejecucion. Los PlayableGraphs se destruyen y la pistola se restaura al acabar o cancelar. Los clips Humanoid se cargan de Resources/PlayerSocial; usan el avatar de RIGGED_ASTRONAUT.v2.0. La recuperacion reutiliza Resources/Animations/StormStandUp.

Todos los participantes deben importar los cambios y reiniciar la sesion: cambio el esquema de propiedades de red. Los ajustes locales de QualitySettings/UnityConnectSettings no forman parte de estos commits.

## Validacion

Compilacion offline de Assembly-CSharp y comprobaciones de activos/referencias. Pendiente prueba visual en Unity y con dos clientes: matar golem a disparos; cancelar emotes; sostener/soltar F; cancelar por dano o distancia; completar reanimacion y comprobar que ambos clientes ven la asistencia y la recuperacion sin interferir con la tormenta ni la animacion de muerte. No se utilizo MCP.
