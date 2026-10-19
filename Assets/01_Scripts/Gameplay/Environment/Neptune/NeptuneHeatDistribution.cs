using UnityEngine;

namespace GameJam.Environment.Neptune
{
    /// <summary>Creates identical exploration rest stops on every client.</summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(TerrainCollider))]
    public sealed class NeptuneHeatDistribution : MonoBehaviour
    {
        [SerializeField] private HeatSource template;
        [SerializeField, Min(2)] private int pointsPerAxis = 5;
        [SerializeField, Min(15f)] private float edgeMargin = 40f;

        private void Awake()
        {
            if (template == null) return;
            Bounds bounds = GetComponent<TerrainCollider>().bounds;
            float margin = Mathf.Min(edgeMargin, Mathf.Min(bounds.size.x, bounds.size.z) * 0.2f);
            int count = Mathf.Max(2, pointsPerAxis);
            for (int z = 0; z < count; z++)
            for (int x = 0; x < count; x++)
            {
                Vector3 position = new Vector3(
                    Mathf.Lerp(bounds.min.x + margin, bounds.max.x - margin, x / (float)(count - 1)),
                    bounds.max.y,
                    Mathf.Lerp(bounds.min.z + margin, bounds.max.z - margin, z / (float)(count - 1)));
                Vector3 offset = position - template.transform.position;
                offset.y = 0;
                if (offset.sqrMagnitude < 30f * 30f) continue;
                // Start() on each copy snaps it to the terrain and creates its effects.
                HeatSource source = Instantiate(template, position, Quaternion.identity);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source.gameObject, gameObject.scene);
                source.name = $"Neptune Exploration Heat {x + 1}-{z + 1}";
            }
        }
    }
}
