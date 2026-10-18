using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Apaga suavemente la luz de un destello de disparo o impacto.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class VfxLightFade : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float duration = 0.12f;
        [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private Light fadeLight;
        private float startIntensity;
        private float elapsed;

        /// <summary>
        /// Guarda la intensidad inicial configurada en el prefab.
        /// </summary>
        private void Awake()
        {
            fadeLight = GetComponent<Light>();
            startIntensity = fadeLight.intensity;
        }

        /// <summary>
        /// Reinicia el destello cada vez que el efecto aparece.
        /// </summary>
        private void OnEnable()
        {
            elapsed = 0f;
            fadeLight.enabled = true;
            fadeLight.intensity = startIntensity;
        }

        /// <summary>
        /// Reduce la intensidad segun la curva y desactiva la luz al terminar.
        /// </summary>
        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            if (progress >= 1f)
            {
                fadeLight.enabled = false;
                return;
            }

            fadeLight.intensity = startIntensity * intensityCurve.Evaluate(progress);
        }
    }
}
