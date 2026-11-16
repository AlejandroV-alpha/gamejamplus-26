using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Salpica sangre al recibir un disparo y deja manchas que permanecen en el suelo.
    /// </summary>
    public sealed class EnemyBloodFx : MonoBehaviour
    {
        private const int MaxSplatsInWorld = 60;
        private static readonly Queue<GameObject> ActiveSplats = new Queue<GameObject>();

        [SerializeField] private GameObject bloodBurst;
        [SerializeField] private GameObject groundSplat;
        [SerializeField, Min(0.1f)] private float burstDuration = 2f;
        [SerializeField, Min(0)] private int splatsPerHit = 3;
        [SerializeField, Min(0.1f)] private float splatSpread = 1.4f;
        [SerializeField] private Vector2 splatSize = new Vector2(0.5f, 1.3f);
        [SerializeField] private LayerMask groundLayers = ~0;
        [SerializeField, Min(0.1f), Tooltip("Tamaño del chorro de sangre (criaturas gigantes usan mas).")] private float burstScale = 1f;

        /// <summary>
        /// Busca un enemigo con sangre junto al impacto y lo hace sangrar.
        /// </summary>
        public static void TryBleedAt(Vector3 point, Vector3 normal)
        {
            Collider[] nearby = Physics.OverlapSphere(point, 0.35f, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider candidate in nearby)
            {
                EnemyBloodFx blood = candidate.GetComponentInParent<EnemyBloodFx>();
                if (blood != null && blood.GetComponent<ReksaiFusionEnemy>()==null && blood.GetComponent<JungleNightmareFusionEnemy>()==null && blood.GetComponent<ElCuernudoFusionEnemy>()==null && blood.GetComponent<VenomwingFusionEnemy>()==null && blood.GetComponent<GusanoMarteFusion>()==null && blood.GetComponent<GameJam.Multiplayer.MarsFusionAvatar>()==null)
                {
                    blood.Bleed(point, normal);
                    return;
                }
            }
        }

        /// <summary>
        /// Lanza el chorro de sangre y coloca manchas en el suelo alrededor del enemigo.
        /// </summary>
        public void Bleed(Vector3 point, Vector3 normal)
        {
            if (bloodBurst != null)
            {
                GameObject burst = Instantiate(bloodBurst, point, Quaternion.LookRotation(normal));
                burst.transform.localScale = Vector3.one * burstScale;
                Destroy(burst, burstDuration);
            }

            if (groundSplat == null)
            {
                return;
            }

            for (int i = 0; i < splatsPerHit; i++)
            {
                Vector2 offset = Random.insideUnitCircle * splatSpread;
                Vector3 from = new Vector3(point.x + offset.x, point.y + 2f, point.z + offset.y);
                if (!TryFindGround(from, out RaycastHit hit))
                {
                    continue;
                }

                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                GameObject splat = Instantiate(groundSplat, hit.point + hit.normal * 0.02f, rotation);
                float size = Random.Range(splatSize.x, splatSize.y) * (i == 0 ? 1.3f : 1f);
                splat.transform.localScale = new Vector3(size, 1f, size);
                Register(splat);
            }
        }

        /// <summary>
        /// Encuentra suelo bajo el punto ignorando el propio cuerpo del enemigo.
        /// </summary>
        private bool TryFindGround(Vector3 from, out RaycastHit ground)
        {
            ground = default;
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 8f, groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best)
                {
                    continue;
                }
                best = hit.distance;
                ground = hit;
            }

            return best < float.MaxValue;
        }

        /// <summary>
        /// Limita las manchas totales para no acumular objetos sin fin.
        /// </summary>
        private static void Register(GameObject splat)
        {
            ActiveSplats.Enqueue(splat);
            while (ActiveSplats.Count > MaxSplatsInWorld)
            {
                GameObject oldest = ActiveSplats.Dequeue();
                if (oldest != null)
                {
                    Destroy(oldest);
                }
            }
        }
    }
}
