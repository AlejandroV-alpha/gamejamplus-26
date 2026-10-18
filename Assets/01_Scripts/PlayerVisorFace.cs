using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Characters
{
    // Cosmetic only: health and movement remain under the existing gameplay systems.
    public sealed class PlayerVisorFace : NetworkBehaviour
    {
        [SerializeField] private SpriteRenderer eyes;
        [SerializeField] private SpriteRenderer mouth;
        [SerializeField] private Sprite neutralEyes, blinkEyes, hurtEyes, deadEyes;
        [SerializeField] private Sprite smileMouth, smallMouth, openMouth, hurtMouth, deadMouth;
        [SerializeField] private bool enablePrototypeKeys = true;
        private readonly NetworkVariable<byte> voice = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<uint> hurtPreview = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private CoreStatsHandler stats;
        private float lastHealth, hurtUntil, nextBlink, blinkUntil, voiceUntil, nextSend, nextHealthCheck;
        private byte requestedVoice;
        private bool hasHealth;
        public string Expression { get; private set; } = "Idle";

        public override void OnNetworkSpawn()
        {
            stats = GetComponent<CoreStatsHandler>();
            hasHealth = false;
            nextBlink = Time.time + Random.Range(2f, 5f);
            hurtPreview.OnValueChanged += OnHurtPreview;
            RenderFace(neutralEyes, smileMouth);
        }

        /// <summary>Call continuously on the owning player with normalized voice amplitude (0..1).
        /// If the voice integration stops reporting, the mouth closes after 0.25 seconds.</summary>
        public void SetVoiceLevel(float amplitude)
        {
            if (!IsSpawned || !IsOwner) return;
            requestedVoice = amplitude < 0.04f ? (byte)0 : amplitude < 0.35f ? (byte)1 : (byte)2;
            voiceUntil = Time.unscaledTime + .25f;
        }

        public void PreviewHurt()
        {
            if (IsSpawned && IsOwner) hurtPreview.Value++;
        }

        private void OnHurtPreview(uint previous, uint current) => hurtUntil = Time.time + .7f;

        private void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                if (enablePrototypeKeys && (Application.isEditor || Debug.isDebugBuild) && Keyboard.current != null)
                {
                    if (Keyboard.current.vKey.isPressed) SetVoiceLevel(.8f);
                    if (Keyboard.current.hKey.wasPressedThisFrame) PreviewHurt();
                }
                if (Time.unscaledTime >= nextSend)
                {
                    nextSend = Time.unscaledTime + .1f;
                    byte level = Time.unscaledTime <= voiceUntil ? requestedVoice : (byte)0;
                    if (voice.Value != level) voice.Value = level;
                }
            }
            if (stats != null && stats.IsSpawned && Time.time >= nextHealthCheck)
            {
                nextHealthCheck = Time.time + .05f;
                // An observer may spawn before the initial stat list arrives.
                foreach (var stat in stats.GetAllStats())
                {
                    if (stat.name != "Health") continue;
                    if (hasHealth && stat.current < lastHealth - .01f) hurtUntil = Time.time + .7f;
                    lastHealth = stat.current;
                    hasHealth = true;
                    break;
                }
            }
            if (hasHealth && lastHealth <= 0)
            {
                Expression = "Eliminated";
                RenderFace(deadEyes, deadMouth);
            }
            else if (Time.time < hurtUntil)
            {
                Expression = "Hurt";
                RenderFace(hurtEyes, hurtMouth);
            }
            else
            {
                if (Time.time >= nextBlink)
                {
                    blinkUntil = Time.time + .12f;
                    nextBlink = Time.time + Random.Range(2.5f, 5f);
                }
                bool talking = voice.Value > 0;
                int frame = Mathf.FloorToInt(Time.unscaledTime * 9f) % 4;
                var speakingMouth = frame == 0 ? smileMouth : frame == 2 && voice.Value > 1 ? openMouth : smallMouth;
                Expression = talking ? "Talking" : "Idle";
                RenderFace(Time.time < blinkUntil ? blinkEyes : neutralEyes, talking ? speakingMouth : smileMouth);
            }
        }

        private void RenderFace(Sprite eyeSprite, Sprite mouthSprite)
        {
            if (eyes != null && eyes.sprite != eyeSprite) eyes.sprite = eyeSprite;
            if (mouth != null && mouth.sprite != mouthSprite) mouth.sprite = mouthSprite;
        }

        public override void OnNetworkDespawn()
        {
            hurtPreview.OnValueChanged -= OnHurtPreview;
            requestedVoice = 0;
            voiceUntil = hurtUntil = 0;
            hasHealth = false;
        }
    }
}
