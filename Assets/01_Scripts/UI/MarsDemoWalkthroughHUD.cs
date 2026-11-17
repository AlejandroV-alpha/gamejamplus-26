using System;
using GameJam.Combat;
using GameJam.Environment.Mars;
using GameJam.Inventory;
using GameJam.Multiplayer;
using GameJam.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>
    /// Walkthrough dinámico e interactivo para la demo y exposición de Marte (~5 min).
    /// Mantiene la estética idéntica a PlayerHUD (Ink, Accent, Cream) y guía al jugador paso a paso.
    /// </summary>
    public sealed class MarsDemoWalkthroughHUD : MonoBehaviour
    {
        public enum DemoPhase
        {
            TrollAndControls = 0,
            DuneMining = 1,
            CraftAndRepair = 2,
            SwarmDefense = 3,
            EvacuationVictory = 4
        }

        private static readonly Color32 Ink = new Color32(13, 23, 29, 235);
        private static readonly Color32 Accent = new Color32(104, 224, 198, 255);
        private static readonly Color32 Cream = new Color32(239, 228, 200, 255);
        private static readonly Color32 Orange = new Color32(255, 178, 102, 255);
        private static readonly Color32 Muted = new Color32(150, 190, 210, 255);
        private static readonly Color32 Green = new Color32(64, 255, 173, 255);

        private MarsFusionAvatar avatar;
        private MarsFusionSession session;
        private TabletMissionTracker tracker;
        private GameJam.Inventory.Inventory inventory;
        private PlayerSandstorm sandstorm;

        private CanvasGroup group;
        private RectTransform panel;
        private TMP_Text titleText, badgeText, bodyText, footerText;
        private Outline outline;
        private Image background;

        private DemoPhase currentPhase = DemoPhase.TrollAndControls;
        private DemoPhase announcedPhase = (DemoPhase)(-1);
        private float demoStartTime;
        private float swarmStartTime = -1f;
        private const float SwarmDefenseDuration = 80f;

        // Interactive Phase 1 checks
        private bool didMove, didShove, didFlop, didHonk, didTablet;
        private bool expanded = true;
        private float nextTick;

        public void Build(MarsFusionAvatar source, RectTransform parent, TMP_FontAsset font)
        {
            avatar = source;
            session = FindAnyObjectByType<MarsFusionSession>();
            inventory = source.GetComponent<GameJam.Inventory.Inventory>();
            sandstorm = source.GetComponent<PlayerSandstorm>();
            demoStartTime = Time.time;

            panel = new GameObject("Mars Demo Walkthrough Guide", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(24, -24);
            panel.sizeDelta = new Vector2(440, 280);

            background = panel.gameObject.AddComponent<Image>();
            background.color = Ink;
            background.raycastTarget = false;

            outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(104, 224, 198, 140);
            outline.effectDistance = new Vector2(1, -1);

            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            // Accent line on top
            var topBar = new GameObject("TopAccent", typeof(RectTransform)).GetComponent<RectTransform>();
            topBar.SetParent(panel, false);
            topBar.anchorMin = new Vector2(0, 1);
            topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.sizeDelta = new Vector2(0, 3);
            var barImg = topBar.gameObject.AddComponent<Image>();
            barImg.color = Accent;
            barImg.raycastTarget = false;

            titleText = CreateText("Title", 10, 24, 15, font, FontStyles.Bold, Accent);
            badgeText = CreateText("Badge", 34, 20, 12, font, FontStyles.Normal, Orange);
            bodyText = CreateText("Body", 58, 185, 12, font, FontStyles.Normal, Cream);
            footerText = CreateText("Footer", 252, 22, 11, font, FontStyles.Normal, Muted);

            LocateTracker();
            UpdateUI();
        }

        private TMP_Text CreateText(string name, float top, float height, int size, TMP_FontAsset font, FontStyles style, Color32 color)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(-28, height);

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = size;
            text.fontStyle = style;
            text.enableAutoSizing = false;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private void LocateTracker()
        {
            if (tracker != null && tracker.IsLive) return;
            if (avatar != null && avatar.LocalUI != null && avatar.LocalUI.Mission != null)
            {
                tracker = avatar.LocalUI.Mission;
            }
            if (tracker == null)
            {
                tracker = FindAnyObjectByType<TabletMissionTracker>();
            }
        }

        private void Update()
        {
            if (avatar == null || !avatar.IsNetworkReady) return;

            LocateTracker();
            PollInputs();
            EvaluatePhases();

            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.12f;
                UpdateUI();
            }
        }

        private void PollInputs()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f1Key.wasPressedThisFrame)
            {
                expanded = !expanded;
                UpdateUI();
            }

            if (currentPhase == DemoPhase.TrollAndControls)
            {
                if (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed)
                    didMove = true;
                if (kb.fKey.wasPressedThisFrame)
                    didShove = true;
                if (kb.yKey.wasPressedThisFrame)
                    didFlop = true;
                if (kb.hKey.wasPressedThisFrame)
                    didHonk = true;
                if (kb.tabKey.wasPressedThisFrame || (avatar != null && avatar.TabletOpen))
                    didTablet = true;
            }
        }

        private int GetTotalCarriedMinerals()
        {
            if (inventory == null) return 0;
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var item = inventory.GetSlotMaterial(i);
                if (item != null && item.kind == CarriedItemKind.Material)
                    total++;
            }
            return total;
        }

        private void EvaluatePhases()
        {
            float elapsed = Time.time - demoStartTime;
            int carriedOre = GetTotalCarriedMinerals();
            int crafted = tracker != null ? tracker.CraftedCount : 0;
            int installed = tracker != null ? tracker.InstalledCount : 0;

            // Phase 1 -> Phase 2
            if (currentPhase == DemoPhase.TrollAndControls)
            {
                bool allTrollDone = didMove && didShove && didFlop && didHonk && didTablet;
                if (allTrollDone || elapsed > 45f || carriedOre > 0 || crafted > 0)
                {
                    SetPhase(DemoPhase.DuneMining);
                }
            }
            // Phase 2 -> Phase 3
            else if (currentPhase == DemoPhase.DuneMining)
            {
                if (carriedOre >= 3 || crafted > 0 || installed > 0 || elapsed > 120f)
                {
                    SetPhase(DemoPhase.CraftAndRepair);
                }
            }
            // Phase 3 -> Phase 4
            else if (currentPhase == DemoPhase.CraftAndRepair)
            {
                if (installed >= 2 || (crafted >= 3 && installed >= 1) || elapsed > 200f)
                {
                    SetPhase(DemoPhase.SwarmDefense);
                    swarmStartTime = Time.time;
                }
            }
            // Phase 4 -> Phase 5
            else if (currentPhase == DemoPhase.SwarmDefense)
            {
                bool timeSurvived = swarmStartTime > 0 && (Time.time - swarmStartTime >= SwarmDefenseDuration);
                bool shipRepaired = installed >= 3;
                if (shipRepaired || timeSurvived)
                {
                    SetPhase(DemoPhase.EvacuationVictory);
                }
            }
        }

        private void SetPhase(DemoPhase newPhase)
        {
            if (currentPhase == newPhase && announcedPhase == newPhase) return;
            currentPhase = newPhase;

            if (announcedPhase != newPhase)
            {
                announcedPhase = newPhase;
                TriggerPhaseCallout(newPhase);
            }
        }

        private void TriggerPhaseCallout(DemoPhase phase)
        {
            if (avatar == null) return;
            Vector3 pos = avatar.transform.position + Vector3.up * 2f;

            switch (phase)
            {
                case DemoPhase.DuneMining:
                    EnemyDamagePopup.ShowCallout(pos, "¡FASE 2: MINERÍA EN LAS DUNAS!", "RECOLECCIÓN", new Color(0.4f, 0.9f, 1f), 1.25f);
                    break;
                case DemoPhase.CraftAndRepair:
                    EnemyDamagePopup.ShowCallout(pos, "¡FASE 3: FABRICACIÓN & REPARACIÓN!", "BASE NAVE", new Color(1f, 0.75f, 0.2f), 1.25f);
                    break;
                case DemoPhase.SwarmDefense:
                    EnemyDamagePopup.ShowCallout(pos, "¡ALERTA ROJA: ENJAMBRE ALFA DESPERTÓ!", "SUPERVIVENCIA", new Color(1f, 0.25f, 0.25f), 1.35f);
                    break;
                case DemoPhase.EvacuationVictory:
                    EnemyDamagePopup.ShowCallout(pos, "¡REACTOR ONLINE! ¡MISIÓN DEMO SUPERADA!", "VICTORIA", new Color(0.2f, 1f, 0.5f), 1.45f);
                    break;
            }
        }

        private void UpdateUI()
        {
            if (group == null || panel == null) return;

            bool visible = avatar != null && avatar.IsNetworkReady && avatar.IsAlive
                && !avatar.TabletOpen && (session == null || !session.MenuOpen)
                && !SceneTransitionVideo.IsBlockingInput && !LobbyDepartureEffects.IsBlockingInput;

            group.alpha = visible ? 1f : 0f;
            if (!visible) return;

            // Responsive scale
            var parentRect = (RectTransform)panel.parent;
            if (parentRect != null)
            {
                float scale = Mathf.Min(1f, Mathf.Max(0.7f, (parentRect.rect.height - 48f) / 360f));
                panel.localScale = Vector3.one * scale;
            }

            if (!expanded)
            {
                panel.sizeDelta = new Vector2(440, 68);
                titleText.text = "OBJETIVOS DE MISIÓN · MARTE [DEMO]";
                badgeText.text = $"<color=#68e0c6>Fase {(int)currentPhase + 1}/5 activa</color> · [F1] Expandir";
                bodyText.gameObject.SetActive(false);
                footerText.gameObject.SetActive(false);
                return;
            }

            panel.sizeDelta = new Vector2(440, 280);
            bodyText.gameObject.SetActive(true);
            footerText.gameObject.SetActive(true);

            titleText.text = "OBJETIVOS DE MISIÓN · MARTE [DEMO]";
            footerText.text = "[F1] Minimizar  ·  [Tab] Tableta  ·  [H] Bocina";

            int carriedOre = GetTotalCarriedMinerals();
            int crafted = tracker != null ? tracker.CraftedCount : 0;
            int installed = tracker != null ? tracker.InstalledCount : 0;

            switch (currentPhase)
            {
                case DemoPhase.TrollAndControls:
                    badgeText.text = "FASE 1/5: FAMILIARIZACIÓN & FÍSICAS CO-OP";
                    outline.effectColor = new Color32(104, 224, 198, 140);
                    bodyText.text =
                        "Aterrizaje en campamento base. Conoce tus mecánicas:\n" +
                        FormatCheck(didMove, "Movimiento y carrera", "[W,A,S,D] / [Shift]") + "\n" +
                        FormatCheck(didShove, "Empujón físico ragdoll", "[F] (Derriba a amigos)") + "\n" +
                        FormatCheck(didFlop, "Buceo / Flop táctico", "[Y] (Ragdoll instantáneo)") + "\n" +
                        FormatCheck(didHonk, "Bocina marciana", "[H] (Atrae a Rek'Sai)") + "\n" +
                        FormatCheck(didTablet, "Consultar planos", "[Tab] (Tableta de nave)");
                    break;

                case DemoPhase.DuneMining:
                    badgeText.text = "FASE 2/5: MINERÍA EN LAS DUNAS";
                    outline.effectColor = new Color32(255, 178, 102, 140);
                    string oreState = carriedOre >= 3 ? "<color=#40ffad>¡3/3 Listos!</color>" : $"Recolectados: {carriedOre}/3";
                    bodyText.text =
                        "Explora las dunas de arena roja y reúne minerales para el reactor:\n" +
                        FormatCheck(carriedOre >= 3, "Extraer 3 Minerales", oreState) + "\n\n" +
                        "<color=#68e0c6>• Menas seguras:</color> Esparcidas en las dunas cercanas.\n" +
                        "<color=#ffb266>• Tormenta:</color> Si arrecia la arena, tírate al suelo con [Y].\n" +
                        "<color=#96bed2>• Perímetro:</color> Los monstruos merodean en el desierto exterior.";
                    break;

                case DemoPhase.CraftAndRepair:
                    badgeText.text = "FASE 3/5: FABRICACIÓN & REPARACIÓN";
                    outline.effectColor = new Color32(255, 209, 102, 140);
                    bodyText.text =
                        "Lleva los materiales a la base central de la nave (0, 14, -19):\n" +
                        FormatCheck(crafted >= 3, "Fabricar 3 componentes", $"Mesa de crafteo ({crafted}/3)") + "\n" +
                        FormatCheck(installed >= 3, "Instalar en la nave", $"Mantén [E] en el casco ({installed}/3)") + "\n\n" +
                        "<color=#68e0c6>• Cooperación:</color> Lleven piezas juntos para acelerar el trabajo.\n" +
                        "<color=#ff6b6b>• ¡Atención!</color> El reactor atraerá al Enjambre Alfa.";
                    break;

                case DemoPhase.SwarmDefense:
                    badgeText.text = "<color=#ff5252>FASE 4/5: ¡DEFENSA ANTE EL ENJAMBRE ALFA!</color>";
                    outline.effectColor = new Color32(255, 82, 82, 180);
                    float remain = swarmStartTime > 0 ? Mathf.Max(0f, SwarmDefenseDuration - (Time.time - swarmStartTime)) : SwarmDefenseDuration;
                    int mins = Mathf.FloorToInt(remain / 60f);
                    int secs = Mathf.FloorToInt(remain % 60f);

                    bodyText.text =
                        $"<color=#ff5252><b>¡REACTOR CARGANDO HIPERIMPULSO: {mins:00}:{secs:00}!</b></color>\n" +
                        "<color=#ffd166>Defiendan el campamento de los titanes:</color>\n" +
                        "• <b>El Cuernudo:</b> Acechador invisible. ¡Dispárale para revelarlo!\n" +
                        "• <b>Frost Nightmare:</b> Titán con azote físico demoledor.\n" +
                        "• <b>Gusanos:</b> Si atrapan a un amigo entre sus dientes, ¡disparen en equipo!\n" +
                        "• <b>Venomwing & Rek'Sai:</b> Ácido aéreo y embestida subterránea.";
                    break;

                case DemoPhase.EvacuationVictory:
                    badgeText.text = "<color=#40ffad>FASE 5/5: ¡EVACUACIÓN EXITOSA!</color>";
                    outline.effectColor = new Color32(64, 255, 173, 180);
                    bodyText.text =
                        "<color=#40ffad><b>★ REACTOR AL 100% · SALTO HIPERESPACIAL LISTO ★</b></color>\n\n" +
                        "¡Todos los componentes reparados y enjambre repelido!\n" +
                        "• <b>Suban a la nave</b> para el despegue inmediato.\n" +
                        "• Físicas, co-op troll, combate y supervivencia completados.\n\n" +
                        "<color=#68e0c6>★ DEMO SHOWCASE LISTA PARA LA EXPOSICIÓN ★</color>";
                    break;
            }
        }

        private static string FormatCheck(bool done, string label, string detail)
        {
            string check = done ? "<color=#40ffad><b>[✓]</b></color>" : "<color=#8fa3b0><b>[ ]</b></color>";
            string text = done ? $"<color=#a8dfc8><s>{label}</s></color>" : $"<color=#efe4c8><b>{label}</b></color>";
            return $"{check} {text} · <color=#96bed2>{detail}</color>";
        }
    }
}
