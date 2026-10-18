using Fusion;
using GameJam.Combat;
using GameJam.Tablet;
using Starter.ThirdPersonCharacter;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Multiplayer
{
    public sealed class MarsFusionAvatar : NetworkBehaviour, ITakeDamage
    {
        public TabletItemDefinition tablet;
        public Transform handSocket;
        public SpriteRenderer eyes, mouth;
        public Sprite neutralEyes, blinkEyes, hurtEyes, deadEyes;
        public Sprite smileMouth, smallMouth, openMouth, hurtMouth, deadMouth;
        [SerializeField, Tooltip("Optional component implementing IPlayerHealth. Owns health and its replication.")]
        private MonoBehaviour healthSource;
        public IPlayerHealth HealthSource => healthSource != null ? healthSource as IPlayerHealth : null;
        public bool HasHealthSource => HealthSource != null;
        // Until the health component is supplied, keep exploration and interaction enabled.
        public bool IsAlive => HealthSource == null || HealthSource.IsAlive;
        [Networked] public float Stamina { get; private set; }
        [Networked] public NetworkBool TabletOpen { get; private set; }
        [Networked] public byte VoiceLevel { get; private set; }
        [Networked] private uint HurtSequence { get; set; }
        [Networked] public byte SuitColorIndex { get; private set; }
        [Networked] public NetworkString<_32> PlayerName { get; private set; }
        [Networked] public NetworkBool LobbyReady { get; private set; }
        [Networked] private NetworkBool IdentityInitialized { get; set; }
        private MarsPlayerIdentityView identityView;
        private PlayerInput input;
        private MarsFusionSession session;
        private TabletGuideUI ui;
        private GameObject heldTablet;
        private float? previousHealth;
        private float hurtUntil, blinkUntil, nextBlink, voiceUntil, nextVoiceSend;
        private bool warnedMissingHealth;
        private uint previousHurt;
        private byte requestedVoice;
        private bool shownOpen;
        public string Expression { get; private set; }
        public TabletGuideUI LocalUI => ui;

        public override void Spawned()
        {
            input = GetComponent<PlayerInput>();
            session = FindFirstObjectByType<MarsFusionSession>();
            if (healthSource == null)
                foreach (var component in GetComponents<MonoBehaviour>())
                    if (component is IPlayerHealth) { healthSource = component; break; }
            if (HasStateAuthority) Stamina = 100;
            if (HasStateAuthority && !IdentityInitialized)
            {
                SuitColorIndex = (byte)Random.Range(0, MarsPlayerIdentityView.Palette.Length);
                // Prefer an unused color among players already present in this session.
                var players = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None);
                for (int offset = 0; offset < MarsPlayerIdentityView.Palette.Length; offset++)
                {
                    byte candidate = (byte)((SuitColorIndex + offset) % MarsPlayerIdentityView.Palette.Length);
                    bool used = false;
                    foreach (var other in players)
                        if (other != this && other.Runner == Runner && other.Object != null && other.Object.IsValid
                            && other.IdentityInitialized && other.SuitColorIndex == candidate) { used = true; break; }
                    if (!used) { SuitColorIndex = candidate; break; }
                }
                var owner = Runner.GameMode == GameMode.Shared ? Object.StateAuthority : Object.InputAuthority;
                PlayerName = "Astronauta " + owner.PlayerId;
                IdentityInitialized = true;
            }
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
                ui.TargetOpen = TabletOpen;
            }
        }

        public void ToggleTablet()
        {
            if (Object != null && Object.IsValid && HasStateAuthority && IsAlive && tablet != null)
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

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || Object == null || !Object.IsValid) return;
            RPC_ForwardDamage(damage);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ForwardDamage(int damage)
        {
            if (damage <= 0) return;
            if (HealthSource != null) HealthSource.TakeDamage(damage);
            else if (!warnedMissingHealth)
            {
                warnedMissingHealth = true;
                Debug.LogWarning("[Player health] No IPlayerHealth component assigned. Damage awaits the player health implementation.", this);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (!IsAlive) TabletOpen = false;
            bool sprint = input.CurrentInput.Sprint && input.CurrentInput.MoveDirection.sqrMagnitude > .01f && Stamina > 0 && IsAlive;
            Stamina = Mathf.Clamp(Stamina + (sprint ? -15 : 10) * Runner.DeltaTime, 0, 100);
        }

        private void Update()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority) return;
            var keyboard = Keyboard.current;
            bool menu = session != null && session.MenuOpen;
            if (keyboard != null && !menu && !SceneTransitionVideo.IsBlockingInput && !LobbyDepartureEffects.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleTablet();
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    if (keyboard.hKey.wasPressedThisFrame) PreviewHurt();
                }
            }
            if (Time.unscaledTime >= nextVoiceSend)
            {
                nextVoiceSend = Time.unscaledTime + .1f;
                byte level = Time.unscaledTime <= voiceUntil && IsAlive ? requestedVoice : (byte)0;
                if (VoiceLevel != level) VoiceLevel = level;
            }
            bool tabletVisible = TabletOpen || (ui != null && ui.IsVisible);
            input.ControlsEnabled = !menu && !tabletVisible && IsAlive && !SceneTransitionVideo.IsBlockingInput && !LobbyDepartureEffects.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput;
            bool cursor = menu || tabletVisible || !IsAlive;
            Cursor.lockState = cursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = cursor;
            if (!input.ControlsEnabled) input.ResetInput();
        }

        public override void Render()
        {
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
            if (!IsAlive) { eye = deadEyes; lip = deadMouth; Expression = "Eliminated"; }
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

        private void OnGUI()
        {
            if (PortalWhiteTransition.IsBlockingInput || SceneTransitionVideo.IsBlockingInput) return;
            if (Object == null || !Object.IsValid || !HasStateAuthority || TabletOpen || (session != null && session.MenuOpen)) return;
            string healthText = HealthSource != null ? $"Salud: {HealthSource.CurrentHealth:0} / {HealthSource.MaxHealth:0}\n" : "";
            GUI.Box(new Rect(20, Screen.height - 85, 280, 65), $"{healthText}Energía: {Stamina:0} / 100   ·   Tab: tablet");
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => CleanUp();
        private void OnDestroy() => CleanUp();
        private void CleanUp()
        {
            if (identityView != null) Destroy(identityView);
            if (ui != null) Destroy(ui.gameObject);
            if (heldTablet != null) Destroy(heldTablet);
        }
    }
}
