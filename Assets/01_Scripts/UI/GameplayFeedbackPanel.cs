using UnityEngine;

namespace GameJam.UI
{
    /// <summary>Shared presentation for the existing IMGUI interaction prompts.</summary>
    internal static class GameplayFeedbackPanel
    {
        private static GUIStyle titleStyle, hintStyle, valueStyle;

        public static void Draw(string title, string hint, Color accent, float progress = -1f, bool top = false)
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = true };
                hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, wordWrap = true };
                valueStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = new Color32(240, 247, 255, 255);
                hintStyle.normal.textColor = new Color32(174, 198, 216, 255);
            }
            float scale = Mathf.Clamp(Screen.height / 1080f, .65f, 1.5f);
            titleStyle.fontSize = Mathf.RoundToInt(22 * scale);
            hintStyle.fontSize = Mathf.RoundToInt(18 * scale);
            valueStyle.fontSize = Mathf.RoundToInt(22 * scale);
            valueStyle.normal.textColor = accent;
            Rect safe = Screen.safeArea;
            float width = Mathf.Min(560 * scale, safe.width - 32 * scale);
            float height = (progress >= 0 ? 150 : 116) * scale;
            float safeTop = Screen.height - safe.yMax;
            float y = top ? safeTop + 32 * scale : Screen.height - safe.yMin - height - 110 * scale;
            y = Mathf.Max(safeTop + 16 * scale, y);
            float x = safe.x + (safe.width - width) * .5f;
            var previousColor = GUI.color;
            try
            {
                GUI.color = Color.white;
                Paint(new Rect(x, y, width, height), new Color32(9, 18, 30, 245));
                Paint(new Rect(x, y, 4 * scale, height), accent);
                float padding = 24 * scale;
                float valueWidth = progress >= 0 ? 82 * scale : 0;
                GUI.Label(new Rect(x + padding, y + 15 * scale, width - 2 * padding - valueWidth, 42 * scale), title, titleStyle);
                if (progress >= 0)
                {
                    progress = Mathf.Clamp01(progress);
                    GUI.Label(new Rect(x + width - padding - valueWidth, y + 15 * scale, valueWidth, 42 * scale), $"{progress * 100:0}%", valueStyle);
                    var track = new Rect(x + padding, y + 69 * scale, width - padding * 2, 10 * scale);
                    Paint(track, new Color32(35, 52, 69, 255));
                    track.width *= progress;
                    Paint(track, accent);
                }
                GUI.Label(new Rect(x + padding, y + (progress >= 0 ? 94 : 61) * scale, width - padding * 2, 44 * scale), hint, hintStyle);
            }
            finally { GUI.color = previousColor; }
        }

        private static void Paint(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
