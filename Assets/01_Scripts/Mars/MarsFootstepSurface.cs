using ModularFootstepSystem;
using UnityEngine;

namespace GameJam.Mars
{
    // Reads the same painted Terrain layer as the ground shader. -1 makes a uniform snow/sand surface.
    public sealed class MarsFootstepSurface : AbstractSurface
    {
        public MarsFootstepProfile profile;
        public int paintedLayer = 1;
        [Range(0, 1)] public float minimumWeight = .4f;
        private Terrain terrain;
        private void Awake() => terrain = GetComponent<Terrain>();
        public override string GetSurfaceType(Vector3 worldPosition) => AllowsFootprint(worldPosition) ? profile.name : null;
        public bool AllowsFootprint(Vector3 worldPosition)
        {
            if (profile == null) return false;
            if (paintedLayer < 0) return true;
            if (terrain == null) terrain = GetComponent<Terrain>();
            if (terrain == null || terrain.terrainData == null) return false;
            var data = terrain.terrainData;
            if (paintedLayer >= data.alphamapLayers) return false;
            Vector3 local = worldPosition - terrain.transform.position;
            if (local.x < 0 || local.z < 0 || local.x > data.size.x || local.z > data.size.z) return false;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            return data.GetAlphamaps(x, z, 1, 1)[0, 0, paintedLayer] >= minimumWeight;
        }
    }
}
