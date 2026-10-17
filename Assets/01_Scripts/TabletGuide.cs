using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace GameJam.Tablet
{
    public sealed class TabletGuide : NetworkBehaviour
    {
        [Header("Equipment slot 01 — graybox inventory")]
        [SerializeField] private TabletItemDefinition equippedTablet;
        [SerializeField] private Transform handSocket;
        [SerializeField] private UnityEvent onTabletRaised;
        [SerializeField] private UnityEvent onTabletLowered;
        private readonly NetworkVariable<bool> open = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private GameObject heldTablet;
        private TabletGuideUI ui;
        private CorePlayerManager player;
        private InteractionAddon interaction;
        private bool controlsLocked, previousLook, previousInteraction, previousMovement;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        public bool HasEquippedTablet => equippedTablet != null;
        public bool IsTabletOpen => IsSpawned && open.Value;
        public TabletGuideUI LocalUI => ui;

        public override void OnNetworkSpawn()
        {
            player = GetComponent<CorePlayerManager>();
            interaction = GetComponent<InteractionAddon>();
            open.OnValueChanged += OnOpenChanged;
            if (HasEquippedTablet && equippedTablet.heldPrefab != null && handSocket != null)
            {
                heldTablet = Instantiate(equippedTablet.heldPrefab, handSocket, false);
                var scale = handSocket.lossyScale;
                heldTablet.transform.localScale = new Vector3(1 / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                    1 / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1 / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
                heldTablet.transform.position = handSocket.position + handSocket.TransformDirection(new Vector3(0.04f, 0.02f, 0.01f));
                heldTablet.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            if (IsOwner)
            {
                var canvas = new GameObject("Local Tablet Guide");
                ui = canvas.AddComponent<TabletGuideUI>();
                ui.Build(CloseTablet, equippedTablet != null ? equippedTablet.catalog : null,
                    equippedTablet != null ? equippedTablet.skin : null);
            }
            OnOpenChanged(false, open.Value);
        }

        public void ToggleTablet()
        {
            if (!IsSpawned || !IsOwner || !HasEquippedTablet || player == null || !player.PlayerState.IsActive) return;
            open.Value = !open.Value;
        }

        public void CloseTablet()
        {
            if (IsSpawned && IsOwner) open.Value = false;
        }

        private void OnOpenChanged(bool previous, bool current)
        {
            if (heldTablet != null) heldTablet.SetActive(current);
            // Hooks for the future draw/stow animation, run for all observing clients.
            if (current) onTabletRaised?.Invoke(); else onTabletLowered?.Invoke();
            if (!IsOwner || ui == null) return;
            ui.TargetOpen = current;
            if (current) LockControls();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ToggleTablet();
            if (player != null && !player.PlayerState.IsActive && open.Value) CloseTablet();
            if (controlsLocked && ui != null && !ui.TargetOpen && !ui.IsVisible) RestoreControls();
        }

        private void LockControls()
        {
            if (controlsLocked) return;
            controlsLocked = true;
            previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
            previousMovement = player.MovementInputEnabled;
            previousLook = player.CoreCamera.LookInputEnabled;
            previousInteraction = interaction != null && interaction.IsEnabled;
            player.SetMovementInputEnabled(false);
            player.CoreCamera.LookInputEnabled = false;
            if (interaction != null) interaction.IsEnabled = false;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void RestoreControls()
        {
            if (!controlsLocked) return;
            controlsLocked = false;
            if (player != null)
            {
                player.SetMovementInputEnabled(previousMovement);
                if (player.CoreCamera != null) player.CoreCamera.LookInputEnabled = previousLook;
            }
            if (interaction != null) interaction.IsEnabled = previousInteraction;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
        }

        public override void OnNetworkDespawn()
        {
            open.OnValueChanged -= OnOpenChanged;
            RestoreControls();
            if (ui != null) Destroy(ui.gameObject);
            if (heldTablet != null) Destroy(heldTablet);
        }

        public override void OnDestroy()
        {
            RestoreControls();
            if (ui != null) Destroy(ui.gameObject);
            if (heldTablet != null) Destroy(heldTablet);
            base.OnDestroy();
        }
    }
}
