using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJam.Mars
{
    /// <summary>Draws baked rock batches without one renderer per rock.</summary>
    [ExecuteAlways]
    public sealed class MarsRockInstances : MonoBehaviour
    {
        [Serializable]
        public sealed class Batch
        {
            public Mesh mesh;
            public Material material;
            public int submesh;
            public Matrix4x4[] matrices;
            public Bounds bounds;
            [NonSerialized] public Matrix4x4[] drawMatrices;
        }

        public Batch[] batches = Array.Empty<Batch>();
        [Min(1)] public float drawDistance = 120;
        private readonly Plane[] planes = new Plane[6];
        public int LastVisibleBatches { get; private set; }
        public int LastVisibleInstances { get; private set; }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Draw;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)
                return;

            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            int visibleBatches = 0, visibleInstances = 0;
            foreach (var batch in batches)
            {
                if (batch.mesh == null || batch.material == null || batch.matrices == null ||
                    batch.matrices.Length == 0 ||
                    (camera.cameraType == CameraType.Game &&
                     batch.bounds.SqrDistance(camera.transform.position) > drawDistance * drawDistance) ||
                    !GeometryUtility.TestPlanesAABB(planes, batch.bounds))
                    continue;

                var parameters = new RenderParams(batch.material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    worldBounds = batch.bounds,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                };
                var matrices = batch.matrices;
                int count = matrices.Length;
                if (camera.cameraType == CameraType.Game)
                {
                    if (batch.drawMatrices == null || batch.drawMatrices.Length != matrices.Length)
                        batch.drawMatrices = new Matrix4x4[matrices.Length];
                    count = 0;
                    float fadeWidth = Mathf.Max(5f, drawDistance * 0.2f);
                    for (int i = 0; i < matrices.Length; i++)
                    {
                        var matrix = matrices[i];
                        float distance = Vector3.Distance(camera.transform.position, matrix.GetColumn(3));
                        if (distance >= drawDistance)
                            continue;
                        float scale = Mathf.SmoothStep(0f, 1f, (drawDistance - distance) / fadeWidth);
                        // Shrink the final distance band without alpha blending or extra draw calls.
                        for (int axis = 0; axis < 3; axis++)
                            matrix.SetColumn(axis, matrix.GetColumn(axis) * scale);
                        batch.drawMatrices[count++] = matrix;
                    }
                    matrices = batch.drawMatrices;
                }
                if (count == 0)
                    continue;
                if (SystemInfo.supportsInstancing)
                    Graphics.RenderMeshInstanced(parameters, batch.mesh, batch.submesh, matrices, count);
                else
                    for (int i = 0; i < count; i++)
                        Graphics.RenderMesh(parameters, batch.mesh, batch.submesh, matrices[i]);
                visibleBatches++;
                visibleInstances += count;
            }
            if (camera.cameraType == CameraType.Game)
            {
                LastVisibleBatches = visibleBatches;
                LastVisibleInstances = visibleInstances;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Snap To Terrain")]
        public void SnapToTerrain()
        {
            SnapInstancesToTerrain(this, 0f);
        }

        [UnityEditor.MenuItem("Tools/Mars/Snap All Rocks To Terrain")]
        public static void SnapAllRocksToTerrain()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("[MarsRockInstances] No active terrain found to snap rocks to.");
                return;
            }

            var instances = FindObjectsByType<MarsRockInstances>(FindObjectsSortMode.None);
            int rockCount = 0;
            UnityEditor.Undo.SetCurrentGroupName("Snap All Rocks To Terrain");
            int group = UnityEditor.Undo.GetCurrentGroup();

            foreach (var inst in instances)
            {
                rockCount += SnapInstancesToTerrain(inst, 0f);
            }

            int colCount = 0;
            foreach (var parentName in new[] { "Rock Collisions", "Dressing Collisions" })
            {
                var parent = GameObject.Find(parentName);
                if (parent == null) continue;
                for (int i = 0; i < parent.transform.childCount; i++)
                {
                    var child = parent.transform.GetChild(i);
                    UnityEditor.Undo.RecordObject(child, "Snap Collider");
                    var pos = child.position;
                    float y = terrain.SampleHeight(pos) + terrain.transform.position.y;
                    child.position = new Vector3(pos.x, y, pos.z);
                    UnityEditor.EditorUtility.SetDirty(child);
                    colCount++;
                }
            }

            UnityEditor.Undo.CollapseUndoOperations(group);
            Debug.Log($"[MarsRockInstances] Snapped {rockCount} rocks and {colCount} colliders to Terrain.");
        }

        public static int SnapInstancesToTerrain(MarsRockInstances inst, float sinkOffset = 0f)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null || inst.batches == null) return 0;

            UnityEditor.Undo.RecordObject(inst, "Snap Rock Instances");
            int count = 0;
            foreach (var batch in inst.batches)
            {
                if (batch.matrices == null || batch.matrices.Length == 0) continue;
                Vector3 meshExtents = batch.mesh != null ? batch.mesh.bounds.extents : Vector3.one;
                Bounds newBounds = default;
                bool first = true;

                for (int i = 0; i < batch.matrices.Length; i++)
                {
                    var matrix = batch.matrices[i];
                    Vector3 pos = matrix.GetColumn(3);
                    float y = terrain.SampleHeight(pos) + terrain.transform.position.y - sinkOffset;
                    matrix.SetColumn(3, new Vector4(pos.x, y, pos.z, 1f));
                    batch.matrices[i] = matrix;

                    Vector3 scale = new Vector3(
                        matrix.GetColumn(0).magnitude,
                        matrix.GetColumn(1).magnitude,
                        matrix.GetColumn(2).magnitude);
                    Vector3 scaledExtents = Vector3.Scale(meshExtents, scale);
                    Vector3 worldPos = new Vector3(pos.x, y, pos.z);

                    if (first)
                    {
                        newBounds = new Bounds(worldPos, scaledExtents * 2f);
                        first = false;
                    }
                    else
                    {
                        newBounds.Encapsulate(worldPos + scaledExtents);
                        newBounds.Encapsulate(worldPos - scaledExtents);
                    }
                    count++;
                }
                newBounds.Expand(1f);
                batch.bounds = newBounds;
            }
            UnityEditor.EditorUtility.SetDirty(inst);
            return count;
        }
#endif
    }
}
