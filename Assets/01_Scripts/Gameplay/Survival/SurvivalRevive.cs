using System.Collections.Generic;
using Fusion;
using GameJam.Combat;
using Fusion.Addons.SimpleKCC;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using CharacterInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Survival
{
    [DisallowMultipleComponent]
    public sealed class SurvivalRevive : NetworkBehaviour, IStateAuthorityChanged
    {
        [SerializeField, Min(0.1f)] private float reviveDuration = 10f;
        [SerializeField, Min(0.1f)] private float reviveRange = 2.5f;
        [SerializeField, Min(1)] private int reviveHealth = 30;
        [SerializeField, Min(0.05f)] private float confirmationInterval = 0.2f;
        [SerializeField, Min(0.2f)] private float confirmationTimeout = 1f;

        [Networked] public NetworkObject Rescuer { get; private set; }
        [Networked] public uint ActiveAttempt { get; private set; }
        [Networked] public TickTimer ReviveDeadline { get; private set; }
        [Networked] private TickTimer confirmationDeadline { get; set; }
        [Networked] private PlayerRef rescuerAuthority { get; set; }
        [Networked] private uint reservedDeath { get; set; }
        [Networked] private uint attemptSequence { get; set; }
        [Networked] public uint AuthorityGeneration { get; private set; }

        private PlayerHealth health;
        private MarsFusionAvatar avatar;
        private SimpleKCC kcc;
        private CharacterInput input;
        private SurvivalRevive selected;
        private SurvivalRevive pendingTarget;
        private PlayerRef selectedAuthority;
        private uint selectedGeneration;
        private uint selectedDeath;
        private TickTimer nextConfirmation;
        private TickTimer acceptanceDeadline;
        private bool sawAcceptance;
        private bool held;
        private bool pressed;
        private bool released;
        private bool completionConfirmed;
        private readonly Dictionary<NetworkId, uint> lastAttempts = new Dictionary<NetworkId, uint>();

        /// <summary>
        /// Obtiene las dependencias del mismo astronauta.
        /// </summary>
        public override void Spawned()
        {
            health = GetComponent<PlayerHealth>();
            avatar = GetComponent<MarsFusionAvatar>();
            kcc = GetComponent<SimpleKCC>();
            input = GetComponent<CharacterInput>();
            if (health == null || avatar == null || kcc == null || input == null)
            {
                Debug.LogError("[SurvivalRevive] Faltan dependencias del astronauta.", this);
            }
        }

        /// <summary>
        /// Captura F sin compartir el buffer que limpia el movimiento.
        /// </summary>
        private void Update()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority)
            {
                return;
            }
            bool nowHeld = Application.isFocused && Keyboard.current != null && Keyboard.current.fKey.isPressed;
            if (nowHeld && !held)
            {
                if (CanInteractLocally())
                {
                    pendingTarget = FindTarget();
                    if (pendingTarget != null)
                    {
                        pressed = true;
                    }
                }
            }
            if (!nowHeld && held)
            {
                released = true;
            }
            held = nowHeld;
        }

        /// <summary>
        /// Cancela reservas invalidas y procesa una sola accion por pulsacion.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }
            if (ActiveAttempt != 0 && (!ReservationValid() || confirmationDeadline.ExpiredOrNotRunning(Runner)))
            {
                ClearReservation();
            }
            if (released || !held || !CanInteractLocally())
            {
                CancelOutgoing();
                pressed = false;
                released = false;
                return;
            }
            if (selected != null)
            {
                ContinueOutgoing();
            }
            else if (pressed)
            {
                selected = pendingTarget;
                pendingTarget = null;
                if (TargetValid(selected))
                {
                    attemptSequence++;
                    selectedAuthority = selected.Object.StateAuthority;
                    selectedGeneration = selected.AuthorityGeneration;
                    selectedDeath = selected.health.DeathSequence;
                    acceptanceDeadline = TickTimer.CreateFromSeconds(Runner, confirmationTimeout * 2f);
                    nextConfirmation = TickTimer.CreateFromSeconds(Runner, confirmationInterval);
                    sawAcceptance = false;
                    selected.RPC_Begin(Object, attemptSequence, selectedDeath, selectedAuthority, selectedGeneration);
                }
                else
                {
                    selected = null;
                }
            }
            pressed = false;
            released = false;
        }

        /// <summary>
        /// Comprueba controles y vida del propietario antes de leer una accion.
        /// </summary>
        private bool CanInteractLocally()
        {
            if (health == null || !health.IsAlive || input == null || !input.ControlsEnabled || avatar == null || avatar.TabletOpen || GameJam.Crafting.CraftingBox.IsBlockingInput)
            {
                return false;
            }
            PlayerInteractionAction action = GetComponent<PlayerInteractionAction>();
            return action == null || !action.IsBusy;
        }

        /// <summary>
        /// Elige el caido mas cercano y desempata mediante su identificador de red.
        /// </summary>
        private SurvivalRevive FindTarget()
        {
            SurvivalRevive nearest = null;
            float best = float.PositiveInfinity;
            foreach (SurvivalRevive candidate in FindObjectsByType<SurvivalRevive>(FindObjectsSortMode.None))
            {
                if (!TargetValid(candidate))
                {
                    continue;
                }
                float distance = (kcc.Position - candidate.kcc.Position).sqrMagnitude;
                if (distance < best || (distance == best && nearest != null && candidate.Object.Id.Raw < nearest.Object.Id.Raw))
                {
                    nearest = candidate;
                    best = distance;
                }
            }
            return nearest;
        }

        /// <summary>
        /// Comprueba el objetivo sin cambiarlo mientras se mantiene F.
        /// </summary>
        private bool TargetValid(SurvivalRevive target)
        {
            if (target == null || target == this || target.Object == null || !target.Object.IsValid || target.Runner != Runner)
            {
                return false;
            }
            if (target.health == null || target.health.IsAlive || target.health.CurrentHealth > 0f || target.kcc == null || kcc == null)
            {
                return false;
            }
            // El rango del objetivo es el que validara su autoridad.
            return (kcc.Position - target.kcc.Position).sqrMagnitude <= target.reviveRange * target.reviveRange;
        }

        /// <summary>
        /// Confirma la pulsacion a intervalos y conserva el objetivo original.
        /// </summary>
        private void ContinueOutgoing()
        {
            if (!TargetValid(selected) || selected.Object.StateAuthority != selectedAuthority || selected.AuthorityGeneration != selectedGeneration || selected.health.DeathSequence != selectedDeath)
            {
                CancelOutgoing();
                return;
            }
            if (selected.Rescuer == Object && selected.ActiveAttempt == attemptSequence)
            {
                sawAcceptance = true;
            }
            else if (sawAcceptance || selected.Rescuer != null || acceptanceDeadline.ExpiredOrNotRunning(Runner))
            {
                CancelOutgoing();
                return;
            }
            if (nextConfirmation.ExpiredOrNotRunning(Runner))
            {
                selected.RPC_Confirm(Object, attemptSequence, selectedDeath, selectedAuthority, selectedGeneration);
                nextConfirmation = TickTimer.CreateFromSeconds(Runner, confirmationInterval);
            }
        }

        /// <summary>
        /// Cancela solo el intento propio y exige otra pulsacion para reiniciar.
        /// </summary>
        private void CancelOutgoing()
        {
            if (selected != null && selected.Object != null && selected.Object.IsValid && selected.Runner == Runner)
            {
                selected.RPC_Cancel(Object, attemptSequence);
            }
            selected = null;
            pendingTarget = null;
            sawAcceptance = false;
        }

        /// <summary>
        /// Verifica la identidad real del emisor y su astronauta registrado.
        /// </summary>
        private bool SenderValid(NetworkObject actor, RpcInfo info)
        {
            return HasStateAuthority && actor != null && actor.IsValid && actor != Object && actor.Runner == Runner
                && actor.StateAuthority == info.Source && Runner.GetPlayerObject(info.Source) == actor;
        }

        /// <summary>
        /// Reserva el objetivo para la primera solicitud valida de cada intento.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Begin(NetworkObject actor, uint attempt, uint death, PlayerRef owner, uint generation, RpcInfo info = default)
        {
            if (!SenderValid(actor, info) || owner != Object.StateAuthority || generation != AuthorityGeneration)
            {
                return;
            }
            uint previous;
            if (lastAttempts.TryGetValue(actor.Id, out previous) && attempt <= previous)
            {
                return;
            }
            lastAttempts[actor.Id] = attempt;
            if (Rescuer != null || !ParticipantsValid(actor) || death != health.DeathSequence)
            {
                return;
            }
            Rescuer = actor;
            rescuerAuthority = info.Source;
            ActiveAttempt = attempt;
            reservedDeath = death;
            ReviveDeadline = TickTimer.CreateFromSeconds(Runner, reviveDuration);
            confirmationDeadline = TickTimer.CreateFromSeconds(Runner, confirmationTimeout);
        }

        /// <summary>
        /// Renueva la reserva y completa solo con una confirmacion vigente.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Confirm(NetworkObject actor, uint attempt, uint death, PlayerRef owner, uint generation, RpcInfo info = default)
        {
            if (!SenderValid(actor, info) || owner != Object.StateAuthority || generation != AuthorityGeneration
                || actor != Rescuer || attempt != ActiveAttempt || death != reservedDeath)
            {
                return;
            }
            if (!ReservationValid() || confirmationDeadline.ExpiredOrNotRunning(Runner))
            {
                ClearReservation();
                return;
            }
            confirmationDeadline = TickTimer.CreateFromSeconds(Runner, confirmationTimeout);
            if (ReviveDeadline.Expired(Runner))
            {
                completionConfirmed = true;
                health.TryRevive(this, reviveHealth);
                completionConfirmed = false;
                ClearReservation();
            }
        }

        /// <summary>
        /// Ignora cancelaciones antiguas y libera solo la reserva correspondiente.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_Cancel(NetworkObject actor, uint attempt, RpcInfo info = default)
        {
            if (!SenderValid(actor, info))
            {
                return;
            }
            uint previous;
            if (!lastAttempts.TryGetValue(actor.Id, out previous) || attempt > previous)
            {
                lastAttempts[actor.Id] = attempt;
            }
            if (actor == Rescuer && attempt == ActiveAttempt)
            {
                ClearReservation();
            }
        }

        /// <summary>
        /// Valida salud y distancia usando el estado visible para la autoridad.
        /// </summary>
        private bool ParticipantsValid(NetworkObject actor)
        {
            if (actor == null || !actor.IsValid || actor == Object || actor.Runner != Runner || health == null || health.IsAlive || health.CurrentHealth > 0f || kcc == null)
            {
                return false;
            }
            PlayerHealth actorHealth = actor.GetComponent<PlayerHealth>();
            SimpleKCC actorKcc = actor.GetComponent<SimpleKCC>();
            if (actorHealth == null || !actorHealth.IsAlive || actorKcc == null)
            {
                return false;
            }
            return (actorKcc.Position - kcc.Position).sqrMagnitude <= reviveRange * reviveRange;
        }

        /// <summary>
        /// Revalida la reserva ante muerte, desconexion o cambio de autoridad.
        /// </summary>
        private bool ReservationValid()
        {
            return ParticipantsValid(Rescuer) && Rescuer.StateAuthority == rescuerAuthority
                && Runner.GetPlayerObject(rescuerAuthority) == Rescuer && health.DeathSequence == reservedDeath;
        }

        /// <summary>
        /// Autoriza una sola restauracion de salud durante la confirmacion final.
        /// </summary>
        internal bool CanComplete()
        {
            return HasStateAuthority && completionConfirmed && ReservationValid() && ReviveDeadline.Expired(Runner);
        }

        /// <summary>
        /// Borra todo el progreso del objetivo sin modificar su vida.
        /// </summary>
        private void ClearReservation()
        {
            Rescuer = null;
            ActiveAttempt = 0;
            ReviveDeadline = TickTimer.None;
            confirmationDeadline = TickTimer.None;
            rescuerAuthority = PlayerRef.None;
            reservedDeath = 0;
        }

        /// <summary>
        /// Cancela conservadoramente las operaciones al cambiar de propietario.
        /// </summary>
        public void StateAuthorityChanged()
        {
            selected = null;
            pendingTarget = null;
            held = false;
            pressed = false;
            released = false;
            completionConfirmed = false;
            if (HasStateAuthority)
            {
                AuthorityGeneration++;
                ClearReservation();
            }
        }

        /// <summary>
        /// Mantiene los tiempos y la salud configurada dentro de rangos validos.
        /// </summary>
        private void OnValidate()
        {
            reviveDuration = Mathf.Max(0.1f, reviveDuration);
            reviveRange = Mathf.Max(0.1f, reviveRange);
            reviveHealth = Mathf.Max(1, reviveHealth);
            confirmationInterval = Mathf.Max(0.05f, confirmationInterval);
            confirmationTimeout = Mathf.Max(confirmationInterval * 3f, confirmationTimeout);
        }
    }
}
