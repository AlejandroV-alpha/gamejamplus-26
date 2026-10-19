using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Survival
{
    public sealed class SurvivalAvatar : MarsFusionAvatar
    {
        [SerializeField, Min(1f)] private float maximumStamina = 100f;
        [SerializeField, Min(0f)] private float sprintCostPerSecond = 12f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 10f;
        [SerializeField, Min(0.01f)] private float recoveryThreshold = 25f;

        [Networked] public NetworkBool IsExhausted { get; private set; }
        public float MaximumStamina => maximumStamina;

        /// <summary>
        /// Inicializa la stamina propia sin duplicar el estado del avatar.
        /// </summary>
        public override void Spawned()
        {
            base.Spawned();
            if (HasStateAuthority)
            {
                Stamina = maximumStamina;
                IsExhausted = false;
            }
        }

        /// <summary>
        /// Delega la simulacion de stamina al controlador de movimiento.
        /// </summary>
        protected override void UpdateStamina()
        {
        }

        /// <summary>
        /// Resuelve el sprint y actualiza la stamina una vez por tick.
        /// </summary>
        public bool SimulateSprint(bool wantsSprint, bool hasMovement)
        {
            if (!HasStateAuthority || !IsAlive)
            {
                return false;
            }

            if (Stamina <= 0f)
            {
                IsExhausted = true;
            }
            if (IsExhausted && Stamina >= recoveryThreshold)
            {
                IsExhausted = false;
            }

            bool sprinting = wantsSprint && hasMovement && !IsExhausted && Stamina > 0f;
            float change = recoveryPerSecond * Runner.DeltaTime;
            if (sprinting)
            {
                change = -sprintCostPerSecond * Runner.DeltaTime;
            }
            Stamina = Mathf.Clamp(Stamina + change, 0f, maximumStamina);
            if (Stamina <= 0f)
            {
                IsExhausted = true;
            }

            // El ultimo tick autorizado consume el remanente; el siguiente camina.
            return sprinting;
        }

        /// <summary>
        /// Mantiene los ajustes del Inspector dentro de rangos validos.
        /// </summary>
        private void OnValidate()
        {
            maximumStamina = Mathf.Max(1f, maximumStamina);
            sprintCostPerSecond = Mathf.Max(0f, sprintCostPerSecond);
            recoveryPerSecond = Mathf.Max(0f, recoveryPerSecond);
            recoveryThreshold = Mathf.Clamp(recoveryThreshold, 0.01f, maximumStamina);
        }
    }
}
