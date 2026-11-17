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
        int resolutionIndex;
        ScrollRect optionsScroll;
        readonly List<Image> navigation = new();
        readonly List<TextMeshProUGUI> navigationLabels = new();
        RectTransform pageHeader;
        TextMeshProUGUI pageTitle, pageDescription;
        Image pageIcon, statusIcon;
        CanvasGroup pageContents, panelReveal;
        int activeSection = -1;
        string[] sectionTitles, sectionDescriptions;
        static readonly string[] SectionIcons = { "monitor_setup", "headset", "key_binding", "server_region" };
        static TMP_FontAsset readableFont;
        GameObject picker;
        bool fullscreen;
        bool wasDisplayPending;
        float nextRefresh;
        Action<Key> captureKey;
        InputActionRebindingExtensions.RebindingOperation rebind;
        InputAction reboundAction;
        bool wasEnabled;
        int cancelledBindingFrame = -1;
        string message = "";

        readonly Color ink = new(.9f, .96f, .98f), muted = new(.48f, .67f, .73f), accent = new(.98f, .61f, .28f);
        public static bool IsOpen => Current != null;
        static string L(string es, string en, string pt) => GameLocalization.CurrentLanguage == GameLanguage.English ? en : GameLocalization.CurrentLanguage == GameLanguage.Portuguese ? pt : es;

        public static void Open(TMP_FontAsset useFont)
        {
            if (Current != null) return;
            var go = new GameObject("Expedition Settings", typeof(RectTransform));
            var panel = go.AddComponent<ExpeditionSettingsPanel>(); Current = panel; panel.font = TMP_Settings.defaultFontAsset ?? useFont; panel.Build();
        }
        public static bool ConsumeEscape()
        {
            if (Current == null) return false;
            if (Current.picker != null) { Destroy(Current.picker); Current.picker=null; return true; }
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
            label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; label.margin = new Vector4(14, 4, 14, 4); return label;
        }
        Button Button(Transform parent, string text, Action clicked, Vector2 size)
        {
            var rt = Rect(text, parent, size); var image = Background(rt, new Color(.035f, .29f, .34f));
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            ExpeditionSprites.Skin(image, "button");
            var colors = button.colors;
            colors.highlightedColor = new Color(.85f, 1f, 1f);
            colors.pressedColor = new Color(.62f, .78f, .8f);
            colors.disabledColor = new Color(.5f, .53f, .55f, .66f);
            button.colors = colors;
            var label = Label(rt, text, 18); label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(() => clicked()); return button;
        }
        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            gameObject.AddComponent<GraphicRaycaster>(); var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 900); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var backdrop = Rect("Backdrop", transform, Vector2.zero); Fill(backdrop); Background(backdrop, new Color(.005f, .015f, .02f, .96f));
            var body = Rect("Datapad", backdrop, new Vector2(1160, 820));
            ExpeditionSprites.Skin(Background(body, new Color(.045f, .085f, .105f)), "panel");
            panelReveal = body.gameObject.AddComponent<CanvasGroup>();
            panelReveal.alpha = 0f;

            var mark = Rect("Settings emblem", body, new Vector2(76, 76));
            mark.anchoredPosition = new Vector2(-490, 337);
            ExpeditionSprites.Skin(Background(mark, new Color(.07f,.15f,.17f)), "slot_active");
            ExpeditionSprites.Icon(mark, "settings", Vector2.zero, new Vector2(38, 38));
            var heading = Rect("Heading", body, new Vector2(650, 48));
            heading.anchoredPosition = new Vector2(-102, 350);
            var title = Label(heading, L("AJUSTES", "SETTINGS", "CONFIGURAÇÕES"), 37);
            title.fontStyle = FontStyles.Bold;
            if (readableFont == null)
            {
                var source = Resources.Load<Font>("UI/Menu/JetBrainsMono-Regular");
                if (source != null) readableFont = TMP_FontAsset.CreateFontAsset(source);
            }
            if (readableFont != null) title.font = readableFont;
            var subtitle = Rect("Subtitle", body, new Vector2(650, 30));
            subtitle.anchoredPosition = new Vector2(-102, 312);
            Label(subtitle, L("Personaliza tu equipo de expedición", "Configure your expedition setup", "Personalize seu equipamento de expedição"), 18).color = muted;

            var close = Button(body, L("VOLVER", "BACK", "VOLTAR"), Close, new Vector2(146, 48));
            ((RectTransform)close.transform).anchoredPosition = new Vector2(467, 343);
            ExpeditionSprites.Icon(close.transform, "back", new Vector2(-52, 0), new Vector2(18, 18));
            var closeText = close.GetComponentInChildren<TextMeshProUGUI>();
            closeText.margin = new Vector4(34, 4, 12, 4);

            var sidebar = Rect("Categories", body, new Vector2(244, 560));
            sidebar.anchoredPosition = new Vector2(-418, -6);
            Background(sidebar, new Color(.04f,.10f,.13f,.92f)).raycastTarget = false;
            sectionTitles = new[] { L("Pantalla", "Display", "Tela"), L("Audio y voz", "Audio & voice", "Áudio e voz"),
                L("Controles", "Controls", "Controles"), L("Conexión", "Connection", "Conexão") };
            sectionDescriptions = new[]
            {
                L("Resolución, calidad y rendimiento", "Resolution, quality and performance", "Resolução, qualidade e desempenho"),
                L("Volumen, idioma y comunicación con la tripulación", "Volume, language and crew communications", "Volume, idioma e comunicação com a tripulação"),
                L("Sensibilidad y asignación de teclas", "Sensitivity and key bindings", "Sensibilidade e atalhos do teclado"),
                L("Región de la sala y diagnóstico de red", "Room region and network diagnostics", "Região da sala e diagnóstico de rede")
            };
            for (int i = 0; i < sectionTitles.Length; i++)
            {
                int index = i;
                var button = Button(sidebar, sectionTitles[i], () => ShowSection(index), new Vector2(218, 72));
                ((RectTransform)button.transform).anchoredPosition = new Vector2(0, 222 - i * 84);
                ExpeditionSprites.Icon(button.transform, SectionIcons[i], new Vector2(-76, 0), new Vector2(34, 34));
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(62, 4, 10, 4);
                label.fontSize = 20;
                navigationLabels.Add(label);
                navigation.Add(button.GetComponent<Image>());
            }

            pageHeader = Rect("Category heading", body, new Vector2(814, 72));
            pageHeader.anchoredPosition = new Vector2(130, 235);
            Background(pageHeader, new Color(.065f, .145f, .165f)).raycastTarget = false;
            pageIcon = ExpeditionSprites.Icon(pageHeader, SectionIcons[0], new Vector2(-365, 0), new Vector2(42, 42));
            var pageTitleArea = Rect("Category title", pageHeader, new Vector2(690, 32));
            pageTitleArea.anchoredPosition = new Vector2(37, 13);
            pageTitle = Label(pageTitleArea, "", 26);
            pageTitle.fontStyle = FontStyles.Bold;
            var pageDescriptionArea = Rect("Category description", pageHeader, new Vector2(690, 27));
            pageDescriptionArea.anchoredPosition = new Vector2(37, -16);
            pageDescription = Label(pageDescriptionArea, "", 17);
            pageDescription.color = muted;

            var viewport = Rect("Viewport", body, new Vector2(814, 486));
            viewport.anchoredPosition = new Vector2(130, -52);
            Background(viewport, new Color(.022f, .052f, .068f));
            viewport.gameObject.AddComponent<RectMask2D>();
            pageContents = viewport.gameObject.AddComponent<CanvasGroup>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); optionsScroll = scroll; scroll.horizontal = false; scroll.viewport = viewport; scroll.scrollSensitivity = 42; scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect("Options", viewport, Vector2.zero); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(18, 30, 18, 24); layout.spacing = 10;
            layout.childControlHeight = true; layout.childForceExpandHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = content;
            var bar = Rect("Scroll rail", viewport, new Vector2(8,0)); bar.anchorMin = new Vector2(1,0); bar.anchorMax = Vector2.one; bar.offsetMin = new Vector2(-13,8); bar.offsetMax = new Vector2(-5,-8); Background(bar,new Color(.08f,.14f,.17f));
            var thumb = Rect("Scroll thumb",bar,Vector2.zero); Fill(thumb); Background(thumb,new Color(.22f,.64f,.67f));
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>(); scrollbar.handleRect=thumb; scrollbar.targetGraphic=thumb.GetComponent<Image>(); scrollbar.direction=Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar=scrollbar;
            var footer = Rect("Status", body, new Vector2(1080, 56));
            footer.anchoredPosition = new Vector2(0, -346);
            Background(footer, new Color(.037f,.09f,.11f));
            statusIcon = ExpeditionSprites.Icon(footer, "autosave", new Vector2(-505, 0), new Vector2(27, 27));
            status = Label(footer, "", 17);
            status.margin = new Vector4(60, 4, 16, 4);
            status.color = ink;
            microphoneTest = gameObject.AddComponent<LocalMicrophoneTest>(); fullscreen = Screen.fullScreen;
            foreach (var resolution in Screen.resolutions)
            { var size = new Vector2Int(resolution.width, resolution.height); if (!resolutions.Contains(size)) resolutions.Add(size); }
            var current = new Vector2Int(Screen.width, Screen.height); if (!resolutions.Contains(current)) resolutions.Add(current);
            resolutionIndex = resolutions.IndexOf(current); ShowSection(0);
        }
        void Clear()
        {
            CancelCapture();
            microphoneTest.StopTest();
            refreshers.Clear();
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            content.anchoredPosition = Vector2.zero;
        }

        void ShowSection(int index)
        {
            if (activeSection == index) return;
            activeSection = index;
            Clear();
            pageTitle.text = sectionTitles[index];
            pageDescription.text = sectionDescriptions[index];
            if (pageIcon != null) pageIcon.sprite = ExpeditionSprites.Get(SectionIcons[index]);
            for (int i = 0; i < navigation.Count; i++)
            {
                bool selected = i == index;
                ExpeditionSprites.Skin(navigation[i], selected ? "button_focus" : "button");
                navigationLabels[i].color = selected ? accent : ink;
                navigationLabels[i].fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
            }
            switch (index)
            {
                case 0: BuildDisplay(); break;
                case 1: BuildVoice(); break;
                case 2: BuildControls(); break;
                case 3: BuildConnection(); break;
            }
            foreach (var refresh in refreshers) refresh();
            Canvas.ForceUpdateCanvases();
            optionsScroll.StopMovement();
            optionsScroll.verticalNormalizedPosition = 1f;
            pageContents.alpha = .55f;
        }
        RectTransform Row(string title, int height = 72)
        {
            var row = Rect(title, content, new Vector2(970, height));
            Background(row, new Color(.052f, .117f, .142f));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return row;
        }
        void Caption(RectTransform parent, string title, string icon)
        {
            var label = Label(parent, title, 20);
            var sprite = ExpeditionSprites.Icon(parent, icon, Vector2.zero, new Vector2(27f, 27f));
            if (sprite == null) return;
            sprite.rectTransform.anchorMin = sprite.rectTransform.anchorMax = new Vector2(0f, .5f);
            sprite.rectTransform.anchoredPosition = new Vector2(27f, 0f);
            label.margin = new Vector4(51f, 4f, 8f, 4f);
        }
        Button Option(string title, Func<string> value, Action clicked, Func<bool> enabled = null, string icon = null)
        {
            var row = Row(title, 76);
            var name = Rect("Option", row, Vector2.zero); name.anchorMin=new Vector2(0,0); name.anchorMax=new Vector2(.49f,1); name.offsetMin=new Vector2(4,4); name.offsetMax=new Vector2(-4,-4);
            Caption(name, title, icon);
            var button = Button(row, "", clicked, Vector2.zero); var rt = (RectTransform)button.transform; rt.anchorMin=new Vector2(.5f,0); rt.anchorMax=Vector2.one; rt.offsetMin=new Vector2(4,12); rt.offsetMax=new Vector2(-14,-12);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.fontSize=18;
            label.enableAutoSizing=true; label.fontSizeMin=15; label.fontSizeMax=18;
            void Refresh() { label.text = value(); button.interactable = enabled == null || enabled(); }
            refreshers.Add(Refresh);
            button.onClick.AddListener(Refresh);
            return button;
        }
        void ToggleOption(string title, Func<bool> enabled, Action toggle, string icon)
        {
            var button = Option(title, () => enabled() ? L("Activo", "On", "Ativo") : L("Inactivo", "Off", "Inativo"), toggle, icon: icon);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = new Vector4(20, 4, 90, 4);
            var rail = Rect("Toggle track", button.transform, new Vector2(58, 28));
            rail.anchorMin = rail.anchorMax = new Vector2(1, .5f);
            rail.anchoredPosition = new Vector2(-45, 0);
            var track = Background(rail, Color.black);
            track.raycastTarget = false;
            var knob = Rect("Toggle thumb", rail, new Vector2(21, 20));
            var handle = Background(knob, Color.white);
            handle.raycastTarget = false;
            void Refresh()
            {
                bool active = enabled();
                track.color = active ? new Color(.055f, .45f, .43f) : new Color(.14f, .20f, .23f);
                handle.color = label.color = active ? new Color(.52f, 1f, .88f) : muted;
                knob.anchoredPosition = new Vector2(active ? 14f : -14f, 0f);
            }
            refreshers.Add(Refresh);
            button.onClick.AddListener(Refresh);
        }
        void Choice(string title, Func<string[]> values, Func<int> selected, Action<int> changed, Func<bool> enabled = null, string icon = null)
        {
            var button = Option(title, () => { var items=values(); return items.Length == 0 ? L("Sin opciones", "No options", "Sem opções") : items[Mathf.Clamp(selected(),0,items.Length-1)]; },
                () => OpenChoices(title,values(),selected(),changed), () => values().Length > 0 && (enabled == null || enabled()), icon);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft; label.margin = new Vector4(20,4,58,4);
            var outline=button.gameObject.AddComponent<Outline>(); outline.effectColor=new Color(.22f,.35f,.40f); outline.effectDistance=new Vector2(1,-1);
            var arrow=Rect("Dropdown chevron",button.transform,new Vector2(22,22)); arrow.anchorMin=arrow.anchorMax=new Vector2(1,.5f); arrow.anchoredPosition=new Vector2(-27,0);
            var chevron=arrow.gameObject.AddComponent<MenuSymbolGraphic>(); chevron.symbol=MenuSymbolGraphic.Symbol.Chevron; chevron.color=muted; chevron.raycastTarget=false;
        }
        void OpenChoices(string title, string[] items, int selected, Action<int> changed)
        {
            if(items.Length == 0) return;
            if(picker!=null) Destroy(picker);
            var shade=Rect("Choose option",transform,Vector2.zero); Fill(shade); picker=shade.gameObject; Background(shade,new Color(0,0,0,.85f));
            float height = Mathf.Clamp(items.Length * 68 + 134, 270, 660);
            var box=Rect("Choices",shade,new Vector2(680,height)); ExpeditionSprites.Skin(Background(box,new Color(.035f,.065f,.09f)), "panel");
            var edge=box.gameObject.AddComponent<Outline>(); edge.effectColor=new Color(.24f,.37f,.43f); edge.effectDistance=new Vector2(1,-1);
            var heading=Rect("Heading",box,new Vector2(460,58)); heading.anchoredPosition=new Vector2(-76,height*.5f-42); Label(heading,title,26).fontStyle=FontStyles.Bold;
            var cancel=Button(box,L("CERRAR","CLOSE","FECHAR"),()=>{Destroy(picker);picker=null;},new Vector2(132,48)); ((RectTransform)cancel.transform).anchoredPosition=new Vector2(252,height*.5f-42);
            var view=Rect("Choice list",box,new Vector2(640,height-104)); view.anchoredPosition=new Vector2(0,-36); Background(view,new Color(.018f,.045f,.055f)); view.gameObject.AddComponent<RectMask2D>();
            var scroll=view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.viewport=view; scroll.scrollSensitivity=40; scroll.movementType=ScrollRect.MovementType.Clamped;
            var list=Rect("Values",view,Vector2.zero); list.anchorMin=new Vector2(0,1); list.anchorMax=Vector2.one; list.pivot=new Vector2(.5f,1); list.sizeDelta=Vector2.zero;
            var layout=list.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(12,12,12,12); layout.spacing=8; layout.childControlWidth=true; layout.childForceExpandWidth=true; layout.childControlHeight=true; layout.childForceExpandHeight=false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize; scroll.content=list;
            for(int i=0;i<items.Length;i++)
            {
                int index=i; var button=Button(list,items[i],()=>{Destroy(picker);picker=null;changed(index);foreach(var refresh in refreshers)refresh();},new Vector2(700,64));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight=64;
                var label=button.GetComponentInChildren<TextMeshProUGUI>(); label.alignment=TextAlignmentOptions.MidlineLeft; label.fontSize=20; label.enableAutoSizing=true;label.fontSizeMin=16;label.fontSizeMax=20;
                label.margin = new Vector4(20,4,58,4);
                ExpeditionSprites.Skin(button.GetComponent<Image>(), i == selected ? "button_focus" : "button");
                if(i==selected)
                {
                    label.fontStyle=FontStyles.Bold;
                    var mark=Rect("Selected option",button.transform,new Vector2(24,24)); mark.anchorMin=mark.anchorMax=new Vector2(1,.5f);mark.anchoredPosition=new Vector2(-28,0);
                    var check=mark.gameObject.AddComponent<MenuSymbolGraphic>();check.symbol=MenuSymbolGraphic.Symbol.Check;check.color=accent;check.raycastTarget=false;
                }
            }
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition=items.Length>0?1f-(float)Mathf.Max(0,selected)/(items.Length-1f+0.001f):1;
        }
        void Note(string text, string icon = null)
        {
            var row = Row("Help", 74);
            row.GetComponent<Image>().color = new Color(.025f,.065f,.08f);
            Caption(row, text, icon);
            var label = row.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 17;
            label.color = muted;
        }
        void Slider(string title, float min, float max, float initial, Action<float> changed, string icon = null)
        {
            var row = Row(title, 104);
            var caption = Rect("Caption",row,Vector2.zero); caption.anchorMin=new Vector2(0,.52f); caption.anchorMax=new Vector2(.78f,1); caption.offsetMin=new Vector2(4,0); caption.offsetMax=new Vector2(0,-4); Caption(caption,title,icon);
            var number = Rect("Value",row,Vector2.zero); number.anchorMin=new Vector2(.78f,.52f); number.anchorMax=Vector2.one; number.offsetMin=Vector2.zero; number.offsetMax=new Vector2(-8,-4);
            string Format(float v) => max <= .2f ? v.ToString("F3") : max <= 1.5f ? Mathf.RoundToInt(v*100)+"%" : v.ToString("F1")+"x";
            var text = Label(number,Format(initial),20); text.alignment=TextAlignmentOptions.MidlineRight; text.color=accent;
            var track = Rect("Slider",row,Vector2.zero); track.anchorMin=new Vector2(0,0); track.anchorMax=new Vector2(1,.5f); track.offsetMin=new Vector2(26,10); track.offsetMax=new Vector2(-26,-4); Background(track,new Color(.035f,.075f,.09f));
            var fillArea=Rect("Fill area",track,Vector2.zero); Fill(fillArea); var fill=Rect("Fill",fillArea,Vector2.zero); Fill(fill);
            var fillImage = Background(fill,new Color(.04f,.42f,.46f));
            ExpeditionSprites.Skin(fillImage, "bar_fill");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            var handleArea=Rect("Handle area",track,Vector2.zero); Fill(handleArea); handleArea.offsetMin=new Vector2(10,0); handleArea.offsetMax=new Vector2(-10,0);
            var handle=Rect("Handle",handleArea,new Vector2(22,36)); Background(handle,accent);
            var slider=track.gameObject.AddComponent<Slider>(); slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>(); slider.minValue=min; slider.maxValue=max; slider.SetValueWithoutNotify(initial);
            slider.onValueChanged.AddListener(v => { text.text=Format(v); changed(v); });
        }
        void BuildDisplay()
        {
            Choice(L("Resolución", "Resolution", "Resolução"), () => resolutions.Select(r=>r.x+" x "+r.y).ToArray(), () => resolutionIndex, i => resolutionIndex=i, icon: "monitor_setup");
            Choice(L("Modo de pantalla", "Display mode", "Modo de tela"), () => new[]{L("Ventana","Windowed","Janela"),L("Sin bordes","Borderless","Sem bordas")},()=>fullscreen?1:0,i=>fullscreen=i==1, icon: "display");
            Option(L("Aplicar pantalla", "Apply display", "Aplicar tela"), () => L("Probar 15 segundos", "Preview for 15 seconds", "Testar por 15 segundos"), () => { var r = resolutions[resolutionIndex]; ExpeditionGraphics.Instance?.PreviewDisplay(r.x, r.y, fullscreen); message = Application.isEditor ? L("Prueba de resolución disponible en ejecutable", "Resolution preview is available in the executable", "Teste de resolução disponível no executável") : ""; }, icon: "play");
            Option(L("Confirmar pantalla", "Keep display", "Confirmar tela"), () => L("Conservar cambios", "Keep changes", "Manter mudanças"), () => ExpeditionGraphics.Instance?.ConfirmDisplay(), () => ExpeditionGraphics.Instance != null && ExpeditionGraphics.Instance.PendingDisplay, icon: "complete");
            Choice(L("Calidad", "Quality", "Qualidade"), () => new[]{L("Baja","Low","Baixa"),L("Media","Medium","Média"),L("Alta","High","Alta")},()=>PlayerPrefs.GetInt("GameJam.Graphics.Preset",1),i=>ExpeditionGraphics.Instance?.SetPreset(i), icon: "settings");
            ToggleOption("VSync", () => PlayerPrefs.GetInt("GameJam.Graphics.VSync", 0) == 1, () => { PlayerPrefs.SetInt("GameJam.Graphics.VSync", 1 - PlayerPrefs.GetInt("GameJam.Graphics.VSync", 0)); ExpeditionGraphics.Instance?.ApplyFrameLimit(); PlayerPrefs.Save(); }, "frame_rate");
            int[] limits = { 30, 60, 90, 120, 144, 165, 240, -1 };
            Choice(L("Límite de FPS (sin VSync)", "FPS cap (VSync off)", "Limite FPS (sem VSync)"),()=>limits.Select(v=>v<0?L("Sin límite","Unlimited","Sem limite"):v.ToString()).ToArray(),()=>Mathf.Max(0,Array.IndexOf(limits,PlayerPrefs.GetInt("GameJam.Graphics.FpsLimit",120))),i=>{PlayerPrefs.SetInt("GameJam.Graphics.FpsLimit",limits[i]);ExpeditionGraphics.Instance?.ApplyFrameLimit();PlayerPrefs.Save();}, icon: "frame_rate");
            ToggleOption(L("Postprocesado", "Post-processing", "Pós-processamento"), () => PlayerPrefs.GetInt("GameJam.Graphics.PostProcessing", 1) == 1, () => ExpeditionGraphics.Instance?.SetPostProcessing(PlayerPrefs.GetInt("GameJam.Graphics.PostProcessing", 1) == 0), "monitor_setup");

            Note(L("Los cambios de pantalla se revierten en 15 s si no confirmas. F11 alterna ventana/completa.", "Display changes revert after 15 s unless confirmed. F11 toggles fullscreen.", "Mudanças de tela revertem em 15 s sem confirmação. F11 alterna tela cheia."));
        }
        void BuildVoice()
        {
            Choice(L("Idioma", "Language", "Idioma"),()=>new[]{"Español","English","Português (Brasil)"},()=>(int)GameLocalization.CurrentLanguage,i=>{GameLocalization.CurrentLanguage=(GameLanguage)i;var previousFont=font;Close();Open(previousFont);}, icon: "language_chat");
            Slider(L("Volumen general", "Master volume", "Volume geral"), 0, 1, AudioListener.volume, v => { AudioListener.volume=v; PlayerPrefs.SetFloat("GameJam.MasterVolume",v); PlayerPrefs.Save(); }, icon: "speaker");
            Note(L("Prueba local: no transmite a la sala. Usa auriculares para evitar eco. V es la tecla inicial.", "Local test never transmits to the room. Use headphones to avoid feedback. Default talk key: V.", "Teste local não transmite à sala. Use fones para evitar eco. Tecla inicial: V."), "privacy");
            Choice(L("Micrófono", "Microphone", "Microfone"),()=>new[]{L("Predeterminado del sistema","System default","Padrão do sistema")}.Concat(Microphone.devices).ToArray(),()=>Array.IndexOf(Microphone.devices,VoicePreferences.Device)+1,i=>{microphoneTest.StopTest();var devices=Microphone.devices;PlayerPrefs.SetString("GameJam.Voice.Device",i>0&&i<=devices.Length?devices[i-1]:"");VoicePreferences.Changed(true);}, icon: "mic");
            Option(L("Detección y recuperación", "Capture recovery", "Recuperar captura"), () => L("Reiniciar micrófono", "Restart microphone", "Reiniciar microfone"), () => { microphoneTest.StopTest(); LocalVoice()?.RefreshCapture(); }, icon: "restore_defaults");
            Option(L("Prueba local con escucha", "Local loopback test", "Teste local com escuta"), () => microphoneTest.IsRunning ? L("DETENER", "STOP", "PARAR") : L("INICIAR", "START", "INICIAR"), () => { if (microphoneTest.IsRunning) microphoneTest.StopTest(); else microphoneTest.StartTest(true); }, icon: "headset");
            Choice(L("Modo de voz", "Voice mode", "Modo de voz"),()=>new[]{L("Mantener tecla para hablar","Hold key to talk","Segurar tecla para falar"),L("Micrófono abierto","Open microphone","Microfone aberto")},()=>MarsProximityVoice.LocalPushToTalk?0:1,i=>{bool ptt=i==0;MarsProximityVoice.LocalPushToTalk=ptt;PlayerPrefs.SetInt("GameJam.VoiceMode",ptt?1:0);LocalVoice()?.SetPushToTalk(ptt);VoicePreferences.Changed();}, icon: "headset");
            Option(L("Tecla para hablar", "Push to talk key", "Tecla para falar"), () => VoicePreferences.TalkKey.ToString(), () => { VoicePreferences.CapturingBinding = true; captureKey = key => { message = VoicePreferences.TrySetTalkKey(key) ? L("Tecla guardada", "Key saved", "Tecla salva") : L("Tecla reservada: elige otra", "Reserved key: choose another", "Tecla reservada: escolha outra"); }; }, icon: "key_binding");
            Slider(L("Umbral de micrófono abierto", "Open microphone threshold", "Limiar de microfone aberto"), .001f, .1f, VoicePreferences.Threshold, v => { PlayerPrefs.SetFloat("GameJam.Voice.Threshold", v); VoicePreferences.Changed(); }, icon: "mic");
            Slider(L("Volumen de voz", "Voice volume", "Volume de voz"), 0, 1.5f, MarsProximityVoice.MasterVoiceVolume, v => { MarsProximityVoice.MasterVoiceVolume = v; PlayerPrefs.SetFloat("GameJam.MasterVoiceVolume", v); PlayerPrefs.Save(); }, icon: "speaker");
        }
        MarsProximityVoice LocalVoice() => MarsFusionAvatar.Local != null ? MarsFusionAvatar.Local.GetComponent<MarsProximityVoice>() : null;
        void BuildControls()
        {
            Slider(L("Sensibilidad", "Sensitivity", "Sensibilidade"), .1f, 3, ExpeditionInputPreferences.Sensitivity, v => { PlayerPrefs.SetFloat("GameJam.Input.Sensitivity", v); PlayerPrefs.Save(); }, icon: "controls");
            ToggleOption(L("Invertir eje Y", "Invert Y", "Inverter Y"), () => ExpeditionInputPreferences.InvertY, () => { PlayerPrefs.SetInt("GameJam.Input.InvertY", ExpeditionInputPreferences.InvertY ? 0 : 1); PlayerPrefs.Save(); }, "controls");
            var player = MarsFusionAvatar.Local != null ? MarsFusionAvatar.Local.GetComponent<PlayerInput>() : null;
            var asset = player != null ? player.BindingAsset : null;
            if (asset == null) { Note(L("Entra en una sala para reasignar movimiento, salto y sprint.", "Join a room to rebind movement, jump and sprint.", "Entre em uma sala para remapear movimento, salto e corrida.")); return; }
            foreach (string name in new[] { "Move", "Jump", "Sprint" })
            {
                var action = asset.FindAction("Player/" + name); if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    int binding = i; var b = action.bindings[i]; if (b.isComposite || !b.effectivePath.Contains("Keyboard")) continue;
                    Option(name + (b.isPartOfComposite ? " / " + b.name : ""), () => action.GetBindingDisplayString(binding), () => StartRebind(action, binding), icon: "key_binding");
                }
            }
            Option(L("Restaurar controles", "Reset bindings", "Restaurar controles"), () => L("Predeterminados", "Defaults", "Padrão"), () => { asset.RemoveAllBindingOverrides(); ExpeditionInputPreferences.Save(asset); }, icon: "restore_defaults");
            Note(L("E, Q, R, Tab, G y teclas numéricas siguen reservadas para sus acciones actuales.", "E, Q, R, Tab, G and number keys remain reserved for existing actions.", "E, Q, R, Tab, G e números continuam reservados para as ações atuais."));
        }
        void StartRebind(InputAction action, int binding)
        {
            CancelCapture(); string previous = action.bindings[binding].overridePath; wasEnabled = action.enabled; reboundAction = action; action.Disable(); VoicePreferences.CapturingBinding = true;
            rebind = action.PerformInteractiveRebinding(binding).WithControlsHavingToMatchPath("<Keyboard>").WithCancelingThrough("<Keyboard>/escape")
                .OnCancel(_ => FinishRebind()).OnComplete(_ =>
                {
                    string path = action.bindings[binding].effectivePath;
                    string[] reserved = { "e", "q", "r", "tab", "i", "g", "0", "escape", "f8", "f11", VoicePreferences.TalkKey.ToString().ToLowerInvariant(), "1", "2", "3", "4", "5", "6", "7", "8", "9" };
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
            Choice(L("Región de Photon", "Photon region", "Região Photon"),()=>MultiplayerConfiguration.Regions.Select(r=>r.ToUpperInvariant()).ToArray(),()=>Array.IndexOf(MultiplayerConfiguration.Regions,MultiplayerConfiguration.SelectedRegion),i=>MultiplayerConfiguration.SelectedRegion=MultiplayerConfiguration.Regions[i],()=>!Running(), icon: "server_region");
            Note("Protocol: " + MultiplayerConfiguration.ProtocolVersion + "\n" + L("SA = Sudamérica. AUTO puede elegir regiones distintas para tus amigos.", "SA = South America. AUTO can choose different regions for friends.", "SA = América do Sul. AUTO pode escolher regiões diferentes para amigos."));
            Option(L("Diagnóstico en pantalla", "Diagnostic overlay", "Diagnóstico na tela"), () => "F8", () => { var runner = NetworkSession()?.Runner; if (runner != null) { var d = MultiplayerDiagnostics.Ensure(runner); d.Visible = !d.Visible; } }, icon: "diagnostics");
            Option(L("Exportar informe sin datos personales", "Export privacy-safe report", "Exportar relatório sem dados pessoais"), () => L("Exportar", "Export", "Exportar"), () => { var runner = NetworkSession()?.Runner; if (runner != null) { var d = MultiplayerDiagnostics.Ensure(runner); d.Export(); message = L("Informe guardado. Consulta la ruta en la consola.", "Report saved. File location is in the console.", "Relatório salvo. Consulte o caminho no console."); } else message = L("Conecta a una sesión primero", "Connect to a session first", "Conecte a uma sessão primeiro"); }, icon: "report");
            Note(L("Todos deben usar la misma región, sala y versión compatible. Cambia región antes de conectar.", "Everyone needs the same region, room and compatible build. Change region before connecting.", "Todos precisam da mesma região, sala e versão compatível. Troque a região antes de conectar."));
        }
        MarsFusionSession NetworkSession() => FindFirstObjectByType<MarsFusionSession>();
        void Update()
        {
            panelReveal.alpha = Mathf.MoveTowards(panelReveal.alpha, 1f, Time.unscaledDeltaTime * 7f);
            pageContents.alpha = Mathf.MoveTowards(pageContents.alpha, 1f, Time.unscaledDeltaTime * 6f);
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
            else if (microphoneTest.IsRunning)
            { var voice = LocalVoice(); text = microphoneTest.IsRunning ? microphoneTest.Status + " | input " + microphoneTest.Level.ToString("F3") : voice != null ? voice.CaptureStatus + " | input " + voice.InputLevel.ToString("F3") + " | PTT " + voice.TalkPressed + " | TX " + voice.IsTransmitting : microphoneTest.Status; }
            if (string.IsNullOrEmpty(text)) text=L("Guardado automático · Solo afecta a tu equipo · Esc para volver", "Auto-saved · Only affects your setup · Esc to go back", "Salvo automaticamente · Apenas neste PC · Esc para voltar");
            status.text = text;
            if (statusIcon != null)
                statusIcon.sprite = ExpeditionSprites.Get(captureKey != null || rebind != null ? "key_binding" : microphoneTest.IsRunning ? "headset" : displayPending ? "monitor_setup" : "autosave");
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
