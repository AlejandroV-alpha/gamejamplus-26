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
        private GameObject backdropObject;
        private RectTransform startChassis, pauseChassis;
        private static TMP_FontAsset headingFont;
        private bool portraitLayout, runnerWasRunning;
        private Vector2 lastCanvasSize;
        private int lastScreenWidth, lastScreenHeight;
        private CanvasGroup startMenuGroup;
        private CanvasGroup pauseMenuGroup;
        private AudioSource menuMusic;
        private float menuMusicTargetVolume;

        // Componentes de Inicio
        private TMP_InputField playerNameInput;
        private TMP_InputField roomCodeInput;
        private Button btnOnlineStart;
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
        private TextMeshProUGUI connectionRegionLabel;
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
        private static readonly Color DimCyan = new(0.48f, 0.60f, 0.69f, 1f);        // Marcas secundarias
        private static readonly Color CyanAccent = new(1.00f, 0.65f, 0.35f, 1f);     // Acento neón cian
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
            public Button MuteButton;
            public TextMeshProUGUI MuteLabel;
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

            if (headingFont == null)
            {
                var source = Resources.Load<Font>("UI/Menu/JetBrainsMono-Regular");
                if (source != null)
                {
                    headingFont = TMP_FontAsset.CreateFontAsset(source);
                    headingFont.name = "Menu JetBrains Mono SDF";
                    if (headingFont.fallbackFontAssetTable == null) headingFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                    if (fontAsset != null) headingFont.fallbackFontAssetTable.Add(fontAsset);
                }
            }
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

            runnerWasRunning = session != null && session.Runner != null && session.Runner.IsRunning;
            SyncCanvasVisibility();
            FitDatapads();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            ExpeditionSettingsPanel.CloseIfOpen();
            if (Instance == this) Instance = null;
            GameLocalization.OnLanguageChanged -= RefreshAllLocalizedTexts;
        }

        private void Update()
        {
            if (!isInitialized) return;
            if (menuMusic != null)
            {
                menuMusic.volume = Mathf.MoveTowards(menuMusic.volume, menuMusicTargetVolume, Time.unscaledDeltaTime * 0.35f);
                if (menuMusicTargetVolume == 0f && menuMusic.volume == 0f && menuMusic.isPlaying)
                    menuMusic.Stop();
            }
            if (session == null) session = FindFirstObjectByType<MarsFusionSession>();
            bool running = session != null && session.Runner != null && session.Runner.IsRunning;
            if (running != runnerWasRunning)
            {
                runnerWasRunning = running;
                session.SetMenu(!running);
            }
            // A successful connection must never leave the initial terminal over gameplay.
            if (running && startMenuGroup != null && startMenuGroup.gameObject.activeSelf)
                SetStartMenuVisible(false);
            if (connectionRegionLabel != null) connectionRegionLabel.text = "REGION: " + MultiplayerConfiguration.SelectedRegion.ToUpperInvariant();
            UpdateResponsiveLayout();

            // Manejo de tecla Escape para pausar / reanudar
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (ExpeditionSettingsPanel.ConsumeEscape()) return;
                if (MarsFusionAvatar.Local != null && MarsFusionAvatar.Local.LocalHud != null && MarsFusionAvatar.Local.LocalHud.ConsumeEscape()) return;
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
            rootCanvas.pixelPerfect = false;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1000);
            scaler.matchWidthOrHeight = 0.5f;

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Fondo translúcido sutil
            var backdrop = CreateRect("Backdrop", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var backdropImg = backdrop.gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0.01f, 0.015f, 0.025f, 0.70f);
            backdropImg.raycastTarget = false;
            backdropObject = backdrop.gameObject;

            // ==================== 1. TERMINAL DE INICIO (LOBBY) ====================
            var startRoot = CreateRect("StartMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            startMenuGroup = startRoot.gameObject.AddComponent<CanvasGroup>();
            BuildStartTerminalDatapad(startRoot.transform);

            // ==================== 2. MENÚ DE PAUSA ====================
            var pauseRoot = CreateRect("PauseMenuRoot", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pauseMenuGroup = pauseRoot.gameObject.AddComponent<CanvasGroup>();
            BuildPauseDatapad(pauseRoot.transform);
            SetGroup(startMenuGroup, false);
            SetGroup(pauseMenuGroup, false);
            SyncCanvasVisibility();
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
            portraitLayout = Screen.width < Screen.height * 1.15f;
            Vector2 chassisSize = portraitLayout ? new Vector2(720, 920) : new Vector2(1120, 760);
            Vector2 displaySize = chassisSize - new Vector2(72, 96);
            var rim = CreateRoundedPanel(parent, "TerminalRim", chassisSize, 30, ChassisRim);
            startChassis = rim;
            var body = CreateRoundedPanel(rim, "TerminalBody", chassisSize - new Vector2(8, 8), 26, ChassisMatte);
            CreateArmorCornerBumpers(body, (chassisSize-new Vector2(8,8))*.5f);
            CreateTopHardwareSensors(body,chassisSize.x);
            CreateBottomHardwareDock(body,chassisSize.x,"ORBIT // FIELD TERMINAL 04");
            var bezel = CreateRoundedPanel(body,"TerminalBezel",displaySize+new Vector2(10,10),22,ChassisBezel);
            var display = CreateRoundedPanel(bezel,"TerminalDisplay",displaySize,18,GlassDisplay);
            display.gameObject.AddComponent<RectMask2D>();
            var accent = CreateRoundedPanel(display,"AmberRail",new Vector2(displaySize.x-48,3),1.5f,AmberAccent);
            accent.anchoredPosition=new Vector2(0,displaySize.y*.5f-18);

            if(!portraitLayout)
            {
                var side=CreateRoundedPanel(display,"MissionIdentity",new Vector2(296,584),16,CardBg);
                side.anchoredPosition=new Vector2(-338,0);
                var brand=CreateLabel(side,"ORBIT",48,new Vector2(0,224),new Vector2(252,64),InkWhite);
                brand.font=headingFont??fontAsset;brand.fontStyle=FontStyles.Bold;
                CreateLabel(side,"FIELD OPERATIONS / 04",14,new Vector2(0,178),new Vector2(252,30),AmberAccent);
                var orbit=CreateRoundedPanel(side,"OrbitRing",new Vector2(194,194),97,ChassisRim);
                orbit.anchoredPosition=new Vector2(0,22);
                CreateRoundedPanel(orbit,"OrbitInset",new Vector2(190,190),95,CardBg);
                var planetView = CreateRect("Planet3D", orbit, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                planetView.gameObject.AddComponent<MenuPlanetPreview>();
                CreateLabel(side,"M A R S  /  O R B I T A L",12,new Vector2(0,-96),new Vector2(260,24),AmberAccent);
                CreateLabel(side,"menu.subtitle",18,new Vector2(0,-136),new Vector2(246,64),InkWhite,true);
                CreateLabel(side,"menu.connection_hint",17,new Vector2(0,-225),new Vector2(246,84),MutedCyan,true);
            }
            var form=CreateRect("ConnectionForm",display,new Vector2(.5f,.5f),new Vector2(.5f,.5f),
                new Vector2(portraitLayout?0:166,0),new Vector2(600,displaySize.y));
            float badgeY=portraitLayout?382:302;
            var badge=CreateLabel(form,"EXPEDITION LINK / MK.04",14,new Vector2(0,badgeY),new Vector2(590,24),AmberAccent);
            badge.font=headingFont??fontAsset;
            var title=CreateLabel(form,"menu.main_title",portraitLayout?32:34,new Vector2(0,portraitLayout?334:250),new Vector2(600,62),InkWhite,true);
            title.font=headingFont??fontAsset;title.fontStyle=FontStyles.Bold;
            CreateLabel(form,"menu.subtitle",18,new Vector2(0,portraitLayout?292:210),new Vector2(590,30),MutedCyan,true);
            float langY=portraitLayout?226:144;
            var languages=CreateCard(form,"LanguageCard",new Vector2(600,88),new Vector2(0,langY));
            CreateLabel(languages,"lang.title",16,new Vector2(0,25),new Vector2(560,24),MutedCyan,true);
            var row=CreateRect("LanguageOptions",languages,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-15),new Vector2(560,38));
            btnStartLangEs=CreateDatapadButton(row,"BtnLangES","ES / Español",new Vector2(-190,0),new Vector2(180,38),()=>SelectLanguage(GameLanguage.Spanish),out btnStartLangEsBg);
            btnStartLangEn=CreateDatapadButton(row,"BtnLangEN","EN / English",Vector2.zero,new Vector2(180,38),()=>SelectLanguage(GameLanguage.English),out btnStartLangEnBg);
            btnStartLangPt=CreateDatapadButton(row,"BtnLangPT","PT / Brasil",new Vector2(190,0),new Vector2(180,38),()=>SelectLanguage(GameLanguage.Portuguese),out btnStartLangPtBg);
            float nameY=portraitLayout?112:34, roomY=portraitLayout?-12:-78;
            var nameCard=CreateCard(form,"NameCard",new Vector2(600,100),new Vector2(0,nameY));
            var nameLabel=CreateLabel(nameCard,"menu.player_name",17,new Vector2(0,28),new Vector2(560,24),MutedCyan,true);nameLabel.alignment=TextAlignmentOptions.Left;
            string savedName=PlayerPrefs.GetString("GameJam.PlayerName","Astronauta").Trim();
            playerNameInput=CreateInputField(nameCard,"PlayerNameInput",string.IsNullOrEmpty(savedName)?"Astronauta":savedName,"menu.player_name_placeholder",new Vector2(0,-14),new Vector2(560,48));
            playerNameInput.characterLimit=24;
            playerNameInput.onValueChanged.AddListener(value=>PlayerPrefs.SetString("GameJam.PlayerName",value.Trim()));
            var roomCard=CreateCard(form,"RoomCard",new Vector2(600,100),new Vector2(0,roomY));
            var roomLabel=CreateLabel(roomCard,"menu.room_code",17,new Vector2(0,28),new Vector2(560,24),MutedCyan,true);roomLabel.alignment=TextAlignmentOptions.Left;
            string savedRoom=PlayerPrefs.GetString("GameJam.RoomName","MARTE-01").Trim();if(string.IsNullOrEmpty(savedRoom))savedRoom="MARTE-01";
            if(session!=null)session.roomName=savedRoom;
            roomCodeInput=CreateInputField(roomCard,"RoomCodeInput",savedRoom,"menu.room_code_placeholder",new Vector2(0,-14),new Vector2(560,48));
            roomCodeInput.onValueChanged.AddListener(value=>{PlayerPrefs.SetString("GameJam.RoomName",value.Trim());if(session!=null)session.roomName=value.Trim();});
            btnOnlineStart=CreateDatapadButton(form,"BtnOnline","menu.btn_join_online",new Vector2(0,portraitLayout?-128:-170),new Vector2(600,58),OnClickStartOnline,out var onlineBg,true);
            onlineBg.color=new Color(.96f,.56f,.27f);
            var onlineText=btnOnlineStart.GetComponentInChildren<TextMeshProUGUI>();onlineText.fontSize=22;onlineText.color=ChassisBezel;onlineText.fontStyle=FontStyles.Bold;

            var status=CreateRoundedPanel(form,"StatusBox",new Vector2(600,44),10,InsetBg);status.anchoredPosition=new Vector2(0,portraitLayout?-212:-246);
            var dot=CreateRoundedPanel(status,"StatusDot",new Vector2(10,10),5,AmberAccent);dot.anchoredPosition=new Vector2(-276,0);statusDot=dot.GetComponent<TabletRoundedPanel>();
            statusLabel=CreateLabel(status,"menu.status_ready",17,new Vector2(12,0),new Vector2(540,32),InkWhite,true);statusLabel.alignment=TextAlignmentOptions.Left;
            if(portraitLayout)CreateLabel(form,"menu.connection_hint",17,new Vector2(0,-350),new Vector2(580,64),MutedCyan,true);
            CreateDatapadButton(form,"BtnSystemSettings","pause.tab_settings",new Vector2(0,portraitLayout?-280:-302),new Vector2(600,40),()=>ExpeditionSettingsPanel.Open(fontAsset),out var systemsBg,true);
            connectionRegionLabel = CreateLabel(status,"Region",14,new Vector2(205,0),new Vector2(160,32),MutedCyan);
            connectionRegionLabel.alignment = TextAlignmentOptions.Right;
            ((RectTransform)statusLabel.transform).sizeDelta = new Vector2(350,32);
            ((RectTransform)statusLabel.transform).anchoredPosition = new Vector2(-85,0);
            UpdateLanguageButtonsHighlight();
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
            pauseChassis = rim;

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

            var title = CreateLabel(display, "pause.new_title", 36, new Vector2(-58, 238), new Vector2(600, 52), InkWhite, true);
            title.alignment = TextAlignmentOptions.Left;
            title.fontStyle = FontStyles.Bold;
            var subtitle = CreateLabel(display, "pause.new_subtitle", 17, new Vector2(-58, 194), new Vector2(600, 30), MutedCyan, true);
            subtitle.alignment = TextAlignmentOptions.Left;
            var close = CreateDatapadButton(display, "BtnClosePause", "ESC", new Vector2(332, 239), new Vector2(64, 38), ResumeGame, out var closeBg);
            closeBg.color = SlotBg;
            var rule = CreateRoundedPanel(display, "Header divider", new Vector2(720, 1), 0, ChassisRim);
            rule.anchoredPosition = new Vector2(0, 161);

            var nav = CreateRect("Navigation", display, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(-251,-18), new Vector2(204,316));
            tabBtnMission = CreatePauseNav(nav, "TabMission", "pause.tab_mission", 116, MenuSymbolGraphic.Symbol.Mission, () => SwitchTab("Mission"), out tabBtnMissionBg);
            tabBtnSettings = CreatePauseNav(nav, "TabSettings", "pause.tab_settings", 46, MenuSymbolGraphic.Symbol.Settings, () => SwitchTab("Settings"), out tabBtnSettingsBg);
            tabBtnCrew = CreatePauseNav(nav, "TabCrew", "pause.tab_crew", -24, MenuSymbolGraphic.Symbol.Crew, () => SwitchTab("Crew"), out tabBtnCrewBg);
            var help = CreateLabel(nav, "pause.controls_location", 16, new Vector2(0,-134), new Vector2(180,72), DimCyan, true);
            help.alignment = TextAlignmentOptions.Left;
            var contentRoot = CreateRect("TabContentArea", display, new Vector2(.5f,.5f), new Vector2(.5f,.5f), new Vector2(124,-40), new Vector2(464,352));

            // ================= 1. TAB: MISIÓN =================
            tabContentMission = CreateRect("MissionContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildMissionTab(tabContentMission.transform);

            // ================= 2. TAB: AJUSTES =================
            tabContentSettings = CreateRect("SettingsContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            // Settings use the same unified surface from the start and pause menus.

            // ================= 3. TAB: TRIPULACIÓN =================
            tabContentCrew = CreateRect("CrewContent", contentRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            BuildCrewTab(tabContentCrew.transform);

            SwitchTab("Mission");
        }

        private static void AddMenuIcon(Transform parent, MenuSymbolGraphic.Symbol symbol, Vector2 position, float size, Color color)
        {
            var rect = CreateRect("Icon " + symbol, parent, new Vector2(.5f,.5f), new Vector2(.5f,.5f), position, Vector2.one * size);
            var icon = rect.gameObject.AddComponent<MenuSymbolGraphic>();
            icon.symbol = symbol; icon.color = color; icon.raycastTarget = false;
        }

        private Button CreatePauseNav(Transform parent, string name, string key, float y, MenuSymbolGraphic.Symbol icon, UnityAction action, out TabletRoundedPanel bg)
        {
            var button = CreateDatapadButton(parent, name, key, new Vector2(0,y), new Vector2(204,56), action, out bg, true);
            button.GetComponent<Outline>().enabled = false;
            bg.radius = 8;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.anchoredPosition = new Vector2(22,0); label.rectTransform.sizeDelta = new Vector2(136,40);
            label.fontSize = 17; label.alignment = TextAlignmentOptions.Left;
            AddMenuIcon(button.transform, icon, new Vector2(-74,0), 26, InkWhite);
            return button;
        }

        private Button CreatePauseAction(Transform parent, string name, string key, float y, MenuSymbolGraphic.Symbol icon, UnityAction action, Color background, Color foreground, float height)
        {
            var button = CreateDatapadButton(parent, name, key, new Vector2(0,y), new Vector2(432,height), action, out var bg, true);
            button.GetComponent<Outline>().enabled = false; bg.color = background; bg.radius = 10;
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.rectTransform.anchoredPosition = new Vector2(24,0); label.rectTransform.sizeDelta = new Vector2(326,height-12);
            label.fontSize = 19; label.alignment = TextAlignmentOptions.Left; label.color = foreground;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 19;
            AddMenuIcon(button.transform, icon, new Vector2(-179,0), 26, foreground);
            return button;
        }

        private void BuildMissionTab(Transform parent)
        {
            var title = CreateLabel(parent, "pause.your_mission", 25, new Vector2(0,145), new Vector2(432,40), InkWhite, true);
            title.alignment = TextAlignmentOptions.Left;
            CreatePauseAction(parent, "BtnResume", "pause.continue", 77, MenuSymbolGraphic.Symbol.Play, ResumeGame, new Color(.94f,.62f,.32f), ChassisBezel, 76);
            var divider = CreateRoundedPanel(parent, "Actions divider", new Vector2(432,1), 0, ChassisRim); divider.anchoredPosition = new Vector2(0,8);
            CreatePauseAction(parent, "BtnReturnLobby", "pause.return", -46, MenuSymbolGraphic.Symbol.Home, OnClickReturnToLobby, SlotBg, InkWhite, 60);
            CreatePauseAction(parent, "BtnDisconnect", "pause.disconnect", -118, MenuSymbolGraphic.Symbol.Exit, OnClickDisconnect, new Color(.14f,.075f,.095f), new Color(.96f,.61f,.60f), 56);
        }

        private void BuildCrewTab(Transform parent)
        {
            var header = CreateLabel(parent, "crew.title", 15, new Vector2(0, 154), new Vector2(432, 26), AmberAccent, true);
            header.alignment = TextAlignmentOptions.Center;

            var scrollCard = CreateCard(parent, "CrewScrollCard", new Vector2(432, 290), new Vector2(0, -24));
            scrollCard.gameObject.AddComponent<RectMask2D>();

            var containerGo = CreateRect("CrewListContent", scrollCard, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 340));
            containerGo.pivot = new Vector2(0.5f, 1f);
            crewListContainer = containerGo;
            var scroll = scrollCard.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = scrollCard; scroll.content = containerGo; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;

            var layout = containerGo.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8;
            layout.padding = new RectOffset(14, 14, 14, 14);

            var fitter = containerGo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            crewEmptyLabel = CreateLabel(scrollCard, "crew.no_other_players", 13, Vector2.zero, new Vector2(400, 70), MutedCyan, true);
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
            outline.effectColor = new Color(0.5f, 0.65f, 0.75f, 0.15f);
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
            var edge=rect.gameObject.AddComponent<Outline>();edge.effectColor=new Color(.28f,.66f,.72f,.55f);edge.effectDistance=new Vector2(1,-1);
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

            var label = CreateLabel(rect, textOrKey, 18, Vector2.zero, size - new Vector2(12, 6), InkWhite, isLocalized);
            label.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private TMP_InputField CreateInputField(Transform parent, string name, string initialText, string placeholderKey, Vector2 anchoredPos, Vector2 size)
        {
            var bgRect = CreateRoundedPanel(parent, name, size, 8, InsetBg);
            bgRect.anchoredPosition = anchoredPos;
            var outline = bgRect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.65f, 0.35f, 0.20f);
            outline.effectDistance = new Vector2(1, -1);

            var textRect = CreateRect("Text", bgRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -8));
            var textComp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            textComp.font = fontAsset ?? TMP_Settings.defaultFontAsset;
            textComp.fontSize = 24;
            textComp.color = InkWhite;
            textComp.alignment = TextAlignmentOptions.Left;

            var phRect = CreateRect("Placeholder", bgRect, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20, -8));
            var phComp = phRect.gameObject.AddComponent<TextMeshProUGUI>();
            phComp.font = fontAsset ?? TMP_Settings.defaultFontAsset;
            phComp.fontSize = 22;
            phComp.color = MutedCyan;
            phComp.alignment = TextAlignmentOptions.Left;
            var loc = phComp.gameObject.AddComponent<LocalizedText>();
            loc.Key = placeholderKey;

            var input = bgRect.gameObject.AddComponent<TMP_InputField>();
            input.textComponent = textComp;
            input.placeholder = phComp;
            input.text = initialText;
            input.characterLimit = 32;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = AmberAccent;
            input.selectionColor = new Color(1f,.65f,.35f,.28f);
            input.targetGraphic = bgRect.GetComponent<TabletRoundedPanel>();
            textComp.raycastTarget = false;
            phComp.raycastTarget = false;

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
            tmp.fontSize = Mathf.Max(16, fontSize);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = size.y >= 48 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.color = color;
            tmp.raycastTarget = false;

            if (isLocalized)
            {
                var loc = tmp.gameObject.AddComponent<LocalizedText>();
                loc.Key = textOrKey;
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

        private static void SetGroup(CanvasGroup group,bool visible)
        {
            if(group==null)return;
            group.alpha=visible?1:0;group.blocksRaycasts=visible;group.interactable=visible;
            group.gameObject.SetActive(visible);
        }
        private void SyncCanvasVisibility()
        {
            bool visible=(startMenuGroup!=null && startMenuGroup.gameObject.activeSelf)
                || (pauseMenuGroup!=null && pauseMenuGroup.gameObject.activeSelf);
            if(rootCanvas!=null)rootCanvas.enabled=visible;
            var raycaster=GetComponent<GraphicRaycaster>();if(raycaster!=null)raycaster.enabled=visible;
            if(backdropObject!=null)backdropObject.SetActive(visible);
            SyncMenuMusic();
        }

        private void SyncMenuMusic()
        {
            bool shouldPlay = IsLobbyScene && startMenuGroup != null && startMenuGroup.gameObject.activeSelf;
            menuMusicTargetVolume = shouldPlay ? 0.35f : 0f;
            if (shouldPlay && menuMusic == null)
            {
                var clip = Resources.Load<AudioClip>("Music/MainMenu/MusicFondoMain");
                if (clip == null) return;
                var musicObject = new GameObject("MainMenuMusic");
                musicObject.transform.SetParent(transform, false);
                menuMusic = musicObject.AddComponent<AudioSource>();
                menuMusic.playOnAwake = false;
                menuMusic.loop = true;
                menuMusic.spatialBlend = 0f;
                menuMusic.volume = 0f;
                menuMusic.clip = clip;
            }
            if (shouldPlay && menuMusic != null && !menuMusic.isPlaying)
                menuMusic.Play();
        }

        private void OnDisable()
        {
            if (menuMusic != null)
            {
                menuMusic.Stop();
                menuMusic.volume = 0f;
            }
        }
        public void ApplySessionMenuState(bool visible,bool connected)
        {
            if (!visible) ExpeditionSettingsPanel.CloseIfOpen();
            SetGroup(startMenuGroup,visible && !connected && IsLobbyScene);
            SetGroup(pauseMenuGroup,visible && connected);
            SyncCanvasVisibility();
            bool menuVisible=(startMenuGroup!=null && startMenuGroup.gameObject.activeSelf)
                || (pauseMenuGroup!=null && pauseMenuGroup.gameObject.activeSelf);
            if(menuVisible){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
            else if(connected){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(visible && connected)SwitchTab("Mission");
        }
        public void SetStartMenuVisible(bool visible)
        {
            bool connected=session!=null && session.Runner!=null && session.Runner.IsRunning;
            SetGroup(startMenuGroup,visible && IsLobbyScene && !connected);
            SyncCanvasVisibility();
        }
        public void SetPauseMenuVisible(bool visible)
        {
            bool connected=session!=null && session.Runner!=null && session.Runner.IsRunning;
            if (visible) ApplySessionMenuState(true,connected);
            else { SetGroup(pauseMenuGroup,false); SyncCanvasVisibility(); }
        }
        public void OpenPauseMenu(){if(session!=null)session.SetMenu(true);}
        public void ResumeGame(){ExpeditionSettingsPanel.CloseIfOpen();if(session!=null)session.SetMenu(false);else ApplySessionMenuState(false,false);}
        private void FitDatapads()
        {
            if(rootCanvas==null)return;
            var size=((RectTransform)rootCanvas.transform).rect.size;
            if(size.x<=0 || size.y<=0)return;
            foreach(var rect in new[]{startChassis,pauseChassis})
                if(rect!=null)rect.localScale=Vector3.one*Mathf.Min(1.4f,Mathf.Min(size.x*.94f/rect.sizeDelta.x,size.y*.90f/rect.sizeDelta.y));
            lastCanvasSize=size;
        }
        private void UpdateResponsiveLayout()
        {
            bool portrait=Screen.width<Screen.height*1.15f;
            if(portrait!=portraitLayout && startMenuGroup!=null && startMenuGroup.gameObject.activeSelf && (session==null || !session.Busy))
            {
                if(startChassis!=null){startChassis.gameObject.SetActive(false);Destroy(startChassis.gameObject);}
                BuildStartTerminalDatapad(startMenuGroup.transform);
            }
            var size=((RectTransform)rootCanvas.transform).rect.size;
            if(lastScreenWidth!=Screen.width || lastScreenHeight!=Screen.height || size!=lastCanvasSize)
            {lastScreenWidth=Screen.width;lastScreenHeight=Screen.height;FitDatapads();}
        }

        public void SwitchTab(string tabName)
        {
            if (tabName == "Settings") { ExpeditionSettingsPanel.Open(fontAsset); return; }
            activeTab = tabName;
            if (tabContentMission != null) tabContentMission.SetActive(tabName == "Mission");
            if (tabContentSettings != null) tabContentSettings.SetActive(tabName == "Settings");
            if (tabContentCrew != null) tabContentCrew.SetActive(tabName == "Crew");

            Color activeTabColor = new(0.19f, 0.28f, 0.35f, 1f);
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

            PlayerPrefs.Save();
            _ = session.StartSession(false);
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
            if (playerNameInput != null) playerNameInput.interactable = !busy;
            if (roomCodeInput != null) roomCodeInput.interactable = !busy;
            if (btnStartLangEs != null) btnStartLangEs.interactable = !busy;
            if (btnStartLangEn != null) btnStartLangEn.interactable = !busy;
            if (btnStartLangPt != null) btnStartLangPt.interactable = !busy;
            if (btnOnlineStart != null) btnOnlineStart.interactable = !busy;

            if (statusLabel != null)
            {
                statusLabel.text = session.DisplayStatus;
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

        private MarsFusionAvatar GetLocalAvatar()
        {
            if (MarsFusionAvatar.Local != null) return MarsFusionAvatar.Local;
            if (session != null && session.Runner != null && session.Runner.IsRunning)
            {
                var localObj = session.Runner.GetPlayerObject(session.Runner.LocalPlayer);
                if (localObj != null) return localObj.GetComponent<MarsFusionAvatar>();
            }
            return null;
        }

        private void SetVoiceMode(bool ptt)
        {
            PlayerPrefs.SetInt("GameJam.VoiceMode", ptt ? 1 : 0);
            PlayerPrefs.Save();

            MarsProximityVoice.LocalPushToTalk = ptt;
            var localAvatar = GetLocalAvatar();
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

            var localAvatar = GetLocalAvatar();
            var runner = localAvatar != null ? localAvatar.Runner : session != null ? session.Runner : null;
            var allAvatars = new List<MarsFusionAvatar>();
            // Only canonical player objects in this session belong to the crew.
            // Scene prototypes, stale avatars and other local runners must not create rows.
            if (runner != null && runner.IsRunning)
            {
                var players = new List<Fusion.PlayerRef>(runner.ActivePlayers);
                players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
                foreach (var player in players)
                {
                    var playerObject = runner.GetPlayerObject(player);
                    if (playerObject == null || !playerObject.IsValid) continue;
                    var avatar = playerObject.GetComponent<MarsFusionAvatar>();
                    if (avatar == null || !avatar.IsNetworkReady || avatar.Runner != runner || allAvatars.Contains(avatar)) continue;
                    allAvatars.Add(avatar);
                }
            }

            int neededRows = allAvatars.Count;
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

            var rowCard = CreateRoundedPanel(crewListContainer, "CrewRow", new Vector2(404, 108), 10, CardBg);
            widget.Root = rowCard.gameObject;

            var outline = rowCard.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.65f, 0.35f, 0.20f);
            outline.effectDistance = new Vector2(1, -1);

            // Icono de casco
            var avatarPill = CreateRoundedPanel(rowCard, "AvatarPill", new Vector2(36, 36), 18, InsetBg);
            avatarPill.anchoredPosition = new Vector2(-174, 24);

            var avatarDot = CreateRoundedPanel(avatarPill, "AvatarCenter", new Vector2(16, 16), 8, CyanAccent);
            avatarDot.anchoredPosition = Vector2.zero;

            // Nombre del astronauta
            widget.NameLabel = CreateLabel(rowCard, "Astronauta", 13, new Vector2(-52, 24), new Vector2(188, 26), InkWhite);
            widget.NameLabel.alignment = TextAlignmentOptions.Left;
            widget.NameLabel.fontStyle = FontStyles.Bold;

            // Indicador de habla
            var speakPill = CreateRoundedPanel(rowCard, "SpeakPill", new Vector2(120, 28), 6, InsetBg);
            speakPill.anchoredPosition = new Vector2(124, 24);

            var dotRect = CreateRoundedPanel(speakPill, "Dot", new Vector2(8, 8), 4, DimCyan);
            dotRect.anchoredPosition = new Vector2(-44, 0);
            widget.SpeakingDot = dotRect.GetComponent<TabletRoundedPanel>();

            widget.SpeakingLabel = CreateLabel(speakPill, "crew.muted", 10, new Vector2(10, 0), new Vector2(80, 20), DimCyan, true);
            widget.SpeakingLabel.alignment = TextAlignmentOptions.Left;

            widget.MuteButton = CreateDatapadButton(rowCard, "MutePeer", GameLocalization.Get("crew.silence"), new Vector2(-142, -28), new Vector2(100, 34), () =>
            {
                if (widget.Avatar != null && widget.Avatar.TryGetComponent<MarsProximityVoice>(out var peer)) peer.SetMuted(!peer.IndividuallyMuted);
            }, out var muteBg);
            widget.MuteLabel = widget.MuteButton.GetComponentInChildren<TextMeshProUGUI>();
            widget.MuteLabel.fontSize = 14;
            // Slider de volumen individual
            var sliderRoot = CreateRect("VolumeGroup", rowCard, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(65, -28), new Vector2(210, 40));

            var volLabel = CreateLabel(sliderRoot, "VOL", 10, new Vector2(-76, 0), new Vector2(44, 24), MutedCyan);
            volLabel.alignment = TextAlignmentOptions.Center;

            widget.VolumePercentLabel = CreateLabel(sliderRoot, "100%", 10, new Vector2(84, 0), new Vector2(62, 24), CyanAccent);
            widget.VolumePercentLabel.alignment = TextAlignmentOptions.Left;

            widget.VolumeSlider = CreateSlider(sliderRoot, "IndivVolSlider", 0f, 2.0f, 1f, new Vector2(0, 0), new Vector2(92, 16));

            return widget;
        }

        private void BindCrewRow(CrewRowWidget widget, MarsFusionAvatar avatar, MarsFusionAvatar localAvatar)
        {
            widget.Avatar = avatar;
            bool isSelf = avatar != null && avatar == localAvatar;

            string rawName = avatar != null ? avatar.PlayerName.ToString() : null;
            string name = !string.IsNullOrWhiteSpace(rawName) ? rawName : "Astronauta";
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

            if (widget.MuteButton != null)
            {
                widget.MuteButton.gameObject.SetActive(!isSelf);
                bool muted = avatar != null && avatar.TryGetComponent<MarsProximityVoice>(out var mutedPeer) && mutedPeer.IndividuallyMuted;
                widget.MuteLabel.text = GameLocalization.Get(muted ? "crew.unmute" : "crew.silence");
            }
            if (widget.VolumeSlider != null)
            {
                widget.VolumeSlider.gameObject.SetActive(!isSelf);
                if (widget.VolumePercentLabel != null)
                    widget.VolumePercentLabel.gameObject.SetActive(!isSelf);

                if (!isSelf && avatar != null)
                {
                    widget.VolumeSlider.onValueChanged.RemoveAllListeners();
                    var peerVoice = avatar.GetComponent<MarsProximityVoice>();
                    float savedVol = peerVoice != null ? peerVoice.IndividualVolume : 1f;
                    widget.VolumeSlider.SetValueWithoutNotify(savedVol);
                    if (widget.VolumePercentLabel != null)
                        widget.VolumePercentLabel.text = Mathf.RoundToInt(savedVol * 100f) + "%";

                    widget.VolumeSlider.onValueChanged.AddListener(val =>
                    {
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
