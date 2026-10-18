using Fusion;
using GameJam.Tablet;
using Starter.ThirdPersonCharacter;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Multiplayer
{
    public sealed class MarsFusionAvatar : NetworkBehaviour
    {
        public TabletItemDefinition tablet;
        public Transform handSocket;
        public SpriteRenderer eyes, mouth;
        public Sprite neutralEyes, blinkEyes, hurtEyes, deadEyes;
        public Sprite smileMouth, smallMouth, openMouth, hurtMouth, deadMouth;
        [Networked] public float Health { get; private set; }
        [Networked] public float Stamina { get; private set; }
        [Networked] public NetworkBool TabletOpen { get; private set; }
        [Networked] public byte VoiceLevel { get; private set; }
        [Networked] private uint HurtSequence { get; set; }
        private PlayerInput input;
        private MarsFusionSession session;
        private TabletGuideUI ui;
        private GameObject heldTablet;
        private float previousHealth, hurtUntil, blinkUntil, nextBlink, voiceUntil, nextVoiceSend;
        private uint previousHurt;
        private byte requestedVoice;
        private bool shownOpen;
        public string Expression { get; private set; }
        public TabletGuideUI LocalUI => ui;

        public override void Spawned()
        {
            input = GetComponent<PlayerInput>();
            session = FindFirstObjectByType<MarsFusionSession>();
            if (HasStateAuthority) { Health = 100; Stamina = 100; }
            previousHealth = Health;
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
            if (Object != null && Object.IsValid && HasStateAuthority && Health > 0 && tablet != null)
                TabletOpen = !TabletOpen;
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

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ApplyDamage(float damage)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0) return;
            Health = Mathf.Max(0, Health - Mathf.Min(damage, 100));
            if (Health <= 0) TabletOpen = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            bool sprint = input.CurrentInput.Sprint && input.CurrentInput.MoveDirection.sqrMagnitude > .01f && Stamina > 0 && Health > 0;
            Stamina = Mathf.Clamp(Stamina + (sprint ? -15 : 10) * Runner.DeltaTime, 0, 100);
        }

        private void Update()
        {
            if (Object == null || !Object.IsValid || !HasStateAuthority) return;
            var keyboard = Keyboard.current;
            bool menu = session != null && session.MenuOpen;
            if (keyboard != null && !menu)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleTablet();
                if (Application.isEditor || Debug.isDebugBuild)
                {
                    if (keyboard.vKey.isPressed) SetVoiceLevel(.8f);
                    if (keyboard.hKey.wasPressedThisFrame) PreviewHurt();
                }
            }
            if (Time.unscaledTime >= nextVoiceSend)
            {
                nextVoiceSend = Time.unscaledTime + .1f;
                byte level = Time.unscaledTime <= voiceUntil && Health > 0 ? requestedVoice : (byte)0;
                if (VoiceLevel != level) VoiceLevel = level;
            }
            bool tabletVisible = TabletOpen || (ui != null && ui.IsVisible);
            input.ControlsEnabled = !menu && !tabletVisible && Health > 0;
            bool cursor = menu || tabletVisible || Health <= 0;
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
            if (Health < previousHealth - .01f || HurtSequence != previousHurt) hurtUntil = Time.time + .7f;
            previousHealth = Health; previousHurt = HurtSequence;
            Sprite eye = neutralEyes, lip = smileMouth;
            Expression = "Idle";
            if (Health <= 0) { eye = deadEyes; lip = deadMouth; Expression = "Eliminated"; }
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
            if (Object == null || !Object.IsValid || !HasStateAuthority || TabletOpen || (session != null && session.MenuOpen)) return;
            GUI.Box(new Rect(20, Screen.height - 85, 280, 65), $"Salud: {Health:0} / 100\nEnergía: {Stamina:0} / 100   ·   Tab: tablet");
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => CleanUp();
        private void OnDestroy() => CleanUp();
        private void CleanUp()
        {
            if (ui != null) Destroy(ui.gameObject);
            if (heldTablet != null) Destroy(heldTablet);
        }
    }
}
