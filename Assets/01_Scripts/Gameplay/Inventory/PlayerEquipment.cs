using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using GameJam.Survival;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJam.Inventory
{
    /// <summary>Selection is inventory state; radio transmission is an explicit, replicated channel.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Inventory))]
    public sealed class PlayerEquipment : NetworkBehaviour
    {
        [SerializeField] private AudioClip radioEquippedSound, channelOpenSound, channelCloseSound;
        [SerializeField, Range(0,1)] private float radioSoundVolume = .4f;
        [Networked] private NetworkBool channelOpen { get; set; }
        private Inventory inventory;
        private MarsFusionAvatar avatar;
        private Starter.ThirdPersonCharacter.PlayerInput input;
        private PlayerInteractionAction action;
        private SurvivalRevive revive;
        private PlayerHurtReaction hurt;
        private Gun gun;
        private AudioSource sounds;
        private bool wasOpen, wasRadio, wasWeapon;
        private bool Ready => Object != null && Object.IsValid && StateBufferIsValid;
        public bool IsWeaponEquipped => Ready && inventory.SelectedItem != null && inventory.SelectedItem.kind == CarriedItemKind.PrimaryWeapon;
        public bool IsRadioEquipped => Ready && inventory.SelectedItem != null && inventory.SelectedItem.kind == CarriedItemKind.Radio;
        public bool HasRadio => Ready && inventory.HasItem(CarriedItemKind.Radio);
        public bool CanPresentItem => Ready && avatar != null && avatar.IsAlive && !avatar.TabletOpen
            && !avatar.IsRagdoll && (avatar.Ragdoll == null || (!avatar.Ragdoll.IsRagdolled && !avatar.Ragdoll.IsRecovering))
            && !(action != null && action.IsBusy) && !(revive != null && revive.IsAnimating)
            && !(hurt != null && hurt.IsReacting)
            && !(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false);
        public bool CanOperate => CanPresentItem && input != null && input.ControlsEnabled
            && !SceneTransitionVideo.IsBlockingInput && !PortalWhiteTransition.IsBlockingInput
            && !LobbyDepartureEffects.IsBlockingInput && !GameJam.Crafting.CraftingBox.IsBlockingInput
            && !GusanoMarteFusion.LocalPlayerHeld;
        public bool IsRadioChannelOpen => Ready && channelOpen && IsRadioEquipped && CanPresentItem;

        private void Awake()
        {
            inventory = GetComponent<Inventory>(); avatar = GetComponent<MarsFusionAvatar>();
            input = GetComponent<Starter.ThirdPersonCharacter.PlayerInput>();
            action = GetComponent<PlayerInteractionAction>(); revive = GetComponent<SurvivalRevive>();
            hurt = GetComponent<PlayerHurtReaction>(); gun = GetComponent<Gun>();
        }
        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                channelOpen = false;
                var go = new GameObject("Local radio feedback"); go.transform.SetParent(transform,false);
                sounds = go.AddComponent<AudioSource>(); sounds.playOnAwake = false; sounds.spatialBlend = 0;
            }
            wasOpen = false; wasRadio = false; wasWeapon = IsWeaponEquipped;
        }
        private void Update()
        {
            if (!Ready || !HasStateAuthority) return;
            if (!CanOperate || !IsRadioEquipped) channelOpen = false;
            else if (!VoicePreferences.InputSuspended && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                ToggleRadioChannel();
            bool open = IsRadioChannelOpen;
            if (open != wasOpen) Play(open ? channelOpenSound : channelCloseSound);
            else if (IsRadioEquipped && !wasRadio) Play(radioEquippedSound);
            if (!IsWeaponEquipped && wasWeapon && gun != null) gun.StopEquippedEffects();
            wasOpen = open; wasRadio = IsRadioEquipped; wasWeapon = IsWeaponEquipped;
        }
        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority && (!IsRadioEquipped || !CanOperate)) channelOpen = false;
        }
        public bool ToggleRadioChannel()
        {
            if (!Ready || !HasStateAuthority || !IsRadioEquipped || !CanOperate) return false;
            channelOpen = !channelOpen;
            return true;
        }
        public static bool CanReceiveRadio(PlayerEquipment sender, PlayerEquipment listener)
        {
            return sender != null && listener != null && sender.Runner == listener.Runner
                && sender.IsRadioChannelOpen && listener.HasRadio
                && listener.avatar != null && listener.avatar.IsAlive;
        }
        private void Play(AudioClip clip)
        {
            if (sounds != null && clip != null) sounds.PlayOneShot(clip,radioSoundVolume);
        }
        private void OnDisable()
        {
            if (sounds != null) sounds.Stop();
            if (Ready && HasStateAuthority) channelOpen = false;
        }
    }
}
