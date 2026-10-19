using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJam.Environment.Neptune
{
    /// <summary>
    /// Proporciona calor estatico en tres zonas concentricas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeatSource : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float innerRadius = 1f;
        [SerializeField, Min(0.02f)] private float middleRadius = 2f;
        [SerializeField, Min(0.03f)] private float outerRadius = 3f;
        [SerializeField, Min(0f)] private float innerHeatRate = 20f;
        [SerializeField, Min(0f)] private float middleHeatRate = 10f;
        [SerializeField, Min(0f)] private float outerHeatRate = 5f;

        [SerializeField] private bool placeOnTerrain;
        [SerializeField] private bool showWarmLight;

        [SerializeField] private Material heatParticleMaterial;

        private void Start()
        {
            if (placeOnTerrain)
            {
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    if (terrain.gameObject.scene != gameObject.scene) continue;
                    var ground = terrain.GetComponent<TerrainCollider>();
                    if (ground == null) continue;
                    var ray = new Ray(new Vector3(transform.position.x, ground.bounds.max.y + 10f,
                        transform.position.z), Vector3.down);
                    if (!ground.Raycast(ray, out RaycastHit hit, ground.bounds.size.y + 30f)) continue;
                    transform.position = hit.point + Vector3.up * 0.02f;
                    break;
                }
            }
            if (!showWarmLight) return;
            // A low heat vent resting on the ground, instead of a floating cube.
            Transform plate = transform.Find("Visual/Cube");
            if (plate != null)
            {
                plate.localPosition = Vector3.up * 0.06f;
                plate.localScale = new Vector3(1.8f, 0.12f, 1.8f);
            }
            var box = GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = Vector3.up * 0.06f;
                box.size = new Vector3(1.8f, 0.12f, 1.8f);
            }

            var glow = new GameObject("Warm light");
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = Vector3.up;
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.35f, 0.06f);
            light.range = outerRadius * 2f;
            light.intensity = 5f;
            light.shadows = LightShadows.None;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", light.color);
            tint.SetColor("_Color", light.color);
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(tint);
            CreateHeatParticles();
        }

        private void CreateHeatParticles()
        {
            if (heatParticleMaterial == null) return;
            var effect = new GameObject("Rising heat embers");
            effect.transform.SetParent(transform, false);
            effect.transform.localPosition = Vector3.up * 0.18f;
            effect.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 100;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.25f, 0.02f),
                new Color(1f, 0.8f, 0.2f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.rateOverTime = 30f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.7f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = heatParticleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        private static readonly HashSet<HeatSource> activeSources = new HashSet<HeatSource>();

        /// <summary>
        /// Registra la fuente mientras permanece habilitada.
        /// </summary>
        private void OnEnable()
        {
            ValidateSettings();
            activeSources.Add(this);
        }

        /// <summary>
        /// Retira la fuente cuando deja de estar disponible.
        /// </summary>
        private void OnDisable()
        {
            activeSources.Remove(this);
        }

        /// <summary>
        /// Consulta los puntos porcentuales de calor por segundo en una posicion.
        /// </summary>
        public float GetHeatRate(Vector3 position)
        {
            if (!isActiveAndEnabled)
            {
                return 0f;
            }

            float squaredDistance = (position - transform.position).sqrMagnitude;
            if (squaredDistance <= innerRadius * innerRadius)
            {
                return innerHeatRate;
            }
            if (squaredDistance <= middleRadius * middleRadius)
            {
                return middleHeatRate;
            }
            if (squaredDistance <= outerRadius * outerRadius)
            {
                return outerHeatRate;
            }

            return 0f;
        }

        /// <summary>
        /// Obtiene el mayor calor de la escena sin sumar fuentes superpuestas.
        /// </summary>
        public static float GetStrongestHeatRate(Vector3 position, Scene scene)
        {
            float strongestRate = 0f;
            foreach (HeatSource source in activeSources)
            {
                if (source == null)
                {
                    continue;
                }
                if (source.gameObject.scene != scene)
                {
                    continue;
                }

                strongestRate = Mathf.Max(strongestRate, source.GetHeatRate(position));
            }

            return strongestRate;
        }

        /// <summary>
        /// Mantiene radios ordenados y tasas positivas al editar el Inspector.
        /// </summary>
        private void OnValidate()
        {
            ValidateSettings();
        }

        /// <summary>
        /// Corrige valores invalidos sin cambiar las referencias de la fuente.
        /// </summary>
        private void ValidateSettings()
        {
            innerRadius = Mathf.Max(0.01f, innerRadius);
            middleRadius = Mathf.Max(innerRadius + 0.01f, middleRadius);
            outerRadius = Mathf.Max(middleRadius + 0.01f, outerRadius);
            innerHeatRate = Mathf.Max(0f, innerHeatRate);
            middleHeatRate = Mathf.Max(0f, middleHeatRate);
            outerHeatRate = Mathf.Max(0f, outerHeatRate);
        }

        /// <summary>
        /// Muestra los tres radios en unidades del mundo al seleccionar la fuente.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, innerRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, middleRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, outerRadius);
            Gizmos.color = previousColor;
        }
    }
}
