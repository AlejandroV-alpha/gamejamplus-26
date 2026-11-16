using System.Collections.Generic;
using GameJam.Inventory;
using UnityEngine;
using PlayerInventory = GameJam.Inventory.Inventory;

namespace GameJam.Combat
{
    /// <summary>
    /// "Game feel" de un enemigo (visual, en cada cliente):
    ///  - Hit flash blanco + sacudida al recibir daño.
    ///  - Hit-stop al morir (se congela en blanco un instante) y explosión de Cartoon FX.
    ///  - Botín magnético: orbes que saltan del cuerpo y vuelan hacia quien lo mató;
    ///    en el cliente del que mató se suman a su inventario.
    /// El enemigo de red llama a estos métodos desde sus RPC; no toca la lógica de juego.
    /// </summary>
    public sealed class EnemyHitFeedback : MonoBehaviour
    {
        [System.Serializable]
        public struct Loot
        {
            public MaterialData material;
            public Vector2Int amount;
        }

        [Header("Golpe")]
        [SerializeField] string displayName = "ENEMIGO";
        [SerializeField, Min(0f)] float flashTime = .07f;
        [SerializeField, Min(0f), Tooltip("Cuánto se sacude con cada golpe (en metros).")] float shakeAmount = .12f;

        [Header("Muerte")]
        [SerializeField, Min(0f)] float hitStopTime = .12f;
        [SerializeField] GameObject deathEffect;
        [SerializeField, Min(.1f)] float deathEffectScale = 1f;

        [Header("Botín")]
        [SerializeField] Loot[] loot;
        [SerializeField] Material orbMaterial;
        [SerializeField] Color orbColor = new Color(.5f, 1f, .7f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        Transform visual;
        Vector3 visualRest;
        float flashUntil, shakeUntil, frozenUntil;
        Vector3 shakeDir;
        Animator animator;
        float animatorSpeedBeforeFreeze = 1f;

        public string DisplayName => displayName;
        /// <summary>Verdadero mientras dura el congelado de la muerte.</summary>
        public bool IsFrozen => Time.time < frozenUntil;
        public float HitStopTime => hitStopTime;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            mpb = new MaterialPropertyBlock();
            animator = GetComponentInChildren<Animator>();
            visual = animator != null ? animator.transform : transform;
            visualRest = visual.localPosition;
        }

        /// <summary>En el cliente que disparó: hitmarker inmediato, sin esperar a la red.</summary>
        public void LocalHit() => HitFeedbackHUD.Hit();

        /// <summary>En todos los clientes cuando la autoridad confirma el daño.</summary>
        public void Damaged(Vector3 fromDirection)
        {
            flashUntil = Time.time + flashTime;
            shakeUntil = Time.time + .16f;
            shakeDir = fromDirection.sqrMagnitude > .01f ? fromDirection.normalized : Random.onUnitSphere;
        }

        /// <summary>En todos los clientes al morir. killerTransform puede ser null.</summary>
        public void Killed(bool localPlayerIsKiller, Transform killerTransform, int seed)
        {
            flashUntil = Time.time + hitStopTime + .05f;
            frozenUntil = Time.time + hitStopTime;
            if (animator != null) { animatorSpeedBeforeFreeze = animator.speed; animator.speed = 0f; }
            if (localPlayerIsKiller) HitFeedbackHUD.Kill(displayName);

            if (deathEffect != null)
            {
                var fx = Instantiate(deathEffect, CenterOfMass(), Quaternion.identity);
                fx.transform.localScale *= deathEffectScale;
                Destroy(fx, 4f);
            }
            SpawnLoot(localPlayerIsKiller, killerTransform, seed);
        }

        void LateUpdate()
        {
            float now = Time.time;
            bool flash = now < flashUntil;
            foreach (var r in renderers)
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (flash)
                {
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, new Color(6f, 6f, 6f, 1f));   // blanco brillante sobre la textura
                    mpb.SetColor(ColorId, new Color(6f, 6f, 6f, 1f));
                    r.SetPropertyBlock(mpb);
                }
                else if (r.HasPropertyBlock()) r.SetPropertyBlock(null);
            }

            if (now < shakeUntil && !IsFrozen)
            {
                float k = (shakeUntil - now) / .16f;
                Vector3 local = transform.InverseTransformDirection(shakeDir) / Mathf.Max(.01f, transform.lossyScale.x);
                visual.localPosition = visualRest + (local * .6f + Random.insideUnitSphere * .4f) * shakeAmount * k / Mathf.Max(.01f, transform.lossyScale.x);
            }
            else if (now - shakeUntil < .05f) visual.localPosition = visualRest;

            if (!IsFrozen && animator != null && animator.speed == 0f && now - frozenUntil < .05f)
                animator.speed = animatorSpeedBeforeFreeze;
        }

        Vector3 CenterOfMass()
        {
            var b = new Bounds(transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b.center : transform.position + Vector3.up;
        }

        void SpawnLoot(bool mine, Transform killer, int seed)
        {
            if (loot == null || loot.Length == 0) return;
            var rng = new System.Random(seed);
            Vector3 from = CenterOfMass();
            foreach (var item in loot)
            {
                if (item.material == null) continue;
                int count = rng.Next(item.amount.x, Mathf.Max(item.amount.x, item.amount.y) + 1);
                for (int i = 0; i < count; i++)
                {
                    var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, .6f + (float)rng.NextDouble(), (float)rng.NextDouble() * 2f - 1f).normalized;
                    LootOrb.Spawn(from, dir * (4f + (float)rng.NextDouble() * 3f), killer, mine ? item.material : null,
                        orbMaterial, orbColor, .25f + i * .06f);
                }
            }
        }
    }

    /// <summary>
    /// Orbe de botín: sale disparado, flota un instante y vuela hacia quien mató al enemigo.
    /// Si material no es null (cliente del que mató), al llegar lo suma a su inventario.
    /// </summary>
    public sealed class LootOrb : MonoBehaviour
    {
        Vector3 velocity;
        Transform target;
        MaterialData material;
        float age, homeDelay;

        public static void Spawn(Vector3 at, Vector3 velocity, Transform target, MaterialData material, Material mat, Color color, float homeDelay)
        {
            var go = new GameObject("OrbeBotin");
            go.transform.position = at;
            var orb = go.AddComponent<LootOrb>();
            orb.velocity = velocity;
            orb.target = target;
            orb.material = material;
            orb.homeDelay = homeDelay + .25f;

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(go.transform, false);
            ball.transform.localScale = Vector3.one * .18f;
            var mr = ball.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color * 2.5f);
            mr.SetPropertyBlock(block);

            var trail = go.AddComponent<TrailRenderer>();
            if (mat != null) trail.sharedMaterial = mat;
            trail.time = .25f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, .14f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point; light.color = color; light.intensity = 1.5f; light.range = 2f;
            Destroy(go, 6f);
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age < homeDelay || target == null)
            {
                // Explota hacia afuera y frena flotando.
                velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-5f * Time.deltaTime));
                velocity += Vector3.down * 2f * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                if (target == null && age > 3f) Destroy(gameObject);
                return;
            }
            // Imán: acelera hacia el pecho del jugador.
            Vector3 goal = target.position + Vector3.up * 1.1f;
            Vector3 to = goal - transform.position;
            float speed = Mathf.Lerp(6f, 30f, Mathf.Clamp01((age - homeDelay) / .8f));
            velocity = Vector3.Lerp(velocity, to.normalized * speed, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.position += velocity * Time.deltaTime;
            if (to.magnitude < .45f) Arrive();
        }

        void Arrive()
        {
            if (material != null)
            {
                var inventory = target.GetComponentInParent<PlayerInventory>();
                if (inventory != null && inventory.HasStateAuthority && inventory.AddMaterial(material, 1))
                {
                    string name = string.IsNullOrEmpty(material.materialName) ? material.name : material.materialName;
                    HitFeedbackHUD.Loot("+1 " + name);
                }
            }
            Destroy(gameObject);
        }
    }
}
