using UnityEngine;
using UnityEngine.UI;

namespace GameJam.UI
{
    internal sealed class ExpeditionSpriteAnimation : MonoBehaviour
    {
        private Image image;
        private readonly Sprite[] frames = new Sprite[4];
        private float start, duration;
        private bool loop;

        public void Configure(Image target, string prefix, float seconds, bool repeat)
        {
            image = target;
            duration = seconds;
            loop = repeat;
            for (int i = 0; i < frames.Length; i++) frames[i] = ExpeditionSprites.Get(prefix + i);
            Restart();
        }

        private void OnEnable() => Restart();

        private void Restart()
        {
            start = Time.unscaledTime;
            if (image != null) image.sprite = frames[0];
        }

        private void Update()
        {
            if (image == null) return;
            int index = Mathf.FloorToInt((Time.unscaledTime - start) * frames.Length / duration);
            var frame = frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
            if (frame != null && image.sprite != frame) image.sprite = frame;
        }
    }
}
