using GameJam.PhysicsControl;
using Fusion;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GameJam.Combat;
using GameJam.Tablet;
using Starter.ThirdPersonCharacter;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Multiplayer
{
    public class MarsFusionAvatar : NetworkBehaviour, ITakeDamage
    {
        public TabletItemDefinition tablet;
        public Transform handSocket;
        public SpriteRenderer eyes, mouth;
        public Sprite neutralEyes, blinkEyes, hurtEyes, deadEyes;
        public Sprite smileMouth, smallMouth, openMouth, hurtMouth, deadMouth;
        [SerializeField, Tooltip("Optional component implementing IPlayerHealth. Owns health and its replication.")]
        private MonoBehaviour healthSource;
        public IPlayerHealth HealthSource
        {
            get
            {
                if (healthSource == null) return null;
                if (healthSource is NetworkBehaviour networkHealth &&
                    (networkHealth.Object == null || !networkHealth.Object.IsValid || !networkHealth.StateBufferIsValid)) return null;
                return healthSource as IPlayerHealth;
            }
        }
        public bool HasHealthSource => HealthSource != null;
        // Until the health component is supplied, keep exploration and interaction enabled.
        public bool IsAlive => healthSource == null || (HealthSource?.IsAlive ?? false);
        public bool IsNetworkReady => Object != null && Object.IsValid && StateBufferIsValid;
        [Networked] private TickTimer friendlyPushTimer { get; set; }
        [Networked] private TickTimer friendlyPushProtection { get; set; }
        [Networked] private Vector3 friendlyPushVelocity { get; set; }
        private const float FriendlyPushDuration = .85f;
        private const float FriendlyPushImmunity = 1.1f;
        private const float FriendlyPushRange = 25f;
        [Networked] public float Stamina { get; protected set; }
        [Networked] public NetworkBool TabletOpen { get; private set; }
        [Networked] public byte VoiceLevel { get; private set; }
        [Networked] private uint HurtSequence { get; set; }
        [Networked] public byte SuitColorIndex { get; private set; }
        [Networked] public NetworkString<_32> PlayerName { get; private set; }
        [Networked] public NetworkBool LobbyReady { get; private set; }
        [Networked] public NetworkBool IsRagdoll { get; set; }
        [Networked] private TickTimer ragdollTimer { get; set; }
        [Networked] private NetworkBool IdentityInitialized { get; set; }
        public static MarsFusionAvatar Local { get; private set; }
        // Session-scoped identity survives scene/avatar recreation, but not a new runner.
        // Runner keys also isolate local multiplayer peers in the same process.
        private static ConditionalWeakTable<NetworkRunner, SessionColorAssignments> sessionColors =
            new ConditionalWeakTable<NetworkRunner, SessionColorAssignments>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSessionColors()
        {
            sessionColors = new ConditionalWeakTable<NetworkRunner, SessionColorAssignments>();
        }

        public bool HasAssignedColor => IsNetworkReady && IdentityInitialized && SuitColorIndex < MarsPlayerIdentityView.Palette.Length;
        private float nextColorRequest;
        private bool warnedColorCapacity;
        private MarsPlayerIdentityView identityView;
        private PlayerInput input;
        private MarsFusionSession session;
        private TabletGuideUI ui;
        private GameObject heldTablet;
        private float? previousHealth;
        private float hurtUntil, blinkUntil, nextBlink, voiceUntil, nextVoiceSend;
        private float fakeDeadUntil, nextTrollActionAt;
        private float pendingHopImpulse;
        private PlayerRagdoll ragdoll;
        public PlayerRagdoll Ragdoll => ragdoll;
        private float punchStartTime = -10f;
        public bool IsPunching => Time.time - punchStartTime < 0.28f;
        private Transform punchSpine, punchRightUpperArm, punchRightForeArm;
        private readonly Collider[] nearbyScanBuffer = new Collider[24];
        private AudioSource trollAudioSource;
        private static AudioClip boopClip, jetpackClip, sirenClip, fakeDeadClip, baitedClip;
        private bool warnedMissingHealth;
        private uint previousHurt;
        private byte requestedVoice;
        private bool shownOpen;
        public string Expression { get; private set; }
        public TabletGuideUI LocalUI => ui;
        public GameJam.UI.PlayerHUD LocalHud { get; private set; }
        public bool LowGravityMode { get; set; }
        public bool TurboOverdriveMode { get; set; }
        public bool IsFakeDead => Time.unscaledTime < fakeDeadUntil;

        public override void Spawned()
        {
            input = GetComponent<PlayerInput>();
            session = FindFirstObjectByType<MarsFusionSession>();
            ragdoll = GetComponent<PlayerRagdoll>() ?? gameObject.AddComponent<PlayerRagdoll>();
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                punchSpine = anim.GetBoneTransform(HumanBodyBones.Spine);
                punchRightUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
                punchRightForeArm = anim.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
            if (healthSource == null)
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component is IPlayerHealth) { healthSource = component; break; }
            if (HasStateAuthority)
            {
                Stamina = 100;
                friendlyPushTimer = TickTimer.None;
                friendlyPushProtection = TickTimer.CreateFromSeconds(Runner, 5f);
            }
            if (HasStateAuthority && !IdentityInitialized)
            {
                SuitColorIndex = byte.MaxValue; // Neutral until the room coordinator confirms a slot.
                var owner = ColorOwner;
                string customName = PlayerPrefs.GetString("GameJam.PlayerName", "").Trim();
                PlayerName = !string.IsNullOrEmpty(customName) ? customName : ("Astronauta " + owner.PlayerId);
                byte saved;
                if (sessionColors.GetOrCreateValue(Runner).TryGet(owner.PlayerId, out saved)
                    && saved < MarsPlayerIdentityView.Palette.Length)
                    ApplyConfirmedColor(saved);
                else if (Runner.GameMode != GameMode.Shared)
                    AssignColorLocally(owner);
            }
            LearnSessionColors();
            identityView = gameObject.AddComponent<MarsPlayerIdentityView>();
            identityView.Initialize(this);
            previousHealth = HealthSource?.CurrentHealth;
            previousHurt = HurtSequence;
            nextBlink = Time.time + Random.Range(2f, 5f);
            if (tablet != null && tablet.heldPrefab != null && handSocket != null)
            {
                heldTablet = Instantiate(tablet.heldPrefab, handSocket, false);
                var scale = handSocket.lossyScale;
                heldTablet.transform.localScale = new Vector3(1 / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(.001f, Mathf.Abs(scale.z)));
                heldTablet.transform.position = handSocket.position + handSocket.TransformDirection(new Vector3(.04f, .02f, .01f));
                heldTablet.transform.localRotation = Quaternion.Euler(0, 0, 90);
                heldTablet.SetActive(TabletOpen);
            }
            if (HasStateAuthority && tablet != null)
            {
                ui = new GameObject("Local Fusion Tablet").AddComponent<TabletGuideUI>();
                ui.Build(CloseTablet, tablet.catalog, tablet.skin);
                ui.BindMission(this);
                ui.TargetOpen = TabletOpen;
            }
            if (HasStateAuthority)
            {
                Local = this;
                LocalHud = new GameObject("Local Player HUD").AddComponent<GameJam.UI.PlayerHUD>();
                LocalHud.Build(this);
            }
        }

        public void RequestFriendlyPush(MarsFusionAvatar shooter)
        {
            if (Object == null || !Object.IsValid || shooter == null || shooter == this
                || shooter.Object == null || !shooter.Object.IsValid || !shooter.HasStateAuthority
                || shooter.Runner != Runner) return;
            RPC_RequestFriendlyPush(shooter.Object.Id);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestFriendlyPush(NetworkId shooterId, RpcInfo info = default)
        {
            if (!IsAlive || TabletOpen || (friendlyPushProtection.IsRunning && !friendlyPushProtection.Expired(Runner))
                || (input != null && !input.ControlsEnabled)
                || (GetComponent<PlayerInteractionAction>()?.IsBusy ?? false)
                || (GetComponent<PlayerHurtReaction>()?.IsReacting ?? false)
                || (GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsTumbling ?? false)) return;
            if (!Runner.TryFindObject(shooterId, out var shooterObject) || shooterObject == Object
                || shooterObject.StateAuthority != info.Source) return;
            var shooter = shooterObject.GetComponent<MarsFusionAvatar>();
            var kcc = GetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>();
            if (shooter == null || !shooter.IsAlive || kcc == null) return;
            Vector3 direction = transform.position - shooter.transform.position;
            if (direction.sqrMagnitude > FriendlyPushRange * FriendlyPushRange) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < .01f) direction = shooter.transform.forward;
            friendlyPushVelocity = direction.normalized * 12.5f;
            pendingHopImpulse = Mathf.Max(pendingHopImpulse, 5.8f);
            friendlyPushTimer = TickTimer.CreateFromSeconds(Runner, FriendlyPushDuration);
            friendlyPushProtection = TickTimer.CreateFromSeconds(Runner, FriendlyPushImmunity);
            HurtSequence++;
            RPC_BroadcastTrollEvent(1, transform.position + Vector3.up * 1.4f);
            if (ragdoll != null)
            {
                Vector3 shoveImpulse = direction.normalized * 15f + Vector3.up * 4.2f;
                RequestRagdollKnockdown(shoveImpulse, 2.4f);
            }
            Debug.Log("[Player push] Empujon recibido.", this);
        }

        public void ApplyLocalShowcaseImpulse(Vector3 horizontalVelocity, float verticalHop = 5.5f)
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive) return;
            friendlyPushVelocity = horizontalVelocity;
            pendingHopImpulse = Mathf.Max(pendingHopImpulse, verticalHop);
            friendlyPushTimer = TickTimer.CreateFromSeconds(Runner, FriendlyPushDuration);
        }

        public float ConsumePendingJumpImpulse()
        {
            float val = pendingHopImpulse;
            pendingHopImpulse = 0f;
            return val;
        }

        // Add to the existing single KCC.Move; never teleport, stack impulses or disable controls.
        public Vector3 ApplyFriendlyPush(Vector3 velocity)
        {
            if (!HasStateAuthority || !IsAlive || !friendlyPushTimer.IsRunning || friendlyPushTimer.Expired(Runner))
                return velocity;
            float weight = Mathf.Clamp01(friendlyPushTimer.RemainingTime(Runner).GetValueOrDefault() / FriendlyPushDuration);
            return (GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false) ? Vector3.zero : velocity + friendlyPushVelocity * weight;
        }

        public void RequestMeleeBoop()
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive || Time.unscaledTime < nextTrollActionAt) return;
            nextTrollActionAt = Time.unscaledTime + .45f;
            punchStartTime = Time.time;
            Vector3 center = transform.position + transform.forward * 1.6f + Vector3.up * .9f;
            int hitCount = Physics.OverlapSphereNonAlloc(center, 2.8f, nearbyScanBuffer, ~0, QueryTriggerInteraction.Collide);
            bool hitTarget = false;
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = nearbyScanBuffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;

                MarsFusionAvatar otherAvatar = col.GetComponentInParent<MarsFusionAvatar>();
                if (otherAvatar != null && otherAvatar != this && otherAvatar.Runner == Runner)
                {
                    Vector3 shoveImpulse = transform.forward * 16f + Vector3.up * 4.5f;
                    otherAvatar.RequestRagdollKnockdown(shoveImpulse, 2.8f);
                    HitFeedbackHUD.Hit();
                    hitTarget = true;
                    break;
                }

                var reksai = col.GetComponentInParent<ReksaiFusionEnemy>();
                if (reksai != null && reksai.Runner == Runner)
                {
                    reksai.TakeDamage(2);
                    EnemyDamagePopup.ShowCallout(col.transform.position + Vector3.up * 1.5f, "¡PUM!", "-2 HP", Color.red, 1.2f);
                    HitFeedbackHUD.Hit();
                    hitTarget = true;
                    break;
                }
                var cuernudo = col.GetComponentInParent<ElCuernudoFusionEnemy>();
                if (cuernudo != null && cuernudo.Runner == Runner)
                {
                    cuernudo.TakeDamage(2);
                    EnemyDamagePopup.ShowCallout(col.transform.position + Vector3.up * 1.8f, "¡PUM!", "-2 HP", Color.red, 1.2f);
                    HitFeedbackHUD.Hit();
                    hitTarget = true;
                    break;
                }
                var venom = col.GetComponentInParent<VenomwingFusionEnemy>();
                if (venom != null && venom.Runner == Runner)
                {
                    venom.TakeDamage(2);
                    EnemyDamagePopup.ShowCallout(col.transform.position + Vector3.up * 1.6f, "¡PUM!", "-2 HP", Color.red, 1.2f);
                    HitFeedbackHUD.Hit();
                    hitTarget = true;
                    break;
                }
            }
            RPC_BroadcastTrollEvent((byte)(hitTarget ? 1 : 6), transform.position + transform.forward * 1.4f + Vector3.up * 1.2f);
        }

        public void TriggerJetpackDash()
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive || Time.unscaledTime < nextTrollActionAt) return;
            nextTrollActionAt = Time.unscaledTime + .75f;
            fakeDeadUntil = 0f;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
            ApplyLocalShowcaseImpulse(forward * 14f, LowGravityMode ? 12.5f : 8.8f);
            Stamina = Mathf.Min(100f, Stamina + 35f);

            int hitCount = Physics.OverlapSphereNonAlloc(transform.position + forward * 1.5f, 3.2f, nearbyScanBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                Collider col = nearbyScanBuffer[i];
                if (col == null || col.transform.IsChildOf(transform)) continue;
                MarsFusionAvatar other = col.GetComponentInParent<MarsFusionAvatar>();
                if (other != null && other != this && other.Runner == Runner)
                    other.RequestFriendlyPush(this);
            }
            RPC_BroadcastTrollEvent(2, transform.position + Vector3.up * 1.1f);
        }

        public void RequestRagdollKnockdown(Vector3 impulse, float duration)
        {
            if (Object == null || !Object.IsValid) return;
            RPC_RequestRagdollKnockdown(impulse, duration);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestRagdollKnockdown(Vector3 impulse, float duration)
        {
            if (!IsAlive || ragdoll == null) return;
            IsRagdoll = true;
            ragdollTimer = TickTimer.CreateFromSeconds(Runner, duration);
            ragdoll.GoRagdoll(impulse, duration);
            EnemyDamagePopup.ShowCallout(transform.position + Vector3.up * 1.5f, "¡RAGDOLL!", "KNOCKDOWN", new Color(1f, 0.4f, 0.2f), 1.4f);
            RPC_SyncRagdoll(true, impulse);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SyncRagdoll(bool active, Vector3 impulse)
        {
            if (ragdoll == null) return;
            if (active)
            {
                if (!ragdoll.IsRagdolled) ragdoll.GoRagdoll(impulse, 2.8f);
            }
            else
            {
                if (ragdoll.IsRagdolled) ragdoll.StandUp();
            }
        }

        public void TriggerSkillIssueSiren()
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive || Time.unscaledTime < nextTrollActionAt) return;
            nextTrollActionAt = Time.unscaledTime + .6f;
            HurtSequence++;
            RPC_BroadcastTrollEvent(3, transform.position + Vector3.up * 1.85f);
            int count = Physics.OverlapSphereNonAlloc(transform.position, 48f, nearbyScanBuffer, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var c = nearbyScanBuffer[i];
                if (c == null) continue;
                var cuernudo = c.GetComponentInParent<ElCuernudoFusionEnemy>();
                if (cuernudo != null && cuernudo.Runner == Runner) cuernudo.AlertNoise(this);
                var venom = c.GetComponentInParent<VenomwingFusionEnemy>();
                if (venom != null && venom.Runner == Runner) venom.AlertNoise(this);
                var reksai = c.GetComponentInParent<ReksaiFusionEnemy>();
                if (reksai != null && reksai.Runner == Runner) reksai.AlertNoise(this);
            }
        }

        public void TriggerPanicEmote()
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive || Time.unscaledTime < nextTrollActionAt) return;
            nextTrollActionAt = Time.unscaledTime + .5f;
            hurtUntil = Time.time + 1.2f;
            RPC_BroadcastTrollEvent(7, transform.position + Vector3.up * 1.8f);
        }

        public void ToggleFakeDeath()
        {
            if (!IsNetworkReady || !HasStateAuthority || !IsAlive || Time.unscaledTime < nextTrollActionAt) return;
            nextTrollActionAt = Time.unscaledTime + .4f;
            if (IsFakeDead)
            {
                fakeDeadUntil = 0f;
                ApplyLocalShowcaseImpulse(Vector3.zero, 6.2f);
                RPC_BroadcastTrollEvent(5, transform.position + Vector3.up * 1.8f);
            }
            else
            {
                fakeDeadUntil = Time.unscaledTime + 3.0f;
                RPC_BroadcastTrollEvent(4, transform.position + Vector3.up * 1.6f);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastTrollEvent(byte eventKind, Vector3 worldPos)
        {
            if (!IsNetworkReady) return;
            EnsureTrollAudio();
            switch (eventKind)
            {
                case 1: // Boop / Push Hit
                    trollAudioSource.pitch = Random.Range(.95f, 1.15f);
                    trollAudioSource.PlayOneShot(boopClip, .85f);
                    EnemyDamagePopup.ShowCallout(worldPos, "¡BOOP!", "EMPUJÓN", new Color(1f, .72f, .18f), 1.35f);
                    SpawnQuickRingBurst(worldPos, new Color(.25f, .95f, 1f));
                    break;
                case 2: // Jetpack Dash
                    trollAudioSource.pitch = Random.Range(.95f, 1.1f);
                    trollAudioSource.PlayOneShot(jetpackClip, .75f);
                    EnemyDamagePopup.ShowCallout(worldPos, "¡TURBO CAOS!", "JETPACK", new Color(.35f, .9f, 1f), 1.25f);
                    SpawnQuickRingBurst(transform.position + Vector3.up * .4f, new Color(1f, .55f, .15f));
                    break;
                case 3: // Martian Airhorn / Monster Bait
                    hurtUntil = Time.time + 1.2f;
                    trollAudioSource.pitch = 1f;
                    trollAudioSource.PlayOneShot(sirenClip, .9f);
                    EnemyDamagePopup.ShowCallout(worldPos, "¡HONK!", "BOCINA", new Color(1f, .28f, .28f), 1.45f);
                    break;
                case 4: // Fake Death Start
                    fakeDeadUntil = Time.unscaledTime + 3.0f;
                    trollAudioSource.pitch = 1f;
                    trollAudioSource.PlayOneShot(fakeDeadClip, .8f);
                    EnemyDamagePopup.ShowCallout(worldPos, "*D.E.P.*", "FINGIENDO", new Color(.95f, .4f, .4f), 1.2f);
                    break;
                case 5: // Fake Death Baited Pop!
                    fakeDeadUntil = 0f;
                    trollAudioSource.pitch = 1.08f;
                    trollAudioSource.PlayOneShot(baitedClip, .9f);
                    EnemyDamagePopup.ShowCallout(worldPos, "¡ESTOY VIVO!", "BUUU!", new Color(.35f, 1f, .45f), 1.4f);
                    SpawnQuickRingBurst(transform.position + Vector3.up * 1f, new Color(.35f, 1f, .45f));
                    break;
                case 6: // Boop Whiff / Swing
                    trollAudioSource.pitch = 1.25f;
                    trollAudioSource.PlayOneShot(boopClip, .45f);
                    SpawnQuickRingBurst(worldPos, new Color(.35f, .95f, .85f));
                    break;
                case 7: // Panic Scream
                    hurtUntil = Time.time + 1.2f;
                    trollAudioSource.pitch = Random.Range(1.3f, 1.5f);
                    trollAudioSource.PlayOneShot(sirenClip, .7f);
                    EnemyDamagePopup.ShowCallout(worldPos, "¡AAAAHHH!", "PÁNICO", new Color(1f, .85f, .2f), 1.35f);
                    break;
            }
        }

        private void EnsureTrollAudio()
        {
            if (trollAudioSource == null)
            {
                trollAudioSource = gameObject.AddComponent<AudioSource>();
                trollAudioSource.playOnAwake = false;
                trollAudioSource.spatialBlend = HasStateAuthority ? 0.15f : 0.75f;
                trollAudioSource.minDistance = 2.5f;
                trollAudioSource.maxDistance = 28f;
            }
            if (boopClip == null) boopClip = SynthesizeProceduralClip("TrollBoop", .16f, 260f, 880f, .7f, true);
            if (jetpackClip == null) jetpackClip = SynthesizeProceduralClip("TrollJetpack", .28f, 180f, 620f, .65f, false);
            if (sirenClip == null) sirenClip = SynthesizeProceduralClip("TrollSiren", .42f, 840f, 420f, .75f, true);
            if (fakeDeadClip == null) fakeDeadClip = SynthesizeProceduralClip("TrollFakeDead", .36f, 620f, 130f, .7f, false);
            if (baitedClip == null) baitedClip = SynthesizeProceduralClip("TrollBaited", .32f, 440f, 1180f, .8f, true);
        }

        private static AudioClip SynthesizeProceduralClip(string clipName, float duration, float fStart, float fEnd, float vol, bool vibrato)
        {
            int rate = 44100;
            int count = Mathf.CeilToInt(duration * rate);
            float[] samples = new float[count];
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)count;
                float freq = Mathf.Lerp(fStart, fEnd, t);
                if (vibrato) freq += Mathf.Sin(t * Mathf.PI * 12f) * 95f;
                phase += 2f * Mathf.PI * freq / rate;
                float env = Mathf.Exp(-t * 4.2f) * Mathf.Clamp01(i / 64f);
                samples[i] = (Mathf.Sin(phase) * .72f + Mathf.Sin(phase * 2f) * .28f) * env * vol;
            }
            AudioClip clip = AudioClip.Create(clipName, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void SpawnQuickRingBurst(Vector3 pos, Color color)
        {
            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), .35f, Mathf.Sin(ang)).normalized;
                LootOrb.Spawn(pos, dir * 5.2f, null, null, null, color, .35f);
            }
        }

        public void ToggleTablet()
        {
            if (Object != null && Object.IsValid && HasStateAuthority && IsAlive && !(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false) && !(GetComponent<GameJam.Survival.SurvivalRevive>()?.IsRecovering ?? false) && tablet != null)
                TabletOpen = !TabletOpen;
        }

        public void SetLobbyReady(bool ready)
        {
            if (Object != null && Object.IsValid && HasStateAuthority) LobbyReady = ready;
        }

        // Later the name-entry UI can call this on the locally owned astronaut.
        public void SetPlayerName(string value)
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority || string.IsNullOrWhiteSpace(value)) return;
            string clean = value.Replace("\n", " ").Replace("\r", " ").Trim();
            PlayerName = clean.Length > 24 ? clean.Substring(0, 24) : clean;
        }
        public void CloseTablet()
        {
            if (Object != null && Object.IsValid && HasStateAuthority) TabletOpen = false;
        }

        public void SetVoiceLevel(float amplitude)
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority) return;
            requestedVoice = amplitude < .04f ? (byte)0 : amplitude < .35f ? (byte)1 : (byte)2;
            voiceUntil = Time.unscaledTime + .25f;
        }

        public void PreviewHurt()
        {
            if (Object != null && Object.IsValid && HasStateAuthority) HurtSequence++;
        }

        private AudioSource monsterHurtAudio;
        private AudioClip monsterHurtClip;

        public void TakeMonsterDamage(int damage)
        {
            if (damage <= 0 || Object == null || !Object.IsValid) return;
            RPC_ForwardDamage(damage, true);
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || Object == null || !Object.IsValid) return;
            RPC_ForwardDamage(damage);
        }

        private void PlayMonsterHurtSound()
        {
            if (monsterHurtClip == null)
                monsterHurtClip = Resources.Load<AudioClip>("Audio/Player/SonidoJugadorReccibeDano");
            if (monsterHurtClip == null) return;
            if (monsterHurtAudio == null)
            {
                monsterHurtAudio = gameObject.AddComponent<AudioSource>();
                monsterHurtAudio.playOnAwake = false;
                monsterHurtAudio.spatialBlend = 0f;
                monsterHurtAudio.volume = .65f;
                monsterHurtAudio.priority = 64;
            }
            monsterHurtAudio.PlayOneShot(monsterHurtClip);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ForwardDamage(int damage, bool fromMonster = false)
        {
            if (damage <= 0) return;
            if (HealthSource != null)
            {
                var health = HealthSource;
                float before = health.CurrentHealth;
                health.TakeDamage(damage);
                if (health.CurrentHealth < before)
                {
                    HurtSequence++;
                    PlayMonsterHurtSound();
                }
            }
            else if (!warnedMissingHealth)
            {
                warnedMissingHealth = true;
                Debug.LogWarning("[Player health] No IPlayerHealth component assigned. Damage awaits the player health implementation.", this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            RequestSessionColor();
            if (!IsAlive) TabletOpen = false;
            if (IsRagdoll && ragdoll != null)
            {
                bool wantsRecover = input != null && (input.CurrentInput.Jump || input.CurrentInput.MoveDirection.sqrMagnitude > 0.1f);
                if (ragdollTimer.Expired(Runner) || (wantsRecover && Time.time > ragdoll.RagdollStartTime + ragdoll.MinRagdollDuration))
                {
                    IsRagdoll = false;
                    ragdoll.StandUp();
                    RPC_SyncRagdoll(false, Vector3.zero);
                    EnemyDamagePopup.ShowCallout(transform.position + Vector3.up * 1.6f, "¡DE PIE!", "", Color.green, 1.2f);
                }
            }
            UpdateStamina();
        }

        /// <summary>
        /// Conserva el consumo y la recuperacion del avatar original.
        /// </summary>
        protected virtual void UpdateStamina()
        {
            if (TurboOverdriveMode)
            {
                Stamina = 100f;
                return;
            }
            bool sprint = input.CurrentInput.Sprint && input.CurrentInput.MoveDirection.sqrMagnitude > .01f && Stamina > 0 && IsAlive;
            float staminaChange = 10f;
            if (sprint)
            {
                staminaChange = -12f;
            }
            Stamina = Mathf.Clamp(Stamina + staminaChange * Runner.DeltaTime, 0, 100);
        }

        private void Update()
        {
            if (!IsNetworkReady || !HasStateAuthority) return;
            var keyboard = Keyboard.current;
            bool menu = (session != null && session.MenuOpen) || GameJam.Crafting.CraftingBox.IsBlockingInput;
            if (keyboard != null && !menu && !SceneTransitionVideo.IsBlockingInput && !LobbyDepartureEffects.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleTablet();
                if (input != null && input.ControlsEnabled)
                {
                    if (keyboard.fKey.wasPressedThisFrame) RequestMeleeBoop();
                    if (keyboard.hKey.wasPressedThisFrame) TriggerSkillIssueSiren();
                    if (keyboard.tKey.wasPressedThisFrame) GetComponent<GameJam.Survival.SurvivalRevive>()?.TriggerEmote(1);
                    if (keyboard.yKey.wasPressedThisFrame)
                    {
                        if (!IsRagdoll)
                        {
                            var player = GetComponent<Starter.ThirdPersonCharacter.Player>();
                            float speed = player != null && player.KCC != null ? player.KCC.RealSpeed : 0f;
                            Vector3 diveImpulse = transform.forward * (speed > 1f ? 11f : 3.5f) + Vector3.up * 2.2f;
                            RequestRagdollKnockdown(diveImpulse, 2.6f);
                            EnemyDamagePopup.ShowCallout(transform.position + Vector3.up * 1.5f, "*FLOP*", "RAGDOLL", new Color(1f, 0.45f, 0.45f), 1.2f);
                        }
                        else if (ragdoll != null && Time.time > ragdoll.RagdollStartTime + 0.6f)
                        {
                            IsRagdoll = false;
                            ragdoll.StandUp();
                            RPC_SyncRagdoll(false, Vector3.zero);
                        }
                    }
                    if (keyboard.eKey.isPressed && !IsRagdoll && ragdoll != null)
                    {
                        int count = Physics.OverlapSphereNonAlloc(transform.position + transform.forward * 1.2f, 2.4f, nearbyScanBuffer, ~0, QueryTriggerInteraction.Ignore);
                        for (int i = 0; i < count; i++)
                        {
                            var c = nearbyScanBuffer[i];
                            if (c == null || c.transform.IsChildOf(transform)) continue;
                            var other = c.GetComponentInParent<MarsFusionAvatar>();
                            if (other != null && other != this && other.IsRagdoll && other.ragdoll != null && other.ragdoll.HipsRigidbody != null)
                            {
                                Vector3 targetPos = transform.position + transform.forward * 1.1f + Vector3.up * 0.35f;
                                Vector3 pull = (targetPos - other.ragdoll.HipsRigidbody.position) * 14f;
                                other.ragdoll.HipsRigidbody.AddForce(pull, ForceMode.Acceleration);
                                break;
                            }
                        }
                    }
                }
            }
            // Auto-pop fake death bait if a teammate walks right up to "revive" us!
            if (IsFakeDead)
            {
                int count = Physics.OverlapSphereNonAlloc(transform.position, 2.3f, nearbyScanBuffer, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = nearbyScanBuffer[i];
                    if (c == null || c.transform.IsChildOf(transform)) continue;
                    MarsFusionAvatar other = c.GetComponentInParent<MarsFusionAvatar>();
                    if (other != null && other != this && other.Runner == Runner && other.IsAlive)
                    {
                        ToggleFakeDeath();
                        other.RequestFriendlyPush(this);
                        break;
                    }
                }
            }
            if (Time.unscaledTime >= nextVoiceSend)
            {
                nextVoiceSend = Time.unscaledTime + .1f;
                byte level = Time.unscaledTime <= voiceUntil && IsAlive ? requestedVoice : (byte)0;
                if (VoiceLevel != level) VoiceLevel = level;
            }
            bool tabletVisible = TabletOpen || (ui != null && ui.IsVisible);
            bool inventoryVisible = LocalHud != null && LocalHud.IsInventoryOpen;
            input.ControlsEnabled = !(GetComponent<GameJam.Survival.SurvivalRevive>()?.IsRecovering ?? false) && !menu && !tabletVisible && !inventoryVisible && IsAlive && !(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false) && !SceneTransitionVideo.IsBlockingInput && !LobbyDepartureEffects.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput && !(GetComponent<PlayerInteractionAction>()?.IsBusy ?? false) && !(GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false) && !GusanoExcavadorFusion.LocalPlayerHeld && !GusanoMarteFusion.LocalPlayerHeld;
            bool cursor = menu || tabletVisible || inventoryVisible || !IsAlive;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursor;
            if (!input.ControlsEnabled) input.ResetInput();
        }

        public override void Render()
        {
            if (!IsNetworkReady) return;
            if (HasAssignedColor)
                sessionColors.GetOrCreateValue(Runner).Observe(ColorOwner.PlayerId, SuitColorIndex, MarsPlayerIdentityView.Palette.Length);
            if (shownOpen != TabletOpen)
            {
                shownOpen = TabletOpen;
                if (heldTablet != null) heldTablet.SetActive(shownOpen);
            }
            if (ui != null) ui.TargetOpen = TabletOpen;
            float? currentHealth = HealthSource?.CurrentHealth;
            if ((currentHealth.HasValue && previousHealth.HasValue && currentHealth.Value < previousHealth.Value - .01f) || HurtSequence != previousHurt)
                hurtUntil = Time.time + .7f;
            previousHealth = currentHealth; previousHurt = HurtSequence;
            Sprite eye = neutralEyes, lip = smileMouth;
            Expression = "Idle";
            if (!IsAlive || IsFakeDead) { eye = deadEyes; lip = deadMouth; Expression = "Eliminated"; }
            else if (Time.time < hurtUntil) { eye = hurtEyes; lip = hurtMouth; Expression = "Hurt"; }
            else
            {
                if (Time.time >= nextBlink) { blinkUntil = Time.time + .12f; nextBlink = Time.time + Random.Range(2.5f, 5f); }
                if (Time.time < blinkUntil) eye = blinkEyes;
                if (VoiceLevel > 0)
                {
                    int frame = Mathf.FloorToInt(Time.unscaledTime * 9f) % 4;
                    lip = frame == 0 ? smileMouth : frame == 2 && VoiceLevel > 1 ? openMouth : smallMouth;
                    Expression = "Talking";
                }
            }
            if (eyes != null && eyes.sprite != eye) eyes.sprite = eye;
            if (mouth != null && mouth.sprite != lip) mouth.sprite = lip;
        }

        private PlayerRef ColorOwner => Runner.GameMode == GameMode.Shared ? Object.StateAuthority : Object.InputAuthority;

        private void ApplyConfirmedColor(byte color)
        {
            if (!HasStateAuthority || color >= MarsPlayerIdentityView.Palette.Length) return;
            SuitColorIndex = color;
            IdentityInitialized = true;
            sessionColors.GetOrCreateValue(Runner).Observe(ColorOwner.PlayerId, color, MarsPlayerIdentityView.Palette.Length);
        }

        private void LearnSessionColors()
        {
            var assignments = sessionColors.GetOrCreateValue(Runner);
            assignments.ForgetDeparted(Runner.ActivePlayers.Select(p => p.PlayerId));
            foreach (var avatar in FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
                if (avatar.Object != null && avatar.Object.IsValid && avatar.Runner == Runner && avatar.HasAssignedColor)
                    assignments.Observe(avatar.ColorOwner.PlayerId, avatar.SuitColorIndex, MarsPlayerIdentityView.Palette.Length);
        }

        private void AssignColorLocally(PlayerRef player)
        {
            LearnSessionColors();
            byte color;
            if (sessionColors.GetOrCreateValue(Runner).TryReserve(player.PlayerId, MarsPlayerIdentityView.Palette.Length,
                Random.Range(0, MarsPlayerIdentityView.Palette.Length), Runner.ActivePlayers.Select(p => p.PlayerId), out color))
                ApplyConfirmedColor(color);
        }

        private void RequestSessionColor()
        {
            if (HasAssignedColor || Time.unscaledTime < nextColorRequest) return;
            nextColorRequest = Time.unscaledTime + .5f;
            if (Runner.GameMode != GameMode.Shared) { AssignColorLocally(ColorOwner); return; }
            NetworkObject masterPlayer;
            if (!Runner.TryGetPlayerObject(Runner.GetMasterClient(), out masterPlayer)
                || masterPlayer == null || !masterPlayer.IsValid) return;
            var coordinator = masterPlayer.GetComponent<MarsFusionAvatar>();
            if (coordinator != null) coordinator.RPC_RequestSessionColor();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestSessionColor(RpcInfo info = default)
        {
            if (!HasStateAuthority || Runner.GameMode != GameMode.Shared || !Runner.IsSharedModeMasterClient) return;
            var requester = info.IsInvokeLocal ? Runner.LocalPlayer : info.Source;
            if (!Runner.ActivePlayers.Contains(requester)) return;
            LearnSessionColors();
            byte color;
            if (!sessionColors.GetOrCreateValue(Runner).TryReserve(requester.PlayerId, MarsPlayerIdentityView.Palette.Length,
                Random.Range(0, MarsPlayerIdentityView.Palette.Length), Runner.ActivePlayers.Select(p => p.PlayerId), out color))
            {
                if (!warnedColorCapacity) Debug.LogError("[Player colors] No hay colores libres. La sala admite como maximo seis jugadores.", this);
                warnedColorCapacity = true;
                return;
            }
            // Reserve first, broadcast second: simultaneous requests cannot take the same slot.
            RPC_ConfirmSessionColor(requester, color);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ConfirmSessionColor(PlayerRef player, byte color)
        {
            if (Runner.GameMode != GameMode.Shared || Object.StateAuthority != Runner.GetMasterClient()
                || color >= MarsPlayerIdentityView.Palette.Length || !Runner.ActivePlayers.Contains(player)) return;
            var assignments = sessionColors.GetOrCreateValue(Runner);
            assignments.ForgetDeparted(Runner.ActivePlayers.Select(p => p.PlayerId));
            if (!assignments.Observe(player.PlayerId, color, MarsPlayerIdentityView.Palette.Length)) return;
            NetworkObject playerObject;
            if (Runner.TryGetPlayerObject(player, out playerObject))
            {
                var avatar = playerObject.GetComponent<MarsFusionAvatar>();
                if (avatar != null && avatar.HasStateAuthority) avatar.ApplyConfirmedColor(color);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => CleanUp();
        private void LateUpdate()
        {
            if (IsPunching)
            {
                float pt = (Time.time - punchStartTime) / 0.28f;
                float pw = Mathf.Sin(pt * Mathf.PI);
                if (punchSpine != null) punchSpine.localRotation *= Quaternion.Euler(-12f * pw, -18f * pw, 0f);
                if (punchRightUpperArm != null) punchRightUpperArm.localRotation *= Quaternion.Euler(-60f * pw, 22f * pw, 32f * pw);
                if (punchRightForeArm != null) punchRightForeArm.localRotation *= Quaternion.Euler(0f, 0f, -28f * pw);
            }
            if (!IsAlive && ragdoll != null && !ragdoll.IsRagdolled)
            {
                Vector3 deathTumble = friendlyPushVelocity.sqrMagnitude > 1f ? (friendlyPushVelocity + Vector3.up * 2.5f) : (Vector3.down * 2.5f);
                ragdoll.GoRagdoll(deathTumble, 99999f);
            }
            else if (IsAlive && !IsRagdoll && ragdoll != null && ragdoll.IsRagdolled && !(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false))
            {
                ragdoll.StandUp();
            }
            else if (!HasStateAuthority && ragdoll != null)
            {
                if (IsRagdoll && !ragdoll.IsRagdolled)
                    ragdoll.GoRagdoll(transform.forward * 4f, 2.5f);
                else if (!IsRagdoll && ragdoll.IsRagdolled && IsAlive)
                    ragdoll.StandUp();
            }
        }

        private void OnDestroy() => CleanUp();
        private void CleanUp()
        {
            if (Local == this) Local = null;
            if (identityView != null) Destroy(identityView);
            if (ui != null) Destroy(ui.gameObject);
            if (LocalHud != null) Destroy(LocalHud.gameObject);
            if (heldTablet != null) Destroy(heldTablet);
        }
    }
}
