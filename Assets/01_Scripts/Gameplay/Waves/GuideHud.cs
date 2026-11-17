using System.Collections.Generic;
using GameJam.Collectibles;
using GameJam.Combat;
using GameJam.Grinder;
using GameJam.Localization;
using GameJam.Multiplayer;
using GameJam.UI;
using UnityEngine;

namespace GameJam.Waves
{
    public sealed class GuideHud : MonoBehaviour
    {
        private static readonly Color Danger = new Color(1f, .38f, .32f);
        private static readonly Color Calm = new Color(.37f, .85f, 1f);
        private static readonly Color Grind = new Color(1f, .62f, .18f);
        private static readonly Color Good = new Color(.45f, 1f, .78f);
        private static readonly Color Gold = new Color(1f, .85f, .3f);
        private static readonly Color Loot = new Color(.35f, 1f, .9f);
        private static readonly Color Plate = new Color(.04f, .06f, .09f, .82f);

        private static readonly Color Muted = new Color32(170, 194, 210, 255);
        private Texture2D disc, ring, thinRing, dashed, sweep, arrow, gear, dot, soft, check, edge, shard;
        private Texture2D shipIcon, alertIcon, beaconIcon, eye, eyeLid, shield, crack;

        private struct Confetti { public Vector2 p, v; public float rot, spin, size, born, life; public Color c; }
        private readonly List<Confetti> confetti = new List<Confetti>();
        private bool confettiFired;
        private GUIStyle body, title;
        private AudioSource audioSource;
        private AudioClip warningCue, victoryCue, hullHitCue, criticalCue, lostCue;
        private WaveDirector observedWave;
        private WaveDirector.Phase lastPhase;
        private ShipObjective observedShip;
        private int seenShipHits;
        private float shipHitAt = -10f, lastHitCueAt = -10f, shipLostAt = -1f;
        private float hullTrail;
        private bool wasCritical;

        private readonly List<Vector3> enemies = new List<Vector3>();
        private readonly List<Vector3> corpses = new List<Vector3>();
        private bool carrying, trayHasLoot, hasGrinder;
        private Vector3 grinderPoint, trayPoint, me;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("GuideHud");
            DontDestroyOnLoad(go);
            go.AddComponent<GuideHud>();
        }

        private void Awake()
        {
            disc = Build(128, (u, v) => Edge(1f - Mathf.Sqrt(u * u + v * v), 40f));
            ring = Build(128, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Edge(d - .86f, 40f) * Edge(.97f - d, 40f); });
            thinRing = Build(128, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Edge(d - .93f, 60f) * Edge(.98f - d, 60f); });
            dashed = Build(128, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                return Edge(d - .88f, 60f) * Edge(.96f - d, 60f) * Edge(Mathf.Sin(a * 12f), 6f);
            });
            sweep = Build(128, (u, v) =>
            {
                // Abanico que se desvanece detras de la linea de barrido (apunta hacia arriba).
                float d = Mathf.Sqrt(u * u + v * v); if (d > .95f) return 0;
                float a = Mathf.Atan2(u, v); if (a < 0) a += Mathf.PI * 2;
                float trail = 1f - a / (Mathf.PI * .55f);
                return Mathf.Clamp01(trail) * .9f * (a < .04f ? 1.6f : 1f);
            });
            dot = Build(32, (u, v) => Edge(.9f - Mathf.Sqrt(u * u + v * v), 12f));
            soft = Build(64, (u, v) => Mathf.Pow(Mathf.Clamp01(1 - Mathf.Sqrt(u * u + v * v)), 2f));
            edge = Build(128, (u, v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)))));
            arrow = Build(64, (u, v) => Edge(.9f - v - Mathf.Abs(u) * 1.15f, 20f) * Edge(-(.38f - v - Mathf.Abs(u) * 1.15f), 20f));
            gear = Build(128, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
                float teeth = Mathf.Clamp01((Mathf.Cos(a * 8f) - .2f) * 3f);
                return Edge(Mathf.Lerp(.72f, .95f, teeth) - d, 40f) * Edge(d - .3f, 40f);
            });
            shard = Build(16, (u, v) => 1f);
            shipIcon = Build(128, (u, v) =>
            {
                float wing = Mathf.Min(.67f - Mathf.Abs(u), Mathf.Min(v + .51f, -.02f - v - Mathf.Abs(u) * .28f));
                float fuselage = Mathf.Min(.25f - Mathf.Abs(u), Mathf.Min(v + .53f, .71f - v - Mathf.Abs(u) * 1.85f));
                float engineGap = Mathf.Max(Mathf.Abs(u) - .09f, v + .32f);
                return Edge(Mathf.Min(Mathf.Max(wing, fuselage), engineGap), 42f);
            });
            beaconIcon = Build(96, (u, v) =>
            {
                float d = Mathf.Abs(u) + Mathf.Abs(v);
                return Edge(d - .53f, 28f) * Edge(.79f - d, 28f);
            });
            alertIcon = Build(128, (u, v) =>
            {
                float triangle = Mathf.Min(v + .59f, .72f - v - Mathf.Abs(u) * 1.56f);
                float rim = Edge(triangle, 45f) * Edge(.18f - triangle, 45f);
                float mark = Edge(.075f - Mathf.Abs(u), 40f) * Edge(.23f - v, 40f) * Edge(v + .15f, 40f);
                float point = Edge(.088f - new Vector2(u, v + .34f).magnitude, 40f);
                return Mathf.Max(rim, Mathf.Max(mark, point));
            });
            check = Build(128, (u, v) =>
            {
                // Check grueso: dos segmentos.
                float a = SegDist(new Vector2(u, v), new Vector2(-.55f, .02f), new Vector2(-.15f, -.4f));
                float b = SegDist(new Vector2(u, v), new Vector2(-.15f, -.4f), new Vector2(.6f, .45f));
                return Edge(.13f - Mathf.Min(a, b), 40f);
            });
            eye = BuildColor(128, EyePixel);
            eyeLid = Build(128, (u, v) => Edge(.06f - Mathf.Abs(v + .02f * Mathf.Cos(u * 3f)), 30f) * Edge(.85f - Mathf.Abs(u), 30f));
            shield = Build(128, ShieldAlpha);
            crack = Build(128, CrackAlpha);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
            warningCue = LoadCue("IncomingWave");
            victoryCue = LoadCue("WaveSecured");
            hullHitCue = LoadCue("HullImpact");
            criticalCue = LoadCue("HullCritical");
            lostCue = LoadCue("HullLost");
        }

        private static AudioClip LoadCue(string name) => Resources.Load<AudioClip>("Audio/Waves/" + name);

        private void Cue(AudioClip clip, float volume)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private void TrackShip()
        {
            var ship = ShipObjective.Instance;
            if (ship == null || ship.Object == null || !ship.Object.IsValid)
            {
                observedShip = null;
                shipLostAt = -1f;
                return;
            }

            bool critical = ship.Ratio <= .25f;
            if (observedShip != ship)
            {
                observedShip = ship;
                seenShipHits = ship.HitCount;
                hullTrail = ship.Ratio;
                wasCritical = critical;
                shipHitAt = lastHitCueAt = -10f;
                shipLostAt = -1f;
            }

            if (ship.Destroyed)
            {
                if (shipLostAt < 0f)
                {
                    shipLostAt = Time.unscaledTime;
                    audioSource.Stop();
                    Cue(lostCue, .86f);
                }
                return;
            }

            float now = Time.unscaledTime;
            if (ship.HitCount != seenShipHits)
            {
                seenShipHits = ship.HitCount;
                shipHitAt = now;
                if (critical && !wasCritical)
                {
                    Cue(criticalCue, .78f);
                    lastHitCueAt = now;
                }
                else if (now - lastHitCueAt >= .8f)
                {
                    Cue(hullHitCue, .52f);
                    lastHitCueAt = now;
                }
            }
            wasCritical = critical;
            if (now - shipHitAt > .18f)
                hullTrail = Mathf.MoveTowards(hullTrail, ship.Ratio, Time.unscaledDeltaTime * .6f);
            hullTrail = Mathf.Max(hullTrail, ship.Ratio);
        }

        private void Update()
        {
            UpdateConfetti();
            enemies.Clear(); corpses.Clear();
            carrying = false; trayHasLoot = false; hasGrinder = false;
            var local = MarsFusionAvatar.Local;
            if (local == null || !local.IsNetworkReady || SceneTransitionVideo.IsPresent)
            {
                if (audioSource.isPlaying) audioSource.Stop();
                observedWave = null;
                observedShip = null;
                return;
            }
            TrackShip();
            me = local.transform.position;

            var wave = WaveDirector.Instance;
            if (wave != null && wave.Object != null && wave.Object.IsValid)
            {
                if ((observedWave != wave || lastPhase != wave.Current) && shipLostAt < 0f)
                {
                    float age = wave.Now - wave.PhaseStart;
                    if (wave.Current == WaveDirector.Phase.Warning && age < wave.PhaseEnd - wave.PhaseStart - .15f)
                        Cue(warningCue, .82f);
                    if (wave.Current == WaveDirector.Phase.Cleared && age < 2f)
                    {
                        Cue(victoryCue, .86f);
                        if (!confettiFired) BurstConfetti();
                    }
                }
                observedWave = wave;
                lastPhase = wave.Current;
                if (wave.Current != WaveDirector.Phase.Cleared) confettiFired = false;
                if (wave.Current == WaveDirector.Phase.Active)
                    foreach (var e in FindObjectsByType<EnemyCombatUI>(FindObjectsSortMode.None))
                        if (!e.IsDead && (e.transform.position - me).sqrMagnitude < 110f * 110f) enemies.Add(e.DamagePosition);
            }

            else observedWave = null;

            foreach (var r in FindObjectsByType<MonsterRemains>(FindObjectsSortMode.None))
            {
                if (r.Object == null || !r.Object.IsValid || !r.Ready || r.IsFed) continue;
                if (r.IsCarried && r.Carrier == local.Object) { carrying = true; continue; }
                if (!r.IsCarried && (r.BodyPoint - me).sqrMagnitude < 60f * 60f) corpses.Add(r.BodyPoint);
            }

            var grinder = MonsterGrinder.Nearest(me);
            if (grinder != null)
            {
                hasGrinder = true;
                grinderPoint = grinder.MouthPoint + Vector3.up * 1.4f;
                trayPoint = grinder.TrayPoint;
                foreach (var m in FindObjectsByType<MaterialPickup>(FindObjectsSortMode.None))
                    if ((m.transform.position - trayPoint).sqrMagnitude < 1.6f * 1.6f) { trayHasLoot = true; break; }
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            var local = MarsFusionAvatar.Local;
            if (cam == null || local == null || !local.IsNetworkReady || SceneTransitionVideo.IsPresent) return;
            float s = InteractionPromptCard.Scale;
            float t = Time.time;

            foreach (var c in corpses) CorpseBadge(cam, c, s, t);
            if (carrying && hasGrinder) GrinderTarget(cam, s, t);
            if (trayHasLoot)
            {
                var collect = ExpeditionSprites.Get("collect");
                if (collect != null) WorldSprite(cam, trayPoint + Vector3.up * (1.1f + .12f * Mathf.Sin(t * 5f)), collect, Loot, 42f * s, t);
            }
            foreach (var e in enemies)
            {
                Vector3 sp = cam.WorldToScreenPoint(e);
                if (sp.z > 0 && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height) continue;
                EdgeArrow(cam, e, Danger, 26f * s, t);
            }

            DrawDefenseHud(cam, t);
            GUI.color = Color.white;
        }

        // ================================================================== HUD de defensa
        // Grupo centrado arriba: a la izquierda el ESCUDO DE LA NAVE con su barra; a la derecha el
        // RADAR DE AMENAZA con el ojo del monstruo. Casi sin texto: todo se lee por forma, color y movimiento.

        private void DrawDefenseHud(Camera cam, float t)
        {
            if (observedShip == null && observedWave == null) return;
            float s = Mathf.Min(InteractionPromptCard.Scale, Screen.width / 640f);
            float radarSize = 96f * s, shipW = 236f * s, gap = 34f * s;
            float total = (observedShip != null ? shipW + gap : 0f) + (observedWave != null ? radarSize : 0f);
            float x = (Screen.width - total) * .5f, top = 22f * s;
            var radar = new Rect(x + (observedShip != null ? shipW + gap : 0f), top, radarSize, radarSize);
            var hull = new Rect(x, top + radarSize * .5f - 26f * s, shipW, 52f * s);

            if (observedShip != null)
            {
                if (shipLostAt >= 0f) { ShipLost(s, t, Time.unscaledTime - shipLostAt); return; }
                DrawHull(cam, hull, s, t);
            }
            if (observedWave != null) DrawRadar(cam, radar, s, t);
            DrawConfetti();
        }

        // ------------------------------------------------------------------ escudo de la nave

        private void DrawHull(Camera cam, Rect area, float s, float t)
        {
            float ratio = observedShip.Ratio;
            float hit = Mathf.Clamp01(1f - (Time.unscaledTime - shipHitAt) * 2.5f);
            bool critical = ratio <= .25f;
            Color tint = critical ? Danger : ratio <= .5f ? Gold : Calm;
            float beat = critical ? Mathf.Abs(Mathf.Sin(t * 6f)) : 0f;
            float shake = hit * 4f * s * Mathf.Sin(Time.unscaledTime * 60f);

            // Emblema a la derecha (pegado al radar): escudo con la nave, que se agrieta.
            float e = area.height * 1.25f;
            var emblem = new Rect(area.xMax - e + shake, area.center.y - e / 2f, e, e);
            GUI.color = Alpha(tint, .22f + .45f * hit + .3f * beat);
            GUI.DrawTexture(Grow(emblem, 1.9f), soft);
            GUI.color = Plate;
            GUI.DrawTexture(Grow(emblem, 1.06f), shield);
            GUI.color = Color.Lerp(tint, Color.white, hit);
            GUI.DrawTexture(emblem, shield);
            if (ratio < .6f) { GUI.color = new Color(.05f, .02f, .02f, Mathf.InverseLerp(.6f, .1f, ratio)); GUI.DrawTexture(emblem, crack); }

            // Barra de casco: placa oscura inclinada, 10 celdas, rastro de daño y brillo superior.
            var bar = new Rect(area.x + shake, area.center.y - 9f * s, area.width - e - 12f * s, 18f * s);
            Fill(new Rect(bar.x - 4f * s, bar.y - 4f * s, bar.width + 8f * s, bar.height + 8f * s), Plate, 6f * s);
            Fill(new Rect(bar.x - 4f * s, bar.yMax + 2f * s, (bar.width + 8f * s) * .35f, 2f * s), Alpha(tint, .7f));
            const int cells = 10;
            float cw = bar.width / cells, pad = 2f * s;
            for (int i = 0; i < cells; i++)
            {
                // Se vacia hacia el emblema: la celda mas cercana a la nave es la ultima en caer.
                float from = 1f - (i + 1) / (float)cells, to = 1f - i / (float)cells;
                float full = Mathf.Clamp01((ratio - from) / (to - from));
                float trail = Mathf.Clamp01((hullTrail - from) / (to - from));
                var cell = new Rect(bar.x + i * cw + pad, bar.y, cw - pad * 2f, bar.height);
                Fill(cell, new Color(1, 1, 1, .07f), 2f * s);
                if (trail > full) Fill(new Rect(cell.xMax - cell.width * trail, cell.y, cell.width * trail, cell.height), Alpha(Color.white, .45f), 2f * s);
                if (full > 0f)
                {
                    var f = new Rect(cell.xMax - cell.width * full, cell.y, cell.width * full, cell.height);
                    Fill(f, Color.Lerp(tint, Color.white, hit * .6f + beat * .25f), 2f * s);
                    Fill(new Rect(f.x, f.y, f.width, f.height * .35f), new Color(1, 1, 1, .22f), 2f * s);
                }
            }

            // La atacan y no la ves: flecha hacia la nave.
            if (Time.unscaledTime - shipHitAt < 2.5f)
            {
                Vector3 world = observedShip.Center + Vector3.up * 3f;
                Vector3 sp = cam.WorldToScreenPoint(world);
                if (sp.z <= 0 || sp.x <= 0 || sp.x >= Screen.width || sp.y <= 0 || sp.y >= Screen.height)
                    EdgeArrow(cam, world, Gold, 30f * s, t);
            }
        }

        // ------------------------------------------------------------------ radar con el ojo del monstruo

        private void DrawRadar(Camera cam, Rect r, float s, float t)
        {
            var wave = observedWave;
            var phase = wave.Current;
            float now = wave.Now;
            float k = Mathf.Clamp01(Mathf.InverseLerp(wave.PhaseStart, wave.PhaseEnd, now));
            float age = Mathf.Max(0f, now - wave.PhaseStart);
            bool calm = phase == WaveDirector.Phase.Calm, warning = phase == WaveDirector.Phase.Warning;
            bool active = phase == WaveDirector.Phase.Active, cleared = phase == WaveDirector.Phase.Cleared;

            float pop = 1f;
            if (warning) pop = 1.1f + .06f * Mathf.Abs(Mathf.Sin(t * 10f));
            if (cleared) pop = age < .3f ? Mathf.Lerp(1f, 1.3f, age / .3f) : Mathf.Lerp(1.3f, 1.04f, Mathf.Clamp01((age - .3f) * 3f));
            r = Grow(r, pop);
            Vector2 c = r.center;

            Color tint = calm ? Color.Lerp(Calm, Danger, k * k) : cleared ? Good : warning ? Gold : Danger;
            float flash = warning ? Mathf.Abs(Mathf.Sin(t * Mathf.Lerp(6f, 14f, k))) : 0f;

            if (warning) { GUI.color = Alpha(Danger, .22f * flash); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edge); }
            if (cleared) { GUI.color = Alpha(Good, .28f * Mathf.Clamp01(1f - age)); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edge); }

            // Halo, plato, rejilla.
            GUI.color = Alpha(tint, .25f + .35f * flash);
            GUI.DrawTexture(Grow(r, 1.6f), soft);
            GUI.color = Plate; GUI.DrawTexture(r, disc);
            GUI.color = Alpha(tint, .16f);
            GUI.DrawTexture(Grow(r, .66f), thinRing);
            GUI.DrawTexture(Grow(r, .33f), thinRing);
            Fill(new Rect(c.x - .5f, r.y + r.height * .1f, 1f, r.height * .8f), Alpha(tint, .14f));
            Fill(new Rect(r.x + r.width * .1f, c.y - .5f, r.width * .8f, 1f), Alpha(tint, .14f));

            if (!cleared)
            {
                GUIUtility.RotateAroundPivot(t * (calm ? 80f : 190f), c);
                GUI.color = Alpha(tint, .32f);
                GUI.DrawTexture(Grow(r, .94f), sweep);
                GUI.matrix = Matrix4x4.identity;
            }
            GUI.color = Alpha(tint, .85f);
            GUI.DrawTexture(r, ring);

            // Anillo de carga grueso por fuera: se llena en la calma, se vacia durante la oleada.
            float progress = active ? 1f - k : calm ? k : 1f;
            Arc(r, progress, tint, s);

            float yaw = cam.transform.eulerAngles.y;
            if (calm)
            {
                // Cinco presencias que se acercan desde el borde mientras el ojo despierta.
                for (int i = 0; i < 5; i++)
                {
                    float a = (i * 72f + wave.Wave * 37f) * Mathf.Deg2Rad;
                    float rad = Mathf.Lerp(.95f, .52f, k) * r.width * .42f;
                    Blip(c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * rad, Danger, (4f + 3f * k) * s, .3f + .7f * k, t + i);
                }
                Eye(r, Mathf.SmoothStep(0f, .35f, k), 0f, t);
            }
            else if (warning)
            {
                for (int i = 0; i < wave.FrontCount; i++)
                {
                    Vector3 front = wave.FrontPoint(i);
                    Blip(c + RadarDir(front - me, yaw) * r.width * .42f, Danger, (10f + 4f * flash) * s, 1f, t);
                    EdgeArrow(cam, front, Danger, 40f * s * (1f + .2f * flash), t);
                }
                Eye(r, Mathf.SmoothStep(.35f, 1f, Mathf.Clamp01(age * 2.5f)), flash, t);
            }
            else if (active)
            {
                foreach (var en in enemies)
                {
                    Vector3 off = en - me; off.y = 0;
                    float dist = Mathf.Min(1f, off.magnitude / 60f);
                    Blip(c + RadarDir(off, yaw) * dist * r.width * .42f, Danger, 5f * s, dist >= 1f ? .45f : 1f, t + off.x);
                }
                Eye(r, 1f, .25f * Mathf.Abs(Mathf.Sin(t * 3f)), t);
                Pips(wave, r, s);
            }
            else Victory(r, age, s, wave.Wave);
        }

        private void Eye(Rect r, float open, float rage, float t)
        {
            var e = Grow(r, .52f);
            if (open < .05f)
            {
                GUI.color = Alpha(Danger, .8f);
                GUI.DrawTexture(e, eyeLid);
                return;
            }
            float wobble = 1f + .04f * Mathf.Sin(t * 3f);
            GUI.color = Alpha(Danger, .45f * open + .4f * rage);
            GUI.DrawTexture(Grow(e, 1.5f), soft);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(e.x, e.center.y - e.height * open * .5f * wobble, e.width, e.height * open * wobble), eye);
        }

        private void Pips(WaveDirector wave, Rect r, float s)
        {
            int total = Mathf.Max(1, wave.Total), left = Mathf.Clamp(wave.Remaining, 0, total);
            float pip = 10f * s, gap = 4f * s;
            int perRow = Mathf.Min(total, 14);
            float rowW = perRow * pip + (perRow - 1) * gap;
            for (int i = 0; i < total; i++)
            {
                int row = i / 14, col = i % 14;
                var p = new Rect(r.center.x - rowW / 2 + col * (pip + gap), r.yMax + 14f * s + row * (pip + gap), pip, pip);
                bool alive = i < left;
                if (alive) { GUI.color = Alpha(Danger, .35f); GUI.DrawTexture(Grow(p, 2f), soft); }
                GUI.color = alive ? Danger : new Color(1, 1, 1, .16f);
                GUI.DrawTexture(p, dot);
            }
        }

        private void Victory(Rect r, float age, float s, int waveNumber)
        {
            for (int i = 0; i < 3; i++)
            {
                float a = age - i * .18f;
                if (a < 0 || a > 1.2f) continue;
                Color col = i == 1 ? Gold : Good;
                GUI.color = Alpha(col, (1f - a / 1.2f) * .9f);
                GUI.DrawTexture(Grow(r, Mathf.Lerp(1f, 4.5f + i, Mathf.Sqrt(a / 1.2f))), thinRing);
            }
            GUI.color = Alpha(Gold, .32f * Mathf.Clamp01(1.5f - age * .4f));
            for (int i = 0; i < 8; i++)
            {
                GUIUtility.RotateAroundPivot(age * 25f + i * 45f, r.center);
                GUI.DrawTexture(new Rect(r.center.x - 3f * s, r.center.y - r.height * 1.3f, 6f * s, r.height * 1.3f), shard);
                GUI.matrix = Matrix4x4.identity;
            }
            Eye(r, Mathf.Clamp01(1f - age * 4f), 0f, Time.time);
            float draw = Mathf.Clamp01((age - .2f) / .35f);
            if (draw > 0)
            {
                var cr = Grow(r, .62f);
                GUI.color = Alpha(Good, .6f);
                GUI.DrawTexture(Grow(cr, 1.5f), soft);
                GUI.color = Color.white;
                GUI.BeginGroup(new Rect(cr.x, cr.y, cr.width * draw, cr.height));
                GUI.DrawTexture(new Rect(0, 0, cr.width, cr.height), check);
                GUI.EndGroup();
            }
            int stars = Mathf.Clamp(waveNumber, 1, 5);
            float sz = 16f * s, gap = 6f * s, w = stars * sz + (stars - 1) * gap;
            for (int i = 0; i < stars; i++)
            {
                float a = Mathf.Clamp01((age - .45f - i * .1f) * 5f);
                if (a <= 0) continue;
                var p = Grow(new Rect(r.center.x - w / 2 + i * (sz + gap), r.yMax + 16f * s, sz, sz), a < 1 ? Mathf.Lerp(2f, 1f, a) : 1f);
                GUI.color = Alpha(Gold, a);
                GUIUtility.RotateAroundPivot(45f, p.center);
                GUI.DrawTexture(Grow(p, .7f), shard);
                GUI.matrix = Matrix4x4.identity;
            }
        }

        // Barra de carga circular gruesa: pista oscura, relleno brillante y cabeza luminosa.
        private void Arc(Rect r, float k, Color col, float s)
        {
            const int segs = 120;
            k = Mathf.Clamp01(k);
            float rad = r.width * .5f + 8f * s, d = 9f * s;
            GUI.color = new Color(0f, 0f, 0f, .55f);
            for (int i = 0; i < segs; i += 2)
            {
                float a = -Mathf.PI / 2 + i / (float)segs * Mathf.PI * 2;
                GUI.DrawTexture(new Rect(r.center.x + Mathf.Cos(a) * rad - d * .7f, r.center.y + Mathf.Sin(a) * rad - d * .7f, d * 1.4f, d * 1.4f), dot);
            }
            int n = Mathf.RoundToInt(segs * k);
            for (int i = 0; i < n; i++)
            {
                float a = -Mathf.PI / 2 + i / (float)segs * Mathf.PI * 2;
                GUI.color = Alpha(col, Mathf.Lerp(.5f, 1f, i / (float)Mathf.Max(1, n)));
                GUI.DrawTexture(new Rect(r.center.x + Mathf.Cos(a) * rad - d / 2, r.center.y + Mathf.Sin(a) * rad - d / 2, d, d), dot);
            }
            if (n > 0)
            {
                float a = -Mathf.PI / 2 + k * Mathf.PI * 2;
                var head = new Vector2(r.center.x + Mathf.Cos(a) * rad, r.center.y + Mathf.Sin(a) * rad);
                float pulse = 1f + .25f * Mathf.Sin(Time.time * 8f);
                GUI.color = Alpha(col, .8f);
                GUI.DrawTexture(new Rect(head.x - d * 2f * pulse, head.y - d * 2f * pulse, d * 4f * pulse, d * 4f * pulse), soft);
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(head.x - d * .6f, head.y - d * .6f, d * 1.2f, d * 1.2f), dot);
            }
        }

        // ------------------------------------------------------------------ derrota y confeti

        private void ShipLost(float s, float t, float age)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.25f, 0f, 0f, Mathf.Clamp01(age * 1.5f) * .55f));
            GUI.color = Alpha(Danger, .5f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), edge);
            float size = 170f * s * (1f + Mathf.Clamp01(age * 3f) * .3f);
            var r = new Rect(Screen.width / 2f - size / 2, Screen.height * .38f - size / 2, size, size);
            r.x += Mathf.Sin(t * 70f) * Mathf.Clamp01(1f - age) * 8f * s;
            GUI.color = Alpha(Danger, .6f);
            GUI.DrawTexture(Grow(r, 1.8f), soft);
            GUI.color = new Color(.85f, .9f, 1f, 1f);
            GUI.DrawTexture(r, shield);
            GUI.color = new Color(.1f, 0f, 0f, Mathf.Clamp01(age * 2f));
            GUI.DrawTexture(r, crack);
            for (int i = 0; i < 14; i++)
            {
                float a = i * 25.7f * Mathf.Deg2Rad;
                float d = Mathf.Clamp01(age - .3f) * 260f * s;
                if (d <= 0) break;
                var p = new Vector2(r.center.x + Mathf.Cos(a) * d, r.center.y + Mathf.Sin(a) * d + d * d * .002f);
                GUI.color = new Color(1f, .5f, .3f, Mathf.Clamp01(1.6f - age));
                GUIUtility.RotateAroundPivot(age * 400f + i * 30f, p);
                GUI.DrawTexture(new Rect(p.x - 6 * s, p.y - 3 * s, 12 * s, 6 * s), shard);
                GUI.matrix = Matrix4x4.identity;
            }
        }

        private void BurstConfetti()
        {
            confettiFired = true;
            float s = InteractionPromptCard.Scale;
            Vector2 origin = new Vector2(Screen.width / 2f, 70f * s);
            Color[] palette = { Good, Gold, Calm, new Color(1f, .5f, .8f), Color.white };
            for (int i = 0; i < 90; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(250f, 900f) * s;
                confetti.Add(new Confetti
                {
                    p = origin, v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * .6f - .4f) * speed,
                    rot = Random.Range(0, 360f), spin = Random.Range(-720f, 720f), size = Random.Range(6f, 13f) * s,
                    born = Time.time, life = Random.Range(1.6f, 2.6f), c = palette[Random.Range(0, palette.Length)],
                });
            }
        }

        private void UpdateConfetti()
        {
            float dt = Time.deltaTime, g = 900f * InteractionPromptCard.Scale;
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var f = confetti[i];
                if (Time.time - f.born > f.life) { confetti.RemoveAt(i); continue; }
                f.v.y += g * dt; f.v *= 1f - 1.6f * dt; f.p += f.v * dt; f.rot += f.spin * dt;
                confetti[i] = f;
            }
        }

        private void DrawConfetti()
        {
            foreach (var f in confetti)
            {
                float k = (Time.time - f.born) / f.life;
                var r = new Rect(f.p.x - f.size / 2, f.p.y - f.size * .3f, f.size, f.size * .6f * Mathf.Abs(Mathf.Cos(f.rot * .05f)) + 1f);
                GUI.color = Alpha(f.c, 1f - Mathf.Clamp01((k - .7f) / .3f));
                GUIUtility.RotateAroundPivot(f.rot, r.center);
                GUI.DrawTexture(r, shard);
                GUI.matrix = Matrix4x4.identity;
            }
        }

        // Ojo de monstruo: almendra, iris ambar con vetas, pupila de reptil y brillo.
        private static Color EyePixel(float u, float v)
        {
            float lid = (1f - u * u) * .62f;
            float shape = Edge(lid - Mathf.Abs(v), 40f) * Edge(.98f - Mathf.Abs(u), 40f);
            if (shape <= 0) return Color.clear;
            float d = Mathf.Sqrt(u * u + v * v);
            Color irisCol = Color.Lerp(new Color(1f, .85f, .2f), new Color(.9f, .2f, .05f), Mathf.Clamp01(d / .52f));
            irisCol *= .85f + .15f * Mathf.Sin(Mathf.Atan2(v, u) * 18f);
            float pupil = Edge(.09f * (1f - v * v * 2.2f) - Mathf.Abs(u), 50f) * Edge(.48f - Mathf.Abs(v), 50f);
            Color c = Color.Lerp(new Color(1f, .9f, .75f), irisCol, Edge(.52f - d, 30f));
            c = Color.Lerp(c, new Color(.05f, 0, 0), pupil);
            c = Color.Lerp(c, Color.white, Edge(.08f - Vector2.Distance(new Vector2(u, v), new Vector2(.16f, .18f)), 40f));
            c = Color.Lerp(c, new Color(.15f, .02f, .02f), Edge(Mathf.Abs(v) - (lid - .07f), 40f));
            c.a = shape;
            return c;
        }

        // Escudo (blason) con una pequeña nave dentro.
        private static float ShieldAlpha(float u, float v)
        {
            float width = v > 0 ? .85f : .85f * Mathf.Sqrt(Mathf.Clamp01(1f + v / 1.05f));
            float outer = Edge(width - Mathf.Abs(u), 30f) * Edge(.92f - v, 30f) * Edge(v + 1f, 30f);
            float inner = Edge(width - .14f - Mathf.Abs(u), 30f) * Edge(.78f - v, 30f) * Edge(v + .86f, 30f);
            float body = Edge(.22f - Mathf.Sqrt(u * u * 2.2f + (v + .02f) * (v + .02f) * .55f), 30f);
            float fins = Edge(.42f - Mathf.Abs(u) - (v + .45f) * 1.6f, 30f) * Edge(v + .55f, 30f) * Edge(-.1f - v, 30f);
            return Mathf.Clamp01(outer - inner + Mathf.Max(body, fins) * inner);
        }

        private static float CrackAlpha(float u, float v)
        {
            float a = SegDist(new Vector2(u, v), new Vector2(-.1f, .9f), new Vector2(.08f, .3f));
            float b = SegDist(new Vector2(u, v), new Vector2(.08f, .3f), new Vector2(-.15f, -.2f));
            float c = SegDist(new Vector2(u, v), new Vector2(-.15f, -.2f), new Vector2(.1f, -.9f));
            float d = SegDist(new Vector2(u, v), new Vector2(.08f, .3f), new Vector2(.55f, .45f));
            return Edge(.045f - Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d)), 60f);
        }

        private void IconWell(Rect rect, Texture2D icon, Color tint, float alpha = 1f)
        {
            GUI.color = Alpha(tint, .12f * alpha);
            GUI.DrawTexture(rect, disc);
            GUI.color = Alpha(tint, .65f * alpha);
            GUI.DrawTexture(rect, thinRing);
            if (icon == null) return;
            GUI.color = Alpha(tint, alpha);
            GUI.DrawTexture(Grow(rect, .74f), icon);
        }

        private static void Frame(Rect rect, Color accent, float alpha)
        {
            Fill(new Rect(rect.x, rect.y + 4f, rect.width, rect.height), new Color(0, 0, 0, .28f * alpha), 5f);
            Fill(rect, new Color32(103, 137, 158, (byte)(130f * alpha)), 5f);
            Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f),
                new Color32(10, 20, 30, (byte)(249f * alpha)), 4f);
            Fill(new Rect(rect.x + 6f, rect.y + 1f, 34f, 2f), Alpha(accent, alpha));
        }

        private static void Meter(Rect rect, float ratio, float trail, Color tint)
        {
            Fill(rect, new Color32(41, 57, 69, 255), 2f);
            if (trail > ratio) Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(trail), rect.height), Alpha(tint, .36f), 2f);
            if (ratio > 0f) Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(ratio), rect.height), tint, 2f);
        }

        private void Label(Rect rect, string value, Color color, float size, bool headline = false)
        {
            if (body == null)
            {
                body = UiFont.Label();
                title = UiFont.Title();
                body.padding = title.padding = new RectOffset();
                body.alignment = title.alignment = TextAnchor.MiddleLeft;
            }
            var style = headline ? title : body;
            style.fontSize = Mathf.Max(8, Mathf.RoundToInt(size));
            style.normal.textColor = color;
            GUI.color = Color.white;
            GUI.Label(rect, value, style);
        }

        private static void Fill(Rect rect, Color color, float radius = 0f)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, Vector4.zero, radius);
        }

        private static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static string L(string es, string en, string pt) => GameLocalization.CurrentLanguage switch
        {
            GameLanguage.English => en,
            GameLanguage.Portuguese => pt,
            _ => es
        };

        // ------------------------------------------------------------------ marcadores en el mundo

        private void CorpseBadge(Camera cam, Vector3 body, float s, float t)
        {
            Vector3 world = body + Vector3.up * (1.7f + .12f * Mathf.Sin(t * 3.5f));
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0 || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height) return;
            float dist = Vector3.Distance(cam.transform.position, body);
            float size = Mathf.Lerp(52f, 30f, Mathf.InverseLerp(5f, 45f, dist)) * s;
            var r = new Rect(sp.x - size / 2, Screen.height - sp.y - size / 2, size, size);
            float pulse = .5f + .5f * Mathf.Sin(t * 4f);

            GUI.color = new Color(Grind.r, Grind.g, Grind.b, .25f + .2f * pulse);
            GUI.DrawTexture(Grow(r, 1.9f + .2f * pulse), soft);
            GUI.color = Plate; GUI.DrawTexture(r, disc);
            GUI.color = Grind;
            GUIUtility.RotateAroundPivot(t * 60f, r.center);
            GUI.DrawTexture(r, dashed);
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(-t * 90f, r.center);
            GUI.DrawTexture(Grow(r, .6f), gear);
            GUI.matrix = Matrix4x4.identity;

            // Flechita orbitando del lado de la trituradora: "llevalo hacia alla".
            if (hasGrinder)
            {
                Vector3 gs = cam.WorldToScreenPoint(grinderPoint);
                Vector2 dir = new Vector2(gs.x - sp.x, -(gs.y - sp.y));
                if (gs.z < 0) dir = -dir;
                if (dir.sqrMagnitude > 1f)
                {
                    dir.Normalize();
                    float push = size * (.72f + .1f * pulse);
                    var ar = new Rect(r.center.x + dir.x * push - size * .2f, r.center.y + dir.y * push - size * .2f, size * .4f, size * .4f);
                    GUI.color = Grind;
                    GUIUtility.RotateAroundPivot(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f, ar.center);
                    GUI.DrawTexture(ar, arrow);
                    GUI.matrix = Matrix4x4.identity;
                }
            }
            // Punta hacia abajo, señalando el cuerpo.
            var down = new Rect(r.center.x - size * .16f, r.yMax + size * (.04f + .08f * pulse), size * .32f, size * .32f);
            GUI.color = Grind;
            GUIUtility.RotateAroundPivot(180f, down.center);
            GUI.DrawTexture(down, arrow);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        private void GrinderTarget(Camera cam, float s, float t)
        {
            Vector3 sp = cam.WorldToScreenPoint(grinderPoint + Vector3.up * .2f * Mathf.Sin(t * 5f));
            bool on = sp.z > 0 && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;
            if (!on) { EdgeArrow(cam, grinderPoint, Grind, 38f * s, t); return; }
            float size = (58f + 8f * Mathf.Sin(t * 8f)) * s;
            var r = new Rect(sp.x - size / 2, Screen.height - sp.y - size / 2, size, size);
            // Anillos que se cierran sobre la trituradora como una diana.
            for (int i = 0; i < 2; i++)
            {
                float a = ((t * .9f) + i * .5f) % 1f;
                GUI.color = new Color(Grind.r, Grind.g, Grind.b, a);
                GUI.DrawTexture(Grow(r, Mathf.Lerp(2.6f, 1f, a)), thinRing);
            }
            GUI.color = new Color(Grind.r, Grind.g, Grind.b, .45f); GUI.DrawTexture(Grow(r, 1.8f), soft);
            GUI.color = Plate; GUI.DrawTexture(r, disc);
            GUI.color = Grind; GUI.DrawTexture(r, ring);
            GUIUtility.RotateAroundPivot(t * 200f, r.center);
            GUI.DrawTexture(Grow(r, .62f), gear);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        private void WorldSprite(Camera cam, Vector3 world, Sprite sprite, Color glow, float size, float t)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return;
            var r = new Rect(sp.x - size / 2, Screen.height - sp.y - size / 2, size, size);
            GUI.color = new Color(glow.r, glow.g, glow.b, .45f + .2f * Mathf.Sin(t * 6f));
            GUI.DrawTexture(Grow(r, 1.9f), soft);
            GUI.color = Color.white;
            GUI.DrawTexture(r, sprite.texture);
        }

        private void EdgeArrow(Camera cam, Vector3 world, Color tint, float size, float t)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 p = new Vector2(sp.x, sp.y);
            if (sp.z < 0) p = c - (p - c);
            Vector2 dir = p - c;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            float margin = size * 1.1f;
            float kx = (Screen.width / 2f - margin) / Mathf.Max(.001f, Mathf.Abs(dir.x));
            float ky = (Screen.height / 2f - margin) / Mathf.Max(.001f, Mathf.Abs(dir.y));
            Vector2 e = c + dir * Mathf.Min(kx, ky) * (1f - .03f * Mathf.Abs(Mathf.Sin(t * 6f)));
            var r = new Rect(e.x - size / 2, Screen.height - e.y - size / 2, size, size);
            float angle = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            GUI.color = new Color(tint.r, tint.g, tint.b, .35f);
            GUI.DrawTexture(Grow(r, 1.7f), soft);
            GUI.color = tint;
            GUIUtility.RotateAroundPivot(angle, r.center);
            GUI.DrawTexture(r, arrow);
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        private void Blip(Vector2 p, Color col, float size, float alpha, float t)
        {
            float ping = (t * 1.5f) % 1f;
            GUI.color = new Color(col.r, col.g, col.b, alpha * (1f - ping) * .6f);
            GUI.DrawTexture(new Rect(p.x - size * (1 + ping * 1.5f), p.y - size * (1 + ping * 1.5f), size * 2 * (1 + ping * 1.5f), size * 2 * (1 + ping * 1.5f)), thinRing);
            GUI.color = new Color(col.r, col.g, col.b, alpha * .5f);
            GUI.DrawTexture(new Rect(p.x - size * 1.4f, p.y - size * 1.4f, size * 2.8f, size * 2.8f), soft);
            GUI.color = new Color(col.r, col.g, col.b, alpha);
            GUI.DrawTexture(new Rect(p.x - size / 2, p.y - size / 2, size, size), dot);
        }

        // Direccion en el radar: arriba = hacia donde mira la camara.
        private static Vector2 RadarDir(Vector3 offset, float camYaw)
        {
            offset.y = 0;
            if (offset.sqrMagnitude < .001f) return Vector2.zero;
            Vector3 local = Quaternion.Euler(0, -camYaw, 0) * offset.normalized;
            return new Vector2(local.x, -local.z);
        }

        private static Rect Grow(Rect r, float k) { var c = r.center; r.size *= k; r.center = c; return r; }
        private static float Edge(float x, float sharp) => Mathf.Clamp01(x * sharp);
        private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - a - ab * h).magnitude;
        }

        private static Texture2D Build(int size, System.Func<float, float, float> alpha) =>
            BuildColor(size, (u, v) => new Color(1, 1, 1, Mathf.Clamp01(alpha(u, v))));

        private static Texture2D BuildColor(int size, System.Func<float, float, Color> color)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size * 2f - 1f, v = (y + .5f) / size * 2f - 1f;
                    px[y * size + x] = color(u, v);
                }
            tex.SetPixels(px); tex.Apply();
            return tex;
        }

    }
}
