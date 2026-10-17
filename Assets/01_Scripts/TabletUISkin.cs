using UnityEngine;

namespace GameJam.Tablet
{
    [CreateAssetMenu(menuName = "GameJam/Tablet UI Skin")]
    public sealed class TabletUISkin : ScriptableObject
    {
        public Sprite panel, inset, button, hover, pressed, tab, selectedTab;
        public Sprite ship, repair, mine, objectives;
        public Sprite wallpaper;
        [Range(0, 1)] public float wallpaperDim = 0.30f;
        [Range(0, 1)] public float widgetOpacity = 0.86f;
    }
}
