using System;
using System.Collections.Generic;
using GameJam.Localization;
using GameJam.Multiplayer;
using HeyHeyThere.PixelUISciFiFree;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GameJam.UI
{
    /// <summary>
    /// Gestor integral de menús Sci-Fi para inicio de sesión y pausa en el juego.
    /// Utiliza el kit de diseño PixelUISciFiFree (Cyan Theme) y se integra con el sistema
    /// de localización (Español, Inglés, Portugués Brasil), configuración de chat de voz
    /// por proximidad, volumen general y sliders de volumen individual por astronauta.
    /// </summary>
    public sealed class SciFiGameMenuManager : MonoBehaviour
    {
        public static SciFiGameMenuManager Instance { get; private set; }

        [Header("Referencias de Sesión")]
        [SerializeField] private MarsFusionSession session;

        [Header("Tema y Fuentes")]
        [SerializeField] private PixelTheme theme;
        [SerializeField] private TMP_FontAsset fontAsset;

        // Raíces y Grupos de Canvas para rendimiento óptimo (sin re-creación continua de UI)
        private Canvas rootCanvas;
        private CanvasGroup startMenuGroup;
        private CanvasGroup pauseMenuGroup;

        // Componentes de Inicio
        private TMP_InputField playerNameInput;
        private TMP_InputField roomCodeInput;
        private PixelButton btnOnlineStart;
        private PixelButton btnSoloStart;
        private TextMeshProUGUI statusLabel;
        private Image statusDot;

        // Botones de Idioma Inicio
        private PixelButton btnStartLangEs;
        private PixelButton btnStartLangEn;
        private PixelButton btnStartLangPt;

        // Botones de Idioma Pausa
        private PixelButton btnPauseLangEs;
        private PixelButton btnPauseLangEn;
        private PixelButton btnPauseLangPt;

        // Pestañas del Menú de Pausa
        private PixelButton tabBtnMission;
        private PixelButton tabBtnSettings;
        private PixelButton tabBtnCrew;
        private GameObject tabContentMission;
        private GameObject tabContentSettings;
        private GameObject tabContentCrew;
        private string activeTab = "Mission";

        // Ajustes de Audio y Voz
        private Slider masterVolumeSlider;
        private TextMeshProUGUI masterVolumePercentLabel;
        private PixelButton btnVoicePtt;
        private PixelButton btnVoiceOpenMic;
        private Slider voiceMasterVolumeSlider;
        private TextMeshProUGUI voiceMasterPercentLabel;

        // Lista de Tripulación
        private RectTransform crewListContainer;
        private TextMeshProUGUI crewEmptyLabel;
        private readonly List<CrewRowWidget> activeCrewRows = new();

        // Control de tiempo para polling suave de tripulación
        private float nextCrewCheckTime;
        private bool isInitialized;

        // Colores de diseño temático Cyan Sci-Fi
        private static readonly Color DeepSpaceBg = new(0.015f, 0.025f, 0.045f, 0.92f);
        private static readonly Color WindowFrameCyan = new(0.00f, 0.90f, 0.95f, 1f);
        private static readonly Color AccentGlow = new(0.00f, 0.95f, 0.90f, 1f);
        private static readonly Color TextWhite = new(0.96f, 0.98f, 1.00f, 1f);
        private static readonly Color TextMuted = new(0.50f, 0.70f, 0.82f, 1f);
        private static readonly Color TextDim = new(0.32f, 0.44f, 0.54f, 1f);
        private static readonly Color InsetCardBg = new(0.025f, 0.045f, 0.075f, 0.95f);
        private static readonly Color InsetSlotBg = new(0.05f, 0.08f, 0.12f, 0.95f);
        private static readonly Color SpeakingGreen = new(0.18f, 0.92f, 0.45f, 1f);

        private class CrewRowWidget
        {
            public GameObject Root;
            public MarsFusionAvatar Avatar;
            public Image AvatarIcon;
            public TextMeshProUGUI NameLabel;
            public Image SpeakingBadge;
            public TextMeshProUGUI SpeakingLabel;
            public GameObject VolumeControlGroup;
            public Slider VolumeSlider;
            public TextMeshProUGUI VolumePercentLabel;
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

            LoadThemeAssets();
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

            // Determinar estado inicial según si hay runner activo
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

            // Actualizar estado de conexión y botones de inicio
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

        private void LoadThemeAssets()
        {
#if UNITY_EDITOR
            if (theme == null)
            {
                theme = UnityEditor.AssetDatabase.LoadAssetAtPath<PixelTheme>("Assets/PixelUISciFiFree/Themes/Cyan.asset");
            }
            if (fontAsset == null)
            {
                fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/PixelUISciFiFree/Font/Pixel UI Sci-Fi TMP.asset");
            }
#endif
            if (fontAsset == null)
            {
                fontAsset = TMP_Settings.defaultFontAsset;
            }
        }

        #region UI Construction

        private void BuildMenuHierarchy()
        {
            EnsureEventSystem();

            // Canvas Principal
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

            // Fondo oscuro de espacio cósmico
            var backdropGo = CreateRect("Backdrop", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var backdropImg = backdropGo.AddComponent<Image>();
            backdropImg.color = DeepSpaceBg;
            backdropImg.raycastTarget = false;

            // ==================== 1. MENÚ DE INICIO / CONEXIÓN ====================
            var startRoot = CreateRect("StartMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            startMenuGroup = startRoot.AddComponent<CanvasGroup>();

            BuildStartMenuWindow(startRoot.transform);

            // ==================== 2. MENÚ DE PAUSA ====================
            var pauseRoot = CreateRect("PauseMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            pauseMenuGroup = pauseRoot.AddComponent<CanvasGroup>();

            BuildPauseMenuWindow(pauseRoot.transform);
        }

        private void EnsureEventSystem()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = FindFirstObjectByType<EventSystem>();
            }
            if (eventSystem == null)
            {
                var esGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                esGo.transform.SetParent(transform, false);
            }
        }

        private void BuildStartMenuWindow(Transform parent)
        {
            // Ventana Principal Centrada (660 x 740)
            var winRect = CreateWindowPanel("StartWindow", parent, new Vector2(660, 750), Vector2.zero);

            // --- HEADER DE LA TERMINAL ---
            var headerGo = CreateRect("Header", winRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -45), new Vector2(600, 70));
            var title = CreateLabel(headerGo.transform, "menu.main_title", 24, Vector2.zero, new Vector2(580, 36), TextWhite, true);
            title.alignment = TextAlignmentOptions.Center;

            var subtitle = CreateLabel(headerGo.transform, "menu.subtitle", 13, new Vector2(0, -28), new Vector2(580, 22), TextMuted, true);
            subtitle.alignment = TextAlignmentOptions.Center;

            // --- SELECTOR DE IDIOMA AL PRINCIPIO DE TODO ---
            var langCard = CreateInsetCard("LanguageCard", winRect, new Vector2(0, 215), new Vector2(580, 80));
            var langLabel = CreateLabel(langCard.transform, "lang.title", 12, new Vector2(0, 20), new Vector2(540, 20), AccentGlow, true);
            langLabel.alignment = TextAlignmentOptions.Center;

            var langRow = CreateRect("LangButtons", langCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(540, 36));
            btnStartLangEs = CreateSciFiButton("BtnLangES", langRow.transform, "ES · Español", new Vector2(-180, 0), new Vector2(170, 34), () => SelectLanguage(GameLanguage.Spanish));
            btnStartLangEn = CreateSciFiButton("BtnLangEN", langRow.transform, "EN · English", new Vector2(0, 0), new Vector2(170, 34), () => SelectLanguage(GameLanguage.English));
            btnStartLangPt = CreateSciFiButton("BtnLangPT", langRow.transform, "PT · Brasil", new Vector2(180, 0), new Vector2(170, 34), () => SelectLanguage(GameLanguage.Portuguese));

            if (btnStartLangEs.label != null) btnStartLangEs.label.fontSize = 11;
            if (btnStartLangEn.label != null) btnStartLangEn.label.fontSize = 11;
            if (btnStartLangPt.label != null) btnStartLangPt.label.fontSize = 11;

            // --- DATOS DEL ASTRONAUTA ---
            var nameCard = CreateInsetCard("NameCard", winRect, new Vector2(0, 115), new Vector2(580, 80));
            var nameHeader = CreateLabel(nameCard.transform, "menu.player_name", 12, new Vector2(0, 22), new Vector2(540, 20), TextMuted, true);
            nameHeader.alignment = TextAlignmentOptions.Left;

            string savedName = PlayerPrefs.GetString("GameJam.PlayerName", "Astronauta").Trim();
            if (string.IsNullOrEmpty(savedName)) savedName = "Astronauta";
            playerNameInput = CreateInputField("PlayerNameInput", nameCard.transform, savedName, "menu.player_name_placeholder", new Vector2(0, -12), new Vector2(540, 38));
            playerNameInput.onValueChanged.AddListener(val =>
            {
                PlayerPrefs.SetString("GameJam.PlayerName", val.Trim());
                PlayerPrefs.Save();
            });

            // --- CÓDIGO DE SALA ---
            var roomCard = CreateInsetCard("RoomCard", winRect, new Vector2(0, 15), new Vector2(580, 80));
            var roomHeader = CreateLabel(roomCard.transform, "menu.room_code", 12, new Vector2(0, 22), new Vector2(540, 20), TextMuted, true);
            roomHeader.alignment = TextAlignmentOptions.Left;

            string savedRoom = PlayerPrefs.GetString("GameJam.RoomName", "MARTE-01").Trim();
            if (string.IsNullOrEmpty(savedRoom)) savedRoom = "MARTE-01";
            if (session != null) session.roomName = savedRoom;

            roomCodeInput = CreateInputField("RoomCodeInput", roomCard.transform, savedRoom, "menu.room_code_placeholder", new Vector2(0, -12), new Vector2(540, 38));
            roomCodeInput.onValueChanged.AddListener(val =>
            {
                string r = val.Trim();
                PlayerPrefs.SetString("GameJam.RoomName", r);
                PlayerPrefs.Save();
                if (session != null) session.roomName = r;
            });

            // --- BOTONES DE CONEXIÓN ---
            btnOnlineStart = CreateSciFiButton("BtnOnline", winRect, "menu.btn_join_online", new Vector2(0, -85), new Vector2(580, 52), OnClickStartOnline, true);
            if (btnOnlineStart.label != null)
            {
                btnOnlineStart.label.fontSize = 14;
                btnOnlineStart.label.enableAutoSizing = true;
                btnOnlineStart.label.fontSizeMin = 10;
                btnOnlineStart.label.fontSizeMax = 15;
            }

            btnSoloStart = CreateSciFiButton("BtnSolo", winRect, "menu.btn_solo", new Vector2(0, -145), new Vector2(580, 46), OnClickStartSolo, true);
            if (btnSoloStart.label != null)
            {
                btnSoloStart.label.fontSize = 13;
                btnSoloStart.label.enableAutoSizing = true;
                btnSoloStart.label.fontSizeMin = 10;
                btnSoloStart.label.fontSizeMax = 14;
            }

            // --- BARRA DE ESTADO DE FUSION ---
            var statusBox = CreateInsetCard("StatusBox", winRect, new Vector2(0, -220), new Vector2(580, 60));
            var dotGo = CreateRect("StatusDot", statusBox.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(12, 12)).gameObject;
            statusDot = dotGo.AddComponent<Image>();
            statusDot.color = AccentGlow;

            statusLabel = CreateLabel(statusBox.transform, "menu.status_ready", 13, new Vector2(48, 0), new Vector2(510, 40), TextWhite, true);
            statusLabel.alignment = TextAlignmentOptions.Left;

            // --- GUÍA DE TECLAS RÁPIDA ---
            var bottomHelp = CreateLabel(winRect, "controls.move", 11, new Vector2(0, -295), new Vector2(580, 30), TextDim, true);
            bottomHelp.alignment = TextAlignmentOptions.Center;
        }

        private void BuildPauseMenuWindow(Transform parent)
        {
            // Ventana Centrada (840 x 680)
            var winRect = CreateWindowPanel("PauseWindow", parent, new Vector2(840, 680), Vector2.zero);

            // --- HEADER DE PAUSA ---
            var headerGo = CreateRect("Header", winRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -42), new Vector2(760, 50));
            var title = CreateLabel(headerGo.transform, "pause.title", 24, Vector2.zero, new Vector2(740, 34), TextWhite, true);
            title.alignment = TextAlignmentOptions.Center;

            // --- PESTAÑAS SUPERIORES ---
            var tabRow = CreateRect("TabRow", winRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(760, 42));
            tabBtnMission = CreateSciFiButton("TabMission", tabRow.transform, "pause.tab_mission", new Vector2(-250, 0), new Vector2(230, 38), () => SwitchTab("Mission"), true);
            tabBtnSettings = CreateSciFiButton("TabSettings", tabRow.transform, "pause.tab_settings", new Vector2(0, 0), new Vector2(230, 38), () => SwitchTab("Settings"), true);
            tabBtnCrew = CreateSciFiButton("TabCrew", tabRow.transform, "pause.tab_crew", new Vector2(250, 0), new Vector2(230, 38), () => SwitchTab("Crew"), true);

            if (tabBtnMission.label != null) tabBtnMission.label.fontSize = 13;
            if (tabBtnSettings.label != null) tabBtnSettings.label.fontSize = 13;
            if (tabBtnCrew.label != null) tabBtnCrew.label.fontSize = 13;

            // Contenedor principal de contenidos de pestañas
            var contentRoot = CreateRect("TabContentArea", winRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(760, 480));

            // ================= 1. TAB: MISIÓN =================
            tabContentMission = CreateRect("MissionContent", contentRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildMissionTab(tabContentMission.transform);

            // ================= 2. TAB: AJUSTES =================
            tabContentSettings = CreateRect("SettingsContent", contentRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildSettingsTab(tabContentSettings.transform);

            // ================= 3. TAB: TRIPULACIÓN =================
            tabContentCrew = CreateRect("CrewContent", contentRoot.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildCrewTab(tabContentCrew.transform);

            SwitchTab("Mission");
        }

        private void BuildMissionTab(Transform parent)
        {
            // Botón Continuar
            var btnResume = CreateSciFiButton("BtnResume", parent, "pause.btn_resume", new Vector2(0, 160), new Vector2(500, 54), ResumeGame, true);
            if (btnResume.label != null)
            {
                btnResume.label.fontSize = 15;
                btnResume.label.enableAutoSizing = true;
                btnResume.label.fontSizeMin = 12;
                btnResume.label.fontSizeMax = 16;
            }

            // Botón Desconectar
            var btnDisconnect = CreateSciFiButton("BtnDisconnect", parent, "pause.btn_disconnect", new Vector2(0, 95), new Vector2(500, 48), OnClickDisconnect, true);
            if (btnDisconnect.label != null)
            {
                btnDisconnect.label.fontSize = 14;
                btnDisconnect.label.enableAutoSizing = true;
                btnDisconnect.label.fontSizeMin = 11;
                btnDisconnect.label.fontSizeMax = 15;
            }

            // Tarjeta de Controles
            var controlsCard = CreateInsetCard("ControlsCard", parent, new Vector2(0, -70), new Vector2(720, 230));
            var cardTitle = CreateLabel(controlsCard.transform, "controls.title", 14, new Vector2(0, 85), new Vector2(680, 24), AccentGlow, true);
            cardTitle.alignment = TextAlignmentOptions.Center;

            string[] controlKeys = { "controls.move", "controls.jump", "controls.tablet", "controls.voice", "controls.pause" };
            for (int i = 0; i < controlKeys.Length; i++)
            {
                var label = CreateLabel(controlsCard.transform, controlKeys[i], 13, new Vector2(0, 48 - i * 32), new Vector2(660, 26), TextWhite, true);
                label.alignment = TextAlignmentOptions.Left;
            }
        }

        private void BuildSettingsTab(Transform parent)
        {
            // --- SELECTOR DE IDIOMA EN PAUSA ---
            var langCard = CreateInsetCard("PauseLangCard", parent, new Vector2(0, 165), new Vector2(720, 90));
            var langLabel = CreateLabel(langCard.transform, "lang.title", 13, new Vector2(0, 24), new Vector2(680, 22), AccentGlow, true);
            langLabel.alignment = TextAlignmentOptions.Center;

            var langRow = CreateRect("PauseLangRow", langCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -12), new Vector2(680, 36));
            btnPauseLangEs = CreateSciFiButton("BtnPauseES", langRow.transform, "ES · Español", new Vector2(-220, 0), new Vector2(200, 34), () => SelectLanguage(GameLanguage.Spanish));
            btnPauseLangEn = CreateSciFiButton("BtnPauseEN", langRow.transform, "EN · English", new Vector2(0, 0), new Vector2(200, 34), () => SelectLanguage(GameLanguage.English));
            btnPauseLangPt = CreateSciFiButton("BtnPausePT", langRow.transform, "PT · Brasil", new Vector2(220, 0), new Vector2(200, 34), () => SelectLanguage(GameLanguage.Portuguese));

            if (btnPauseLangEs.label != null) btnPauseLangEs.label.fontSize = 11;
            if (btnPauseLangEn.label != null) btnPauseLangEn.label.fontSize = 11;
            if (btnPauseLangPt.label != null) btnPauseLangPt.label.fontSize = 11;

            // --- VOLUMEN GENERAL ---
            var audioCard = CreateInsetCard("AudioCard", parent, new Vector2(0, 60), new Vector2(720, 80));
            var audioHeader = CreateLabel(audioCard.transform, "audio.master_volume", 13, new Vector2(0, 18), new Vector2(660, 22), TextWhite, true);
            audioHeader.alignment = TextAlignmentOptions.Left;

            masterVolumePercentLabel = CreateLabel(audioCard.transform, "100%", 13, new Vector2(300, 18), new Vector2(80, 22), AccentGlow, false);
            masterVolumePercentLabel.alignment = TextAlignmentOptions.Right;

            masterVolumeSlider = CreateSlider("MasterVolSlider", audioCard.transform, 0f, 1f, AudioListener.volume, new Vector2(0, -15), new Vector2(660, 24));
            masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);

            // --- CONFIGURACIÓN DE VOZ POR PROXIMIDAD ---
            var voiceCard = CreateInsetCard("VoiceCard", parent, new Vector2(0, -95), new Vector2(720, 190));
            var voiceTitle = CreateLabel(voiceCard.transform, "voice.title", 14, new Vector2(0, 68), new Vector2(680, 24), AccentGlow, true);
            voiceTitle.alignment = TextAlignmentOptions.Center;

            // Modos de transmisión
            var modeRow = CreateRect("ModeRow", voiceCard.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(680, 40));
            btnVoicePtt = CreateSciFiButton("BtnPTT", modeRow.transform, "voice.ptt", new Vector2(-160, 0), new Vector2(300, 36), () => SetVoiceMode(true), true);
            btnVoiceOpenMic = CreateSciFiButton("BtnOpenMic", modeRow.transform, "voice.open_mic", new Vector2(160, 0), new Vector2(300, 36), () => SetVoiceMode(false), true);

            // Slider de volumen maestro de voz
            var voiceVolLabel = CreateLabel(voiceCard.transform, "crew.voice_volume", 12, new Vector2(0, -22), new Vector2(660, 20), TextMuted, true);
            voiceVolLabel.alignment = TextAlignmentOptions.Left;

            voiceMasterPercentLabel = CreateLabel(voiceCard.transform, "100%", 12, new Vector2(300, -22), new Vector2(80, 20), AccentGlow, false);
            voiceMasterPercentLabel.alignment = TextAlignmentOptions.Right;

            voiceMasterVolumeSlider = CreateSlider("VoiceMasterVolSlider", voiceCard.transform, 0f, 1.5f, MarsProximityVoice.MasterVoiceVolume, new Vector2(0, -48), new Vector2(660, 20));
            voiceMasterVolumeSlider.onValueChanged.AddListener(SetVoiceMasterVolume);

            // Detalle informativo
            var rangeLabel = CreateLabel(voiceCard.transform, "voice.distance", 11, new Vector2(0, -72), new Vector2(660, 18), TextDim, true);
            rangeLabel.alignment = TextAlignmentOptions.Center;
        }

        private void BuildCrewTab(Transform parent)
        {
            var header = CreateLabel(parent, "crew.title", 16, new Vector2(0, 200), new Vector2(720, 28), AccentGlow, true);
            header.alignment = TextAlignmentOptions.Center;

            // Scroll container para los miembros de la tripulación
            var scrollArea = CreateInsetCard("CrewScrollArea", parent, new Vector2(0, -10), new Vector2(720, 360));
            scrollArea.gameObject.AddComponent<RectMask2D>();

            var containerGo = CreateRect("CrewListContent", scrollArea.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 360));
            containerGo.pivot = new Vector2(0.5f, 1f);
            crewListContainer = containerGo;

            var layout = containerGo.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(12, 12, 12, 12);

            var fitter = containerGo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            crewEmptyLabel = CreateLabel(scrollArea.transform, "crew.no_other_players", 14, Vector2.zero, new Vector2(660, 40), TextMuted, true);
            crewEmptyLabel.alignment = TextAlignmentOptions.Center;
        }

        #endregion

        #region UI Component Helpers

        private RectTransform CreateWindowPanel(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);
            var go = rect.gameObject;
            var img = go.AddComponent<Image>();

            Sprite windowSprite = null;
            if (theme != null && theme.Has("Window"))
            {
                var p = theme.Prefab("Window");
                if (p != null && p.TryGetComponent<Image>(out var pImg))
                    windowSprite = pImg.sprite;
            }

            if (windowSprite != null)
            {
                img.sprite = windowSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.02f, 0.04f, 0.07f, 0.98f);
                var outline = go.AddComponent<Outline>();
                outline.effectColor = WindowFrameCyan;
                outline.effectDistance = new Vector2(2, -2);
            }
            return rect;
        }

        private RectTransform CreateInsetCard(string name, Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);
            var go = rect.gameObject;
            var img = go.AddComponent<Image>();

            Sprite insetSprite = null;
            if (theme != null && theme.Has("InsetPanel"))
            {
                var p = theme.Prefab("InsetPanel");
                if (p != null && p.TryGetComponent<Image>(out var pImg))
                    insetSprite = pImg.sprite;
            }

            if (insetSprite != null)
            {
                img.sprite = insetSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = InsetCardBg;
                var outline = go.AddComponent<Outline>();
                outline.effectColor = new Color(0.00f, 0.90f, 0.95f, 0.35f);
                outline.effectDistance = new Vector2(1, -1);
            }
            return rect;
        }

        private PixelButton CreateSciFiButton(string name, Transform parent, string textOrKey, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick, bool isLocalized = false)
        {
            GameObject btnGo = null;
            if (theme != null && theme.Has("Button"))
            {
                btnGo = theme.Make("Button", parent);
                btnGo.name = name;
            }
            else
            {
                btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(PixelButton));
                btnGo.transform.SetParent(parent, false);
            }

            var rect = (RectTransform)btnGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var pixelBtn = btnGo.GetComponent<PixelButton>();
            if (pixelBtn == null) pixelBtn = btnGo.AddComponent<PixelButton>();

            if (isLocalized)
            {
                pixelBtn.text = GameLocalization.Get(textOrKey);
                var loc = btnGo.GetComponent<LocalizedText>();
                if (loc == null) loc = btnGo.AddComponent<LocalizedText>();
                loc.Key = textOrKey;
            }
            else
            {
                pixelBtn.text = textOrKey;
            }

            if (pixelBtn.label != null)
            {
                if (fontAsset != null) pixelBtn.label.font = fontAsset;
                pixelBtn.label.enableWordWrapping = false;
                pixelBtn.label.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (onClick != null)
                pixelBtn.onClick.AddListener(onClick);

            return pixelBtn;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string textOrKey, float fontSize, Vector2 anchoredPos, Vector2 size, Color color, bool isLocalized)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            if (isLocalized)
            {
                var loc = go.AddComponent<LocalizedText>();
                loc.Key = textOrKey;
            }
            else
            {
                tmp.text = textOrKey;
            }

            return tmp;
        }

        private TMP_InputField CreateInputField(string name, Transform parent, string initialText, string placeholderKey, Vector2 anchoredPos, Vector2 size)
        {
            GameObject inputGo = null;
            if (theme != null && theme.Has("InputField"))
            {
                inputGo = theme.Make("InputField", parent);
                inputGo.name = name;
            }
            else
            {
                inputGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
                inputGo.transform.SetParent(parent, false);
            }

            var rect = (RectTransform)inputGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var inputField = inputGo.GetComponent<TMP_InputField>();
            if (inputField != null)
            {
                inputField.text = initialText;
                if (inputField.placeholder is TextMeshProUGUI placeholderTmp)
                {
                    var loc = placeholderTmp.GetComponent<LocalizedText>();
                    if (loc == null) loc = placeholderTmp.gameObject.AddComponent<LocalizedText>();
                    loc.Key = placeholderKey;
                }
            }

            return inputField;
        }

        private Slider CreateSlider(string name, Transform parent, float min, float max, float current, Vector2 anchoredPos, Vector2 size)
        {
            GameObject sliderGo = null;
            if (theme != null && theme.Has("Slider"))
            {
                sliderGo = theme.Make("Slider", parent);
                sliderGo.name = name;
            }
            else
            {
                sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
                sliderGo.transform.SetParent(parent, false);
            }

            var rect = (RectTransform)sliderGo.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var slider = sliderGo.GetComponent<Slider>();
            if (slider != null)
            {
                slider.minValue = min;
                slider.maxValue = max;
                slider.value = current;
            }

            return slider;
        }

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

        #endregion

        #region Language Management

        public void SelectLanguage(GameLanguage lang)
        {
            GameLocalization.CurrentLanguage = lang;
            UpdateLanguageButtonsHighlight();
            RefreshAllLocalizedTexts();
        }

        private void UpdateLanguageButtonsHighlight()
        {
            var current = GameLocalization.CurrentLanguage;
            if (btnStartLangEs != null) btnStartLangEs.on = (current == GameLanguage.Spanish);
            if (btnStartLangEn != null) btnStartLangEn.on = (current == GameLanguage.English);
            if (btnStartLangPt != null) btnStartLangPt.on = (current == GameLanguage.Portuguese);

            if (btnPauseLangEs != null) btnPauseLangEs.on = (current == GameLanguage.Spanish);
            if (btnPauseLangEn != null) btnPauseLangEn.on = (current == GameLanguage.English);
            if (btnPauseLangPt != null) btnPauseLangPt.on = (current == GameLanguage.Portuguese);
        }

        private void RefreshAllLocalizedTexts()
        {
            UpdateLanguageButtonsHighlight();

            // Refrescar todos los LocalizedText hijos del Canvas
            var localizedList = GetComponentsInChildren<LocalizedText>(true);
            foreach (var loc in localizedList)
            {
                if (loc != null) loc.Refresh();
            }

            // Actualizar textos dinámicos
            if (masterVolumePercentLabel != null)
                masterVolumePercentLabel.text = Mathf.RoundToInt(AudioListener.volume * 100f) + "%";

            if (voiceMasterPercentLabel != null)
                voiceMasterPercentLabel.text = Mathf.RoundToInt(MarsProximityVoice.MasterVoiceVolume * 100f) + "%";
        }

        #endregion

        #region Audio & Voice Controls

        public void SetMasterVolume(float vol)
        {
            AudioListener.volume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat("GameJam.MasterVolume", vol);
            PlayerPrefs.Save();
            if (masterVolumePercentLabel != null)
                masterVolumePercentLabel.text = Mathf.RoundToInt(vol * 100f) + "%";
        }

        public void SetVoiceMasterVolume(float vol)
        {
            float clamped = Mathf.Clamp(vol, 0f, 2f);
            MarsProximityVoice.MasterVoiceVolume = clamped;
            PlayerPrefs.SetFloat("GameJam.MasterVoiceVolume", clamped);
            PlayerPrefs.Save();
            if (voiceMasterPercentLabel != null)
                voiceMasterPercentLabel.text = Mathf.RoundToInt(clamped * 100f) + "%";
        }

        public void SetVoiceMode(bool ptt)
        {
            PlayerPrefs.SetInt("GameJam.VoiceMode", ptt ? 1 : 0);
            PlayerPrefs.Save();
            UpdateVoiceModeButtons(ptt);

            // Actualizar local proximity voice
            var voices = FindObjectsByType<MarsProximityVoice>(FindObjectsSortMode.None);
            foreach (var v in voices)
            {
                if (v != null) v.SetPushToTalk(ptt);
            }
        }

        private void UpdateVoiceModeButtons(bool ptt)
        {
            if (btnVoicePtt != null) btnVoicePtt.on = ptt;
            if (btnVoiceOpenMic != null) btnVoiceOpenMic.on = !ptt;
        }

        #endregion

        #region Tab Management

        public void SwitchTab(string tab)
        {
            activeTab = tab;
            if (tabBtnMission != null) tabBtnMission.on = (tab == "Mission");
            if (tabBtnSettings != null) tabBtnSettings.on = (tab == "Settings");
            if (tabBtnCrew != null) tabBtnCrew.on = (tab == "Crew");

            if (tabContentMission != null) tabContentMission.SetActive(tab == "Mission");
            if (tabContentSettings != null) tabContentSettings.SetActive(tab == "Settings");
            if (tabContentCrew != null) tabContentCrew.SetActive(tab == "Crew");

            if (tab == "Crew")
            {
                UpdateCrewList();
            }
        }

        #endregion

        #region Crew List Updates (Individual Volume Sliders)

        private void UpdateCrewList()
        {
            if (crewListContainer == null) return;

            var avatars = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None);
            int remoteCount = 0;

            // Asegurar widgets para cada avatar
            for (int i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
                if (avatar == null || avatar.Object == null || !avatar.Object.IsValid) continue;

                bool isLocal = avatar.HasStateAuthority || (avatar.Runner != null && avatar.Object.InputAuthority == avatar.Runner.LocalPlayer);
                if (!isLocal) remoteCount++;

                CrewRowWidget widget = null;
                if (i < activeCrewRows.Count)
                {
                    widget = activeCrewRows[i];
                    widget.Root.SetActive(true);
                }
                else
                {
                    widget = CreateCrewRowWidget(crewListContainer);
                    activeCrewRows.Add(widget);
                }

                BindCrewRowWidget(widget, avatar, isLocal);
            }

            // Ocultar widgets sobrantes
            for (int i = avatars.Length; i < activeCrewRows.Count; i++)
            {
                activeCrewRows[i].Root.SetActive(false);
            }

            if (crewEmptyLabel != null)
            {
                crewEmptyLabel.gameObject.SetActive(remoteCount == 0);
            }
        }

        private CrewRowWidget CreateCrewRowWidget(Transform parent)
        {
            var rowCard = CreateInsetCard("CrewRow", parent, Vector2.zero, new Vector2(680, 56));
            var widget = new CrewRowWidget { Root = rowCard.gameObject };

            // Icono de Astronauta
            var iconGo = CreateRect("AstronautIcon", rowCard.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(30, 30)).gameObject;
            widget.AvatarIcon = iconGo.AddComponent<Image>();
            widget.AvatarIcon.color = WindowFrameCyan;

            // Nombre del Jugador
            var nameGo = CreateRect("NameLabel", rowCard.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(175, 0), new Vector2(240, 34)).gameObject;
            widget.NameLabel = nameGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) widget.NameLabel.font = fontAsset;
            widget.NameLabel.fontSize = 15;
            widget.NameLabel.color = TextWhite;
            widget.NameLabel.alignment = TextAlignmentOptions.Left;

            // Indicador de Voz (Hablando)
            var speakingGo = CreateRect("SpeakingBadge", rowCard.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(80, 26)).gameObject;
            widget.SpeakingBadge = speakingGo.AddComponent<Image>();
            widget.SpeakingBadge.color = new Color(0.18f, 0.92f, 0.45f, 0.25f);

            var speakTextGo = CreateRect("SpeakText", speakingGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            widget.SpeakingLabel = speakTextGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) widget.SpeakingLabel.font = fontAsset;
            widget.SpeakingLabel.fontSize = 11;
            widget.SpeakingLabel.color = SpeakingGreen;
            widget.SpeakingLabel.text = "VOZ";
            widget.SpeakingLabel.alignment = TextAlignmentOptions.Center;

            // Contenedor de Slider de Volumen Individual (para remotos)
            var volGroupGo = CreateRect("VolumeControl", rowCard.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-130, 0), new Vector2(240, 40));
            widget.VolumeControlGroup = volGroupGo.gameObject;

            var percentGo = CreateRect("VolPercent", volGroupGo.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(30, 0), new Vector2(50, 20)).gameObject;
            widget.VolumePercentLabel = percentGo.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) widget.VolumePercentLabel.font = fontAsset;
            widget.VolumePercentLabel.fontSize = 12;
            widget.VolumePercentLabel.color = AccentGlow;
            widget.VolumePercentLabel.alignment = TextAlignmentOptions.Right;

            widget.VolumeSlider = CreateSlider("IndivVolSlider", volGroupGo.transform, 0f, 1.5f, 1f, new Vector2(-35, 0), new Vector2(160, 18));

            return widget;
        }

        private void BindCrewRowWidget(CrewRowWidget widget, MarsFusionAvatar avatar, bool isLocal)
        {
            widget.Avatar = avatar;

            string pName = avatar.PlayerName.ToString();
            if (string.IsNullOrEmpty(pName)) pName = "Astronauta";
            if (isLocal) pName += "  " + GameLocalization.Get("crew.you");

            widget.NameLabel.text = pName;

            // Tint del color de traje
            if (avatar.SuitColorIndex < MarsPlayerIdentityView.Palette.Length)
            {
                widget.AvatarIcon.color = MarsPlayerIdentityView.Palette[avatar.SuitColorIndex];
            }

            var proximityVoice = avatar.GetComponent<MarsProximityVoice>();
            bool speaking = (proximityVoice != null && proximityVoice.IsSpeaking) || avatar.VoiceLevel > 15;
            widget.SpeakingBadge.gameObject.SetActive(speaking);

            if (isLocal)
            {
                widget.VolumeControlGroup.SetActive(false);
            }
            else
            {
                widget.VolumeControlGroup.SetActive(true);
                float curVol = proximityVoice != null ? proximityVoice.IndividualVolume : 1f;

                widget.VolumeSlider.onValueChanged.RemoveAllListeners();
                widget.VolumeSlider.value = curVol;
                widget.VolumePercentLabel.text = Mathf.RoundToInt(curVol * 100f) + "%";

                widget.VolumeSlider.onValueChanged.AddListener(val =>
                {
                    if (proximityVoice != null) proximityVoice.SetIndividualVolume(val);
                    widget.VolumePercentLabel.text = Mathf.RoundToInt(val * 100f) + "%";
                });
            }
        }

        #endregion

        #region Start & Session Actions

        private void OnClickStartOnline()
        {
            if (session == null || session.Busy) return;
            _ = session.StartSession(false);
        }

        private void OnClickStartSolo()
        {
            if (session == null || session.Busy) return;
            _ = session.StartSession(true);
        }

        private void OnClickDisconnect()
        {
            if (session == null || session.Busy) return;
            _ = session.Disconnect();
            SetPauseMenuVisible(false);
            SetStartMenuVisible(true);
        }

        public void ResumeGame()
        {
            SetPauseMenuVisible(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void OpenPauseMenu()
        {
            SetPauseMenuVisible(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SwitchTab(activeTab);
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
                if (session.Runner != null && session.Runner.IsRunning)
                    statusDot.color = SpeakingGreen;
                else if (session.Busy)
                    statusDot.color = new Color(1f, 0.8f, 0.2f, 1f);
                else
                    statusDot.color = AccentGlow;
            }

            // Si la sesión conectó con éxito, ocultar menú de inicio y bloquear cursor
            if (session.Runner != null && session.Runner.IsRunning)
            {
                SetStartMenuVisible(false);
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void SetStartMenuVisible(bool visible)
        {
            if (startMenuGroup == null) return;
            startMenuGroup.alpha = visible ? 1f : 0f;
            startMenuGroup.interactable = visible;
            startMenuGroup.blocksRaycasts = visible;

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SetPauseMenuVisible(bool visible)
        {
            if (pauseMenuGroup == null) return;
            pauseMenuGroup.alpha = visible ? 1f : 0f;
            pauseMenuGroup.interactable = visible;
            pauseMenuGroup.blocksRaycasts = visible;

            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        #endregion
    }
}
