using System.Collections.Generic;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using CharacterInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.UI
{
    /// <summary>
    /// Enseña las mecanicas sin saturar la pantalla (solo jugador local):
    ///  1. Pistas en contexto (cerca de un compañero → [F] Empujar…), solo las primeras veces.
    ///  2. Aviso "¡Nueva acción!" la primera vez que se usa cada mecanica.
    ///  3. Pastilla [C] Controles que abre una hoja con todas las teclas.
    ///  4. En el lobby, lista "Prueba antes de salir" que se marca sola.
    /// Lo aprendido se guarda en PlayerPrefs: un jugador veterano deja de ver pistas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MechanicsCoach : MonoBehaviour
    {
        private const string Prefix = "GameJam.Coach.";
        private const int MaxHintShows = 3;
        private const float HintSeconds = 6f, HintCooldown = 12f, DiscoverySeconds = 3f;

        private sealed class Mechanic
        {
            public string Id, Key, Name, Hint;
            public System.Func<Keyboard, bool> Pressed;
            public bool Used => PlayerPrefs.GetInt(Prefix + "Used." + Id, 0) == 1;
            public int Shows => PlayerPrefs.GetInt(Prefix + "Shows." + Id, 0);
        }

        private readonly List<Mechanic> mechanics = new List<Mechanic>();
        private readonly HashSet<string> triedThisSession = new HashSet<string>();
        private MarsFusionAvatar avatar;
        private CharacterInput input;

        // Pista activa
        private Mechanic hint;
        private string hintText;
        private float hintStart, hintEnd = -100f, hintAlpha, idleTime, sessionTime;
        // Descubrimiento
        private Mechanic discovered;
        private float discoveredAt = -100f;
        // Hoja de controles
        private bool sheetOpen;
        private float sheetAlpha;
        // Lobby
        private float lobbyAlpha, allDoneAt = -1f;

        private GUIStyle title, body, small, keyLabel;

        private void Awake()
        {
            avatar = GetComponent<MarsFusionAvatar>();
            input = GetComponent<CharacterInput>();
            Add("push", "F", "Empujar", "Empuja a {0}", k => k.fKey.wasPressedThisFrame);
            Add("flop", "Y", "Flop", "Tírate al suelo · otra vez para levantarte", k => k.yKey.wasPressedThisFrame);
            Add("wave", "T", "Saludar", "Saluda a {0}", k => k.tKey.wasPressedThisFrame);
            Add("horn", "H", "Bocina", "Llama a tu equipo desde lejos", k => k.hKey.wasPressedThisFrame);
            Add("talk", "V", "Hablar", "Mantén para hablar con tu tripulación", k => k[VoicePreferences.TalkKey].wasPressedThisFrame);
            Add("emote", "7·8·9", "Gestos", "Haz un gesto mientras estás quieto",
                k => k.digit7Key.wasPressedThisFrame || k.digit8Key.wasPressedThisFrame || k.digit9Key.wasPressedThisFrame);
        }

        private void Add(string id, string key, string name, string hintText, System.Func<Keyboard, bool> pressed)
            => mechanics.Add(new Mechanic { Id = id, Key = key, Name = name, Hint = hintText, Pressed = pressed });

        private bool IsLocal => avatar != null && avatar.Object != null && avatar.Object.IsValid && avatar.HasStateAuthority;
        private bool CanAct => IsLocal && avatar.IsAlive && !avatar.TabletOpen && input != null && input.ControlsEnabled
            && !VoicePreferences.InputSuspended && Keyboard.current != null;

        private void Update()
        {
            if (!IsLocal) return;
            var kb = Keyboard.current;
            sessionTime += Time.unscaledDeltaTime;

            if (kb != null && !VoicePreferences.InputSuspended && kb.cKey.wasPressedThisFrame && !avatar.TabletOpen) sheetOpen = !sheetOpen;
            if (sheetOpen && kb != null && kb.escapeKey.wasPressedThisFrame) sheetOpen = false;

            if (!CanAct) { idleTime = 0f; return; }
            var kcc = GetComponent<Fusion.Addons.SimpleKCC.SimpleKCC>();
            idleTime = kcc != null && kcc.enabled && kcc.RealSpeed < .15f ? idleTime + Time.unscaledDeltaTime : 0f;

            // Descubrimiento y progreso.
            foreach (var m in mechanics)
            {
                if (!m.Pressed(kb)) continue;
                triedThisSession.Add(m.Id);
                if (hint == m) hintEnd = Mathf.Min(hintEnd, Time.unscaledTime);
                if (!m.Used)
                {
                    PlayerPrefs.SetInt(Prefix + "Used." + m.Id, 1);
                    PlayerPrefs.Save();
                    discovered = m;
                    discoveredAt = Time.unscaledTime;
                }
            }
            UpdateHint();
        }

        // ───────────── Pistas en contexto ─────────────

        private void UpdateHint()
        {
            float now = Time.unscaledTime;
            if (hint != null && now < hintEnd) return;
            if (hint != null) { hint = null; return; }
            if (now < hintEnd + HintCooldown || sheetOpen) return;

            MarsFusionAvatar mate = null;
            float best = float.PositiveInfinity;
            foreach (var other in FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None))
            {
                if (other == avatar || other.Object == null || !other.Object.IsValid || !other.IsAlive) continue;
                float d = Vector3.Distance(other.transform.position, transform.position);
                if (d < best) { best = d; mate = other; }
            }
            string mateName = mate != null && mate.PlayerName.ToString().Length > 0 ? mate.PlayerName.ToString() : "tu compañero";

            if (mate != null && best < 3f) Offer("push", mateName);
            else if (mate != null && best < 14f && idleTime > 1.5f) Offer("wave", mateName);
            else if (idleTime > 5f) Offer(idleTime > 9f ? "emote" : "flop", mateName);
            else if (mate != null && best > 22f && sessionTime > 40f) Offer("horn", mateName);
            else if (mate != null && sessionTime > 8f && sessionTime < 90f) Offer("talk", mateName);
        }

        private void Offer(string id, string mateName)
        {
            var m = mechanics.Find(x => x.Id == id);
            if (m == null || m.Used || m.Shows >= MaxHintShows) return;
            PlayerPrefs.SetInt(Prefix + "Shows." + id, m.Shows + 1);
            hint = m;
            hintText = string.Format(m.Hint, mateName);
            hintStart = Time.unscaledTime;
            hintEnd = hintStart + HintSeconds;
        }

        // ───────────── Dibujo ─────────────

        private void OnGUI()
        {
            if (!IsLocal) return;
            Setup();
            float s = InteractionPromptCard.Scale;
            if (Event.current.type == EventType.Repaint)
            {
                float dt = Time.unscaledDeltaTime;
                hintAlpha = Mathf.MoveTowards(hintAlpha, hint != null && Time.unscaledTime < hintEnd ? 1f : 0f, dt * 5f);
                sheetAlpha = Mathf.MoveTowards(sheetAlpha, sheetOpen ? 1f : 0f, dt * 7f);
                lobbyAlpha = Mathf.MoveTowards(lobbyAlpha, ShowLobby ? 1f : 0f, dt * 4f);
            }
            if (avatar.IsAlive && !avatar.TabletOpen)
            {
                DrawControlsPill(s);
                DrawHint(s);
                DrawDiscovery(s);
                DrawLobbyChecklist(s);
            }
            DrawSheet(s);
        }

        private Rect Safe => Screen.safeArea;
        // Por encima del panel de vida (abajo a la izquierda) para no taparlo.
        private float Bottom(float s) => Screen.height - Safe.yMin - 205 * s;

        private void DrawControlsPill(float s)
        {
            // Mas grande y opaca para leerse bien a distancia.
            float a = Mathf.Lerp(.95f, .4f, sheetAlpha);
            title.fontSize = GameJam.UI.UiFont.Px(17 * s);
            string label = "Controles";
            float kw = InteractionPromptCard.KeyWidth("C", s);
            float w = 12 * s + kw + 10 * s + title.CalcSize(new GUIContent(label)).x + 18 * s, h = 42 * s;
            var r = new Rect(Safe.xMin + 24 * s, Bottom(s) - h, w, h);
            InteractionPromptCard.Fill(new Rect(r.x + 2 * s, r.y + 3 * s, w, h), new Color(0, 0, 0, .3f), a, h * .5f);
            InteractionPromptCard.Fill(r, InteractionPromptCard.Panel, a, h * .5f);
            InteractionPromptCard.DrawKey(new Rect(r.x + 10 * s, r.y + 6 * s, kw, h - 12 * s), "C", true, a, s);
            title.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(r.x + 20 * s + kw, r.y, w, h), label, title);
        }

        /// <summary>Tarjeta de pista sobre la pastilla de controles (abajo a la izquierda).</summary>
        private void DrawHint(float s)
        {
            if (hintAlpha <= .01f || hint == null && hintAlpha <= .01f) return;
            var m = hint ?? discovered;
            if (m == null) return;
            float a = hintAlpha;
            float ease = 1f - (1f - a) * (1f - a);
            title.fontSize = GameJam.UI.UiFont.Px(16 * s);
            small.fontSize = GameJam.UI.UiFont.Px(13 * s);
            float kw = Mathf.Max(InteractionPromptCard.KeyWidth(m.Key, s), 36 * s);
            float textW = Mathf.Max(title.CalcSize(new GUIContent(m.Name)).x + 60 * s, small.CalcSize(new GUIContent(hintText)).x);
            float w = 16 * s + kw + 14 * s + textW + 18 * s, h = 62 * s;
            float x = Safe.xMin + 24 * s - (1f - ease) * 40 * s;
            var card = new Rect(x, Bottom(s) - 32 * s - 12 * s - h, w, h);
            InteractionPromptCard.Fill(new Rect(card.x + 2 * s, card.y + 4 * s, w, h), new Color(0, 0, 0, .3f), a, 10 * s);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 10 * s);
            var accent = new Color(.45f, .8f, 1f);
            InteractionPromptCard.Fill(new Rect(card.x, card.y + 10 * s, 3 * s, h - 20 * s), accent, a, 1.5f * s);
            InteractionPromptCard.DrawKey(new Rect(card.x + 16 * s, card.y + (h - 34 * s) * .5f, kw, 34 * s), m.Key, true, a, s);
            float tx = card.x + 16 * s + kw + 14 * s;
            title.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(tx, card.y + 9 * s, textW, 22 * s), m.Name, title);
            // Chip "PRUÉBALO".
            float nameW = title.CalcSize(new GUIContent(m.Name)).x;
            keyLabel.fontSize = GameJam.UI.UiFont.Px(9 * s);
            var chip = new Rect(tx + nameW + 8 * s, card.y + 12 * s, keyLabel.CalcSize(new GUIContent("PRUÉBALO")).x + 12 * s, 16 * s);
            InteractionPromptCard.Fill(chip, InteractionPromptCard.Fade(accent, .2f), a, 8 * s);
            keyLabel.normal.textColor = InteractionPromptCard.Fade(accent, a);
            GUI.Label(chip, "PRUÉBALO", keyLabel);
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            GUI.Label(new Rect(tx, card.y + 33 * s, textW, 18 * s), hintText, small);
            // Barra de tiempo restante.
            float left = Mathf.Clamp01((hintEnd - Time.unscaledTime) / HintSeconds);
            InteractionPromptCard.Fill(new Rect(card.x + 10 * s, card.yMax - 4 * s, (w - 20 * s) * left, 2 * s), InteractionPromptCard.Fade(accent, .6f), a, s);
        }

        /// <summary>Banner arriba al centro la primera vez que usas una mecanica.</summary>
        private void DrawDiscovery(float s)
        {
            if (discovered == null) return;
            float t = Time.unscaledTime - discoveredAt;
            if (t > DiscoverySeconds) return;
            float a = Mathf.Clamp01(t / .2f) * Mathf.Clamp01((DiscoverySeconds - t) / .5f);
            float pop = 1f + .08f * Mathf.Exp(-t * 10f);
            var gold = new Color(1f, .8f, .3f);
            int found = mechanics.FindAll(m => m.Used).Count;
            title.fontSize = GameJam.UI.UiFont.Px(18 * s * pop);
            keyLabel.fontSize = GameJam.UI.UiFont.Px(10 * s);
            small.fontSize = GameJam.UI.UiFont.Px(12 * s);
            string head = "¡NUEVA ACCIÓN!";
            float kw = InteractionPromptCard.KeyWidth(discovered.Key, s);
            float nameW = title.CalcSize(new GUIContent(discovered.Name)).x;
            float w = Mathf.Max(kw + 10 * s + nameW, 220 * s) + 48 * s, h = 92 * s;
            var card = new Rect(Screen.width * .5f - w * .5f, Screen.height - Safe.yMax + 110 * s - (1f - a) * 10 * s, w, h);
            InteractionPromptCard.Fill(new Rect(card.x - 4 * s, card.y - 4 * s, w + 8 * s, h + 8 * s), InteractionPromptCard.Fade(gold, .18f * a), 1f, 14 * s);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 12 * s);
            InteractionPromptCard.Fill(new Rect(card.x + 12 * s, card.y, w - 24 * s, 2 * s), gold, a, s);
            keyLabel.normal.textColor = InteractionPromptCard.Fade(gold, a);
            GUI.Label(new Rect(card.x, card.y + 8 * s, w, 16 * s), head, keyLabel);
            float rowX = card.x + (w - (kw + 10 * s + nameW)) * .5f;
            InteractionPromptCard.DrawKey(new Rect(rowX, card.y + 28 * s, kw, 32 * s), discovered.Key, true, a, s);
            title.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(rowX + kw + 10 * s, card.y + 28 * s, nameW + 4, 32 * s), discovered.Name, title);
            // Progreso de acciones descubiertas.
            float dot = 8 * s, gap = 6 * s;
            float dotsW = mechanics.Count * dot + (mechanics.Count - 1) * gap;
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            string prog = found + "/" + mechanics.Count;
            float pw = small.CalcSize(new GUIContent(prog)).x;
            float px = card.x + (w - dotsW - 8 * s - pw) * .5f;
            for (int i = 0; i < mechanics.Count; i++)
                InteractionPromptCard.Fill(new Rect(px + i * (dot + gap), card.y + 70 * s, dot, dot), i < found ? gold : new Color(1, 1, 1, .15f), a, dot * .5f);
            GUI.Label(new Rect(px + dotsW + 8 * s, card.y + 64 * s, pw + 4, 20 * s), prog, small);
        }

        // ───────────── Hoja de controles (C) ─────────────

        private static readonly (string group, (string key, string what)[] rows)[] Sheet =
        {
            ("MOVIMIENTO", new[] { ("WASD", "Moverse"), ("Espacio", "Saltar"), ("Shift", "Correr") }),
            ("INTERACCIÓN", new[] { ("E", "Recoger · mantener: reanimar"), (InteractionPromptCard.MouseLeft, "Arrastrar (manos vacías)"), ("Q", "Reparar la nave"), ("Tab", "Tablet y recetas") }),
            ("ARMA", new[] { ("Clic der.", "Apuntar"), (InteractionPromptCard.MouseLeft, "Disparar"), ("R", "Recargar") }),
            ("INVENTARIO", new[] { ("1–6", "Elegir objeto"), ("G", "Soltar objeto") }),
            ("CO-OP Y DIVERSIÓN", new[] { ("F", "Empujar"), ("Y", "Flop"), ("T", "Saludar"), ("H", "Bocina"), ("7·8·9", "Gestos"), ("V", "Hablar") }),
            ("SISTEMA", new[] { ("Esc", "Menú"), ("C", "Esta hoja de controles") }),
        };

        private float SheetColumnWidth(float s)
        {
            body.fontSize = GameJam.UI.UiFont.Px(14 * s);
            float widest = 0f;
            foreach (var group in Sheet)
            {
                float colKey = 0f, text = 0f;
                foreach (var row in group.rows)
                {
                    colKey = Mathf.Max(colKey, InteractionPromptCard.KeyWidth(row.key, s * .9f));
                    text = Mathf.Max(text, body.CalcSize(new GUIContent(row.what)).x);
                }
                widest = Mathf.Max(widest, colKey + 12 * s + text + 8 * s);
            }
            return widest;
        }

        private void DrawSheet(float s)
        {
            if (sheetAlpha <= .01f) return;
            float a = sheetAlpha;
            InteractionPromptCard.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .55f), a, 0);
            // Hoja grande: 1.45x y columnas medidas por su texto (antes se cortaban las frases largas).
            s *= 1.45f;
            float colW = SheetColumnWidth(s);
            float maxW = Screen.width * .94f;
            float needed = 3 * colW + 2 * 28 * s + 2 * 30 * s;
            if (needed > maxW) { s *= maxW / needed; colW = SheetColumnWidth(s); }
            float rowH = 32 * s, gap = 28 * s, pad = 30 * s;
            int cols = 3;
            float[] heights = new float[Sheet.Length];
            for (int i = 0; i < Sheet.Length; i++) heights[i] = 30 * s + Sheet[i].rows.Length * rowH + 14 * s;
            float colH0 = heights[0] + heights[3], colH1 = heights[1] + heights[5], colH2 = heights[4] + heights[2];
            float contentH = Mathf.Max(colH0, Mathf.Max(colH1, colH2));
            float w = cols * colW + (cols - 1) * gap + pad * 2, h = 70 * s + contentH + 40 * s;
            var card = new Rect(Screen.width * .5f - w * .5f, Screen.height * .5f - h * .5f + (1f - a) * 16 * s, w, h);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 14 * s);
            InteractionPromptCard.Fill(new Rect(card.x + 20 * s, card.y, w - 40 * s, 3 * s), new Color(1f, .68f, .25f), a, 1.5f * s);
            title.fontSize = GameJam.UI.UiFont.Px(22 * s);
            title.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(card.x + pad, card.y + 20 * s, w, 32 * s), "Controles", title);
            small.fontSize = GameJam.UI.UiFont.Px(12 * s);
            small.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextSoft, a);
            GUI.Label(new Rect(card.xMax - pad - 200 * s, card.y + 24 * s, 200 * s, 24 * s), "C o Esc para cerrar", small);

            int[][] layout = { new[] { 0, 3 }, new[] { 1, 5 }, new[] { 4, 2 } };
            for (int c = 0; c < cols; c++)
            {
                float x = card.x + pad + c * (colW + gap), y = card.y + 70 * s;
                foreach (int g in layout[c])
                {
                    keyLabel.fontSize = GameJam.UI.UiFont.Px(11 * s);
                    keyLabel.alignment = TextAnchor.MiddleLeft;
                    keyLabel.normal.textColor = InteractionPromptCard.Fade(new Color(1f, .68f, .25f), a);
                    GUI.Label(new Rect(x, y, colW, 20 * s), Sheet[g].group, keyLabel);
                    keyLabel.alignment = TextAnchor.MiddleCenter;
                    y += 26 * s;
                    float colKey = 0f;
                    foreach (var row in Sheet[g].rows) colKey = Mathf.Max(colKey, InteractionPromptCard.KeyWidth(row.key, s * .9f));
                    foreach (var row in Sheet[g].rows)
                    {
                        float kw = InteractionPromptCard.KeyWidth(row.key, s * .9f);
                        InteractionPromptCard.DrawKey(new Rect(x, y + 2 * s, kw, rowH - 6 * s), row.key, true, a, s * .9f);
                        body.fontSize = GameJam.UI.UiFont.Px(14 * s);
                        body.normal.textColor = InteractionPromptCard.Fade(InteractionPromptCard.TextOn, a);
                        GUI.Label(new Rect(x + colKey + 12 * s, y, colW - colKey - 12 * s, rowH - 2 * s), row.what, body);
                        y += rowH;
                    }
                    y += 14 * s;
                }
            }
        }

        // ───────────── Lobby: "Prueba antes de salir" ─────────────

        private bool ShowLobby => SciFiGameMenuManager.IsLobbyScene && !sheetOpen
            && (allDoneAt < 0f || Time.unscaledTime - allDoneAt < 5f);

        private void DrawLobbyChecklist(float s)
        {
            if (!SciFiGameMenuManager.IsLobbyScene && !previewLobby) return;
            int done = 0;
            foreach (var m in mechanics) if (triedThisSession.Contains(m.Id) || m.Used) done++;
            if (done == mechanics.Count && allDoneAt < 0f) allDoneAt = Time.unscaledTime;
            if (lobbyAlpha <= .01f) return;
            float a = lobbyAlpha;
            float w = 270 * s, rowH = 34 * s, h = 64 * s + mechanics.Count * rowH + 14 * s;
            var card = new Rect(Safe.xMax - w - 24 * s + (1f - a) * 30 * s, Screen.height * .32f, w, h);
            InteractionPromptCard.Fill(new Rect(card.x + 2 * s, card.y + 4 * s, w, h), new Color(0, 0, 0, .3f), a, 12 * s);
            InteractionPromptCard.Fill(card, InteractionPromptCard.Panel, a, 12 * s);
            var green = new Color(.35f, 1f, .6f);
            keyLabel.fontSize = GameJam.UI.UiFont.Px(11 * s);
            keyLabel.alignment = TextAnchor.MiddleLeft;
            keyLabel.normal.textColor = InteractionPromptCard.Fade(new Color(.45f, .8f, 1f), a);
            GUI.Label(new Rect(card.x + 18 * s, card.y + 14 * s, w, 16 * s), "PRUEBA ANTES DE SALIR", keyLabel);
            keyLabel.alignment = TextAnchor.MiddleCenter;
            title.fontSize = GameJam.UI.UiFont.Px(15 * s);
            title.normal.textColor = InteractionPromptCard.Fade(done == mechanics.Count ? green : InteractionPromptCard.TextOn, a);
            GUI.Label(new Rect(card.x + 18 * s, card.y + 30 * s, w, 22 * s),
                done == mechanics.Count ? "¡Listo para la misión!" : done + " de " + mechanics.Count + " acciones", title);
            float y = card.y + 62 * s;
            foreach (var m in mechanics)
            {
                bool ok = triedThisSession.Contains(m.Id) || m.Used;
                float kw = InteractionPromptCard.KeyWidth(m.Key, s * .85f);
                InteractionPromptCard.DrawKey(new Rect(card.x + 18 * s, y + 3 * s, kw, rowH - 8 * s), m.Key, !ok, a, s * .85f);
                body.fontSize = GameJam.UI.UiFont.Px(14 * s);
                body.normal.textColor = InteractionPromptCard.Fade(ok ? InteractionPromptCard.TextOff : InteractionPromptCard.TextOn, a);
                GUI.Label(new Rect(card.x + 28 * s + kw, y, w, rowH - 2 * s), m.Name, body);
                // Casilla.
                var box = new Rect(card.xMax - 36 * s, y + 7 * s, 18 * s, 18 * s);
                InteractionPromptCard.Fill(box, ok ? green : new Color(1, 1, 1, .12f), a, 9 * s);
                if (ok)
                {
                    keyLabel.normal.textColor = InteractionPromptCard.Fade(new Color32(10, 30, 20, 255), a);
                    keyLabel.fontSize = GameJam.UI.UiFont.Px(12 * s);
                    GUI.Label(box, "✓", keyLabel);
                    keyLabel.fontSize = GameJam.UI.UiFont.Px(11 * s);
                }
                y += rowH;
            }
        }

#if UNITY_EDITOR
        /// <summary>Solo editor: dibuja todos los elementos con datos de ejemplo para revisarlos.</summary>
        internal void EditorPreview(bool sheet)
        {
            Setup();
            float s = InteractionPromptCard.Scale;
            if (sheet) { sheetAlpha = 1f; DrawSheet(s); return; }
            hint = mechanics[1]; hintText = mechanics[1].Hint; hintEnd = Time.unscaledTime + 4f; hintAlpha = 1f;
            discovered = mechanics[0]; discoveredAt = Time.unscaledTime - .6f;
            triedThisSession.Add("push"); triedThisSession.Add("wave");
            DrawControlsPill(s);
            DrawHint(s);
            DrawDiscovery(s);
            float a = lobbyAlpha; lobbyAlpha = 1f;
            DrawLobbyPreview(s);
            lobbyAlpha = a;
        }

        private void DrawLobbyPreview(float s)
        {
            bool saved = previewLobby; previewLobby = true; DrawLobbyChecklist(s); previewLobby = saved;
        }
#endif
        private bool previewLobby;

        private void Setup()
        {
            if (title != null) return;
            title = new GUIStyle(GameJam.UI.UiFont.Label()) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(), wordWrap = false };
            body = new GUIStyle(title) { fontStyle = FontStyle.Normal };
            small = new GUIStyle(body);
            keyLabel = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
            keyLabel.font = UiFont.TitleFont;
        }
    }
}
