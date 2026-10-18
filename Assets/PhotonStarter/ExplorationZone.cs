using UnityEngine;

namespace GameJam.Multiplayer
{
    // A horizontal radius, independent of terrain geometry and player height.
    public sealed class ExplorationZone : MonoBehaviour
    {
        [Min(1)] public float radius = 460;
        [Min(1)] public float returnSeconds = 10;
        [Tooltip("Optional terrain footprint: prevents a wide radius from including space beyond a rectangular map.")]
        public Terrain terrain;
        [Min(0)] public float terrainEdgeMargin = 2;
        public bool Contains(Vector3 position)
        {
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 p=position-terrain.transform.position;
                Vector3 size=terrain.terrainData.size;
                if(p.x < terrainEdgeMargin || p.z < terrainEdgeMargin || p.x > size.x-terrainEdgeMargin || p.z > size.z-terrainEdgeMargin) return false;
            }
            Vector3 d = position-transform.position; d.y = 0;
            return d.sqrMagnitude <= radius*radius;
        }
        public static ExplorationZone ForPlayer(Component player)
        {
            foreach (var zone in FindObjectsByType<ExplorationZone>(FindObjectsSortMode.None))
                if (zone.isActiveAndEnabled && zone.gameObject.scene == player.gameObject.scene) return zone;
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var zone in FindObjectsByType<ExplorationZone>(FindObjectsSortMode.None))
                if(zone.isActiveAndEnabled && zone.gameObject.scene==active) return zone;
            return null;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,.35f,.1f,.9f);
            Vector3 previous = transform.position+Vector3.right*radius;
            for(int i=1;i<=96;i++)
            {
                float angle=i*Mathf.PI*2/96;
                Vector3 next=transform.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius;
                Gizmos.DrawLine(previous,next); previous=next;
            }
        }
    }
}
