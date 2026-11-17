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
    /// HUD minimalista, ultralegible y dinámico para la demo de Marte (~5 min).
    /// Muestra un solo objetivo activo a la vez con tipografía vectorial nítida (SDF)
    /// y sin sobrecargar la pantalla del jugador.
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

        private static readonly Color32 BgDark = new Color32(10, 18, 24, 225);
        private static readonly Color32 CyanAccent = new Color32(104, 224, 198, 255);
        private static readonly Color32 OrangeAccent = new Color32(255, 178, 102, 255);
        private static readonly Color32 YellowAccent = new Color32(255, 218, 102, 255);
        private static readonly Color32 RedAccent = new Color32(255, 82, 82, 255);
        private static readonly Color32 GreenAccent = new Color32(64, 255, 173, 255);
        private static readonly Color32 TextWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 TextMuted = new Color32(150, 182, 198, 255);

        private MarsFusionAvatar avatar;
        private MarsFusionSession session;
        private TabletMissionTracker tracker;
        private GameJam.Inventory.Inventory inventory;
        private PlayerSandstorm sandstorm;

        private CanvasGroup group;
        private RectTransform panel;
        private Image background;
        private Outline outline;
        private Image leftStrip;

        private TMP_Text headerText;
        private TMP_Text counterText;
        private TMP_Text goalText;
        private TMP_Text hintText;
        private TMP_Text minimizedText;

        private DemoPhase currentPhase = DemoPhase.TrollAndControls;
        private DemoPhase announcedPhase = (DemoPhase)(-1);
        private float demoStartTime;
        private float swarmStartTime = -1f;
        private const float SwarmDefenseDuration = 80f;

        // Step-by-step tutorial flags
        private bool didMove, didShove, didFlop, didTablet;
        private bool expanded = true;
        private float nextTick;

        public void Build(MarsFusionAvatar source, RectTransform parent, TMP_FontAsset _)
        {
            avatar = source;
            session = FindAnyObjectByType<MarsFusionSession>();
            inventory = source.GetComponent<GameJam.Inventory.Inventory>();
            sandstorm = source.GetComponent<PlayerSandstorm>();
            demoStartTime = Time.time;

            // Prioritize crisp vector SDF default font over retro pixel fonts
            // Misma fuente pixel que el resto del HUD.
            TMP_FontAsset crispFont = _ != null ? _ : TMP_Settings.defaultFontAsset;

            panel = new GameObject("Mars Walkthrough HUD Pill", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(parent, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1);
            panel.anchoredPosition = new Vector2(24, -20);
            panel.sizeDelta = new Vector2(390, 72);
            // Mas grande para leerse bien: escala todo el panel (texto incluido) sin rehacer su diseño.
            panel.localScale = Vector3.one * 1.45f;

            background = panel.gameObject.AddComponent<Image>();
            background.color = BgDark;
            ExpeditionSprites.Skin(background, "panel");
            background.raycastTarget = false;

            outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(104, 224, 198, 110);
            outline.effectDistance = new Vector2(1, -1);

            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            // Colored accent bar on the left edge
            var stripGo = new GameObject("LeftAccentStrip", typeof(RectTransform));
            stripGo.transform.SetParent(panel, false);
            var stripRect = stripGo.GetComponent<RectTransform>();
            stripRect.anchorMin = new Vector2(0, 0);
            stripRect.anchorMax = new Vector2(0, 1);
            stripRect.pivot = new Vector2(0, 0.5f);
            stripRect.anchoredPosition = new Vector2(0, 0);
            stripRect.sizeDelta = new Vector2(4, 0);
            leftStrip = stripGo.AddComponent<Image>();
            leftStrip.color = CyanAccent;
            leftStrip.raycastTarget = false;

            // Header text (Phase title)
            headerText = CreateLabel("Header", panel, crispFont, 11, FontStyles.Bold, CyanAccent,
                new Vector2(16, -6), new Vector2(260, 18), TextAlignmentOptions.Left);

            // Counter badge (right aligned on top)
            counterText = CreateLabel("Counter", panel, crispFont, 12, FontStyles.Bold, YellowAccent,
                new Vector2(-14, -6), new Vector2(95, 18), TextAlignmentOptions.Right);
            var counterRect = counterText.rectTransform;
            counterRect.anchorMin = new Vector2(1, 1);
            counterRect.anchorMax = new Vector2(1, 1);
            counterRect.pivot = new Vector2(1, 1);

            // Goal text (main objective)
            goalText = CreateLabel("Goal", panel, crispFont, 13, FontStyles.Bold, TextWhite,
                new Vector2(16, -26), new Vector2(360, 22), TextAlignmentOptions.Left);

            // Hint text (control instructions or tips)
            hintText = CreateLabel("Hint", panel, crispFont, 10, FontStyles.Normal, TextMuted,
                new Vector2(16, -49), new Vector2(360, 17), TextAlignmentOptions.Left);

            // Minimized single-line text
            minimizedText = CreateLabel("Minimized", panel, crispFont, 11, FontStyles.Normal, TextWhite,
                new Vector2(14, -8), new Vector2(250, 18), TextAlignmentOptions.Left);
            minimizedText.gameObject.SetActive(false);

            LocateTracker();
            UpdateUI();
        }

        private static TMP_Text CreateLabel(string name, RectTransform parent, TMP_FontAsset font, int size,
            FontStyles style, Color32 color, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = font != null ? font : TMP_Settings.defaultFontAsset;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.enableWordWrapping = false;
            txt.overflowMode = TextOverflowModes.Ellipsis;
            txt.raycastTarget = false;
            txt.richText = true;
            return txt;
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
                bool allTrollDone = didMove && didShove && didFlop && didTablet;
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

            int carriedOre = GetTotalCarriedMinerals();
            int crafted = tracker != null ? tracker.CraftedCount : 0;
            int installed = tracker != null ? tracker.InstalledCount : 0;

            string headerStr = "";
            string counterStr = "";
            string goalStr = "";
            string hintStr = "";
            Color32 phaseColor = CyanAccent;
            Color32 counterColor = YellowAccent;

            switch (currentPhase)
            {
                case DemoPhase.TrollAndControls:
                    phaseColor = CyanAccent;
                    headerStr = "FASE 1/5 · CONTROLES CO-OP";
                    if (!didMove)
                    {
                        counterStr = "[1/4]";
                        goalStr = "Muévete por el campamento";
                        hintStr = "Usa [W,A,S,D] para moverte y [Shift] para correr";
                    }
                    else if (!didShove)
                    {
                        counterStr = "[2/4]";
                        goalStr = "¡Empuja a tu compañero!";
                        hintStr = "Presiona [F] para empujón físico ragdoll";
                    }
                    else if (!didFlop)
                    {
                        counterStr = "[3/4]";
                        goalStr = "Tírate al suelo (Flop)";
                        hintStr = "Presiona [Y] para activar ragdoll táctico";
                    }
                    else if (!didTablet)
                    {
                        counterStr = "[4/4]";
                        goalStr = "Consulta los planos de la nave";
                        hintStr = "Presiona [Tab] para abrir tu tableta";
                    }
                    else
                    {
                        counterStr = "[✓]";
                        counterColor = GreenAccent;
                        goalStr = "¡Listo! Ve a las dunas a minar";
                        hintStr = "Explora los alrededores del campamento";
                    }
                    break;

                case DemoPhase.DuneMining:
                    phaseColor = OrangeAccent;
                    headerStr = "FASE 2/5 · MINERÍA EN LAS DUNAS";
                    counterStr = $"[{carriedOre}/3]";
                    counterColor = carriedOre >= 3 ? GreenAccent : YellowAccent;
                    goalStr = "Extrae 3 minerales en las dunas";
                    hintStr = "Explora la arena roja · Presiona [E] para recoger";
                    break;

                case DemoPhase.CraftAndRepair:
                    phaseColor = YellowAccent;
                    if (crafted < 3)
                    {
                        headerStr = "FASE 3/5 · FABRICACIÓN";
                        counterStr = $"[{crafted}/3]";
                        counterColor = YellowAccent;
                        goalStr = "Fabrica 3 piezas en la mesa de crafteo";
                        hintStr = "Usa la mesa de crafteo en el campamento base";
                    }
                    else
                    {
                        headerStr = "FASE 3/5 · REPARACIÓN";
                        counterStr = $"[{installed}/3]";
                        counterColor = installed >= 3 ? GreenAccent : YellowAccent;
                        goalStr = "Instala los componentes en la nave";
                        hintStr = "Acércate al núcleo de la nave y presiona [Q]";
                    }
                    break;

                case DemoPhase.SwarmDefense:
                    phaseColor = RedAccent;
                    float remain = swarmStartTime > 0 ? Mathf.Max(0f, SwarmDefenseDuration - (Time.time - swarmStartTime)) : SwarmDefenseDuration;
                    int mins = Mathf.FloorToInt(remain / 60f);
                    int secs = Mathf.FloorToInt(remain % 60f);
                    headerStr = "FASE 4/5 · ¡ENJAMBRE ALFA!";
                    counterStr = $"{mins:00}:{secs:00}";
                    counterColor = RedAccent;
                    goalStr = "¡Defiende el campamento del enjambre!";
                    hintStr = "Dispara a los monstruos con [Click Izq] · Coopera";
                    break;

                case DemoPhase.EvacuationVictory:
                    phaseColor = GreenAccent;
                    headerStr = "FASE 5/5 · ¡MISIÓN SUPERADA!";
                    counterStr = "[ÉXITO]";
                    counterColor = GreenAccent;
                    goalStr = "¡Reactor online! Sube a la nave";
                    hintStr = "¡Demostración completada con éxito!";
                    break;
            }

            if (leftStrip != null) leftStrip.color = phaseColor;
            if (outline != null) outline.effectColor = new Color32(phaseColor.r, phaseColor.g, phaseColor.b, 90);

            if (!expanded)
            {
                panel.sizeDelta = new Vector2(280, 32);
                headerText.gameObject.SetActive(false);
                counterText.gameObject.SetActive(false);
                goalText.gameObject.SetActive(false);
                hintText.gameObject.SetActive(false);
                minimizedText.gameObject.SetActive(true);
                minimizedText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(phaseColor)}>Fase {(int)currentPhase + 1}/5</color> {counterStr} · [F1] Ver";
                return;
            }

            panel.sizeDelta = new Vector2(390, 72);
            // Mas grande para leerse bien: escala todo el panel (texto incluido) sin rehacer su diseño.
            panel.localScale = Vector3.one * 1.45f;
            headerText.gameObject.SetActive(true);
            counterText.gameObject.SetActive(true);
            goalText.gameObject.SetActive(true);
            hintText.gameObject.SetActive(true);
            minimizedText.gameObject.SetActive(false);

            headerText.text = headerStr;
            headerText.color = phaseColor;
            counterText.text = counterStr;
            counterText.color = counterColor;
            goalText.text = goalStr;
            hintText.text = $"{hintStr}  ·  <color=#6f8a9a>[F1] Ocultar</color>";
        }
    }
}
