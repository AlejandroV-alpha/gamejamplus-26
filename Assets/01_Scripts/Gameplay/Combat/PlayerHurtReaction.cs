using GameJam.Multiplayer;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Reaccion al dano: el astronauta se encoge, retrocede, sacude la cabeza y suelta salpicadura.
    /// Se dispara en todos los clientes al bajar la vida replicada.
    /// </summary>
    [DefaultExecutionOrder(11900)]
    [DisallowMultipleComponent]
    public sealed class PlayerHurtReaction : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField, Min(0.05f)] private float duration = 0.4f;
        [SerializeField] private float recoilAngle = -18f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.12f;
        [SerializeField, Min(0f)] private float shake = 6f;
        [SerializeField] private GameObject hurtEffect;
        [SerializeField, Min(0.1f)] private float hurtEffectDuration = 2f;

        private MarsFusionAvatar avatar;
        private EnemyBloodFx splatter;
        private Animator animator;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Transform head, spine, leftUpperArm, rightUpperArm;
        private float? lastHealth;
        private float hurtTime = -10f;
        private float strength;
        private bool playing;
        public bool IsReacting => avatar!=null && avatar.Object!=null && avatar.Object.IsValid && avatar.IsAlive && Time.time-hurtTime<duration;

        /// <summary>
        /// Usa el modelo animado como cuerpo y localiza huesos humanoides.
        /// </summary>
        private void Awake()
        {
            avatar = GetComponent<MarsFusionAvatar>();
            splatter = GetComponent<EnemyBloodFx>();
            animator = GetComponentInChildren<Animator>();
            if (body == null && animator != null)
            {
                body = animator.transform;
            }
            if (body != null)
            {
                restPosition = body.localPosition;
                restRotation = body.localRotation;
                restScale = body.localScale;
            }
            if (animator != null && animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
        }

        /// <summary>
        /// Detecta bajadas de vida y anima la reaccion encima de la animacion actual.
        /// </summary>
        private void LateUpdate()
        {
            if (avatar == null || body == null || avatar.Object == null || !avatar.Object.IsValid)
            {
                return;
            }

            IPlayerHealth health = avatar.HealthSource;
            float? current = health != null ? health.CurrentHealth : (float?)null;
            if (current.HasValue && lastHealth.HasValue && current.Value < lastHealth.Value - 0.01f)
            {
                float maxHealth = Mathf.Max(1f, health.MaxHealth);
                OnHurt(Mathf.Clamp01((lastHealth.Value - current.Value) / (maxHealth * 0.2f)));
            }
            lastHealth = current;

            // La muerte tiene su propia animacion; no se pelea por el cuerpo.
            if (!avatar.IsAlive)
            {
                playing = false;
                return;
            }

            float t = (Time.time - hurtTime) / duration;
            if (t >= 1f)
            {
                if (playing)
                {
                    if (!(GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false))
                    {
                        body.localPosition = restPosition;
                        body.localRotation = restRotation;
                        body.localScale = restScale;
                    }
                    playing = false;
                }
                return;
            }

            playing = true;
            Animate(t);
        }

        /// <summary>
        /// Lanza particulas y salpicadura en el pecho y reinicia la reaccion.
        /// </summary>
        private void OnHurt(float amount)
        {
            hurtTime = Time.time;
            strength = Mathf.Lerp(0.6f, 1.2f, amount);

            Vector3 chest = spine != null ? spine.position : body.position + Vector3.up * 1.1f;
            Vector3 outward = -body.forward;
            if (hurtEffect != null)
            {
                Destroy(Instantiate(hurtEffect, chest, Quaternion.LookRotation(outward)), hurtEffectDuration);
            }
            if (splatter != null)
            {
                splatter.Bleed(chest, outward);
            }
        }

        /// <summary>
        /// Golpe seco hacia atras, aplastamiento y vuelta elastica a la pose.
        /// </summary>
        private void Animate(float t)
        {
            // Impacto inmediato que decae con un pequeño rebote elastico.
            float hit = Mathf.Exp(-6f * t) * Mathf.Cos(t * Mathf.PI * 2.5f) * strength;
            float jitter = Mathf.Sin(t * 70f) * (1f - t) * shake * strength;

            bool lying = GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false;
            // The storm samples a prone humanoid pose before this reaction. Keep the
            // root close to the floor and apply the existing pain mostly to the limbs.
            float rootStrength = lying ? .2f : 1f;
            Vector3 basePosition = lying ? body.localPosition : restPosition;
            Quaternion baseRotation = lying ? body.localRotation : restRotation;
            body.localPosition = basePosition - baseRotation * Vector3.forward * recoilDistance * hit * rootStrength;
            body.localRotation = baseRotation * Quaternion.Euler(recoilAngle * hit * rootStrength, jitter * rootStrength, jitter * 0.5f * rootStrength);
            float squash = (lying ? .025f : .12f) * Mathf.Max(0f, hit);
            body.localScale = Vector3.Scale(restScale, new Vector3(1f + squash, 1f - squash, 1f + squash));

            Rotate(spine, new Vector3(recoilAngle * 0.8f * hit, 0f, 0f));
            Rotate(head, new Vector3(-25f * hit, jitter * 2f, jitter * 1.5f));
            Rotate(leftUpperArm, new Vector3(0f, 0f, -35f * Mathf.Max(0f, hit)));
            Rotate(rightUpperArm, new Vector3(0f, 0f, 35f * Mathf.Max(0f, hit)));
        }

        private static void Rotate(Transform bone, Vector3 euler)
        {
            if (bone != null)
            {
                bone.localRotation *= Quaternion.Euler(euler);
            }
        }
    }
}
