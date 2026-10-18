using ModularFootstepSystem.Extensions;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameJam.Mars
{
    // One shared bounded package pool per client, including all observed players.
    public sealed class MarsFootprintPool : ExtendedBehaviourPool
    {
        private static MarsFootprintPool shared;
        public static MarsFootprintPool GetShared(GameObject decalPrefab)
        {
            if (shared != null) return shared;
            var go = new GameObject("Mars footprints — shared pool (64)");
            go.SetActive(false);
            shared = go.AddComponent<MarsFootprintPool>();
            shared.prefab = decalPrefab;
            shared.defaultCapacity = 8;
            shared.maxSize = 64;
            shared.isReusedActiveObjects = true;
            go.SetActive(true);
            return shared;
        }
        private void OnDestroy()
        {
            foreach (var obj in activeObjects) if (obj != null) Destroy(obj);
            pool?.Clear();
            if (shared == this) shared = null;
        }
    }
}
