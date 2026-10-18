using UnityEngine;

namespace GameJam.Mars
{
    // Baked scene resources/guard spawners, not another network spawning system.
    [DefaultExecutionOrder(-500)]
    public sealed class MarsInterestZone : MonoBehaviour
    {
        public string zoneName;
        public string landmark;
        public Terrain terrain;
        public Transform[] groundObjects;
        [Min(1)] public float recognizableRadius = 18f;
        private void Awake()
        {
            if (terrain == null || terrain.terrainData == null || groundObjects == null) return;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            foreach (var item in groundObjects)
            {
                if (item == null) continue;
                Vector3 p = item.position;
                if (p.x < origin.x || p.z < origin.z || p.x > origin.x + size.x || p.z > origin.z + size.z) continue;
                p.y = terrain.SampleHeight(p) + origin.y + .05f;
                item.position = p;
            }
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.25f, .85f, 1f, .65f);
            Gizmos.DrawWireSphere(transform.position, recognizableRadius);
            if (groundObjects == null) return;
            foreach (var item in groundObjects)
                if (item != null) Gizmos.DrawLine(transform.position, item.position);
        }
    }
}
