using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Mars
{
    // Local visual ambience: no networking or gameplay collisions required.
    [ExecuteAlways]
    public sealed class b_efectoPolvoMarte : MonoBehaviour
    {
        public Material material;
        public Terrain terrain;
        public Transform followTarget;
        [Range(0f, 2f)] public float intensity = 1f;
        [Min(5f)] public float radius = 28f;
        public Vector3 wind = new Vector3(2.2f, 0f, 0.7f);
        private ParticleSystem dust;
        private ParticleSystem gusts;
        private Transform container;
        private float dustBudget, gustBudget;
        private Camera viewCamera;

        private void Initialize()
        {
            if (material == null)
            {
                Debug.LogWarning("b_efecto: falta el material de polvo.", this);
                enabled = false;
                return;
            }
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (container != null) return;
            container = new GameObject("b_efecto_ambiente").transform;
            container.gameObject.hideFlags = Application.isPlaying
                ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            container.SetParent(transform, false);
            dust = Create("b_efecto_polvoSuelo", 180);
            gusts = Create("b_efecto_rafagas", 40);
        }

        private ParticleSystem Create(string effectName, int limit)
        {
            var obj = new GameObject(effectName);
            obj.hideFlags = Application.isPlaying
                ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            obj.transform.SetParent(container, false);
            var system = obj.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = limit;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(0.65f, 0.65f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.2f;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 0.25f;
            system.Play();
            return system;
        }

        private void Update() { if (Application.isPlaying) Tick(); }

        private void Tick()
        {
            if (dust == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying && UnityEditor.SceneView.lastActiveSceneView != null)
                viewCamera = UnityEditor.SceneView.lastActiveSceneView.camera;
#endif
            if (followTarget == null && (viewCamera == null || (Application.isPlaying && !viewCamera.isActiveAndEnabled)))
                viewCamera = Camera.main;
            var target = followTarget != null ? followTarget : viewCamera != null ? viewCamera.transform : null;
            if (target == null) return;
            float delta = Application.isPlaying ? Mathf.Min(Time.deltaTime, 0.1f) : 1f / 30f;
            dustBudget += delta * 24f * intensity;
            // Editor preview needs an advancing clock even outside Play mode.
            float effectTime = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                effectTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            // A short lull followed by a visible wave every eight seconds.
            float pulse = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(-0.35f, 0.65f, Mathf.Cos(effectTime * Mathf.PI / 4f)));
            gustBudget += delta * 8f * pulse * intensity;
            while (dustBudget >= 1f) { dustBudget--; Emit(dust, target.position, false); }
            while (gustBudget >= 1f) { gustBudget--; Emit(gusts, target.position, true); }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                dust.Simulate(delta, false, false);
                gusts.Simulate(delta, false, false);
            }
#endif
        }

        private void Emit(ParticleSystem system, Vector3 center, bool gust)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 position = center + new Vector3(offset.x, 0f, offset.y);
            if (terrain == null || terrain.terrainData == null) return;
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (local.x < 0f || local.z < 0f || local.x > size.x || local.z > size.z) return;
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + (gust ? 1.6f : 0.6f);
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = wind * (gust ? Random.Range(1.8f, 2.4f) : Random.Range(0.75f, 1.25f)),
                startLifetime = gust ? Random.Range(2.5f, 4f) : Random.Range(3f, 6f),
                startSize = gust ? Random.Range(5f, 8f) : Random.Range(1.3f, 3f),
                startColor = gust ? new Color(0.78f, 0.48f, 0.28f, 0.48f)
                    : new Color(0.68f, 0.39f, 0.23f, 0.40f),
                rotation = Random.Range(0f, 360f)
            };
            system.Emit(particle, 1);
        }

#if UNITY_EDITOR
        private double nextPreview;
        private void PreviewTick()
        {
            if (this == null) return;
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now < nextPreview) return;
            nextPreview = now + 1.0 / 30.0;
            Tick();
            UnityEditor.SceneView.RepaintAll();
        }
#endif

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PreviewTick;
#endif
            Cleanup();
        }

        private void OnEnable()
        {
            Initialize();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PreviewTick;
            UnityEditor.EditorApplication.update += PreviewTick;
#endif
            if (dust != null) dust.Play();
            if (gusts != null) gusts.Play();
        }

        private void OnDestroy() => Cleanup();

        private void Cleanup()
        {
            if (container == null) return;
            var previousContainer = container;
            container = null;
            dust = null;
            gusts = null;
            dustBudget = gustBudget = 0f;
            if (Application.isPlaying) Destroy(previousContainer.gameObject);
#if UNITY_EDITOR
            else
            {
                // Release selected preview objects before the Inspector rebuilds.
                var selection = new System.Collections.Generic.List<Object>();
                foreach (var selected in UnityEditor.Selection.objects)
                {
                    if (selected == null) continue;
                    Transform selectedTransform = selected is GameObject go ? go.transform
                        : selected is Component component ? component.transform : null;
                    if (selectedTransform != null &&
                        (selectedTransform == previousContainer || selectedTransform.IsChildOf(previousContainer)))
                        continue;
                    selection.Add(selected);
                }
                UnityEditor.Selection.objects = selection.ToArray();
                // Do not destroy Inspector targets inside OnDisable/OnEnable callbacks.
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (previousContainer != null) DestroyImmediate(previousContainer.gameObject);
                };
            }
#endif
        }
    }
}
