using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Crafting
{
    /// <summary>
    /// Caja de crafteo tipo mesa de Minecraft: el jugador local la abre con E,
    /// la tapa se anima y aparece la interfaz de recetas de la CraftingTable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CraftingTable))]
    public sealed class CraftingBox : MonoBehaviour
    {
        private static readonly int OpenTrigger = Animator.StringToHash("Abrir");
        private static readonly int CloseTrigger = Animator.StringToHash("Cerrar");

        /// <summary>
        /// Verdadero mientras la interfaz esta abierta: el avatar libera el cursor y bloquea el movimiento.
        /// </summary>
        public static bool IsBlockingInput { get; private set; }

        [SerializeField] private Animator lidAnimator;
        [SerializeField, Min(0.5f)] private float interactionDistance = 2.8f;
        [SerializeField, Min(0f)] private float panelDelay = 0.45f;
        [SerializeField] private string title = "Caja de crafteo";

        private CraftingTable table;
        private CraftingBoxUI ui;
        private MarsFusionAvatar localPlayer;
        private float nextPlayerSearch;
        private float openedAt;
        private bool open;

        private void Awake()
        {
            table = GetComponent<CraftingTable>();
            if (lidAnimator == null)
            {
                lidAnimator = GetComponentInChildren<Animator>();
            }
        }

        private void OnDisable()
        {
            if (open)
            {
                Close();
            }
        }

        /// <summary>
        /// Muestra el aviso de cercania y alterna la caja con E (o la cierra con Escape).
        /// </summary>
        private void Update()
        {
            FindLocalPlayer();
            bool near = localPlayer != null && localPlayer.IsAlive
                && (localPlayer.transform.position - transform.position).sqrMagnitude <= interactionDistance * interactionDistance;

            EnsureUI();
            ui.SetPromptVisible(near && !open && !localPlayer.TabletOpen);

            if (open && !near)
            {
                Close();
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!open && near && !localPlayer.TabletOpen && keyboard.eKey.wasPressedThisFrame)
            {
                Open();
            }
            else if (open && (keyboard.eKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                Close();
            }

            if (open)
            {
                ui.SetPanelVisible(Time.time - openedAt >= panelDelay);
                ui.Refresh(table, GetInventory());
            }
        }

        private void Open()
        {
            open = true;
            openedAt = Time.time;
            IsBlockingInput = true;
            if (lidAnimator != null)
            {
                lidAnimator.ResetTrigger(CloseTrigger);
                lidAnimator.SetTrigger(OpenTrigger);
            }
        }

        private void Close()
        {
            open = false;
            IsBlockingInput = false;
            if (ui != null)
            {
                ui.SetPanelVisible(false);
            }
            if (lidAnimator != null)
            {
                lidAnimator.ResetTrigger(OpenTrigger);
                lidAnimator.SetTrigger(CloseTrigger);
            }
        }

        /// <summary>
        /// Pide la receta a la mesa de red; ella valida distancia, materiales y progreso.
        /// </summary>
        private void Craft(int recipeIndex)
        {
            if (table.RequestCraft(recipeIndex))
            {
                ui.Flash("Fabricando...");
            }
            else
            {
                ui.Flash("No se puede fabricar todavia");
            }
        }

        private void EnsureUI()
        {
            if (ui == null)
            {
                ui = CraftingBoxUI.Create(title, Craft, Close);
            }
        }

        private PlayerInventory GetInventory()
        {
            return localPlayer != null ? localPlayer.GetComponent<PlayerInventory>() : null;
        }

        /// <summary>
        /// Busca el avatar con autoridad local sin recorrer la escena cada frame.
        /// </summary>
        private void FindLocalPlayer()
        {
            if (localPlayer != null && localPlayer.Object != null && localPlayer.Object.IsValid)
            {
                return;
            }
            if (Time.time < nextPlayerSearch)
            {
                return;
            }

            nextPlayerSearch = Time.time + 0.5f;
            localPlayer = null;
            foreach (MarsFusionAvatar avatar in FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
            {
                if (avatar.Object != null && avatar.Object.IsValid && avatar.HasStateAuthority)
                {
                    localPlayer = avatar;
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            if (open)
            {
                IsBlockingInput = false;
            }
            if (ui != null)
            {
                Destroy(ui.gameObject);
            }
        }
    }
}
