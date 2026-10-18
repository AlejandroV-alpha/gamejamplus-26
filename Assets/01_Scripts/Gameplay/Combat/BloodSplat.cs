using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Mancha de sangre en el suelo: se expande al caer, permanece y luego se desvanece.
    /// </summary>
    public sealed class BloodSplat : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float growTime = 0.18f;
        [SerializeField, Min(0f)] private float stayTime = 25f;
        [SerializeField, Min(0.01f)] private float fadeTime = 4f;

        private Renderer splatRenderer;
        private MaterialPropertyBlock block;
        private Vector3 fullScale;
        private Color baseColor;
        private float age;

        /// <summary>
        /// Guarda la escala final y el color del material compartido.
        /// </summary>
        private void Start()
        {
            splatRenderer = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            fullScale = transform.localScale;
            baseColor = splatRenderer.sharedMaterial.GetColor("_BaseColor");
            transform.localScale = new Vector3(fullScale.x * 0.2f, fullScale.y, fullScale.z * 0.2f);
        }

        /// <summary>
        /// Anima la aparicion y el desvanecimiento sin duplicar materiales.
        /// </summary>
        private void Update()
        {
            age += Time.deltaTime;
            float grow = Mathf.Clamp01(age / growTime);
            float eased = 1f - (1f - grow) * (1f - grow);
            transform.localScale = new Vector3(fullScale.x * Mathf.Lerp(0.2f, 1f, eased), fullScale.y, fullScale.z * Mathf.Lerp(0.2f, 1f, eased));

            float fade = Mathf.Clamp01((age - growTime - stayTime) / fadeTime);
            Color color = baseColor;
            color.a *= 1f - fade;
            splatRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            splatRenderer.SetPropertyBlock(block);

            if (fade >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
