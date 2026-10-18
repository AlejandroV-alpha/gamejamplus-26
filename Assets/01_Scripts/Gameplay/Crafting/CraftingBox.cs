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
        [SerializeField] private bool smoothLidAnimation;
        [SerializeField, Min(0.5f)] private float interactionDistance = 2.8f;
        [SerializeField, Min(0f)] private float panelDelay = 0.45f;
        [SerializeField] private string title = "Caja de crafteo";

        private CraftingTable table;
        private CraftingBoxUI ui;
        private MarsFusionAvatar localPlayer;
        private float nextPlayerSearch;
        private float openedAt;
        private bool open;
        private Transform lidHinge;
        private Quaternion closedLidRotation;
        private Quaternion openedLidRotation;
        private float lidProgress;
        private float lidVelocity;

        private void Awake()
        {
            table = GetComponent<CraftingTable>();
            if (lidAnimator == null)
            {
                lidAnimator = GetComponentInChildren<Animator>();
            }
            InitializeSmoothLid();
        }

        private void InitializeSmoothLid()
        {
            if (!smoothLidAnimation || lidAnimator == null ||
                !lidAnimator.HasState(0, Animator.StringToHash("Base Layer.Cerrada")) ||
                !lidAnimator.HasState(0, Animator.StringToHash("Base Layer.Abierta")))
            {
                return;
            }

            foreach (Transform child in lidAnimator.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Bisagra_Tapa")
                {
                    lidHinge = child;
                    break;
                }
            }
            if (lidHinge == null)
            {
                return;
            }

            // Keep the scene's placement: imported clips also contain root transform keys.
            Transform root = lidAnimator.transform;
            Vector3 position = root.localPosition;
            Quaternion rotation = root.localRotation;
            Vector3 scale = root.localScale;
            lidAnimator.Play("Base Layer.Abierta", 0, 0f);
            lidAnimator.Update(0f);
            openedLidRotation = lidHinge.localRotation;
            lidAnimator.Play("Base Layer.Cerrada", 0, 0f);
            lidAnimator.Update(0f);
            lidAnimator.enabled = false;
            root.SetLocalPositionAndRotation(position, rotation);
            root.localScale = scale;
            closedLidRotation = CalculateClosedLidRotation();
            lidHinge.localRotation = closedLidRotation;
        }

        private Quaternion CalculateClosedLidRotation()
        {
            foreach (MeshFilter mesh in lidHinge.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mesh.name != "Caja_Tapa" || mesh.sharedMesh == null)
                {
                    continue;
                }

                // Measured from the largest lid faces in Caja_Craft_Juego.fbx.
                // The surface is baked diagonally into the mesh, so neither zero
                // hinge rotation nor the mesh bounds describe its closed pose.
                Vector3 normal = mesh.transform.TransformDirection(new Vector3(0f, 0.676f, 0.737f).normalized);
                Vector3 up = transform.up;
                if (Vector3.Dot(normal, up) < 0f)
                {
                    normal = -normal;
                }
                Vector3 axis = lidHinge.parent.TransformDirection(Vector3.right);
                Vector3 from = Vector3.ProjectOnPlane(normal, axis);
                Vector3 to = Vector3.ProjectOnPlane(up, axis);
                float correction = Vector3.SignedAngle(from, to, axis);
                return Quaternion.AngleAxis(correction, Vector3.right) * lidHinge.localRotation;
            }
            return lidHinge.localRotation;
        }

        private void LateUpdate()
        {
            if (lidHinge == null)
            {
                return;
            }

            float target = open ? 1f : 0f;
            lidProgress = Mathf.SmoothDamp(lidProgress, target, ref lidVelocity,
                open ? 0.16f : 0.12f);
            if (Mathf.Abs(lidProgress - target) < 0.001f)
            {
                lidProgress = target;
                lidVelocity = 0f;
            }
            lidHinge.localRotation = Quaternion.Slerp(closedLidRotation, openedLidRotation, lidProgress);
        }

        private void OnDisable()
        {
            if (open)
            {
                Close();
            }
            if (lidHinge != null)
            {
                lidProgress = 0f;
                lidVelocity = 0f;
                lidHinge.localRotation = closedLidRotation;
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
                ui.SetPanelVisible(Time.time - openedAt >= panelDelay &&
                    (lidHinge == null || lidProgress >= 0.98f));
                ui.Refresh(table, GetInventory());
            }
        }

        private void Open()
        {
            open = true;
            openedAt = Time.time;
            IsBlockingInput = true;
            if (lidAnimator != null && lidHinge == null)
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
            if (lidAnimator != null && lidHinge == null)
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
