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

        private void Start()
        {
            if (placeOnTerrain)
            {
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    if (terrain.gameObject.scene != gameObject.scene) continue;
                    Vector3 local = terrain.transform.InverseTransformPoint(transform.position);
                    Vector3 size = terrain.terrainData.size;
                    if (local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z) continue;
                    local.y = terrain.terrainData.GetInterpolatedHeight(local.x / size.x, local.z / size.z);
                    transform.position = terrain.transform.TransformPoint(local) + Vector3.up * 0.5f;
                    break;
                }
            }
            if (!showWarmLight) return;
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
