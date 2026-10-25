using System;
using System.Collections.Generic;
using System.Linq;
using GameJam.Localization;
using GameJam.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PlayerInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.UI
{
    // Local settings surface; no gameplay Networked fields or inventory mutations.
    public sealed class ExpeditionSettingsPanel : MonoBehaviour
    {
        public static ExpeditionSettingsPanel Current { get; private set; }
        RectTransform content;
        TMP_FontAsset font;
        TextMeshProUGUI status;
        LocalMicrophoneTest microphoneTest;
        readonly List<Action> refreshers = new();
        readonly List<Vector2Int> resolutions = new();
        int resolutionIndex, tab;
        bool fullscreen;
        bool wasDisplayPending;
        float nextRefresh;
        Action<Key> captureKey;
        InputActionRebindingExtensions.RebindingOperation rebind;
        InputAction reboundAction;
        bool wasEnabled;
        int cancelledBindingFrame = -1;
        string message = "";
        int microphoneIndex;
        readonly Color ink = new(.9f, .96f, .98f), muted = new(.48f, .67f, .73f), accent = new(.98f, .61f, .28f);
        public static bool IsOpen => Current != null;
        static string L(string es, string en, string pt) => GameLocalization.CurrentLanguage == GameLanguage.English ? en : GameLocalization.CurrentLanguage == GameLanguage.Portuguese ? pt : es;

        public static void Open(TMP_FontAsset useFont)
        {
            if (Current != null) return;
            var go = new GameObject("Expedition Settings", typeof(RectTransform));
            var panel = go.AddComponent<ExpeditionSettingsPanel>(); Current = panel; panel.font = useFont ?? TMP_Settings.defaultFontAsset; panel.Build();
        }
        public static bool ConsumeEscape()
        {
            if (Current == null) return false;
            if (Current.cancelledBindingFrame == Time.frameCount) return true;
            if (Current.captureKey != null) { Current.captureKey = null; VoicePreferences.CapturingBinding = false; }
            else if (Current.rebind != null) Current.rebind.Cancel();
            else Current.Close();
            return true;
        }
        public static void CloseIfOpen() { if (Current != null) Current.Close(); }
        RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false); rt.sizeDelta = size; rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f); return rt;
        }
        void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
        Image Background(RectTransform rt, Color color) { var image = rt.gameObject.AddComponent<Image>(); image.color = color; return image; }
        TextMeshProUGUI Label(Transform parent, string text, int size = 20)
        {
            var rt = Rect("Label", parent, Vector2.zero); Fill(rt); var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = text; label.fontSize = size; label.color = ink; label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = true; label.margin = new Vector4(14, 4, 14, 4); return label;
        }
        Button Button(Transform parent, string text, Action clicked, Vector2 size)
        {
            var rt = Rect(text, parent, size); var image = Background(rt, new Color(.035f, .29f, .34f));
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.65f, 1, 1); colors.pressedColor = new Color(.4f, .7f, .75f); button.colors = colors;
            var label = Label(rt, text, 18); label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => clicked()); return button;
        }
        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            gameObject.AddComponent<GraphicRaycaster>(); var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var backdrop = Rect("Backdrop", transform, Vector2.zero); Fill(backdrop); Background(backdrop, new Color(.005f, .015f, .02f, .96f));
            var body = Rect("Datapad", backdrop, new Vector2(1060, 790)); Background(body, new Color(.04f, .075f, .1f));
            var heading = Rect("Heading", body, new Vector2(980, 58)); heading.anchoredPosition = new Vector2(-15, 350);
            var title = Label(heading, L("SISTEMAS DE EXPEDICIÓN", "EXPEDITION SYSTEMS", "SISTEMAS DE EXPEDIÇÃO"), 28); title.fontStyle = FontStyles.Bold; title.color = accent;
            var close = Button(body, "X", Close, new Vector2(48, 48)); ((RectTransform)close.transform).anchoredPosition = new Vector2(480, 350);
            string[] tabs = { L("PANTALLA", "DISPLAY", "TELA"), L("VOZ", "VOICE", "VOZ"), L("CONTROLES", "CONTROLS", "CONTROLES"), L("CONEXIÓN", "CONNECTION", "CONEXÃO") };
            for (int i = 0; i < tabs.Length; i++)
            {
                int selected = i; var button = Button(body, tabs[i], () => ShowTab(selected), new Vector2(238, 46));
                ((RectTransform)button.transform).anchoredPosition = new Vector2(-375 + i * 250, 280);
            }
            var viewport = Rect("Viewport", body, new Vector2(1000, 550)); viewport.anchoredPosition = new Vector2(0, -30);
            Background(viewport, new Color(.02f, .045f, .06f)); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = viewport; scroll.scrollSensitivity = 32;
            content = Rect("Options", viewport, new Vector2(1000, 0)); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 10;
            layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = content;
            var footer = Rect("Status", body, new Vector2(1000, 62)); footer.anchoredPosition = new Vector2(0, -350); status = Label(footer, "", 17); status.color = accent;
            microphoneTest = gameObject.AddComponent<LocalMicrophoneTest>(); fullscreen = Screen.fullScreen;
            foreach (var resolution in Screen.resolutions)
            { var size = new Vector2Int(resolution.width, resolution.height); if (!resolutions.Contains(size)) resolutions.Add(size); }
            var current = new Vector2Int(Screen.width, Screen.height); if (!resolutions.Contains(current)) resolutions.Add(current);
            resolutionIndex = resolutions.IndexOf(current); ShowTab(0);
        }
        void Clear()
        {
            CancelCapture(); microphoneTest.StopTest(); refreshers.Clear();
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            content.anchoredPosition = Vector2.zero;
        }
        void ShowTab(int selected)
        {
            Clear(); tab = selected; message = "";
            if (tab == 0) BuildDisplay(); else if (tab == 1) BuildVoice(); else if (tab == 2) BuildControls(); else BuildConnection();
            foreach (var refresh in refreshers) refresh();
        }
        RectTransform Row(string title, int height = 72)
        {
            var row = Rect(title, content, new Vector2(970, height)); Background(row, new Color(.07f, .12f, .15f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return row;
        }
        void Option(string title, Func<string> value, Action clicked, Func<bool> enabled = null)
        {
            var row = Row(title); var name = Rect("Option", row, new Vector2(550, 64)); name.anchorMin = name.anchorMax = new Vector2(0, .5f); name.anchoredPosition = new Vector2(275, 0); Label(name, title);
            var button = Button(row, "", clicked, new Vector2(370, 52)); var rt = (RectTransform)button.transform; rt.anchorMin = rt.anchorMax = new Vector2(1, .5f); rt.anchoredPosition = new Vector2(-195, 0);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(); refreshers.Add(() => { label.text = value(); button.interactable = enabled == null || enabled(); });
        }
        void Note(string text)
        {
            var row = Row("Help", 80); var label = Label(row, text, 18); label.color = muted;
        }
        void Slider(string title, float min, float max, float initial, Action<float> changed)
        {
            var row = Row(title, 94); var caption = Rect("Caption", row, new Vector2(930, 40)); caption.anchoredPosition = new Vector2(0, 23); var text = Label(caption, title + "  " + initial.ToString(max <= .2f ? "F3" : "F2"));
            var track = Rect("Slider", row, new Vector2(900, 18)); track.anchoredPosition = new Vector2(0, -21); Background(track, new Color(.02f, .04f, .05f));
            var handle = Rect("Handle", track, new Vector2(22, 30)); Background(handle, accent);
            var slider = track.gameObject.AddComponent<Slider>(); slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = min; slider.maxValue = max; slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(v => { text.text = title + "  " + v.ToString(max <= .2f ? "F3" : "F2"); changed(v); });
        }
        void BuildDisplay()
        {
            Note(L("Los cambios de pantalla se revierten en 15 s si no confirmas. F11 alterna ventana/completa.", "Display changes revert after 15 s unless confirmed. F11 toggles fullscreen.", "Mudanças de tela revertem em 15 s sem confirmação. F11 alterna tela cheia."));
            Option(L("Resolución", "Resolution", "Resolução"), () => resolutions[resolutionIndex].x + " x " + resolutions[resolutionIndex].y, () => resolutionIndex = (resolutionIndex + 1) % resolutions.Count);
            Option(L("Modo", "Mode", "Modo"), () => fullscreen ? L("Sin bordes", "Borderless", "Sem bordas") : L("Ventana", "Windowed", "Janela"), () => fullscreen = !fullscreen);
            Option(L("Aplicar pantalla", "Apply display", "Aplicar tela"), () => L("Probar 15 segundos", "Preview for 15 seconds", "Testar por 15 segundos"), () => { var r = resolutions[resolutionIndex]; ExpeditionGraphics.Instance?.PreviewDisplay(r.x, r.y, fullscreen); message = Application.isEditor ? L("Prueba de resolución disponible en ejecutable", "Resolution preview is available in the executable", "Teste de resolução disponível no executável") : ""; });
            Option(L("Confirmar pantalla", "Keep display", "Confirmar tela"), () => L("Conservar cambios", "Keep changes", "Manter mudanças"), () => ExpeditionGraphics.Instance?.ConfirmDisplay(), () => ExpeditionGraphics.Instance != null && ExpeditionGraphics.Instance.PendingDisplay);
            Option(L("Calidad", "Quality", "Qualidade"), () => new[] { L("Baja", "Low", "Baixa"), L("Media", "Medium", "Média"), L("Alta", "High", "Alta") }[PlayerPrefs.GetInt("GameJam.Graphics.Preset", 1)], () => ExpeditionGraphics.Instance?.SetPreset((PlayerPrefs.GetInt("GameJam.Graphics.Preset", 1) + 1) % 3));
            Option("VSync", () => PlayerPrefs.GetInt("GameJam.Graphics.VSync", 0) == 1 ? "ON" : "OFF", () => { PlayerPrefs.SetInt("GameJam.Graphics.VSync", 1 - PlayerPrefs.GetInt("GameJam.Graphics.VSync", 0)); ExpeditionGraphics.Instance?.ApplyFrameLimit(); PlayerPrefs.Save(); });
            int[] limits = { 30, 60, 90, 120, 144, 165, 240, -1 };
            Option(L("Límite de FPS (sin VSync)", "FPS cap (VSync off)", "Limite FPS (sem VSync)"), () => PlayerPrefs.GetInt("GameJam.Graphics.FpsLimit", 120).ToString(), () => { int i = Array.IndexOf(limits, PlayerPrefs.GetInt("GameJam.Graphics.FpsLimit", 120)); PlayerPrefs.SetInt("GameJam.Graphics.FpsLimit", limits[(i + 1) % limits.Length]); ExpeditionGraphics.Instance?.ApplyFrameLimit(); PlayerPrefs.Save(); });
            Option(L("Postprocesado", "Post-processing", "Pós-processamento"), () => PlayerPrefs.GetInt("GameJam.Graphics.PostProcessing", 1) == 1 ? "ON" : "OFF", () => ExpeditionGraphics.Instance?.SetPostProcessing(PlayerPrefs.GetInt("GameJam.Graphics.PostProcessing", 1) == 0));
            Slider(L("Volumen general", "Master volume", "Volume geral"), 0, 1, AudioListener.volume, v => { AudioListener.volume = v; PlayerPrefs.SetFloat("GameJam.MasterVolume", v); PlayerPrefs.Save(); });
        }
        void BuildVoice()
        {
            Note(L("Prueba local: no transmite a la sala. Usa auriculares para evitar eco. V es la tecla inicial.", "Local test never transmits to the room. Use headphones to avoid feedback. Default talk key: V.", "Teste local não transmite à sala. Use fones para evitar eco. Tecla inicial: V."));
            Option(L("Micrófono", "Microphone", "Microfone"), () => string.IsNullOrEmpty(VoicePreferences.Device) ? L("Predeterminado", "Default", "Padrão") : VoicePreferences.Device, () =>
            {
                microphoneTest.StopTest(); string[] devices = Microphone.devices; microphoneIndex = Array.IndexOf(devices, VoicePreferences.Device) + 1;
                string selected = microphoneIndex >= devices.Length ? "" : devices[microphoneIndex]; PlayerPrefs.SetString("GameJam.Voice.Device", selected); VoicePreferences.Changed(true);
            });
            Option(L("Detección y recuperación", "Capture recovery", "Recuperar captura"), () => L("Reiniciar micrófono", "Restart microphone", "Reiniciar microfone"), () => { microphoneTest.StopTest(); LocalVoice()?.RefreshCapture(); });
            Option(L("Prueba local con escucha", "Local loopback test", "Teste local com escuta"), () => microphoneTest.IsRunning ? L("DETENER", "STOP", "PARAR") : L("INICIAR", "START", "INICIAR"), () => { if (microphoneTest.IsRunning) microphoneTest.StopTest(); else microphoneTest.StartTest(true); });
            Option(L("Modo de voz", "Voice mode", "Modo de voz"), () => MarsProximityVoice.LocalPushToTalk ? "Push to talk" : L("Micrófono abierto", "Open microphone", "Microfone aberto"), () =>
            { bool ptt = !MarsProximityVoice.LocalPushToTalk; MarsProximityVoice.LocalPushToTalk = ptt; PlayerPrefs.SetInt("GameJam.VoiceMode", ptt ? 1 : 0); LocalVoice()?.SetPushToTalk(ptt); VoicePreferences.Changed(); });
            Option(L("Tecla para hablar", "Push to talk key", "Tecla para falar"), () => VoicePreferences.TalkKey.ToString(), () => { VoicePreferences.CapturingBinding = true; captureKey = key => { message = VoicePreferences.TrySetTalkKey(key) ? L("Tecla guardada", "Key saved", "Tecla salva") : L("Tecla reservada: elige otra", "Reserved key: choose another", "Tecla reservada: escolha outra"); }; });
            Slider(L("Umbral de micrófono abierto", "Open microphone threshold", "Limiar de microfone aberto"), .001f, .1f, VoicePreferences.Threshold, v => { PlayerPrefs.SetFloat("GameJam.Voice.Threshold", v); VoicePreferences.Changed(); });
            Slider(L("Volumen de voz", "Voice volume", "Volume de voz"), 0, 1.5f, MarsProximityVoice.MasterVoiceVolume, v => { MarsProximityVoice.MasterVoiceVolume = v; PlayerPrefs.SetFloat("GameJam.MasterVoiceVolume", v); PlayerPrefs.Save(); });
        }
        MarsProximityVoice LocalVoice() => MarsFusionAvatar.Local != null ? MarsFusionAvatar.Local.GetComponent<MarsProximityVoice>() : null;
        void BuildControls()
        {
            Slider(L("Sensibilidad", "Sensitivity", "Sensibilidade"), .1f, 3, ExpeditionInputPreferences.Sensitivity, v => { PlayerPrefs.SetFloat("GameJam.Input.Sensitivity", v); PlayerPrefs.Save(); });
            Option(L("Invertir eje Y", "Invert Y", "Inverter Y"), () => ExpeditionInputPreferences.InvertY ? "ON" : "OFF", () => { PlayerPrefs.SetInt("GameJam.Input.InvertY", ExpeditionInputPreferences.InvertY ? 0 : 1); PlayerPrefs.Save(); });
            var player = MarsFusionAvatar.Local != null ? MarsFusionAvatar.Local.GetComponent<PlayerInput>() : null;
            var asset = player != null ? player.BindingAsset : null;
            if (asset == null) { Note(L("Entra en una sala para reasignar movimiento, salto y sprint.", "Join a room to rebind movement, jump and sprint.", "Entre em uma sala para remapear movimento, salto e corrida.")); return; }
            foreach (string name in new[] { "Move", "Jump", "Sprint" })
            {
                var action = asset.FindAction("Player/" + name); if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    int binding = i; var b = action.bindings[i]; if (b.isComposite || !b.effectivePath.Contains("Keyboard")) continue;
                    Option(name + (b.isPartOfComposite ? " / " + b.name : ""), () => action.GetBindingDisplayString(binding), () => StartRebind(action, binding));
                }
            }
            Option(L("Restaurar controles", "Reset bindings", "Restaurar controles"), () => L("Predeterminados", "Defaults", "Padrão"), () => { asset.RemoveAllBindingOverrides(); ExpeditionInputPreferences.Save(asset); });
            Note(L("E, Q, R, Tab, I y teclas numéricas siguen reservadas para sus acciones actuales.", "E, Q, R, Tab, I and number keys remain reserved for existing actions.", "E, Q, R, Tab, I e números continuam reservados para as ações atuais."));
        }
        void StartRebind(InputAction action, int binding)
        {
            CancelCapture(); string previous = action.bindings[binding].overridePath; wasEnabled = action.enabled; reboundAction = action; action.Disable(); VoicePreferences.CapturingBinding = true;
            rebind = action.PerformInteractiveRebinding(binding).WithControlsHavingToMatchPath("<Keyboard>").WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind()).OnComplete(_ =>
                {
                    string path = action.bindings[binding].effectivePath;
                    string[] reserved = { "e", "q", "r", "tab", "i", "escape", "f8", "f11", VoicePreferences.TalkKey.ToString().ToLowerInvariant(), "1", "2", "3", "4", "5", "6", "7", "8", "9" };
                    bool conflict = reserved.Any(key => path.Equals("<Keyboard>/" + key, StringComparison.OrdinalIgnoreCase));
                    foreach (var other in action.actionMap.actions)
                        for (int i = 0; i < other.bindings.Count; i++)
                            if (!(other == action && i == binding) && !other.bindings[i].isComposite && other.bindings[i].effectivePath == path) conflict = true;
                    if (conflict) { action.ApplyBindingOverride(binding, previous); message = L("Conflicto: se conservó la tecla anterior", "Conflict: previous binding restored", "Conflito: tecla anterior mantida"); }
                    else { ExpeditionInputPreferences.Save(action.actionMap.asset); message = L("Control guardado", "Binding saved", "Controle salvo"); }
                    FinishRebind();
                }).Start();
        }
        void FinishRebind() { if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) cancelledBindingFrame = Time.frameCount; rebind?.Dispose(); rebind = null; if (reboundAction != null && wasEnabled) reboundAction.Enable(); reboundAction = null; VoicePreferences.CapturingBinding = false; }
        void CancelCapture() { captureKey = null; if (rebind != null) rebind.Cancel(); VoicePreferences.CapturingBinding = false; }
        void BuildConnection()
        {
            bool Running() => NetworkSession() != null && (NetworkSession().Busy || NetworkSession().Runner != null);
            Note(L("Todos deben usar la misma región, sala y versión compatible. Cambia región antes de conectar.", "Everyone needs the same region, room and compatible build. Change region before connecting.", "Todos precisam da mesma região, sala e versão compatível. Troque a região antes de conectar."));
            Option(L("Región de Photon", "Photon region", "Região Photon"), () => MultiplayerConfiguration.SelectedRegion.ToUpperInvariant(), () => { int i = Array.IndexOf(MultiplayerConfiguration.Regions, MultiplayerConfiguration.SelectedRegion); MultiplayerConfiguration.SelectedRegion = MultiplayerConfiguration.Regions[(i + 1) % MultiplayerConfiguration.Regions.Length]; }, () => !Running());
            Note("Protocol: " + MultiplayerConfiguration.ProtocolVersion + "\n" + L("SA = Sudamérica. AUTO puede elegir regiones distintas para tus amigos.", "SA = South America. AUTO can choose different regions for friends.", "SA = América do Sul. AUTO pode escolher regiões diferentes para amigos."));
            Option(L("Diagnóstico en pantalla", "Diagnostic overlay", "Diagnóstico na tela"), () => "F8", () => { var runner = NetworkSession()?.Runner; if (runner != null) { var d = MultiplayerDiagnostics.Ensure(runner); d.Visible = !d.Visible; } });
            Option(L("Exportar informe sin datos personales", "Export privacy-safe report", "Exportar relatório sem dados pessoais"), () => L("Exportar", "Export", "Exportar"), () => { var runner = NetworkSession()?.Runner; if (runner != null) { var d = MultiplayerDiagnostics.Ensure(runner); d.Export(); message = d.LastReport; } else message = L("Conecta a una sesión primero", "Connect to a session first", "Conecte a uma sessão primeiro"); });
        }
        MarsFusionSession NetworkSession() => FindFirstObjectByType<MarsFusionSession>();
        void Update()
        {
            if (captureKey != null && Keyboard.current != null)
                foreach (var key in Keyboard.current.allKeys)
                    if (key.keyCode != Key.Escape && key.wasPressedThisFrame) { var callback = captureKey; captureKey = null; VoicePreferences.CapturingBinding = false; callback(key.keyCode); break; }
            if (Time.unscaledTime < nextRefresh) return; nextRefresh = Time.unscaledTime + .2f;
            bool displayPending = ExpeditionGraphics.Instance != null && ExpeditionGraphics.Instance.PendingDisplay;
            if (wasDisplayPending && !displayPending) StartCoroutine(SyncDisplayForm());
            wasDisplayPending = displayPending;
            foreach (var refresh in refreshers) refresh();
            string text = message;
            if (captureKey != null || rebind != null) text = L("Pulsa una tecla / Esc cancela", "Press a key / Esc cancels", "Pressione uma tecla / Esc cancela");
            else if (ExpeditionGraphics.Instance != null && ExpeditionGraphics.Instance.PendingDisplay) text = L("Confirma pantalla: ", "Confirm display: ", "Confirme a tela: ") + ExpeditionGraphics.Instance.SecondsRemaining + " s";
            else if (tab == 1)
            { var voice = LocalVoice(); text = microphoneTest.IsRunning ? microphoneTest.Status + " | input " + microphoneTest.Level.ToString("F3") : voice != null ? voice.CaptureStatus + " | input " + voice.InputLevel.ToString("F3") + " | PTT " + voice.TalkPressed + " | TX " + voice.IsTransmitting : microphoneTest.Status; }
            status.text = text;
        }
        System.Collections.IEnumerator SyncDisplayForm()
        {
            yield return new WaitForSecondsRealtime(.15f);
            fullscreen = Screen.fullScreen;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
            resolutionIndex = resolutions.IndexOf(size);
            message = ExpeditionGraphics.Instance != null && ExpeditionGraphics.Instance.LastDisplayConfirmed
                ? L("Pantalla confirmada", "Display confirmed", "Tela confirmada")
                : L("Se restauró la pantalla anterior", "Previous display restored", "Tela anterior restaurada");
        }
        void Close() { if (ExpeditionGraphics.Instance != null) ExpeditionGraphics.Instance.RevertDisplay(); CancelCapture(); microphoneTest?.StopTest(); Current = null; Destroy(gameObject); }
        void OnDestroy() { CancelCapture(); microphoneTest?.StopTest(); if (Current == this) Current = null; }
    }
}
