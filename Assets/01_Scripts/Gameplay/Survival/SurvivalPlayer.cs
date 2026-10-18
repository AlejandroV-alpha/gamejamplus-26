using Starter.ThirdPersonCharacter;
using UnityEngine;

namespace GameJam.Survival
{
    public sealed class SurvivalPlayer : Player
    {
        private SurvivalAvatar survivalAvatar;

        /// <summary>
        /// Conserva la configuracion original y obtiene el avatar propio.
        /// </summary>
        public override void Spawned()
        {
            base.Spawned();
            survivalAvatar = GetComponent<SurvivalAvatar>();
            if (survivalAvatar == null)
            {
                Debug.LogError("[SurvivalPlayer] Falta SurvivalAvatar.", this);
            }
        }

        /// <summary>
        /// Bloquea acciones pendientes al morir o deshabilitar controles.
        /// </summary>
        protected override GameplayInput PrepareMovementInput(GameplayInput input)
        {
            if (survivalAvatar == null || !survivalAvatar.IsAlive || !PlayerInput.ControlsEnabled)
            {
                input.MoveDirection = Vector2.zero;
                input.Sprint = false;
                input.Jump = false;
                input.AimToggle = false;
            }
            return input;
        }

        /// <summary>
        /// Usa una sola decision para velocidad, animacion y consumo.
        /// </summary>
        protected override bool ResolveSprint(GameplayInput input)
        {
            if (survivalAvatar == null)
            {
                return false;
            }
            bool hasMovement = input.MoveDirection.sqrMagnitude > 0.01f;
            return survivalAvatar.SimulateSprint(input.Sprint, hasMovement);
        }
    }
}
