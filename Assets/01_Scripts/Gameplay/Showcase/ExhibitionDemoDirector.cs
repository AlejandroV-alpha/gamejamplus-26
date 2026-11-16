using GameJam.Combat;
using GameJam.Environment.Neptune;
using GameJam.Inventory;
using GameJam.Localization;
using GameJam.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Showcase
{
    /// <summary>
    /// Live Exhibition & Multiplayer Troll Director.
    /// Provides instant showcase hotkeys (F3-F9) and hilarious multiplayer troll actions (B, X, Z, C)
    /// with zero per-frame allocations and cohesive Sci-Fi HUD presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExhibitionDemoDirector : MonoBehaviour
    {
        private static readonly Color32 PanelBg = new Color32(8, 16, 26, 232);
        private static readonly Color32 NeonGreen = new Color32(81, 229, 45, 255);
        private static readonly Color32 NeonCyan = new Color32(104, 224, 198, 255);
        private static readonly Color32 WarmGold = new Color32(255, 196, 75, 255);
        private static readonly Color32 SoftWhite = new Color32(235, 244, 250, 255);

        private MarsFusionAvatar avatar;
        private MarsFusionSession session;
        private PlayerInventory inventory;
        private Gun gun;
        private PlayerHealth playerHealth;
        private PlayerCold playerCold;

        private CanvasGroup group;
        private RectTransform panelRect;
        private TMP_Text headerText;
        private TMP_Text bodyText;
        private TMP_Text footerText;
        private Outline borderOutline;

        private bool expanded;
        private bool lastLowGrav;
        private bool lastTurbo;
        private bool lastFakeDead;

        public void Build(MarsFusionAvatar source, RectTransform safeAreaRoot, TMP_FontAsset font)
        {
            avatar = source;
            session = FindAnyObjectByType<MarsFusionSession>();
            inventory = source.GetComponent<PlayerInventory>();
            gun = source.GetComponentInChildren<Gun>(true);
            playerHealth = source.GetComponent<PlayerHealth>();
            playerCold = source.GetComponent<PlayerCold>();

            panelRect = new GameObject("Exhibition Demo Director", typeof(RectTransform)).GetComponent<RectTransform>();
            panelRect.SetParent(safeAreaRoot, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -18f);
            panelRect.sizeDelta = new Vector2(650f, 44f);

            Image bg = panelRect.gameObject.AddComponent<Image>();
            bg.color = PanelBg;
            bg.raycastTarget = false;

            borderOutline = panelRect.gameObject.AddComponent<Outline>();
            borderOutline.effectColor = new Color32(81, 229, 45, 175);
            borderOutline.effectDistance = new Vector2(1.5f, -1.5f);

            group = panelRect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            headerText = CreateLabel("Header", 8f, 30f, 17, font, TextAlignmentOptions.Center);
            headerText.color = NeonGreen;
            headerText.fontStyle = FontStyles.Bold;

            bodyText = CreateLabel("Body", 42f, 228f, 16, font, TextAlignmentOptions.TopLeft);
            bodyText.color = SoftWhite;

            footerText = CreateLabel("Footer", 272f, 26f, 15, font, TextAlignmentOptions.Center);
            footerText.color = WarmGold;

            GameLocalization.OnLanguageChanged += RefreshText;
            RefreshText();
        }

        private TMP_Text CreateLabel(string labelName, float topOffset, float height, int fontSize, TMP_FontAsset font, TextAlignmentOptions align)
        {
            RectTransform rt = new GameObject(labelName, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(panelRect, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
            rt.sizeDelta = new Vector2(-32f, height);

            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.richText = true;
            return tmp;
        }

        private void Update()
        {
            if (avatar == null || !avatar.IsNetworkReady || !avatar.HasStateAuthority || group == null) return;

            bool blocked = (session != null && session.MenuOpen)
                || SceneTransitionVideo.IsBlockingInput
                || PortalWhiteTransition.IsBlockingInput
                || LobbyDepartureEffects.IsBlockingInput
                || GameJam.Crafting.CraftingBox.IsBlockingInput;

            bool hudVisible = !blocked && !avatar.TabletOpen;
            group.alpha = hudVisible ? 1f : 0f;
            if (!hudVisible || VoicePreferences.CapturingBinding) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f3Key.wasPressedThisFrame)
            {
                expanded = !expanded;
                RefreshText();
            }

            // Multiplayer Troll Hotkeys
            if (kb.bKey.wasPressedThisFrame) avatar.RequestMeleeBoop();
            if (kb.xKey.wasPressedThisFrame) avatar.TriggerJetpackDash();
            if (kb.zKey.wasPressedThisFrame) avatar.TriggerSkillIssueSiren();
            if (kb.cKey.wasPressedThisFrame) avatar.ToggleFakeDeath();

            // Live Exhibition Showcase Hotkeys
            if (kb.f4Key.wasPressedThisFrame) TriggerExhibitionKit();
            if (kb.f5Key.wasPressedThisFrame) ToggleLowGravity();
            if (kb.f6Key.wasPressedThisFrame) ToggleTurboOverdrive();
            if (kb.f7Key.wasPressedThisFrame) TriggerDamageAndLootFountain();
            if (kb.f8Key.wasPressedThisFrame) HologramCombatDummy.SpawnInFrontOf(avatar.transform);
            if (kb.f9Key.wasPressedThisFrame) RestorePlayerState();

            if (lastLowGrav != avatar.LowGravityMode || lastTurbo != avatar.TurboOverdriveMode || lastFakeDead != avatar.IsFakeDead)
            {
                lastLowGrav = avatar.LowGravityMode;
                lastTurbo = avatar.TurboOverdriveMode;
                lastFakeDead = avatar.IsFakeDead;
                RefreshText();
            }
        }

        private void TriggerExhibitionKit()
        {
            if (inventory == null) inventory = avatar.GetComponent<PlayerInventory>();
            if (gun == null) gun = avatar.GetComponentInChildren<Gun>(true);

            inventory?.GrantExhibitionKit();
            gun?.RefillMagazineImmediate();

            Vector3 popPos = avatar.transform.position + Vector3.up * 2.0f;
            EnemyDamagePopup.ShowCallout(popPos, "KIT DE EXPO LISTO", "LOADOUT 100%", new Color(0.35f, 1f, 0.55f), 1.3f);
            HitFeedbackHUD.Loot("★ Kit Completo: Arma + Radio + Materiales");
        }

        private void ToggleLowGravity()
        {
            avatar.LowGravityMode = !avatar.LowGravityMode;
            if (avatar.LowGravityMode)
            {
                avatar.ApplyLocalShowcaseImpulse(Vector3.zero, 9.5f);
            }
            string state = avatar.LowGravityMode ? "GRAVEDAD LUNAR: ON" : "GRAVEDAD NORMAL";
            Color col = avatar.LowGravityMode ? new Color(0.4f, 0.9f, 1f) : new Color(0.85f, 0.85f, 0.9f);
            EnemyDamagePopup.ShowCallout(avatar.transform.position + Vector3.up * 2.0f, state, "ZERO-G", col, 1.3f);
            RefreshText();
        }

        private void ToggleTurboOverdrive()
        {
            avatar.TurboOverdriveMode = !avatar.TurboOverdriveMode;
            string state = avatar.TurboOverdriveMode ? "TURBO TRAJE: ON" : "TURBO TRAJE: OFF";
            Color col = avatar.TurboOverdriveMode ? new Color(1f, 0.78f, 0.2f) : new Color(0.85f, 0.85f, 0.9f);
            EnemyDamagePopup.ShowCallout(avatar.transform.position + Vector3.up * 2.0f, state, "SPEED x1.85", col, 1.3f);
            RefreshText();
        }

        private void TriggerDamageAndLootFountain()
        {
            Vector3 center = avatar.transform.position + avatar.transform.forward * 3.2f + Vector3.up * 1.4f;
            DamageKind[] kinds = { DamageKind.Normal, DamageKind.Burn, DamageKind.Bleed, DamageKind.Poison, DamageKind.Heal };
            for (int i = 0; i < kinds.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0.85f, (i % 2) * 0.35f, 0f);
                bool crit = (i == 0 || i == 2);
                EnemyDamagePopup.Show(null, Random.Range(24, 99), center + offset, kinds[i], crit);
            }
            EnemyDamagePopup.ShowCallout(center + Vector3.up * 0.9f, "¡JUICE & GAME FEEL!", "CRIT!", new Color(1f, 0.85f, 0.2f), 1.35f);

            for (int i = 0; i < 6; i++)
            {
                float ang = i * 60f * Mathf.Deg2Rad;
                Vector3 vel = new Vector3(Mathf.Cos(ang) * 4.5f, 4.2f, Mathf.Sin(ang) * 4.5f);
                Color c = (i % 2 == 0) ? new Color(0.35f, 1f, 0.65f) : new Color(1f, 0.65f, 0.2f);
                LootOrb.Spawn(center, vel, avatar.transform, null, null, c, 0.25f + i * 0.05f);
            }
            HitFeedbackHUD.Loot("+100% Game Feel & Pixel Popups");
        }

        private void RestorePlayerState()
        {
            if (playerHealth == null) playerHealth = avatar.GetComponent<PlayerHealth>();
            if (playerCold == null) playerCold = avatar.GetComponent<PlayerCold>();
            if (gun == null) gun = avatar.GetComponentInChildren<Gun>(true);

            playerHealth?.RestoreFullHealth();
            playerCold?.ResetCold();
            gun?.RefillMagazineImmediate();

            EnemyDamagePopup.Show(avatar, 100f, avatar.transform.position + Vector3.up * 1.9f, DamageKind.Heal, true);
            HitFeedbackHUD.Loot("♥ Salud, Escudo y Munición al 100%");
        }

        private void RefreshText()
        {
            if (panelRect == null) return;

            panelRect.sizeDelta = new Vector2(expanded ? 680f : 640f, expanded ? 304f : 42f);
            bodyText.gameObject.SetActive(expanded);
            footerText.gameObject.SetActive(expanded);

            string gravBadge = (avatar != null && avatar.LowGravityMode) ? "<color=#59E1FF>[ZERO-G ON]</color> " : "";
            string turboBadge = (avatar != null && avatar.TurboOverdriveMode) ? "<color=#FFC44B>[TURBO ON]</color> " : "";

            if (!expanded)
            {
                headerText.text = $"{gravBadge}{turboBadge}<b>F3</b> · PANEL EXPO & TROLLS   <color=#68E0C6>[B]</color> Empujar  <color=#68E0C6>[X]</color> Jetpack  <color=#68E0C6>[Z]</color> Skill Issue  <color=#68E0C6>[C]</color> Fingir Muerte";
                return;
            }

            headerText.text = "★ HOUSTON, WE HAVE A SKILL ISSUE — DIRECTOR DE EXPOSICIÓN (F3)";
            string gState = (avatar != null && avatar.LowGravityMode) ? "<color=#51E52D>ACTIVO</color>" : "OFF";
            string tState = (avatar != null && avatar.TurboOverdriveMode) ? "<color=#51E52D>ACTIVO</color>" : "OFF";
            string dState = (avatar != null && avatar.IsFakeDead) ? "<color=#FF6E5F>FINGIENDO</color>" : "LISTO";

            bodyText.text =
                "<color=#FFC44B><b>CAOS & TROLLEO MULTIJUGADOR (SINCRONIZADO EN RED):</b></color>\n" +
                "  <b>[B]</b> Empujón Táctico (Boop a compañeros / Dron con onda expansiva)\n" +
                "  <b>[X]</b> Propulsor Descontrolado (Turbo salto + derribo en área)\n" +
                "  <b>[Z]</b> Sirena «¡SKILL ISSUE!» (Alarma espacial + holograma en casco)\n" +
                $"  <b>[C]</b> Fingir Muerte / Trampa de Revivir ({dState}) + <b>[7-9]</b> Emotes\n\n" +
                "<color=#68E0C6><b>HERRAMIENTAS DE DEMOSTRACIÓN EN VIVO (EXPO):</b></color>\n" +
                "  <b>[F4]</b> Equipar Kit Completo (Arma + Walkie-Talkie + Materiales + Balas)\n" +
                $"  <b>[F5]</b> Gravedad Lunar / Zero-G ({gState})   ·   <b>[F6]</b> Turbo Traje ({tState})\n" +
                "  <b>[F7]</b> Fiesta de Números de Daño & Orbes   ·   <b>[F8]</b> Invocar Dron de Combate\n" +
                "  <b>[F9]</b> Curación Total + Reset de Frío y Munición (+100 HP)";

            footerText.text = "Pulsa F3 en cualquier momento para minimizar este panel durante la presentación";
        }

        private void OnDestroy()
        {
            GameLocalization.OnLanguageChanged -= RefreshText;
            if (panelRect != null) Destroy(panelRect.gameObject);
        }
    }
}
