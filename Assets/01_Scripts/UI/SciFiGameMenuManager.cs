using System;
using System.Collections.Generic;
using GameJam.Localization;
using GameJam.Multiplayer;
using GameJam.Tablet;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>
    /// Gestor integral de interfaces para terminal de inicio y menú de pausa.
    /// Diseñado con la estética de datapad de campo rugoso idéntica a la Tablet:
    /// chasis de titanio, cuerpo mate con tornillos hexagonales, pantalla embutida con cristal oscuro,
    /// tarjetas de datos retroiluminadas y tipografía moderna de alta resolución.
    /// 
    /// Flujo de escenas:
    /// - En el Lobby (j_LobyDeEspera.unity): La terminal de inicio está activa para configurar sala/idioma y conectar.
    /// - Al conectar: La terminal se oculta y el astronauta camina libremente en la nave.
    /// - En escenas de misión (Mars, Neptune): La terminal de inicio NUNCA se muestra.
    /// - Tecla [Esc]: Abre el datapad de Pausa (Misión, Ajustes, Tripulación) con opción de volver al Lobby.
    /// </summary>
    public sealed class SciFiGameMenuManager : MonoBehaviour
    {
        public static SciFiGameMenuManager Instance { get; private set; }

        [Header("Referencias")]
        [SerializeField] private MarsFusionSession session;
        [SerializeField] private TMP_FontAsset fontAsset;

        // Raíces y Grupos de Canvas
        private Canvas rootCanvas;
        private CanvasGroup startMenuGroup;
        private CanvasGroup pauseMenuGroup;

        // Componentes de Inicio
        private TMP_InputField playerNameInput;
        private TMP_InputField roomCodeInput;
        private Button btnOnlineStart;
        private Button btnSoloStart;
        private TextMeshProUGUI statusLabel;
        private TabletRoundedPanel statusDot;

        // Botones de Idioma Inicio
        private Button btnStartLangEs;
        private Button btnStartLangEn;
        private Button btnStartLangPt;
        private TabletRoundedPanel btnStartLangEsBg;
        private TabletRoundedPanel btnStartLangEnBg;
        private TabletRoundedPanel btnStartLangPtBg;

        // Botones de Idioma Pausa
        private Button btnPauseLangEs;
        private Button btnPauseLangEn;
        private Button btnPauseLangPt;
        private TabletRoundedPanel btnPauseLangEsBg;
        private TabletRoundedPanel btnPauseLangEnBg;
        private TabletRoundedPanel btnPauseLangPtBg;

        // Pestañas del Menú de Pausa
        private Button tabBtnMission;
        private Button tabBtnSettings;
        private Button tabBtnCrew;
        private TabletRoundedPanel tabBtnMissionBg;
        private TabletRoundedPanel tabBtnSettingsBg;
        private TabletRoundedPanel tabBtnCrewBg;

        private GameObject tabContentMission;
        private GameObject tabContentSettings;
        private GameObject tabContentCrew;
        private string activeTab = "Mission";

        // Ajustes de Audio y Voz
        private Slider masterVolumeSlider;
        private TextMeshProUGUI masterVolumePercentLabel;
        private Button btnVoicePtt;
        private Button btnVoiceOpenMic;
        private TabletRoundedPanel btnVoicePttBg;
        private TabletRoundedPanel btnVoiceOpenMicBg;
        private Slider voiceMasterVolumeSlider;
        private TextMeshProUGUI voiceMasterPercentLabel;

        // Lista de Tripulación
        private RectTransform crewListContainer;
        private TextMeshProUGUI crewEmptyLabel;
        private readonly List<CrewRowWidget> activeCrewRows = new();
        private float nextCrewCheckTime;
        private bool isInitialized;

        // Paleta de Hardware y Datapad Aeroespacial (Idéntica a TabletGuideUI)
        private static readonly Color ChassisRim = new(0.18f, 0.24f, 0.32f, 1f);     // Titanio exterior
        private static readonly Color ChassisMatte = new(0.065f, 0.085f, 0.115f, 1f);// Cuerpo aleación mate
        private static readonly Color ChassisBezel = new(0.035f, 0.045f, 0.065f, 1f);// Bisel interior de pantalla
        private static readonly Color ArmorBumper = new(0.12f, 0.16f, 0.22f, 1f);   // Protectores de esquina
        private static readonly Color HexBolt = new(0.26f, 0.34f, 0.46f, 1f);       // Tornillos hexagonales
        private static readonly Color GlassDisplay = new(0.02f, 0.035f, 0.055f, 0.98f);// Cristal pantalla embutida
        private static readonly Color CardBg = new(0.045f, 0.075f, 0.12f, 0.96f);    // Tarjeta oscura
        private static readonly Color InsetBg = new(0.02f, 0.035f, 0.055f, 0.95f);   // Pozo hundido para inputs
        private static readonly Color SlotBg = new(0.07f, 0.11f, 0.17f, 0.95f);     // Botón normal

        // Colores de Tipografía y Señales
        private static readonly Color InkWhite = new(0.96f, 0.98f, 1.00f, 1f);       // Texto principal blanco nítido
        private static readonly Color MutedCyan = new(0.48f, 0.68f, 0.80f, 1f);      // Subtítulo técnico
        private static readonly Color DimCyan = new(0.26f, 0.40f, 0.50f, 1f);        // Marcas secundarias
        private static readonly Color CyanAccent = new(0.00f, 0.92f, 0.88f, 1f);     // Acento neón cian
        private static readonly Color AmberAccent = new(1.00f, 0.65f, 0.35f, 1f);    // Acento cálido ámbar
        private static readonly Color SpeakingGreen = new(0.18f, 0.92f, 0.45f, 1f);  // Transmisión de voz activa
        private static readonly Color DangerRed = new(0.92f, 0.25f, 0.25f, 1f);      // Desconectar / Salir
        private static readonly Color GoldPin = new(0.85f, 0.70f, 0.25f, 1f);        // Pines pogo dock
        private static readonly Color HardwareStamp = new(0.28f, 0.38f, 0.50f, 1f);  // Marcas técnicas

        private class CrewRowWidget
        {
            public GameObject Root;
            public MarsFusionAvatar Avatar;
            public TextMeshProUGUI NameLabel;
            public TabletRoundedPanel SpeakingDot;
            public TextMeshProUGUI SpeakingLabel;
            public Slider VolumeSlider;
            public TextMeshProUGUI VolumePercentLabel;
        }

        public static bool IsLobbyScene
        {
            get
            {
                string sceneName = SceneManager.GetActiveScene().name;
                return sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       sceneName.IndexOf("Loby", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (session == null)
                session = FindFirstObjectByType<MarsFusionSession>();

            if (fontAsset == null)
                fontAsset = TMP_Settings.defaultFontAsset;

            BuildMenuHierarchy();
        }

        private void Start()
        {
            GameLocalization.OnLanguageChanged += RefreshAllLocalizedTexts;
            RefreshAllLocalizedTexts();

            // Cargar preferencias de audio y voz
            float masterVol = PlayerPrefs.GetFloat("GameJam.MasterVolume", 1f);
            AudioListener.volume = masterVol;
            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;

            float voiceVol = PlayerPrefs.GetFloat("GameJam.MasterVoiceVolume", 1f);
            MarsProximityVoice.MasterVoiceVolume = voiceVol;
            if (voiceMasterVolumeSlider != null) voiceMasterVolumeSlider.value = voiceVol;

            bool ptt = PlayerPrefs.GetInt("GameJam.VoiceMode", 1) == 1;
            UpdateVoiceModeButtons(ptt);

            UpdateLanguageButtonsHighlight();

            // El menú de inicio sólo es visible en el Lobby cuando NO hay un runner activo
            if (IsLobbyScene)
            {
                if (session != null && session.Runner != null && session.Runner.IsRunning)
                {
                    SetStartMenuVisible(false);
                    SetPauseMenuVisible(false);
                }
                else
                {
                    SetStartMenuVisible(true);
                    SetPauseMenuVisible(false);
                }
            }
            else
            {
                // En escenas de misión (Marte, Neptuno), NUNCA se muestra la terminal de inicio
                SetStartMenuVisible(false);
                SetPauseMenuVisible(false);
            }

            isInitialized = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            GameLocalization.OnLanguageChanged -= RefreshAllLocalizedTexts;
        }

        private void Update()
        {
            if (!isInitialized) return;

            // Manejo de tecla Escape para pausar / reanudar
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (session != null && session.Runner != null && session.Runner.IsRunning)
                {
                    if (pauseMenuGroup != null && pauseMenuGroup.alpha > 0.5f)
                    {
                        ResumeGame();
                    }
                    else
                    {
                        OpenPauseMenu();
                    }
                }
            }

            // Actualizar estado de conexión en terminal de inicio
            if (startMenuGroup != null && startMenuGroup.alpha > 0.05f)
            {
                UpdateStartMenuState();
            }

            // Actualizar tripulación si el menú de pausa está visible
            if (pauseMenuGroup != null && pauseMenuGroup.alpha > 0.05f && activeTab == "Crew")
            {
                if (Time.unscaledTime >= nextCrewCheckTime)
                {
                    nextCrewCheckTime = Time.unscaledTime + 0.15f;
                    UpdateCrewList();
                }
            }
        }

        #region Construcción de la Jerarquía de Interfaces

        private void BuildMenuHierarchy()
        {
            EnsureEventSystem();

            rootCanvas = gameObject.GetComponent<Canvas>();
            if (rootCanvas == null) rootCanvas = gameObject.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            rootCanvas.sortingOrder = 200;
            rootCanvas.pixelPerfect = true;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Fondo translúcido sutil
            var backdrop = CreateRect("Backdrop", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0.01f, 0.015f, 0.025f, 0.70f);
            backdropImg.raycastTarget = false;

            // ==================== 1. TERMINAL DE INICIO (LOBBY) ====================
            var startRoot = CreateRect("StartMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            startMenuGroup = startRoot.gameObject.AddComponent<CanvasGroup>();
            BuildStartTerminalDatapad(startRoot.transform);

            // ==================== 2. MENÚ DE PAUSA ====================
            var pauseRoot = CreateRect("PauseMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pauseMenuGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
            BuildPauseDatapad(pauseRoot.transform);
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var existing = FindFirstObjectByType<EventSystem>();
                if (existing == null)
                {
                    var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    esGo.transform.SetParent(transform, false);
                }
            }
        }

        #endregion

        #region 1. Terminal de Expedición (Estilo Datapad de Campo)

        private void BuildStartTerminalDatapad(Transform parent)
        {
            Vector2 chassisSize = new(720, 800);
            Vector2 displaySize = new(650, 710);

            // Chasis exterior de titanio
            var rim = CreateRoundedPanel(parent, "TerminalRim", chassisSize, 32, ChassisRim);
            rim.anchoredPosition = Vector2.zero;

            // Cuerpo aleación oscura mate
            var body = CreateRoundedPanel(rim, "TerminalBody", chassisSize - new Vector2(8, 8), 28, ChassisMatte);
            body.anchoredPosition = Vector2.zero;

            // 4 Protectores de esquina rugosos con tornillos hexagonales
            CreateArmorCornerBumpers(body, (chassisSize - new Vector2(8, 8)) * 0.5f);

            // Sensores superiores y marcas inferiores de hardware
            CreateTopHardwareSensors(body, chassisSize.x);
            CreateBottomHardwareDock(body, chassisSize.x, "TERMINAL-EXP // DOCK-01");

            // Bisel de pantalla embutida
            var bezel = CreateRoundedPanel(body, "ScreenBezel", displaySize + new Vector2(10, 10), 22, ChassisBezel);
            bezel.anchoredPosition = new Vector2(0, -6);

            // Pantalla de cristal oscuro
            var display = CreateRoundedPanel(bezel, "GlassDisplay", displaySize, 20, GlassDisplay);
            display.anchoredPosition = Vector2.zero;
            display.gameObject.AddComponent<RectMask2D>();

            // --- HEADER DE LA TERMINAL ---
            var badge = CreateLabel(display, "ORBIT AEROSPACE // EXP-TERMINAL MK-IV", 10, new Vector2(0, 318), new Vector2(580, 18), DimCyan);
            badge.alignment = TextAlignmentOptions.Center;

            var title = CreateLabel(display, "menu.main_title", 22, new Vector2(0, 292), new Vector2(580, 32), InkWhite, true);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            var subtitle = CreateLabel(display, "menu.subtitle", 12, new Vector2(0, 268), new Vector2(580, 20), MutedCyan, true);
            subtitle.alignment = TextAlignmentOptions.Center;

            // --- SELECTOR DE IDIOMA ---
            var langCard = CreateCard(display, "LanguageCard", new Vector2(590, 72), new Vector2(0, 210));
            var langLabel = CreateLabel(langCard, "lang.title", 11, new Vector2(0, 18), new Vector2(550, 18), AmberAccent, true);
            langLabel.alignment = TextAlignmentOptions.Center;

            var langRow = CreateRect("LangRow", langCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(560, 32));
            btnStartLangEs = CreateDatapadButton(langRow, "BtnLangES", "ES · Español", new Vector2(-180, 0), new Vector2(170, 30), () => SelectLanguage(GameLanguage.Spanish), out btnStartLangEsBg);
            btnStartLangEn = CreateDatapadButton(langRow, "BtnLangEN", "EN · English", new Vector2(0, 0), new Vector2(170, 30), () => SelectLanguage(GameLanguage.English), out btnStartLangEnBg);
            btnStartLangPt = CreateDatapadButton(langRow, "BtnLangPT", "PT · Brasil", new Vector2(180, 0), new Vector2(170, 30), () => SelectLanguage(GameLanguage.Portuguese), out btnStartLangPtBg);

            // --- NOMBRE DEL ASTRONAUTA ---
            var nameCard = CreateCard(display, "NameCard", new Vector2(590, 74), new Vector2(0, 126));
            var nameHeader = CreateLabel(nameCard, "menu.player_name", 11, new Vector2(0, 18), new Vector2(550, 18), MutedCyan, true);
            nameHeader.alignment = TextAlignmentOptions.Left;

            string savedName = PlayerPrefs.GetString("GameJam.PlayerName", "Astronauta").Trim();
            if (string.IsNullOrEmpty(savedName)) savedName = "Astronauta";
            playerNameInput = CreateInputField(nameCard, "PlayerNameInput", savedName, "menu.player_name_placeholder", new Vector2(0, -12), new Vector2(550, 34));
            playerNameInput.onValueChanged.AddListener(val =>
            {
                PlayerPrefs.SetString("GameJam.PlayerName", val.Trim());
                PlayerPrefs.Save();
            });

            // --- CÓDIGO DE SALA ---
            var roomCard = CreateCard(display, "RoomCard", new Vector2(590, 74), new Vector2(0, 42));
            var roomHeader = CreateLabel(roomCard, "menu.room_code", 11, new Vector2(0, 18), new Vector2(550, 18), MutedCyan, true);
            roomHeader.alignment = TextAlignmentOptions.Left;

            string savedRoom = PlayerPrefs.GetString("GameJam.RoomName", "MARTE-01").Trim();
            if (string.IsNullOrEmpty(savedRoom)) savedRoom = "MARTE-01";
            if (session != null) session.roomName = savedRoom;

            roomCodeInput = CreateInputField(roomCard, "RoomCodeInput", savedRoom, "menu.room_code_placeholder", new Vector2(0, -12), new Vector2(550, 34));
            roomCodeInput.onValueChanged.AddListener(val =>
            {
                string r = val.Trim();
                PlayerPrefs.SetString("GameJam.RoomName", r);
                PlayerPrefs.Save();
                if (session != null) session.roomName = r;
            });

            // --- BOTONES DE CONEXIÓN ---
            btnOnlineStart = CreateDatapadButton(display, "BtnOnline", "menu.btn_join_online", new Vector2(0, -42), new Vector2(590, 48), OnClickStartOnline, out var onlineBg, true);
            onlineBg.color = new Color(0.00f, 0.45f, 0.52f, 0.95f);
            var onlineTxt = btnOnlineStart.GetComponentInChildren<TextMeshProUGUI>();
            if (onlineTxt != null) { onlineTxt.fontSize = 14; onlineTxt.fontStyle = FontStyles.Bold; }

            btnSoloStart = CreateDatapadButton(display, "BtnSolo", "menu.btn_solo", new Vector2(0, -100), new Vector2(590, 44), OnClickStartSolo, out var soloBg, true);
            soloBg.color = new Color(0.10f, 0.15f, 0.22f, 0.95f);
            var soloTxt = btnSoloStart.GetComponentInChildren<TextMeshProUGUI>();
            if (soloTxt != null) { soloTxt.fontSize = 13; soloTxt.fontStyle = FontStyles.Normal; }

            // --- BARRA DE ESTADO DE FUSION ---
            var statusBox = CreateRoundedPanel(display, "StatusBox", new Vector2(590, 44), 10, InsetBg);
            statusBox.anchoredPosition = new Vector2(0, -164);

            var dotRect = CreateRoundedPanel(statusBox, "StatusDot", new Vector2(10, 10), 5, CyanAccent);
            dotRect.anchoredPosition = new Vector2(-265, 0);
            statusDot = dotRect.GetComponent<TabletRoundedPanel>();

            statusLabel = CreateLabel(statusBox, "menu.status_ready", 12, new Vector2(14, 0), new Vector2(520, 24), InkWhite, true);
            statusLabel.alignment = TextAlignmentOptions.Left;

            // --- CONTROLES RÁPIDOS INFORMATIVOS ---
            var controlsHint = CreateLabel(display, "WASD: Desplazamiento · E: Interactuar · Tab: Tablet · Esc: Pausa", 11, new Vector2(0, -215), new Vector2(580, 24), DimCyan);
            controlsHint.alignment = TextAlignmentOptions.Center;
        }

        #endregion

        #region 2. Menú de Pausa (Datapad de Comando)

        private void BuildPauseDatapad(Transform parent)
        {
            Vector2 chassisSize = new(880, 700);
            Vector2 displaySize = new(810, 610);

            // Chasis exterior de titanio
            var rim = CreateRoundedPanel(parent, "PauseRim", chassisSize, 32, ChassisRim);
            rim.anchoredPosition = Vector2.zero;

            // Cuerpo mate
            var body = CreateRoundedPanel(rim, "PauseBody", chassisSize - new Vector2(8, 8), 28, ChassisMatte);
            body.anchoredPosition = Vector2.zero;

            CreateArmorCornerBumpers(body, (chassisSize - new Vector2(8, 8)) * 0.5f);
            CreateTopHardwareSensors(body, chassisSize.x);
            CreateBottomHardwareDock(body, chassisSize.x, "COMMAND-DATAPAD // SECURE-LINK");

            // Bisel y Pantalla de cristal
            var bezel = CreateRoundedPanel(body, "PauseBezel", displaySize + new Vector2(10, 10), 22, ChassisBezel);
            bezel.anchoredPosition = new Vector2(0, -6);

            var display = CreateRoundedPanel(bezel, "PauseDisplay", displaySize, 20, GlassDisplay);
            display.anchoredPosition = Vector2.zero;
            display.gameObject.AddComponent<RectMask2D>();

            // --- HEADER DE PAUSA ---
            var title = CreateLabel(display, "pause.title", 20, new Vector2(0, 268), new Vector2(700, 28), InkWhite, true);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            // Botón de reanudar rápido [X]
            CreateDatapadButton(display, "BtnClosePause", "✕", new Vector2(360, 268), new Vector2(36, 32), ResumeGame, out var closeBg);
            closeBg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);

            // --- FILA DE PESTAÑAS (PILLS) ---
            var tabRow = CreateRect("TabRow", display, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 225), new Vector2(750, 38));
            tabBtnMission = CreateDatapadButton(tabRow, "TabMission", "pause.tab_mission", new Vector2(-240, 0), new Vector2(230, 34), () => SwitchTab("Mission"), out tabBtnMissionBg, true);
            tabBtnSettings = CreateDatapadButton(tabRow, "TabSettings", "pause.tab_settings", new Vector2(0, 0), new Vector2(230, 34), () => SwitchTab("Settings"), out tabBtnSettingsBg, true);
            tabBtnCrew = CreateDatapadButton(tabRow, "TabCrew", "pause.tab_crew", new Vector2(240, 0), new Vector2(230, 34), () => SwitchTab("Crew"), out tabBtnCrewBg, true);

            // Contenedor principal de contenidos de pestañas
            var contentRoot = CreateRect("TabContentArea", display, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(760, 440));

            // ================= 1. TAB: MISIÓN =================
            tabContentMission = CreateRect("MissionContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildMissionTab(tabContentMission.transform);

            // ================= 2. TAB: AJUSTES =================
            tabContentSettings = CreateRect("SettingsContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildSettingsTab(tabContentSettings.transform);

            // ================= 3. TAB: TRIPULACIÓN =================
            tabContentCrew = CreateRect("CrewContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildCrewTab(tabContentCrew.transform);

            SwitchTab("Mission");
        }

        private void BuildMissionTab(Transform parent)
        {
            // Botón Continuar
            var btnResume = CreateDatapadButton(parent, "BtnResume", "pause.btn_resume", new Vector2(0, 150), new Vector2(520, 48), ResumeGame, out var resumeBg, true);
            resumeBg.color = new Color(0.00f, 0.45f, 0.52f, 0.95f);
            var resumeTxt = btnResume.GetComponentInChildren<TextMeshProUGUI>();
            if (resumeTxt != null) { resumeTxt.fontSize = 14; resumeTxt.fontStyle = FontStyles.Bold; }

            // Botón Volver al Lobby
            var btnReturnLobby = CreateDatapadButton(parent, "BtnReturnLobby", "pause.btn_return_lobby", new Vector2(0, 92), new Vector2(520, 44), OnClickReturnToLobby, out var lobbyBg, true);
            lobbyBg.color = new Color(0.12f, 0.18f, 0.26f, 0.95f);

            // Botón Desconectar
            var btnDisconnect = CreateDatapadButton(parent, "BtnDisconnect", "pause.btn_disconnect", new Vector2(0, 38), new Vector2(520, 40), OnClickDisconnect, out var discBg, true);
            discBg.color = new Color(0.24f, 0.08f, 0.10f, 0.95f);
            var discTxt = btnDisconnect.GetComponentInChildren<TextMeshProUGUI>();
            if (discTxt != null) discTxt.color = new Color(1f, 0.65f, 0.65f, 1f);

            // Tarjeta de Controles
            var controlsCard = CreateCard(parent, "ControlsCard", new Vector2(720, 195), new Vector2(0, -95));
            var cardTitle = CreateLabel(controlsCard, "controls.title", 12, new Vector2(0, 72), new Vector2(680, 22), AmberAccent, true);
            cardTitle.alignment = TextAlignmentOptions.Center;

            string[] controlKeys = { "controls.move", "controls.jump", "controls.tablet", "controls.voice", "controls.pause" };
            for (int i = 0; i < controlKeys.Length; i++)
            {
                var label = CreateLabel(controlsCard, controlKeys[i], 12, new Vector2(0, 38 - i * 28), new Vector2(660, 24), InkWhite, true);
                label.alignment = TextAlignmentOptions.Left;
            }
        }

        private void BuildSettingsTab(Transform parent)
        {
            // --- SELECTOR DE IDIOMA EN PAUSA ---
            var langCard = CreateCard(parent, "PauseLangCard", new Vector2(720, 80), new Vector2(0, 145));
            var langLabel = CreateLabel(langCard, "lang.title", 12, new Vector2(0, 20), new Vector2(680, 20), AmberAccent, true);
            langLabel.alignment = TextAlignmentOptions.Center;

            var langRow = CreateRect("PauseLangRow", langCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(680, 34));
            btnPauseLangEs = CreateDatapadButton(langRow, "BtnPauseES", "ES · Español", new Vector2(-220, 0), new Vector2(200, 32), () => SelectLanguage(GameLanguage.Spanish), out btnPauseLangEsBg);
            btnPauseLangEn = CreateDatapadButton(langRow, "BtnPauseEN", "EN · English", new Vector2(0, 0), new Vector2(200, 32), () => SelectLanguage(GameLanguage.English), out btnPauseLangEnBg);
            btnPauseLangPt = CreateDatapadButton(langRow, "BtnPausePT", "PT · Brasil", new Vector2(220, 0), new Vector2(200, 32), () => SelectLanguage(GameLanguage.Portuguese), out btnPauseLangPtBg);

            // --- VOLUMEN GENERAL ---
            var audioCard = CreateCard(parent, "AudioCard", new Vector2(720, 74), new Vector2(0, 55));
            var audioHeader = CreateLabel(audioCard, "audio.master_volume", 12, new Vector2(-60, 16), new Vector2(540, 20), InkWhite, true);
            audioHeader.alignment = TextAlignmentOptions.Left;

            masterVolumePercentLabel = CreateLabel(audioCard, "100%", 12, new Vector2(300, 16), new Vector2(80, 20), CyanAccent);
            masterVolumePercentLabel.alignment = TextAlignmentOptions.Right;

            masterVolumeSlider = CreateSlider(audioCard, "MasterVolSlider", 0f, 1f, AudioListener.volume, new Vector2(0, -14), new Vector2(660, 18));
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);

            // --- CONFIGURACIÓN DE VOZ POR PROXIMIDAD ---
            var voiceCard = CreateCard(parent, "VoiceCard", new Vector2(720, 175), new Vector2(0, -85));
            var voiceTitle = CreateLabel(voiceCard, "voice.title", 13, new Vector2(0, 62), new Vector2(680, 22), AmberAccent, true);
            voiceTitle.alignment = TextAlignmentOptions.Center;

            // Modos de transmisión
            var modeRow = CreateRect("ModeRow", voiceCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(680, 36));
            btnVoicePtt = CreateDatapadButton(modeRow, "BtnPTT", "voice.ptt", new Vector2(-160, 0), new Vector2(300, 32), () => SetVoiceMode(true), out btnVoicePttBg, true);
            btnVoiceOpenMic = CreateDatapadButton(modeRow, "BtnOpenMic", "voice.open_mic", new Vector2(160, 0), new Vector2(300, 32), () => SetVoiceMode(false), out btnVoiceOpenMicBg, true);

            // Slider de volumen de voz
            var voiceVolLabel = CreateLabel(voiceCard, "crew.voice_volume", 12, new Vector2(-60, -20), new Vector2(540, 20), MutedCyan, true);
            voiceVolLabel.alignment = TextAlignmentOptions.Left;

            voiceMasterPercentLabel = CreateLabel(voiceCard, "100%", 12, new Vector2(300, -20), new Vector2(80, 20), CyanAccent);
            voiceMasterPercentLabel.alignment = TextAlignmentOptions.Right;

            voiceMasterVolumeSlider = CreateSlider(voiceCard, "VoiceMasterVolSlider", 0f, 1.5f, MarsProximityVoice.MasterVoiceVolume, new Vector2(0, -46), new Vector2(660, 18));
            voiceMasterVolumeSlider.onValueChanged.AddListener(SetVoiceMasterVolume);

            var rangeLabel = CreateLabel(voiceCard, "voice.distance", 11, new Vector2(0, -70), new Vector2(660, 16), DimCyan, true);
            rangeLabel.alignment = TextAlignmentOptions.Center;
        }

        private void BuildCrewTab(Transform parent)
        {
            var header = CreateLabel(parent, "crew.title", 15, new Vector2(0, 185), new Vector2(720, 26), AmberAccent, true);
            header.alignment = TextAlignmentOptions.Center;

            var scrollCard = CreateCard(parent, "CrewScrollCard", new Vector2(720, 340), new Vector2(0, -15));
            scrollCard.gameObject.AddComponent<RectMask2D>();

            var containerGo = CreateRect("CrewListContent", scrollCard, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 340));
            containerGo.pivot = new Vector2(0.5f, 1f);
            crewListContainer = containerGo;

            var layout = containerGo.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(14, 14, 14, 14);

            var fitter = containerGo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            crewEmptyLabel = CreateLabel(scrollCard, "crew.no_other_players", 13, Vector2.zero, new Vector2(660, 36), MutedCyan, true);
            crewEmptyLabel.alignment = TextAlignmentOptions.Center;
        }

        #endregion

        #region Helpers de Construcción de UI

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform CreateRoundedPanel(Transform parent, string name, Vector2 size, float radius, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TabletRoundedPanel));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            var panel = go.GetComponent<TabletRoundedPanel>();
            panel.radius = radius;
            panel.color = color;
            return rect;
        }

        private static RectTransform CreateCard(Transform parent, string name, Vector2 size, Vector2 anchoredPos)
        {
            var card = CreateRoundedPanel(parent, name, size, 14, CardBg);
            card.anchoredPosition = anchoredPos;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.25f);
            outline.effectDistance = new Vector2(1, -1);
            return card;
        }

        private Button CreateDatapadButton(Transform parent, string name, string textOrKey, Vector2 anchoredPos, Vector2 size, UnityAction onClick, out TabletRoundedPanel bg, bool isLocalized = false)
        {
            var rect = CreateRoundedPanel(parent, name, size, 10, SlotBg);
            rect.anchoredPosition = anchoredPos;
            bg = rect.GetComponent<TabletRoundedPanel>();

            var btn = rect.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f),
                pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            if (onClick != null) btn.onClick.AddListener(onClick);

            var label = CreateLabel(rect, textOrKey, 12, Vector2.zero, size - new Vector2(12, 6), InkWhite, isLocalized);
            label.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private TMP_InputField CreateInputField(Transform parent, string name, string initialText, string placeholderKey, Vector2 anchoredPos, Vector2 size)
        {
            var bgRect = CreateRoundedPanel(parent, name, size, 8, InsetBg);
            bgRect.anchoredPosition = anchoredPos;
            var outline = bgRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
            outline.effectDistance = new Vector2(1, -1);

            var textRect = CreateRect("Text", bgRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -8));
            var textComp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            textComp.font = fontAsset ?? TMP_Settings.defaultFontAsset;
            textComp.fontSize = 13;
            textComp.color = InkWhite;
            textComp.alignment = TextAlignmentOptions.Left;

            var phRect = CreateRect("Placeholder", bgRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -8));
            var phComp = phRect.gameObject.AddComponent<TextMeshProUGUI>();
            phComp.font = fontAsset ?? TMP_Settings.defaultFontAsset;
            phComp.fontSize = 13;
            phComp.color = DimCyan;
            phComp.alignment = TextAlignmentOptions.Left;
            var loc = phComp.gameObject.AddComponent<LocalizedText>();
            loc.SetKey(placeholderKey);

            var input = bgRect.gameObject.AddComponent<TMP_InputField>();
            input.textComponent = textComp;
            input.placeholder = phComp;
            input.text = initialText;
            input.characterLimit = 32;

            return input;
        }

        private Slider CreateSlider(Transform parent, string name, float min, float max, float currentVal, Vector2 anchoredPos, Vector2 size)
        {
            var root = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);

            // Pista del slider
            var track = CreateRoundedPanel(root, "Track", new Vector2(size.x, 8), 4, new Color(0.02f, 0.035f, 0.05f));
            track.anchoredPosition = Vector2.zero;

            // Área de relleno
            var fillArea = CreateRect("FillArea", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16, 0));
            var fill = CreateRoundedPanel(fillArea, "Fill", new Vector2(0, 8), 4, CyanAccent);
            fill.anchorMin = new Vector2(0, 0.5f);
            fill.anchorMax = new Vector2(0, 0.5f);
            fill.pivot = new Vector2(0, 0.5f);

            // Handle área
            var handleArea = CreateRect("HandleArea", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16, 0));
            var handle = CreateRoundedPanel(handleArea, "Handle", new Vector2(16, 16), 8, InkWhite);
            var handlePin = CreateRoundedPanel(handle, "Pin", new Vector2(6, 6), 3, new Color(0.00f, 0.50f, 0.60f));
            handlePin.anchoredPosition = Vector2.zero;

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<TabletRoundedPanel>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = currentVal;

            return slider;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string textOrKey, float fontSize, Vector2 anchoredPos, Vector2 size, Color color, bool isLocalized = false)
        {
            var rect = CreateRect("Label", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = fontAsset ?? TMP_Settings.defaultFontAsset;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.raycastTarget = false;

            if (isLocalized)
            {
                var loc = tmp.gameObject.AddComponent<LocalizedText>();
                loc.SetKey(textOrKey);
            }
            else
            {
                tmp.text = textOrKey;
            }

            return tmp;
        }

        private static void CreateArmorCornerBumpers(RectTransform parent, Vector2 halfSize)
        {
            Vector2[] corners = {
                new(-halfSize.x + 18, halfSize.y - 18),
                new( halfSize.x - 18, halfSize.y - 18),
                new(-halfSize.x + 18, -halfSize.y + 18),
                new( halfSize.x - 18, -halfSize.y + 18)
            };

            foreach (var pos in corners)
            {
                var plate = CreateRoundedPanel(parent, "CornerArmor", new Vector2(30, 30), 8, ArmorBumper);
                plate.anchoredPosition = pos;

                var boltRim = CreateRoundedPanel(plate, "HexBolt", new Vector2(10, 10), 5, HexBolt);
                boltRim.anchoredPosition = Vector2.zero;

                var boltCenter = CreateRoundedPanel(boltRim, "Pin", new Vector2(4, 4), 2, new Color(0.08f, 0.12f, 0.16f));
                boltCenter.anchoredPosition = Vector2.zero;
            }
        }

        private static void CreateTopHardwareSensors(RectTransform parent, float width)
        {
            var cluster = CreateRect("SensorCluster", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(width * 0.5f, 18));

            var atmosGrille = CreateRoundedPanel(cluster, "AtmosSensorGrille", new Vector2(44, 4), 2, new Color(0.02f, 0.035f, 0.05f));
            atmosGrille.anchoredPosition = new Vector2(-55, 0);

            var lensRim = CreateRoundedPanel(cluster, "VisualLensRim", new Vector2(12, 12), 6, new Color(0.02f, 0.03f, 0.05f));
            lensRim.anchoredPosition = new Vector2(-15, 0);
            var lensIris = CreateRoundedPanel(lensRim, "LensIris", new Vector2(6, 6), 3, new Color(0.10f, 0.40f, 0.70f));
            lensIris.anchoredPosition = Vector2.zero;

            var irLens = CreateRoundedPanel(cluster, "IRSensor", new Vector2(8, 8), 4, new Color(0.25f, 0.08f, 0.12f));
            irLens.anchoredPosition = new Vector2(2, 0);

            var led = CreateRoundedPanel(cluster, "StatusLED", new Vector2(6, 6), 3, new Color(0.00f, 0.96f, 0.74f, 0.95f));
            led.anchoredPosition = new Vector2(40, 0);
        }

        private static void CreateBottomHardwareDock(RectTransform parent, float width, string serial)
        {
            float halfH = parent.sizeDelta.y * 0.5f;

            var leftStamp = CreateRect("StampLeft", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-width * 0.28f, -halfH + 12), new Vector2(width * 0.42f, 16)).gameObject.AddComponent<TextMeshProUGUI>();
            leftStamp.font = TMP_Settings.defaultFontAsset;
            leftStamp.fontSize = 9;
            leftStamp.color = HardwareStamp;
            leftStamp.text = "ORBIT AEROSPACE // EXP-DATAPAD MK-VII";
            leftStamp.alignment = TextAlignmentOptions.Left;

            var rightStamp = CreateRect("StampRight", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width * 0.28f, -halfH + 12), new Vector2(width * 0.42f, 16)).gameObject.AddComponent<TextMeshProUGUI>();
            rightStamp.font = TMP_Settings.defaultFontAsset;
            rightStamp.fontSize = 9;
            rightStamp.color = HardwareStamp;
            rightStamp.text = serial;
            rightStamp.alignment = TextAlignmentOptions.Right;

            var dockSlot = CreateRoundedPanel(parent, "PogoDockConnector", new Vector2(80, 7), 3.5f, new Color(0.02f, 0.035f, 0.05f));
            dockSlot.anchoredPosition = new Vector2(0, -halfH + 12);

            for (int p = -2; p <= 2; p++)
            {
                var pin = CreateRoundedPanel(dockSlot, $"Pin_{p}", new Vector2(4, 4), 2, GoldPin);
                pin.anchoredPosition = new Vector2(p * 14, 0);
            }
        }

        #endregion

        #region Control de Visibilidad y Pestañas

        public void SetStartMenuVisible(bool visible)
        {
            // La terminal de inicio NUNCA debe mostrarse fuera del Lobby
            if (visible && !IsLobbyScene)
            {
                visible = false;
            }

            if (startMenuGroup != null)
            {
                startMenuGroup.alpha = visible ? 1f : 0f;
                startMenuGroup.blocksRaycasts = visible;
                startMenuGroup.interactable = visible;
            }

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (pauseMenuGroup == null || pauseMenuGroup.alpha < 0.5f)
            {
                if (session != null && session.Runner != null && session.Runner.IsRunning)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public void SetPauseMenuVisible(bool visible)
        {
            if (pauseMenuGroup != null)
            {
                pauseMenuGroup.alpha = visible ? 1f : 0f;
                pauseMenuGroup.blocksRaycasts = visible;
                pauseMenuGroup.interactable = visible;
            }

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                SwitchTab("Mission");
            }
            else if (startMenuGroup == null || startMenuGroup.alpha < 0.5f)
            {
                if (session != null && session.Runner != null && session.Runner.IsRunning)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        public void OpenPauseMenu() => SetPauseMenuVisible(true);

        public void ResumeGame() => SetPauseMenuVisible(false);

        public void SwitchTab(string tabName)
        {
            activeTab = tabName;
            if (tabContentMission != null) tabContentMission.SetActive(tabName == "Mission");
            if (tabContentSettings != null) tabContentSettings.SetActive(tabName == "Settings");
            if (tabContentCrew != null) tabContentCrew.SetActive(tabName == "Crew");

            Color activeTabColor = new(0.00f, 0.45f, 0.52f, 0.95f);
            Color inactiveTabColor = SlotBg;

            if (tabBtnMissionBg != null) tabBtnMissionBg.color = tabName == "Mission" ? activeTabColor : inactiveTabColor;
            if (tabBtnSettingsBg != null) tabBtnSettingsBg.color = tabName == "Settings" ? activeTabColor : inactiveTabColor;
            if (tabBtnCrewBg != null) tabBtnCrewBg.color = tabName == "Crew" ? activeTabColor : inactiveTabColor;

            if (tabName == "Crew")
            {
                UpdateCrewList();
            }
        }

        #endregion

        #region Lógica de Sesión y Transiciones

        private void OnClickStartOnline()
        {
            if (session == null || session.Busy) return;

            string room = roomCodeInput != null ? roomCodeInput.text.Trim() : "MARTE-01";
            if (string.IsNullOrEmpty(room)) room = "MARTE-01";
            session.roomName = room;

            _ = session.StartSession(false);
        }

        private void OnClickStartSolo()
        {
            if (session == null || session.Busy) return;
            _ = session.StartSession(true);
        }

        private void OnClickReturnToLobby()
        {
            int lobbyIndex = SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/j_LobyDeEspera.unity");
            if (lobbyIndex < 0) lobbyIndex = 11;

            if (session != null && session.Runner != null && session.Runner.IsRunning)
            {
                if (session.Runner.IsServer || session.Runner.IsSharedModeMasterClient)
                {
                    // Autoridad / Host: devuelve a todo el escuadrón conectado al Lobby
                    ResumeGame();
                    session.Runner.LoadScene(Fusion.SceneRef.FromIndex(lobbyIndex));
                    return;
                }
            }

            // Cliente o sin sesión: desconectar y volver al lobby
            OnClickDisconnect();
        }

        private void OnClickDisconnect()
        {
            ResumeGame();
            if (session != null)
            {
                _ = session.Disconnect();
            }
            else
            {
                int lobbyIndex = SceneUtility.GetBuildIndexByScenePath("Assets/00_Scenes/j_LobyDeEspera.unity");
                if (lobbyIndex >= 0)
                    SceneManager.LoadScene(lobbyIndex);
            }
        }

        private void UpdateStartMenuState()
        {
            if (session == null) return;

            bool busy = session.Busy;
            if (btnOnlineStart != null) btnOnlineStart.interactable = !busy;
            if (btnSoloStart != null) btnSoloStart.interactable = !busy;

            if (statusLabel != null)
            {
                statusLabel.text = session.Status;
            }

            if (statusDot != null)
            {
                if (session.Busy)
                    statusDot.color = AmberAccent;
                else if (session.Runner != null && session.Runner.IsRunning)
                    statusDot.color = SpeakingGreen;
                else
                    statusDot.color = CyanAccent;
            }
        }

        #endregion

        #region Ajustes de Audio y Modo de Voz

        public void SelectLanguage(GameLanguage lang)
        {
            GameLocalization.CurrentLanguage = lang;
            UpdateLanguageButtonsHighlight();
        }

        private void UpdateLanguageButtonsHighlight()
        {
            var cur = GameLocalization.CurrentLanguage;
            Color activeColor = new(0.00f, 0.45f, 0.52f, 0.95f);
            Color inactiveColor = SlotBg;

            if (btnStartLangEsBg != null) btnStartLangEsBg.color = cur == GameLanguage.Spanish ? activeColor : inactiveColor;
            if (btnStartLangEnBg != null) btnStartLangEnBg.color = cur == GameLanguage.English ? activeColor : inactiveColor;
            if (btnStartLangPtBg != null) btnStartLangPtBg.color = cur == GameLanguage.Portuguese ? activeColor : inactiveColor;

            if (btnPauseLangEsBg != null) btnPauseLangEsBg.color = cur == GameLanguage.Spanish ? activeColor : inactiveColor;
            if (btnPauseLangEnBg != null) btnPauseLangEnBg.color = cur == GameLanguage.English ? activeColor : inactiveColor;
            if (btnPauseLangPtBg != null) btnPauseLangPtBg.color = cur == GameLanguage.Portuguese ? activeColor : inactiveColor;
        }

        private void SetMasterVolume(float val)
        {
            AudioListener.volume = val;
            PlayerPrefs.SetFloat("GameJam.MasterVolume", val);
            PlayerPrefs.Save();
            if (masterVolumePercentLabel != null)
                masterVolumePercentLabel.text = Mathf.RoundToInt(val * 100f) + "%";
        }

        private void SetVoiceMode(bool ptt)
        {
            PlayerPrefs.SetInt("GameJam.VoiceMode", ptt ? 1 : 0);
            PlayerPrefs.Save();

            MarsProximityVoice.LocalPushToTalk = ptt;
            var localAvatar = MarsFusionAvatar.Local;
            if (localAvatar != null && localAvatar.TryGetComponent<MarsProximityVoice>(out var voice))
            {
                voice.SetPushToTalk(ptt);
            }

            UpdateVoiceModeButtons(ptt);
        }

        private void UpdateVoiceModeButtons(bool ptt)
        {
            Color activeColor = new(0.00f, 0.45f, 0.52f, 0.95f);
            Color inactiveColor = SlotBg;

            if (btnVoicePttBg != null) btnVoicePttBg.color = ptt ? activeColor : inactiveColor;
            if (btnVoiceOpenMicBg != null) btnVoiceOpenMicBg.color = !ptt ? activeColor : inactiveColor;
        }

        private void SetVoiceMasterVolume(float val)
        {
            MarsProximityVoice.MasterVoiceVolume = val;
            PlayerPrefs.SetFloat("GameJam.MasterVoiceVolume", val);
            PlayerPrefs.Save();

            if (voiceMasterPercentLabel != null)
                voiceMasterPercentLabel.text = Mathf.RoundToInt(val * 100f) + "%";
        }

        private void RefreshAllLocalizedTexts()
        {
            foreach (var loc in FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                loc.Refresh();
            }

            if (masterVolumeSlider != null && masterVolumePercentLabel != null)
                masterVolumePercentLabel.text = Mathf.RoundToInt(masterVolumeSlider.value * 100f) + "%";

            if (voiceMasterVolumeSlider != null && voiceMasterPercentLabel != null)
                voiceMasterPercentLabel.text = Mathf.RoundToInt(voiceMasterVolumeSlider.value * 100f) + "%";

            UpdateLanguageButtonsHighlight();
        }

        #endregion

        #region Lista de Tripulación y Audio Individual

        private void UpdateCrewList()
        {
            if (crewListContainer == null) return;

            var allAvatars = FindObjectsByType<MarsFusionAvatar>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var localAvatar = MarsFusionAvatar.Local;

            int neededRows = allAvatars.Length;
            while (activeCrewRows.Count < neededRows)
            {
                activeCrewRows.Add(CreateCrewRowWidget());
            }

            for (int i = 0; i < activeCrewRows.Count; i++)
            {
                if (i < neededRows)
                {
                    activeCrewRows[i].Root.SetActive(true);
                    BindCrewRow(activeCrewRows[i], allAvatars[i], localAvatar);
                }
                else
                {
                    activeCrewRows[i].Root.SetActive(false);
                }
            }

            if (crewEmptyLabel != null)
            {
                crewEmptyLabel.gameObject.SetActive(neededRows <= 1);
            }
        }

        private CrewRowWidget CreateCrewRowWidget()
        {
            var widget = new CrewRowWidget();

            var rowCard = CreateRoundedPanel(crewListContainer, "CrewRow", new Vector2(680, 56), 10, CardBg);
            widget.Root = rowCard.gameObject;

            var outline = rowCard.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.00f, 0.92f, 0.88f, 0.20f);
            outline.effectDistance = new Vector2(1, -1);

            // Icono de casco
            var avatarPill = CreateRoundedPanel(rowCard, "AvatarPill", new Vector2(36, 36), 18, InsetBg);
            avatarPill.anchoredPosition = new Vector2(-310, 0);

            var avatarDot = CreateRoundedPanel(avatarPill, "AvatarCenter", new Vector2(16, 16), 8, CyanAccent);
            avatarDot.anchoredPosition = Vector2.zero;

            // Nombre del astronauta
            widget.NameLabel = CreateLabel(rowCard, "Astronauta", 13, new Vector2(-170, 0), new Vector2(230, 26), InkWhite);
            widget.NameLabel.alignment = TextAlignmentOptions.Left;
            widget.NameLabel.fontStyle = FontStyles.Bold;

            // Indicador de habla
            var speakPill = CreateRoundedPanel(rowCard, "SpeakPill", new Vector2(120, 28), 6, InsetBg);
            speakPill.anchoredPosition = new Vector2(30, 0);

            var dotRect = CreateRoundedPanel(speakPill, "Dot", new Vector2(8, 8), 4, DimCyan);
            dotRect.anchoredPosition = new Vector2(-44, 0);
            widget.SpeakingDot = dotRect.GetComponent<TabletRoundedPanel>();

            widget.SpeakingLabel = CreateLabel(speakPill, "crew.muted", 10, new Vector2(10, 0), new Vector2(80, 20), DimCyan, true);
            widget.SpeakingLabel.alignment = TextAlignmentOptions.Left;

            // Slider de volumen individual
            var sliderRoot = CreateRect("VolumeGroup", rowCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230, 0), new Vector2(170, 40));

            var volLabel = CreateLabel(sliderRoot, "VOL", 10, new Vector2(-70, 0), new Vector2(30, 20), MutedCyan);
            volLabel.alignment = TextAlignmentOptions.Center;

            widget.VolumePercentLabel = CreateLabel(sliderRoot, "100%", 10, new Vector2(65, 0), new Vector2(40, 20), CyanAccent);
            widget.VolumePercentLabel.alignment = TextAlignmentOptions.Left;

            widget.VolumeSlider = CreateSlider(sliderRoot, "IndivVolSlider", 0f, 1.5f, 1f, new Vector2(2, 0), new Vector2(90, 16));

            return widget;
        }

        private void BindCrewRow(CrewRowWidget widget, MarsFusionAvatar avatar, MarsFusionAvatar localAvatar)
        {
            widget.Avatar = avatar;
            bool isSelf = avatar != null && avatar == localAvatar;

            string name = avatar != null ? avatar.PlayerName : "Astronauta";
            if (isSelf)
            {
                name += " " + GameLocalization.Get("crew.you", "(Tú)");
            }
            widget.NameLabel.text = name;

            bool isSpeaking = false;
            if (avatar != null && avatar.TryGetComponent<MarsProximityVoice>(out var voice))
            {
                isSpeaking = voice.IsSpeaking;
            }

            if (widget.SpeakingDot != null)
            {
                widget.SpeakingDot.color = isSpeaking ? SpeakingGreen : DimCyan;
            }

            if (widget.SpeakingLabel != null)
            {
                widget.SpeakingLabel.text = isSpeaking
                    ? GameLocalization.Get("crew.speaking", "TRANSMITIENDO")
                    : GameLocalization.Get("crew.muted", "EN ESPERA");
                widget.SpeakingLabel.color = isSpeaking ? SpeakingGreen : DimCyan;
            }

            if (widget.VolumeSlider != null)
            {
                widget.VolumeSlider.gameObject.SetActive(!isSelf);
                if (widget.VolumePercentLabel != null)
                    widget.VolumePercentLabel.gameObject.SetActive(!isSelf);

                if (!isSelf && avatar != null)
                {
                    widget.VolumeSlider.onValueChanged.RemoveAllListeners();
                    string prefKey = "GameJam.PlayerVol_" + avatar.PlayerName;
                    float savedVol = PlayerPrefs.GetFloat(prefKey, 1f);
                    widget.VolumeSlider.value = savedVol;
                    if (widget.VolumePercentLabel != null)
                        widget.VolumePercentLabel.text = Mathf.RoundToInt(savedVol * 100f) + "%";

                    widget.VolumeSlider.onValueChanged.AddListener(val =>
                    {
                        PlayerPrefs.SetFloat(prefKey, val);
                        if (widget.VolumePercentLabel != null)
                            widget.VolumePercentLabel.text = Mathf.RoundToInt(val * 100f) + "%";

                        if (avatar.TryGetComponent<MarsProximityVoice>(out var v))
                        {
                            v.SetIndividualVolume(val);
                        }
                    });
                }
            }
        }

        #endregion
    }
}
