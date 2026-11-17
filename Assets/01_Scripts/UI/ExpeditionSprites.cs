using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    internal static class ExpeditionSprites
    {
        private const string Folder = "UI/Expedition/";
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!Cache.TryGetValue(name, out var sprite))
            {
                sprite = Resources.Load<Sprite>(Folder + name);
                Cache[name] = sprite;
            }
            return sprite;
        }

        public static Image Icon(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var sprite = Get(name);
            if (sprite == null) return null;
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static bool Skin(Image image, string name)
        {
            var sprite = Get(name);
            if (sprite == null) return false;
            image.sprite = sprite;
            image.type = sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = 2f;
            image.color = Color.white;
            return true;
        }

        public static Image Surface(RectTransform parent, string name)
        {
            var image = Icon(parent, name, Vector2.zero, Vector2.zero);
            if (image == null) return null;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            image.preserveAspect = false;
            Skin(image, name);
            return image;
        }

        public static Image Sequence(Transform parent, string name, Vector2 position, Vector2 size, float seconds, bool loop)
        {
            var image = Icon(parent, name + "0", position, size);
            if (image != null) image.gameObject.AddComponent<ExpeditionSpriteAnimation>().Configure(image, name, seconds, loop);
            return image;
        }

        public static void Draw(Rect rect, string name, ScaleMode mode = ScaleMode.ScaleToFit)
        {
            var sprite = Get(name);
            if (sprite != null) GUI.DrawTexture(rect, sprite.texture, mode, true);
        }

        public static string Frame(string name, float elapsed, float seconds, bool loop)
        {
            int frame = Mathf.FloorToInt(4 * Mathf.Max(0, elapsed) / seconds);
            return name + (loop ? frame % 4 : Mathf.Min(frame, 3));
        }
    }
}
