using System;
using System.Collections.Generic;
using GameJam.Combat;
using GameJam.Environment.Mars;
using GameJam.Inventory;
using GameJam.Localization;
using GameJam.Multiplayer;
using GameJam.Ship;
using GameJam.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>
    /// Dynamic, interactive ~5-minute walkthrough HUD for the Mars Gameplay Demo & Exhibition.
    /// Tracks live co-op troll mechanics, dune mining, craft & ship repair, swarm defense, and victory.
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
        private int previousInventoryCount = -1;

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
            panel.sizeDelta = new Vector2(495, 345);

            background = panel.gameObject.AddComponent<Image>();
            background.color = new Color32(9, 17, 27, 245);
            background.raycastTarget = false;

            outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(104, 224, 198, 210);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 1f;

            titleText = CreateText("Title", 14, 28, 17, font, FontStyles.Bold, new Color32(104, 224, 198, 255));
            badgeText = CreateText("Badge", 42, 24, 13, font, FontStyles.Normal, new Color32(255, 185, 110, 255));
            bodyText = CreateText("Body", 70, 225, 13, font, FontStyles.Normal, new Color32(235, 244, 250, 255));
            footerText = CreateText("Footer", 305, 26, 12, font, FontStyles.Italic, new Color32(150, 190, 210, 255));

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
            rect.sizeDelta = new Vector2(-36, height);

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
            else
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
                nextTick = Time.time + 0.15f;
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

            // Phase 1 interaction monitors
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
            var list = inventory.GetItemList();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].material != null)
                        total += list[i].amount;
                }
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
                if (allTrollDone || elapsed > 50f || carriedOre > 0 || crafted > 0)
                {
                    SetPhase(DemoPhase.DuneMining);
                }
            }
            // Phase 2 -> Phase 3
            else if (currentPhase == DemoPhase.DuneMining)
            {
                if (carriedOre >= 3 || crafted > 0 || installed > 0 || elapsed > 130f)
                {
                    SetPhase(DemoPhase.CraftAndRepair);
                }
            }
            // Phase 3 -> Phase 4
            else if (currentPhase == DemoPhase.CraftAndRepair)
            {
                if (installed >= 2 || (crafted >= 3 && installed >= 1) || elapsed > 210f)
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
                    EnemyDamagePopup.ShowCallout(pos, "¡FASE 2: MINERÍA EN LAS DUNAS!", "RECOLECCIÓN", new Color(0.4f, 0.9f, 1f), 1.35f);
                    break;
                case DemoPhase.CraftAndRepair:
                    EnemyDamagePopup.ShowCallout(pos, "¡FASE 3: FABRICACIÓN & REPARACIÓN!", "BASE NAVE", new Color(1f, 0.75f, 0.2f), 1.35f);
                    break;
                case DemoPhase.SwarmDefense:
                    EnemyDamagePopup.ShowCallout(pos, "¡ALERTA ROJA: ENJAMBRE ALFA DESPERTÓ!", "SUPERVIVENCIA", new Color(1f, 0.25f, 0.25f), 1.45f);
                    break;
                case DemoPhase.EvacuationVictory:
                    EnemyDamagePopup.ShowCallout(pos, "¡REACTOR ONLINE! ¡MISIÓN DEMO SUPERADA!", "VICTORIA", new Color(0.2f, 1f, 0.5f), 1.55f);
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
                panel.sizeDelta = new Vector2(495, 76);
                titleText.text = "DEMO GAMEPLAY MARTE [5 MIN]";
                badgeText.text = $"<color=#68e0c6>Fase {(int)currentPhase + 1}/5 activa</color> · [F1] Expandir";
                bodyText.gameObject.SetActive(false);
                footerText.gameObject.SetActive(false);
                return;
            }

            panel.sizeDelta = new Vector2(495, 345);
            bodyText.gameObject.SetActive(true);
            footerText.gameObject.SetActive(true);

            titleText.text = "MISIÓN DEMO: ESCAPE DE MARTE · EXPOSICIÓN";
            footerText.text = "[F1] Ocultar / Expandir  ·  [Tab] Tableta de Planos  ·  [H] Bocina";

            int carriedOre = GetTotalCarriedMinerals();
            int crafted = tracker != null ? tracker.CraftedCount : 0;
            int installed = tracker != null ? tracker.InstalledCount : 0;

            switch (currentPhase)
            {
                case DemoPhase.TrollAndControls:
                    badgeText.text = "<color=#68e0c6>FASE 1/5: FAMILIARIZACIÓN & FÍSICAS TROLL (0:00 - 0:45)</color>";
                    outline.effectColor = new Color32(104, 224, 198, 220);
                    bodyText.text =
                        "¡Aterrizaje forzoso en Marte! Domina tus controles y mecánicas co-op:\n" +
                        FormatCheck(didMove, "Movimiento y Carrera", "[W,A,S,D] + [Shift]") + "\n" +
                        FormatCheck(didShove, "Empujón Físico Troll", "[F] (Derriba amigos con ragdoll)") + "\n" +
                        FormatCheck(didFlop, "Buceo / Flop Táctico", "[Y] (Ragdoll instantáneo para esquivar)") + "\n" +
                        FormatCheck(didHonk, "Bocina Marciana", "[H] (Atrae a Rek'Sai bajo tierra)") + "\n" +
                        FormatCheck(didTablet, "Consultar Tableta", "[Tab] (Planos de piezas de la nave)");
                    break;

                case DemoPhase.DuneMining:
                    badgeText.text = "<color=#ffa659>FASE 2/5: MINERÍA EN LAS DUNAS MARCIANAS (0:45 - 2:00)</color>";
                    outline.effectColor = new Color32(255, 166, 89, 220);
                    string oreState = carriedOre >= 3 ? "<color=#68e0c6>¡Listo para fabricar!</color>" : $"Recolectados: {carriedOre}/3";
                    bodyText.text =
                        "Explora las dunas de arena roja y reúne minerales para el reactor:\n" +
                        FormatCheck(carriedOre >= 3, "Extraer Minerales Marcianos", oreState) + "\n" +
                        "<color=#ffd166>• Duna Scrap:</color> Nodos de menas abundantes alrededor del cráter.\n" +
                        "<color=#ff6b6b>• ¡Alerta Tormenta!</color> Si arrecia el viento, tírate al suelo o sufrirás ragdoll.\n" +
                        "<color=#ff9f43>• Depredadores:</color> Avispa Venomwing patrulla el cielo; Rek'Sai oye tus pasos.";
                    break;

                case DemoPhase.CraftAndRepair:
                    badgeText.text = "<color=#ffd166>FASE 3/5: FABRICACIÓN & REPARACIÓN (2:00 - 3:30)</color>";
                    outline.effectColor = new Color32(255, 209, 102, 220);
                    bodyText.text =
                        "Lleva los materiales a la base central de la nave espacial (0, 14, -19):\n" +
                        FormatCheck(crafted >= 3, "Fabricar 3 Componentes", $"Mesa de Crafteo ({crafted}/3 fabricados)") + "\n" +
                        FormatCheck(installed >= 3, "Instalar en la Nave", $"Mantén [E] en el casco ({installed}/3 instalados)") + "\n" +
                        "<color=#68e0c6>• Co-op:</color> Los compañeros pueden coordinarse para llevar piezas.\n" +
                        "<color=#ff6b6b>• ¡Atención!</color> Activar el reactor emitirá un pulso que atraerá al Enjambre Alfa.";
                    break;

                case DemoPhase.SwarmDefense:
                    badgeText.text = "<color=#ff5252>FASE 4/5: ¡DEFENSA ANTE EL ENJAMBRE ALFA! (3:30 - 5:00)</color>";
                    outline.effectColor = new Color32(255, 82, 82, 240);
                    float remain = swarmStartTime > 0 ? Mathf.Max(0f, SwarmDefenseDuration - (Time.time - swarmStartTime)) : SwarmDefenseDuration;
                    int mins = Mathf.FloorToInt(remain / 60f);
                    int secs = Mathf.FloorToInt(remain % 60f);
                    string timerStr = $"{mins:00}:{secs:00}";

                    bodyText.text =
                        $"<color=#ff5252><b>¡ALARMA ROJA! ENJAMBRE ENFURECIDO · REACTOR CARGANDO: {timerStr}</b></color>\n" +
                        "<color=#ffd166>Defiendan la nave contra los 6 titanes marcianos:</color>\n" +
                        "• <b>El Cuernudo:</b> Se vuelve invisible. ¡Dispárale para romper su camuflaje!\n" +
                        "• <b>Frost Nightmare:</b> Titán de hielo con golpe demoledor ragdoll.\n" +
                        "• <b>Gusanos Excavadores:</b> ¡Si atrapan a un amigo entre sus dientes, disparen en equipo!\n" +
                        "• <b>Venomwing & Rek'Sai:</b> Ácido aéreo y emboscadas desde la arena.";
                    break;

                case DemoPhase.EvacuationVictory:
                    badgeText.text = "<color=#2ecc71>FASE 5/5: ¡MISIÓN CUMPLIDA! EVACUACIÓN EXITOSA (5:00+)</color>";
                    outline.effectColor = new Color32(46, 204, 113, 240);
                    bodyText.text =
                        "<color=#2ecc71><b>★ REACTOR AL 100% · HIPERIMPULSOR EN LÍNEA ★</b></color>\n\n" +
                        "¡Todos los componentes están instalados y el enjambre ha sido repelido!\n" +
                        "• <b>Suban a la nave</b> para iniciar el salto a Neptuno.\n" +
                        "• <b>Excelente trabajo en equipo:</b> Físicas, co-op troll, combate y supervivencia completados.\n\n" +
                        "<color=#68e0c6>★ DEMO SHOWCASE LISTA PARA LA EXPOSICIÓN ★</color>";
                    break;
            }
        }

        private static string FormatCheck(bool done, string label, string detail)
        {
            string check = done ? "<color=#2ecc71><b>[✓]</b></color>" : "<color=#8fa3b0><b>[ ]</b></color>";
            string text = done ? $"<color=#a8dfc8><s>{label}</s></color>" : $"<color=#ebf4fa><b>{label}</b></color>";
            return $"{check} {text} · <color=#96bed2>{detail}</color>";
        }
    }
}
