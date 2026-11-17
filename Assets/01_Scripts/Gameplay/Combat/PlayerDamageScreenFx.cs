using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace GameJam.Combat
{
    /// <summary>
    /// Feedback en pantalla del jugador local (solo presentacion, no toca la red):
    ///  - Al recibir daño: destello rojo en los bordes proporcional al golpe y un ligero temblor de camara.
    ///  - Con poca vida (menos del 30 %): borde rojo que late sincronizado con un latido sonoro.
    ///    Late mas fuerte y mas rapido cuanto menos vida queda; entra y sale con fundido.
    /// Se crea solo al arrancar el juego y sigue a MarsFusionAvatar.Local.
    /// </summary>
    public sealed class PlayerDamageScreenFx : MonoBehaviour
    {
        private const float LowHealthRatio = .3f;

        private RawImage vignette, flash;
        private AudioSource heartbeat;
        private float? lastHealth;
        private float hitAmount, hitAge = 10f, shakeAmount, danger, beatTime, nextBeatAt;
        private MarsFusionAvatar tracked;
        private Camera shakenCamera;
        private Vector3 shakeOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("PlayerDamageScreenFx");
            DontDestroyOnLoad(go);
            go.AddComponent<PlayerDamageScreenFx>();
        }

        private void Awake()
        {
            var canvasGo = new GameObject("DamageScreenCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70; // debajo de las barras de enemigos (80) y menus
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;

            var edge = BuildVignetteTexture();
            vignette = FullScreen("LowHealthVignette", canvasGo.transform, edge);
            flash = FullScreen("HitFlash", canvasGo.transform, edge);
            // Un poco mas grande que la pantalla para que al "respirar" nunca deje ver un hueco en el borde.
            vignette.rectTransform.offsetMin = new Vector2(-110, -110);
            vignette.rectTransform.offsetMax = new Vector2(110, 110);

            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.playOnAwake = false;
            heartbeat.spatialBlend = 0;
            heartbeat.priority = 32;
            heartbeat.clip = BuildHeartbeatClip();
        }

        private static RawImage FullScreen(string name, Transform parent, Texture texture)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            image.color = new Color(1, 0, 0, 0);
            return image;
        }

        // Borde rojo: centro limpio, bordes irregulares (ruido) y mas oscuros hacia el marco, como sangre.
        private static Texture2D BuildVignetteTexture()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (size - 1f) * 2f - 1f, v = y / (size - 1f) * 2f - 1f;
                    float d = Mathf.Sqrt(u * u * .8f + v * v * 1.1f);
                    float angle = Mathf.Atan2(v, u);
                    float noise = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + 7f, Mathf.Sin(angle) * 2.2f + 3f) - .5f;
                    float a = Mathf.Clamp01(Mathf.InverseLerp(.62f + noise * .22f, 1.4f, d));
                    a = a * a * (3f - 2f * a);
                    byte r = (byte)Mathf.Lerp(235f, 95f, a);
                    pixels[y * size + x] = new Color32(r, (byte)(10 * (1 - a)), (byte)(14 * (1 - a)), (byte)(a * 255));
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // Latido "lub-dub" sintetizado: cuerpo grave con caida de tono + golpe seco; el segundo, mas corto.
        private const float DubDelay = .19f;
        private static AudioClip BuildHeartbeatClip()
        {
            const int rate = 44100;
            int length = (int)(rate * .6f);
            var data = new float[length];
            void Thump(float start, float gain, float pitch, float decay)
            {
                int s0 = (int)(start * rate);
                float phase = 0f;
                for (int i = 0; i < rate * .3f && s0 + i < length; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Min(1f, t / .006f) * Mathf.Exp(-t * decay);
                    float freq = pitch * (1f + 1.6f * Mathf.Exp(-t * 60f));
                    phase += 2f * Mathf.PI * freq / rate;
                    float body = Mathf.Sin(phase) + .35f * Mathf.Sin(phase * .5f);
                    float click = Mathf.Sin(phase * 3.1f) * Mathf.Exp(-t * 180f) * .25f;
                    data[s0 + i] += (body + click) * env * gain;
                }
            }
            Thump(0f, .8f, 52f, 22f);
            Thump(DubDelay, .5f, 64f, 30f);
            // Paso bajo simple y normalizado para que suene redondo y no sature.
            float prev = 0f, peak = .001f;
            for (int i = 0; i < length; i++) { prev += (data[i] - prev) * .35f; data[i] = prev; peak = Mathf.Max(peak, Mathf.Abs(prev)); }
            for (int i = 0; i < length; i++) data[i] = data[i] / peak * .9f;
            var clip = AudioClip.Create("Heartbeat", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void LateUpdate()
        {
            var avatar = MarsFusionAvatar.Local;
            if (avatar != tracked) { tracked = avatar; lastHealth = null; hitAmount = 0; danger = 0; }

            float ratio = 1f;
            bool active = avatar != null && avatar.IsNetworkReady && avatar.HasHealthSource
                && !SceneTransitionVideo.IsPresent;
            if (active)
            {
                var health = avatar.HealthSource;
                float max = Mathf.Max(1f, health.MaxHealth), current = health.CurrentHealth;
                ratio = Mathf.Clamp01(current / max);
                if (lastHealth.HasValue && current < lastHealth.Value - .01f && avatar.IsAlive)
                {
                    float hit = Mathf.Clamp01((lastHealth.Value - current) / (max * .25f));
                    hitAmount = Mathf.Max(hitAge < .3f ? hitAmount : 0f, Mathf.Lerp(.4f, 1f, hit));
                    hitAge = 0f;
                    shakeAmount = Mathf.Max(shakeAmount, Mathf.Lerp(.06f, .2f, hit));
                }
                lastHealth = current;
                if (!avatar.IsAlive) { active = false; ratio = 1f; }
            }
            else lastHealth = null;

            // Destello de golpe: entra en 60 ms, aguanta un instante y se va con curva suave.
            hitAge += Time.unscaledDeltaTime;
            float rise = Mathf.Clamp01(hitAge / .06f);
            float fall = Mathf.Clamp01((hitAge - .08f) / .55f);
            float flashAlpha = hitAmount * rise * (1f - fall * fall * (3f - 2f * fall));
            if (fall >= 1f) hitAmount = 0f;
            flash.color = new Color(1f, 1f, 1f, flashAlpha * .8f);

            // Vida baja: intensidad suavizada para que el efecto entre y salga con fundido.
            float targetDanger = active && ratio < LowHealthRatio ? Mathf.Lerp(.25f, 1f, 1f - ratio / LowHealthRatio) : 0f;
            danger = Mathf.MoveTowards(danger, targetDanger, Time.unscaledDeltaTime * (targetDanger > danger ? .8f : 1.5f));
            if (danger > .01f)
            {
                beatTime += Time.unscaledDeltaTime;
                if (beatTime >= nextBeatAt)
                {
                    // Sonido y pulso visual arrancan en el mismo frame. De 65 a 125 latidos por minuto.
                    beatTime = 0f;
                    nextBeatAt = 60f / Mathf.Lerp(65f, 125f, danger);
                    if (heartbeat.clip != null) heartbeat.PlayOneShot(heartbeat.clip, Mathf.Lerp(.25f, .75f, danger));
                }
            }
            else { beatTime = 10f; nextBeatAt = 0f; }
            float lub = Mathf.Exp(-beatTime * 9f) * Mathf.Min(1f, beatTime / .03f);
            float dub = beatTime > DubDelay ? .6f * Mathf.Exp(-(beatTime - DubDelay) * 11f) * Mathf.Min(1f, (beatTime - DubDelay) / .03f) : 0f;
            float pulse = Mathf.Max(lub, dub);
            float baseAlpha = Mathf.Lerp(.18f, .5f, danger) * Mathf.Clamp01(danger * 4f);
            vignette.color = new Color(1f, 1f, 1f, Mathf.Min(1f, baseAlpha + pulse * .28f * danger));
            // El borde "respira" hacia dentro con cada latido.
            vignette.rectTransform.localScale = Vector3.one * (1f - .035f * pulse * danger);

            ApplyShake();
        }

        // Temblor breve de la camara al recibir el golpe. Se deshace al frame siguiente.
        private void ApplyShake()
        {
            if (shakenCamera != null) shakenCamera.transform.localPosition -= shakeOffset;
            shakeOffset = Vector3.zero;
            shakenCamera = null;
            shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, Time.unscaledDeltaTime * (.15f + shakeAmount * 3f));
            if (shakeAmount <= 0f) return;
            var cam = Camera.main;
            if (cam == null) return;
            float n = Time.unscaledTime * 28f;
            shakeOffset = new Vector3(Mathf.PerlinNoise(n, 0f) - .5f, Mathf.PerlinNoise(0f, n) - .5f, 0f) * 2f * shakeAmount;
            cam.transform.localPosition += shakeOffset;
            shakenCamera = cam;
        }
    }
}
