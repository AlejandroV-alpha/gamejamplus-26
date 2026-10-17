using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Mars
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class b_efectoCinturonAsteroides : MonoBehaviour
    {
        public Material material;
        [Range(50, 1400)] public int asteroidCount = 850;
        [Range(0f, 360f)] public float azimuth = 180f;
        [Range(40f, 180f)] public float arcWidth = 170f;
        [Range(15f, 80f)] public float peakElevation = 52f;
        [Range(1f, 30f)] public float beltThickness = 13f;
        [Range(0.2f, 3f)] public float rockSize = 1f;
        public int seed = 417;
        private Mesh mesh;
        private bool rebuild = true;

        private void OnEnable()
        {
            rebuild = true;
            RenderPipelineManager.beginCameraRendering += Draw;
        }
        private void OnValidate() => rebuild = true;
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
            ReleaseMesh();
        }
        private void ReleaseMesh()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (material == null || camera == null || camera.orthographic ||
                (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (rebuild || mesh == null) Build();
            float radius = Mathf.Min(600f, camera.farClipPlane * 0.8f);
            var parameters = new RenderParams(material)
            {
                camera = camera,
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(camera.transform.position, Vector3.one * radius * 3f)
            };
            Graphics.RenderMesh(parameters, mesh, 0,
                Matrix4x4.TRS(camera.transform.position, Quaternion.identity, Vector3.one * radius));
        }
        private void Build()
        {
            ReleaseMesh();
            var random = new System.Random(seed);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var centers = new List<Vector4>();
            var colors = new List<Color>();
            var indices = new List<int>();
            Vector3[] shape = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[] triangles = { 0,4,3, 0,3,5, 0,5,2, 0,2,4, 1,3,4, 1,5,3, 1,2,5, 1,4,2 };
            float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            for (int i = 0; i < Mathf.Clamp(asteroidCount, 50, 1400); i++)
            {
                float t = Next(-1f, 1f);
                float yaw = (azimuth + t * arcWidth * 0.5f) * Mathf.Deg2Rad;
                // Concentrate most fragments in the core, with a sparse outer fringe.
                float spread = i % 5 == 0 ? Next(-1f, 1f)
                    : (Next(-1f, 1f) + Next(-1f, 1f) + Next(-1f, 1f)) / 3f;
                float ribbon = Mathf.Sin(t * 9f) * 0.7f;
                float taper = Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(1f - t*t));
                float elevation = (Mathf.Lerp(12f, peakElevation, 1f - t*t) + ribbon +
                    spread * beltThickness * 0.5f * taper) * Mathf.Deg2Rad;
                Vector3 center = new Vector3(Mathf.Sin(yaw)*Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Cos(yaw)*Mathf.Cos(elevation));
                float size = Mathf.Lerp(0.0012f, 0.007f, Mathf.Pow(Next(0f, 1f), 3f)) * rockSize;
                // Fine distant debris fills the band without making all rocks equally large.
                if (i % 3 == 0) size *= 0.45f;
                Quaternion rotation = Quaternion.Euler(Next(0f,360f), Next(0f,360f), Next(0f,360f));
                var points = new Vector3[6];
                for (int j = 0; j < 6; j++)
                    points[j] = rotation * Vector3.Scale(shape[j], new Vector3(Next(0.6f,1.4f),Next(0.6f,1.4f),Next(0.6f,1.4f))) * size;
                Color tint = Color.Lerp(new Color(0.23f,0.15f,0.12f), new Color(0.68f,0.49f,0.32f), Next(0f,1f));
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    Vector3 a=points[triangles[j]], b=points[triangles[j+1]], c=points[triangles[j+2]];
                    Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
                    foreach (var point in new[] { a,b,c })
                    {
                        indices.Add(vertices.Count);
                        vertices.Add(center+point);
                        normals.Add(normal);
                        centers.Add(new Vector4(center.x,center.y,center.z,i*1.73f));
                        colors.Add(tint);
                    }
                }
            }
            mesh = new Mesh { name = "b_efecto_fragmentosCinturon", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetUVs(1,centers); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            rebuild = false;
        }
    }
}
