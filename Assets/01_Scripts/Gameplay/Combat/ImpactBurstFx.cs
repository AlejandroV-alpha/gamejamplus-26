using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Impacto cartoon para empujones y acciones de troleo: destello, onda expansiva, chispas
    /// alargadas que salen disparadas, estrellitas que giran y una nube de polvo.
    /// Todo procedural (texturas y materiales generados), aditivo y del color que se pida.
    /// </summary>
    public static class ImpactBurstFx
    {
        private static Material additive, alpha;
        private static Texture2D softTex, ringTex, starTex, streakTex;

        public static void Spawn(Vector3 position, Color color, float strength = 1f)
        {
            Ensure();
            var root = new GameObject("ImpactBurst");
            root.transform.position = position;
            Color light = Color.Lerp(color, Color.white, .55f);

            // 1. Destello central: aparece grande y se encoge enseguida.
            var flash = Make(root, "Flash", additive, softTex);
            Main(flash, .16f, 0f, 2.2f * strength, light);
            Size(flash, AnimationCurve.EaseInOut(0, 1, 1, .2f));
            flash.Emit(1);

            // 2. Onda expansiva plana mirando a la camara.
            var ring = Make(root, "Shockwave", additive, ringTex);
            Main(ring, .32f, 0f, 3.2f * strength, color);
            Size(ring, new AnimationCurve(new Keyframe(0, .15f, 0, 4f), new Keyframe(1, 1f)));
            Fade(ring, 1f, .0f);
            ring.Emit(1);
            // Segunda onda, mas lenta y tenue.
            var ring2 = Make(root, "Shockwave2", additive, ringTex);
            Main(ring2, .45f, 0f, 2.2f * strength, light);
            Size(ring2, new AnimationCurve(new Keyframe(0, .1f, 0, 3f), new Keyframe(1, 1f)));
            Fade(ring2, .6f, 0f);
            ring2.Emit(1);

            // 3. Chispas alargadas en la direccion del movimiento.
            var sparks = Make(root, "Sparks", additive, streakTex);
            var m = Main(sparks, .32f, 0f, .16f * strength, light);
            m.startSpeed = new ParticleSystem.MinMaxCurve(7f * strength, 13f * strength);
            m.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .34f);
            m.gravityModifier = .6f;
            Shape(sparks, ParticleSystemShapeType.Sphere, .1f);
            var r = sparks.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = .055f; r.lengthScale = 1.5f;
            Size(sparks, AnimationCurve.Linear(0, 1, 1, 0));
            var drag = sparks.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 4f; drag.dampen = .2f;
            sparks.Emit(Mathf.RoundToInt(16 * strength));

            // 4. Estrellitas que giran, rebotan y caen.
            var stars = Make(root, "Stars", additive, starTex);
            m = Main(stars, .75f, 0f, .32f * strength, color);
            m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(.5f, .85f);
            m.gravityModifier = 1.4f;
            m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            m.startColor = new ParticleSystem.MinMaxGradient(color, light);
            Shape(stars, ParticleSystemShapeType.Hemisphere, .15f);
            var rot = stars.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-9f, 9f);
            Size(stars, new AnimationCurve(new Keyframe(0, 0), new Keyframe(.12f, 1.2f), new Keyframe(.3f, 1f), new Keyframe(1, 0)));
            stars.Emit(Mathf.RoundToInt(7 * strength));

            // 5. Polvo suave que se expande (no aditivo, para que se lea sobre fondos claros).
            var dust = Make(root, "Dust", alpha, softTex);
            m = Main(dust, .7f, 0f, .7f * strength, new Color(1f, .93f, .85f, .5f));
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .75f);
            Shape(dust, ParticleSystemShapeType.Sphere, .2f);
            Size(dust, AnimationCurve.Linear(0, .5f, 1, 1.8f));
            Fade(dust, .5f, 0f);
            var d2 = dust.limitVelocityOverLifetime; d2.enabled = true; d2.drag = 3f;
            dust.Emit(8);

            Object.Destroy(root, 1.2f);
        }

        // ------------------------------------------------------------------ helpers

        private static ParticleSystem Make(GameObject root, string name, Material mat, Texture tex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var instance = new Material(mat);
            instance.SetTexture("_BaseMap", tex);
            r.sharedMaterial = instance;
            r.sortingFudge = -10;
            go.AddComponent<DestroyMaterialOnDestroy>().material = instance;
            return ps;
        }

        private static ParticleSystem.MainModule Main(ParticleSystem ps, float life, float speed, float size, Color color)
        {
            var m = ps.main;
            m.startLifetime = life; m.startSpeed = speed; m.startSize = size; m.startColor = color;
            return m;
        }

        private static void Size(ParticleSystem ps, AnimationCurve curve)
        {
            var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void Fade(ParticleSystem ps, float from, float to)
        {
            var c = ps.colorOverLifetime; c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(from, 0), new GradientAlphaKey(from, .25f), new GradientAlphaKey(to, 1) });
            c.color = g;
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius)
        {
            var s = ps.shape; s.enabled = true; s.shapeType = type; s.radius = radius;
            if (type == ParticleSystemShapeType.Hemisphere) s.rotation = new Vector3(-90, 0, 0);
        }

        private static void Ensure()
        {
            if (additive != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            additive = new Material(shader);
            Transparent(additive, true);
            alpha = new Material(shader);
            Transparent(alpha, false);

            softTex = Tex((u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Pow(Mathf.Clamp01(1f - d), 2f); });
            ringTex = Tex((u, v) => { float d = Mathf.Sqrt(u * u + v * v); return Mathf.Clamp01(1f - Mathf.Abs(d - .82f) / .1f) * (d < 1 ? 1 : 0); });
            starTex = Tex((u, v) =>
            {
                // Estrella de 4 puntas con brillo central.
                float a = Mathf.Abs(u) * Mathf.Abs(v);
                float arms = Mathf.Clamp01(1f - (Mathf.Abs(u) + Mathf.Abs(v)) - a * 6f);
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 2.2f);
                return Mathf.Clamp01(arms * 1.6f + core);
            });
            streakTex = Tex((u, v) => Mathf.Clamp01(1f - Mathf.Abs(u) * 1.1f) * Mathf.Clamp01(1f - Mathf.Abs(v)) );
        }

        private static void Transparent(Material m, bool add)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", add ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (add) m.EnableKeyword("_BLENDMODE_ADD");
            m.renderQueue = 3100;
        }

        private static Texture2D Tex(System.Func<float, float, float> alphaAt)
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float u = (x + .5f) / s * 2f - 1f, v = (y + .5f) / s * 2f - 1f;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(u, v)) * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        private sealed class DestroyMaterialOnDestroy : MonoBehaviour
        {
            public Material material;
            private void OnDestroy() { if (material != null) Destroy(material); }
        }
    }
}
