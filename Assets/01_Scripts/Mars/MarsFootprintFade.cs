using ModularFootstepSystem.Extensions;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameJam.Mars
{
    public sealed class MarsFootprintFade : MonoBehaviour
    {
        private DecalProjector projector;
        private PooledObject pooled;
        private MeshRenderer mark;
        private MaterialPropertyBlock properties;
        private float born, lifetime;
        public void Restart(float duration)
        {
            if (projector == null) projector = GetComponent<DecalProjector>();
            if (pooled == null) pooled = GetComponent<PooledObject>();
            if (mark == null) mark = GetComponentInChildren<MeshRenderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            born = Time.time; lifetime = Mathf.Max(1, duration);
            projector.fadeFactor = 1;
            SetOpacity(1);
        }
        private void Update()
        {
            if (projector == null) return;
            float age = (Time.time - born) / lifetime;
            float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1, age));
            projector.fadeFactor = opacity;
            SetOpacity(opacity);
            if (age >= 1 && pooled != null && pooled.Pool != null) pooled.Release();
        }
        private void SetOpacity(float opacity)
        {
            if (mark == null) return;
            properties.SetFloat("_Opacity", opacity);
            mark.SetPropertyBlock(properties);
        }
    }
}
