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

        [Networked] public byte Emote { get; private set; }
        [Networked] public TickTimer EmoteDeadline { get; private set; }
        [Networked] public TickTimer GetUpDeadline { get; private set; }
        [Networked] public TickTimer RescueExitDeadline { get; private set; }
        // Arrastre: el que arrastra publica su objetivo; la autoridad del objetivo acepta al arrastrador.
        [Networked] public NetworkObject DragTarget { get; private set; }
        [Networked] public NetworkObject Dragger { get; private set; }
        [SerializeField, Min(0.5f)] private float dragGrabRange = 2.6f;
        [SerializeField, Min(1f)] private float dragBreakRange = 9f;
        [SerializeField, Min(0.1f)] private float dragSpeedMultiplier = 0.65f;
        private bool dragHeld;
        private float nextHintLookup;
        private SurvivalRevive hintTarget;
        private float nextSocialLookup;
        private SurvivalRevive rescueTarget;
        public bool IsRecovering => Object != null && Object.IsValid && health != null && health.IsAlive
            && GetUpDeadline.IsRunning && !GetUpDeadline.Expired(Runner);
        public bool IsEmoting => Object != null && Object.IsValid && Emote != 0 && EmoteDeadline.IsRunning && !EmoteDeadline.Expired(Runner);
        public bool IsRescuing => RescueTarget != null;
        public bool IsLeavingRescue => Object != null && Object.IsValid && health != null && health.IsAlive
            && RescueExitDeadline.IsRunning && !RescueExitDeadline.Expired(Runner);
        public bool IsAnimating => IsRecovering || IsEmoting || IsRescuing || IsLeavingRescue;
        public float ReviveElapsed => Object != null && Object.IsValid && ReviveDeadline.IsRunning
            ? Mathf.Max(0f, reviveDuration - ReviveDeadline.RemainingTime(Runner).GetValueOrDefault()) : 0f;
        public float ReviveProgress => Mathf.Clamp01(ReviveElapsed / reviveDuration);
        public SurvivalRevive RescueTarget
        {
            get
            {
                if (Object == null || !Object.IsValid || avatar == null || !avatar.IsAlive) return null;
                if (Time.unscaledTime >= nextSocialLookup)
                {
                    nextSocialLookup = Time.unscaledTime + .15f;
                    rescueTarget = null;
                    foreach (var candidate in FindObjectsByType<SurvivalRevive>(FindObjectsSortMode.None))
                        if (candidate.Object != null && candidate.Object.IsValid && candidate.Runner == Runner
                            && candidate.Rescuer == Object && candidate.ActiveAttempt != 0)
                        { rescueTarget = candidate; break; }
                }
                return rescueTarget != null && rescueTarget.Object != null && rescueTarget.Object.IsValid
                    && rescueTarget.Rescuer == Object && rescueTarget.ActiveAttempt != 0 ? rescueTarget : null;
            }
        }

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
            if (GetComponent<PlayerSocialAnimation>() == null) gameObject.AddComponent<PlayerSocialAnimation>();
            if (GetComponent<SurvivalReviveFeedback>() == null) gameObject.AddComponent<SurvivalReviveFeedback>();
            if (GetComponent<PlayerDragArm>() == null) gameObject.AddComponent<PlayerDragArm>();
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
            ReadEmotes();
            ReadDrag();
            bool nowHeld = Application.isFocused && Keyboard.current != null && Keyboard.current.eKey.isPressed;
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
            ValidateDrag();
            if (Emote != 0 && (EmoteDeadline.ExpiredOrNotRunning(Runner) || avatar == null || !avatar.IsAlive
                || avatar.TabletOpen || (GetComponent<PlayerHurtReaction>()?.IsReacting ?? false)
                || (GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false))) Emote = 0;
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
                if (IsRescuing && kcc != null)
                {
                    Vector3 facing = selected.transform.position - transform.position;
                    facing.y = 0;
                    if (facing.sqrMagnitude > .01f) kcc.SetLookRotation(Quaternion.LookRotation(facing).eulerAngles);
                }
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

        private void ReadEmotes()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !Application.isFocused) return;
            if (IsEmoting)
            {
                bool cancel = keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed
                    || keyboard.dKey.isPressed || keyboard.spaceKey.isPressed || keyboard.eKey.isPressed
                    || (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed));
                if (cancel) Emote = 0;
                return;
            }
            if (!CanInteractLocally() || IsRescuing || kcc == null || !kcc.IsGrounded || kcc.RealSpeed > .15f) return;
            byte choice = keyboard.digit7Key.wasPressedThisFrame ? (byte)1
                : keyboard.digit8Key.wasPressedThisFrame ? (byte)2 : keyboard.digit9Key.wasPressedThisFrame ? (byte)3 : (byte)0;
            if (choice == 0) return;
            TriggerEmote(choice);
        }

        public void TriggerEmote(byte choice)
        {
            if (choice < 1 || choice > 3 || !HasStateAuthority) return;
            if (avatar != null && (avatar.IsRagdoll || (avatar.Ragdoll != null && (avatar.Ragdoll.IsRagdolled || avatar.Ragdoll.IsRecovering)))) return;
            var clip = PlayerSocialAnimation.EmoteClip(choice);
            if (clip == null) return;
            Emote = choice;
            EmoteDeadline = TickTimer.CreateFromSeconds(Runner, clip.length);
        }

        internal void BeginGetUp()
        {
            if (!HasStateAuthority) return;
            var clip = PlayerSocialAnimation.ReviveGetUpClip;
            GetUpDeadline = TickTimer.CreateFromSeconds(Runner, clip != null ? clip.length : 8.267f);
        }

        // ───────────── Arrastre de companeros ─────────────

        public bool IsDragging => Object != null && Object.IsValid && DragTarget != null && DragTarget.IsValid;
        public bool IsBeingDragged => Object != null && Object.IsValid && Dragger != null && Dragger.IsValid;
        public bool CanBeRevived => health != null && !health.IsAlive && health.CurrentHealth <= 0f;

        /// <summary>Companero caido o en ragdoll mas cercano (para el letrero de interaccion).</summary>
        public SurvivalRevive NearbyTeammate
        {
            get
            {
                if (Time.unscaledTime < nextHintLookup) return hintTarget;
                nextHintLookup = Time.unscaledTime + .15f;
                hintTarget = null;
                if (Object == null || !Object.IsValid || health == null || !health.IsAlive || kcc == null) return null;
                float best = float.PositiveInfinity;
                foreach (var candidate in FindObjectsByType<SurvivalRevive>(FindObjectsSortMode.None))
                {
                    if (!Draggable(candidate) && !TargetValid(candidate)) continue;
                    float d = (BodyPosition - candidate.BodyPosition).sqrMagnitude;
                    float range = Mathf.Max(dragGrabRange, candidate.reviveRange);
                    if (d <= range * range && d < best) { best = d; hintTarget = candidate; }
                }
                return hintTarget;
            }
        }

        /// <summary>Hay alguien a quien reanimar al alcance (E queda reservada para reanimar).</summary>
        public bool HasReviveTargetNearby => CanInteractLocally() && FindTarget() != null;

        public bool HandsEmpty
        {
            get
            {
                var inventory = GetComponent<GameJam.Inventory.Inventory>();
                return inventory == null || inventory.SelectedItem == null;
            }
        }

        /// <summary>Posicion real del cuerpo: la cadera si esta en ragdoll.</summary>
        public Vector3 BodyPosition
        {
            get
            {
                if (avatar != null && avatar.Ragdoll != null && avatar.Ragdoll.IsRagdolled && avatar.Ragdoll.HipsRigidbody != null)
                    return avatar.Ragdoll.HipsRigidbody.position;
                return kcc != null && kcc.enabled ? kcc.Position : transform.position;
            }
        }

        private bool IsDownedOrRagdolled => (health != null && !health.IsAlive)
            || (avatar != null && (avatar.IsRagdoll || (avatar.Ragdoll != null && avatar.Ragdoll.IsRagdolled)));

        private bool Draggable(SurvivalRevive target)
        {
            return target != null && target != this && target.Object != null && target.Object.IsValid
                && target.Runner == Runner && target.IsDownedOrRagdolled && target.Rescuer == null
                && (target.Dragger == null || target.Dragger == Object);
        }

        private bool CanDragLocally()
        {
            return CanInteractLocally() && !IsRescuing && HandsEmpty && avatar != null && !avatar.IsRagdoll
                && (avatar.Ragdoll == null || (!avatar.Ragdoll.IsRagdolled && !avatar.Ragdoll.IsRecovering));
        }

        private void ReadDrag()
        {
            bool nowHeld = Application.isFocused && Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (nowHeld && !dragHeld && CanDragLocally())
            {
                SurvivalRevive best = null;
                float bestDistance = dragGrabRange * dragGrabRange;
                foreach (var candidate in FindObjectsByType<SurvivalRevive>(FindObjectsSortMode.None))
                {
                    if (!Draggable(candidate)) continue;
                    float d = (BodyPosition - candidate.BodyPosition).sqrMagnitude;
                    if (d <= bestDistance) { bestDistance = d; best = candidate; }
                }
                if (best != null)
                {
                    DragTarget = best.Object;
                    best.RPC_BeginDrag(Object);
                }
            }
            if (!nowHeld && dragHeld) StopDragging();
            dragHeld = nowHeld;
        }

        private void StopDragging()
        {
            if (DragTarget != null && DragTarget.IsValid)
            {
                var target = DragTarget.GetComponent<SurvivalRevive>();
                if (target != null) target.RPC_EndDrag(Object);
            }
            DragTarget = null;
        }

        /// <summary>Corta arrastres invalidos en ambos extremos.</summary>
        private void ValidateDrag()
        {
            if (DragTarget != null)
            {
                var target = DragTarget.IsValid ? DragTarget.GetComponent<SurvivalRevive>() : null;
                if (target == null || !target.IsDownedOrRagdolled || target.Rescuer != null || !CanDragLocally()
                    || (BodyPosition - target.BodyPosition).sqrMagnitude > dragBreakRange * dragBreakRange)
                    StopDragging();
            }
            if (Dragger != null)
            {
                var dragger = Dragger.IsValid ? Dragger.GetComponent<SurvivalRevive>() : null;
                if (dragger == null || dragger.DragTarget != Object || !IsDownedOrRagdolled || Rescuer != null
                    || dragger.health == null || !dragger.health.IsAlive
                    || (dragger.BodyPosition - BodyPosition).sqrMagnitude > dragBreakRange * dragBreakRange)
                    Dragger = null;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_BeginDrag(NetworkObject actor, RpcInfo info = default)
        {
            if (!SenderValid(actor, info) || !IsDownedOrRagdolled || Rescuer != null) return;
            if (Dragger != null && Dragger != actor && Dragger.IsValid) return;
            Dragger = actor;
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_EndDrag(NetworkObject actor, RpcInfo info = default)
        {
            if (!SenderValid(actor, info)) return;
            if (Dragger == actor) Dragger = null;
        }

        private Vector3 DragAnchor(SurvivalRevive dragger)
        {
            Vector3 back = dragger.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < .01f) back = Vector3.forward;
            return dragger.BodyPosition - back.normalized * 1.25f;
        }

        /// <summary>
        /// Llamado por el movimiento del astronauta: frena al que arrastra y mueve al caido.
        /// </summary>
        public Vector3 ApplyDrag(Vector3 velocity)
        {
            if (IsDragging) velocity *= dragSpeedMultiplier;
            if (IsBeingDragged && kcc != null && kcc.enabled)
            {
                var dragger = Dragger.GetComponent<SurvivalRevive>();
                if (dragger != null)
                {
                    Vector3 delta = DragAnchor(dragger) - kcc.Position;
                    delta.y = 0f;
                    velocity = delta.magnitude > .2f ? Vector3.ClampMagnitude(delta * 6f, 7f) : Vector3.zero;
                }
            }
            return velocity;
        }

        /// <summary>
        /// El ragdoll se simula en cada cliente, asi que todos tiran de la cadera hacia el arrastrador.
        /// </summary>
        private void FixedUpdate()
        {
            if (Object == null || !Object.IsValid || !IsBeingDragged || avatar == null || avatar.Ragdoll == null
                || !avatar.Ragdoll.IsRagdolled || avatar.Ragdoll.HipsRigidbody == null) return;
            var dragger = Dragger.GetComponent<SurvivalRevive>();
            if (dragger == null) return;
            Rigidbody hips = avatar.Ragdoll.HipsRigidbody;
            if (ragdollBodies == null) ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
            // Antes solo se empujaba la cadera con una fuerza suave: el resto del cuerpo frenaba,
            // el companero se quedaba atras y el arrastre se cortaba. Ahora se guia la velocidad
            // horizontal de todo el cuerpo hacia el punto detras del que arrastra.
            Vector3 offset = DragAnchor(dragger) - hips.position;
            offset.y = 0f;
            Vector3 desired = offset.magnitude > .2f ? Vector3.ClampMagnitude(offset * 5f, 8f) : Vector3.zero;
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null || body.isKinematic) continue;
                float grip = body == hips ? .6f : .35f;
                Vector3 current = body.linearVelocity;
                Vector3 change = new Vector3(desired.x - current.x, 0f, desired.z - current.z) * grip;
                // Levanta un poco la cadera para que no se atasque con el suelo.
                if (body == hips && hips.position.y < dragger.BodyPosition.y + .25f) change.y = Mathf.Max(0f, 1.2f - current.y) * .3f;
                body.AddForce(change, ForceMode.VelocityChange);
                body.WakeUp();
            }
        }

        private Rigidbody[] ragdollBodies;

        /// <summary>
        /// Comprueba controles y vida del propietario antes de leer una accion.
        /// </summary>
        private bool CanInteractLocally()
        {
            if (IsRecovering || IsLeavingRescue || IsEmoting || health == null || !health.IsAlive || input == null || !input.ControlsEnabled || avatar == null || avatar.TabletOpen || GameJam.Crafting.CraftingBox.IsBlockingInput)
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
                float distance = (BodyPosition - candidate.BodyPosition).sqrMagnitude;
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
            return (BodyPosition - target.BodyPosition).sqrMagnitude <= target.reviveRange * target.reviveRange;
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
            // The rescuer owns this timer; completion and cancellation use the same exit on every peer.
            bool accepted = sawAcceptance || (selected != null && selected.Object != null && selected.Object.IsValid
                && selected.Rescuer == Object && selected.ActiveAttempt == attemptSequence);
            if (selected != null && accepted && health != null && health.IsAlive && !IsRecovering)
            {
                var clip = PlayerSocialAnimation.RescueStandClip;
                RescueExitDeadline = TickTimer.CreateFromSeconds(Runner, clip != null ? clip.length : 3.334f);
            }
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
        /// <summary>
        /// En modo Shared el PlayerObject de otro cliente puede no estar registrado en este peer
        /// (SetPlayerObject solo se llama localmente), lo que rechazaba toda reanimacion remota.
        /// Se acepta si es un astronauta propiedad de ese jugador y no contradice un registro existente.
        /// </summary>
        private bool IsRegisteredAvatar(NetworkObject actor, PlayerRef owner)
        {
            if (actor == null || actor.GetComponent<SurvivalRevive>() == null) return false;
            NetworkObject registered = Runner.GetPlayerObject(owner);
            return registered == null || registered == actor;
        }

        private bool SenderValid(NetworkObject actor, RpcInfo info)
        {
            return HasStateAuthority && actor != null && actor.IsValid && actor != Object && actor.Runner == Runner
                && actor.StateAuthority == info.Source && IsRegisteredAvatar(actor, info.Source);
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
            return (actor.GetComponent<SurvivalRevive>().BodyPosition - BodyPosition).sqrMagnitude <= reviveRange * reviveRange;
        }

        /// <summary>
        /// Revalida la reserva ante muerte, desconexion o cambio de autoridad.
        /// </summary>
        private bool ReservationValid()
        {
            return ParticipantsValid(Rescuer) && Rescuer.StateAuthority == rescuerAuthority
                && IsRegisteredAvatar(Rescuer, rescuerAuthority) && health.DeathSequence == reservedDeath;
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
            dragHeld = false;
            held = false;
            pressed = false;
            released = false;
            completionConfirmed = false;
            if (HasStateAuthority)
            {
                AuthorityGeneration++;
                RescueExitDeadline = TickTimer.None;
                DragTarget = null;
                Dragger = null;
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
