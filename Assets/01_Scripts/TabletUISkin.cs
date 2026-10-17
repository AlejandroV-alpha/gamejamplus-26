using TMPro;
using UnityEngine;

namespace GameJam.Tablet
{
    [CreateAssetMenu(menuName = "GameJam/Tablet UI Skin")]
    public sealed class TabletUISkin : ScriptableObject
    {
        [Header("Typography")]
        public Font font;
        public TMP_FontAsset tmpFont;

        [Header("Frames & Containers")]
        public Sprite window;
        public Sprite panel;
        public Sprite inset;
        public Sprite bracket;
        public Sprite slot;

        [Header("Buttons")]
        public Sprite button;
        public Sprite hover;
        public Sprite pressed;

        [Header("Tabs")]
        public Sprite tab;
        public Sprite selectedTab;
        public Sprite tabHover;

        [Header("Icons")]
        public Sprite ship;
        public Sprite repair;
        public Sprite mine;
        public Sprite objectives;
        public Sprite closeIcon;
        public Sprite checkIcon;

        [Header("Display")]
        public Sprite wallpaper;
        [Range(0, 1)] public float wallpaperDim = 0.30f;
        [Range(0, 1)] public float widgetOpacity = 0.86f;
    }
}
