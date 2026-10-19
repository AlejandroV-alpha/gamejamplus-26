using UnityEngine;

namespace GameJam.Environment.Neptune
{
    /// <summary>Closes the terrain perimeter before players spawn, on every client.</summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(Terrain), typeof(TerrainCollider))]
    public sealed class NeptuneMapBoundary : MonoBehaviour
    {
        [SerializeField] private Material iceMaterial;
        [SerializeField, Min(1f)] private float thickness = 6f;
        [SerializeField, Min(4f)] private float heightAboveGround = 16f;

        private void Awake()
        {
            Terrain terrain = GetComponent<Terrain>();
            Bounds bounds = GetComponent<TerrainCollider>().bounds;
            // Use physics bounds: terrain scale handling can differ from ordinary meshes.
            float highestEdge = bounds.min.y;
            for (int i = 0; i <= 128; i++)
            {
                float t = i / 128f;
                float x = Mathf.Lerp(bounds.min.x, bounds.max.x, t);
                float z = Mathf.Lerp(bounds.min.z, bounds.max.z, t);
                highestEdge = Mathf.Max(highestEdge,
                    terrain.SampleHeight(new Vector3(x, 0, bounds.min.z)) + terrain.transform.position.y,
                    terrain.SampleHeight(new Vector3(x, 0, bounds.max.z)) + terrain.transform.position.y,
                    terrain.SampleHeight(new Vector3(bounds.min.x, 0, z)) + terrain.transform.position.y,
                    terrain.SampleHeight(new Vector3(bounds.max.x, 0, z)) + terrain.transform.position.y);
            }

            float bottom = bounds.min.y - 10f;
            float top = highestEdge + heightAboveGround;
            float y = (bottom + top) * 0.5f;
            float height = top - bottom;
            // Walls sit inside the ground limits and overlap at all four corners.
            CreateWall("West", new Vector3(bounds.min.x + thickness / 2, y, bounds.center.z),
                new Vector3(thickness, height, bounds.size.z));
            CreateWall("East", new Vector3(bounds.max.x - thickness / 2, y, bounds.center.z),
                new Vector3(thickness, height, bounds.size.z));
            CreateWall("South", new Vector3(bounds.center.x, y, bounds.min.z + thickness / 2),
                new Vector3(bounds.size.x, height, thickness));
            CreateWall("North", new Vector3(bounds.center.x, y, bounds.max.z - thickness / 2),
                new Vector3(bounds.size.x, height, thickness));
        }

        private void CreateWall(string side, Vector3 position, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Neptune Ice Boundary - " + side;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(wall, gameObject.scene);
            wall.transform.position = position;
            wall.transform.localScale = size;
            wall.transform.SetParent(transform, true);
            if (iceMaterial != null) wall.GetComponent<Renderer>().sharedMaterial = iceMaterial;
        }
    }
}
