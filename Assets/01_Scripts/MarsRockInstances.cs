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
                if (SystemInfo.supportsInstancing)
                    Graphics.RenderMeshInstanced(parameters, batch.mesh, batch.submesh, batch.matrices);
                else
                    foreach (var matrix in batch.matrices)
                        Graphics.RenderMesh(parameters, batch.mesh, batch.submesh, matrix);
                visibleBatches++;
                visibleInstances += batch.matrices.Length;
            }
            if (camera.cameraType == CameraType.Game)
            {
                LastVisibleBatches = visibleBatches;
                LastVisibleInstances = visibleInstances;
            }
        }
    }
}
