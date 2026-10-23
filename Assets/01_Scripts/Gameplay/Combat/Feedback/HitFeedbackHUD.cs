using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// HUD local de "game feel": hitmarker en la mira (blanco al acertar, rojo y más grande al matar),
    /// cartel de eliminación, avisos de botín (+1 Cristal) y sonidos cortos generados por código.
    /// Es local a cada jugador: solo lo ve quien disparó / mató / recogió.
    /// </summary>
    public sealed class HitFeedbackHUD : MonoBehaviour
    {
        static HitFeedbackHUD instance;

        float hitAt = -10f, killAt = -10f, bannerAt = -10f;
        string banner;
        AudioSource audioSource;
        AudioClip hitClip, killClip, pickupClip;
        Texture2D lineTex;

        struct Popup { public string text; public float at; }
        readonly List<Popup> popups = new List<Popup>();

        static HitFeedbackHUD Get()
        {
            if (instance != null) return instance;
            var go = new GameObject("HitFeedbackHUD");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitFeedbackHUD>();
            return instance;
        }

        void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            hitClip = Tone("hit", .06f, 1900f, 900f, .55f, .0f);
            killClip = Tone("kill", .28f, 520f, 180f, .8f, .35f);
            pickupClip = Tone("pickup", .12f, 1100f, 1800f, .35f, .0f);
            lineTex = Texture2D.whiteTexture;
        }

        /// <summary>Acierto del jugador local.</summary>
        public static void Hit()
        {
            var h = Get();
            h.hitAt = Time.unscaledTime;   // solo la cruz en la mira, sin sonido
        }

        /// <summary>Golpe final del jugador local.</summary>
        public static void Kill(string enemyName)
        {
            var h = Get();
            h.killAt = Time.unscaledTime;
            h.bannerAt = Time.unscaledTime;
            h.banner = enemyName + " ELIMINADO";
            h.audioSource.PlayOneShot(h.killClip, .9f);
        }

        /// <summary>Botín recibido por el jugador local.</summary>
        public static void Loot(string text)
        {
            var h = Get();
            h.popups.Add(new Popup { text = text, at = Time.unscaledTime });
            if (h.popups.Count > 6) h.popups.RemoveAt(0);
            h.audioSource.PlayOneShot(h.pickupClip, .5f);
        }

        void OnGUI()
        {
            float now = Time.unscaledTime;
            var c = new Vector2(Screen.width * .5f, Screen.height * .5f);

            // Hitmarker: 4 trazos diagonales alrededor de la mira.
            float tk = now - killAt, th = now - hitAt;
            bool kill = tk < .35f;
            float t = kill ? tk / .35f : th / .18f;
            if (t < 1f)
            {
                float a = 1f - t * t;
                float gap = kill ? 9f + 10f * t : 7f + 5f * t;
                float len = kill ? 15f : 10f;
                float width = kill ? 4f : 3f;
                GUI.color = kill ? new Color(1f, .2f, .15f, a) : new Color(1f, 1f, 1f, a);
                for (int i = 0; i < 4; i++)
                {
                    float ang = 45f + 90f * i;
                    var dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                    var start = c + dir * gap;
                    var m = GUI.matrix;
                    GUIUtility.RotateAroundPivot(ang, start);
                    GUI.DrawTexture(new Rect(start.x, start.y - width * .5f, len, width), lineTex);
                    GUI.matrix = m;
                }
                GUI.color = Color.white;
            }

            // Cartel de eliminación.
            float tb = now - bannerAt;
            if (tb < 1.8f && !string.IsNullOrEmpty(banner))
            {
                float alpha = tb < .12f ? tb / .12f : 1f - Mathf.Clamp01((tb - 1.3f) / .5f);
                float scale = 1f + .25f * Mathf.Exp(-tb * 10f);
                var style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(30 * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(0f, 0f, 0f, .6f * alpha);
                var r = new Rect(0, Screen.height * .32f, Screen.width, 50);
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), banner, style);
                style.normal.textColor = new Color(1f, .35f, .2f, alpha);
                GUI.Label(r, banner, style);
            }

            // Avisos de botín, abajo a la derecha, suben y se desvanecen.
            popups.RemoveAll(p => now - p.at > 2.2f);
            var ps = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            for (int i = 0; i < popups.Count; i++)
            {
                float age = now - popups[i].at;
                float alpha = 1f - Mathf.Clamp01((age - 1.6f) / .6f);
                float y = Screen.height * .72f - (popups.Count - 1 - i) * 26f - age * 10f;
                ps.normal.textColor = new Color(0f, 0f, 0f, .6f * alpha);
                GUI.Label(new Rect(Screen.width - 362, y + 2, 320, 26), popups[i].text, ps);
                ps.normal.textColor = new Color(.55f, 1f, .65f, alpha);
                GUI.Label(new Rect(Screen.width - 364, y, 320, 26), popups[i].text, ps);
            }
        }

        // Tono corto con barrido de frecuencia (y un "golpe" grave opcional) para no depender de archivos de audio.
        static AudioClip Tone(string name, float seconds, float f0, float f1, float volume, float thump)
        {
            int rate = 44100, n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            float phase = 0f, phase2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = Mathf.Lerp(f0, f1, k);
                phase += 2f * Mathf.PI * f / rate;
                phase2 += 2f * Mathf.PI * 70f / rate;
                float env = Mathf.Exp(-k * 6f) * Mathf.Clamp01(i / 60f);
                float s = Mathf.Sin(phase) * .7f + Mathf.Sin(phase * 2f) * .2f;
                s += thump * Mathf.Sin(phase2) * Mathf.Exp(-k * 4f);
                data[i] = s * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
