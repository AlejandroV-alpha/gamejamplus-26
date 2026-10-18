using GameJam.Multiplayer;
using UnityEngine;
using System.Collections.Generic;

namespace GameJam.Combat
{
    /// <summary>
    /// Muerte cómica del astronauta: trastabilla hacia atrás, se deja caer de espaldas
    /// abriendo los brazos como echándose, rebota, patalea y le giran estrellitas.
    /// Es solo visual; PlayerHealth decide la vida.
    /// </summary>
    [DefaultExecutionOrder(12100)]
    [DisallowMultipleComponent]
    public sealed class PlayerDeathAnimation : MonoBehaviour
    {
        // Fases de la secuencia en segundos.
        private const float StaggerEnd = 0.3f;
        private const float FallEnd = 0.95f;
        private const float BounceEnd = 1.45f;

        [SerializeField] private Transform body;
        [SerializeField, Min(0f)] private float staggerDistance = 0.25f;
        [SerializeField, Min(0f)] private float lyingLift = 0.18f;
        [SerializeField] private float armSpread = 85f;
        [SerializeField] private float legSpread = 18f;
        [SerializeField, Min(0.2f)] private float twitchInterval = 1.6f;
        [SerializeField] private Color starColor = new Color(1f, 0.85f, 0.1f);

        private MarsFusionAvatar avatar;
        private Animator animator;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 restScale;
        private Transform head, spine, leftUpperArm, rightUpperArm, leftLowerArm, rightLowerArm,
            leftUpperLeg, rightUpperLeg, leftLowerLeg, rightLowerLeg;
        private Transform[] stars;
        private bool wasDead;
        private float deathTime;
        private readonly Dictionary<Transform,Quaternion> deathPose = new Dictionary<Transform,Quaternion>();
        private Material starMaterial;

        /// <summary>
        /// Usa el modelo animado como cuerpo y localiza huesos humanoides para las extremidades.
        /// </summary>
        private void Awake()
        {
            avatar = GetComponent<MarsFusionAvatar>();
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
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            }
        }

        /// <summary>
        /// Detecta la muerte y la reaparicion despues de que el personaje se animo.
        /// </summary>
        private void LateUpdate()
        {
            if (avatar == null || body == null || !avatar.IsNetworkReady || !avatar.HasHealthSource)
            {
                return;
            }

            bool dead = !avatar.IsAlive;
            if (dead && !wasDead)
            {
                deathTime = Time.time;
                deathPose.Clear();
                foreach(var bone in new[]{head,spine,leftUpperArm,rightUpperArm,leftLowerArm,rightLowerArm,leftUpperLeg,rightUpperLeg,leftLowerLeg,rightLowerLeg})
                    if(bone!=null)deathPose[bone]=bone.localRotation;
                if (animator != null)
                {
                    // Congela la pose actual; los huesos se mueven encima de forma aditiva.
                    animator.speed = 0f;
                }
            }
            else if (!dead && wasDead)
            {
                Restore();
            }
            wasDead = dead;

            if (dead)
            {
                Play(Time.time - deathTime);
            }
        }

        /// <summary>
        /// Evalua la secuencia completa para el tiempo transcurrido desde la muerte.
        /// </summary>
        private void Play(float t)
        {
            // Evaluate from the captured pose: frozen Animator bones must not accumulate rotations.
            foreach(var bone in deathPose)if(bone.Key!=null)bone.Key.localRotation=bone.Value;
            float back = 0f;      // Inclinacion hacia atras en grados.
            float height = 0f;
            float step = 0f;      // Retroceso en metros.
            float open = 0f;      // 0 = brazos abajo, 1 = totalmente abiertos.
            Vector3 squash = Vector3.one;

            if (t < StaggerEnd)
            {
                // Trastabilla hacia atras, con los brazos empezando a abrirse.
                float u = t / StaggerEnd;
                back = 12f * Mathf.Sin(u * Mathf.PI * 0.5f) + Mathf.Sin(u * Mathf.PI * 3f) * 4f;
                step = staggerDistance * EaseOut(u);
                open = 0.35f * u;
            }
            else if (t < FallEnd)
            {
                // Se deja caer de espaldas como un tronco, abriendo los brazos de par en par.
                float u = (t - StaggerEnd) / (FallEnd - StaggerEnd);
                back = Mathf.Lerp(12f, 90f, u * u);
                step = staggerDistance;
                height = lyingLift * u;
                open = Mathf.Lerp(0.35f, 1.15f, EaseOut(u));
            }
            else if (t < BounceEnd)
            {
                // Rebota contra el suelo y los brazos se asientan abiertos.
                float u = (t - FallEnd) / (BounceEnd - FallEnd);
                float bounce = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * (1f - u) * 0.25f;
                back = 90f - Mathf.Sin(u * Mathf.PI * 2f) * 6f * (1f - u);
                step = staggerDistance;
                height = lyingLift + bounce;
                open = Mathf.Lerp(1.15f, 1f, u);
                float impact = (1f - Mathf.Clamp01(bounce / 0.08f)) * (1f - u);
                squash = new Vector3(1f + 0.1f * impact, 1f - 0.1f * impact, 1f);
            }
            else
            {
                back = 90f;
                step = staggerDistance;
                height = lyingLift;
                open = 1f;
            }

            ApplyBody(back, height, step, squash);
            ApplyLimbs(t, open, back);
            UpdateStars(t);
        }

        /// <summary>
        /// Inclina el cuerpo hacia atras respecto al astronauta, sin depender de los ejes del modelo.
        /// </summary>
        private void ApplyBody(float back, float height, float step, Vector3 squash)
        {
            Transform parent = body.parent;
            Vector3 right = parent != null ? parent.InverseTransformDirection(transform.right) : transform.right;
            Vector3 forward = parent != null ? parent.InverseTransformDirection(transform.forward) : transform.forward;
            Vector3 up = parent != null ? parent.InverseTransformDirection(transform.up) : transform.up;

            // Girar sobre el eje derecho con angulo negativo lleva la cabeza hacia atras.
            body.localRotation = Quaternion.AngleAxis(-back, right) * restRotation;
            body.localPosition = restPosition - forward * step + up * height;
            body.localScale = Vector3.Scale(restScale, squash);
        }

        /// <summary>
        /// Abre brazos y piernas en el espacio del torso y añade pataleo una vez tendido.
        /// </summary>
        private void ApplyLimbs(float t, float open, float back)
        {
            // Eje que atraviesa el pecho (el frente del astronauta ya inclinado):
            // girar sobre el abre brazos y piernas hacia los lados.
            Vector3 chestAxis = Quaternion.AngleAxis(-back, transform.right) * transform.forward;

            float arms = armSpread * open;
            RotateWorld(leftUpperArm, chestAxis, -arms);
            RotateWorld(rightUpperArm, chestAxis, arms);
            // Codos un poco doblados para que se vea relajado, no rigido.
            RotateWorld(leftLowerArm, chestAxis, -15f * open);
            RotateWorld(rightLowerArm, chestAxis, 15f * open);

            float legs = legSpread * Mathf.Clamp01(open);
            RotateWorld(leftUpperLeg, chestAxis, -legs);
            RotateWorld(rightUpperLeg, chestAxis, legs);

            if (t < BounceEnd)
            {
                return;
            }

            // Pataleo cómico cada cierto tiempo, cada vez mas debil.
            float since = t - BounceEnd;
            float phase = since % twitchInterval;
            float fade = Mathf.Clamp01(1f - since / 12f);
            if (phase < 0.45f)
            {
                Vector3 kneeAxis = transform.right;
                float kick = Mathf.Sin(phase / 0.45f * Mathf.PI * 4f) * 25f * fade;
                RotateWorld(leftUpperLeg, kneeAxis, kick);
                RotateWorld(rightUpperLeg, kneeAxis, -kick);
                RotateWorld(leftLowerLeg, kneeAxis, -Mathf.Abs(kick));
                RotateWorld(rightLowerLeg, kneeAxis, -Mathf.Abs(kick));
            }

            // Respiracion exagerada del pecho mientras esta tendido.
            if (spine != null)
            {
                RotateWorld(spine, transform.right, Mathf.Sin(since * 2.5f) * 3f * fade);
            }
        }

        /// <summary>
        /// Tres estrellitas giran sobre la cabeza mientras esta noqueado.
        /// </summary>
        private void UpdateStars(float t)
        {
            if (t < FallEnd || head == null)
            {
                SetStarsActive(false);
                return;
            }

            EnsureStars();
            SetStarsActive(true);
            float grow = Mathf.Clamp01((t - FallEnd) / 0.3f);
            Vector3 center = head.position + Vector3.up * 0.35f;
            for (int i = 0; i < stars.Length; i++)
            {
                float angle = t * 4f + i * Mathf.PI * 2f / stars.Length;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2f) * 0.15f, Mathf.Sin(angle)) * 0.35f;
                stars[i].position = center + offset;
                stars[i].localScale = Vector3.one * 0.1f * grow * (1f + 0.25f * Mathf.Sin(t * 12f + i));
                stars[i].rotation = Quaternion.Euler(0f, t * 360f, 45f);
            }
        }

        private void EnsureStars()
        {
            if (stars != null)
            {
                return;
            }

            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            starMaterial=material;
            material.SetColor("_BaseColor", starColor);
            stars = new Transform[3];
            for (int i = 0; i < stars.Length; i++)
            {
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(star.GetComponent<Collider>());
                star.name = "EstrellaMareo_" + i;
                star.GetComponent<Renderer>().sharedMaterial = material;
                star.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                star.transform.SetParent(transform, true);
                stars[i] = star.transform;
            }
        }

        private void SetStarsActive(bool active)
        {
            if (stars == null)
            {
                return;
            }
            foreach (Transform star in stars)
            {
                if (star.gameObject.activeSelf != active)
                {
                    star.gameObject.SetActive(active);
                }
            }
        }

        private static void RotateWorld(Transform bone, Vector3 axis, float angle)
        {
            if (bone != null)
            {
                bone.rotation = Quaternion.AngleAxis(angle, axis) * bone.rotation;
            }
        }

        private static float EaseOut(float u)
        {
            return 1f - (1f - u) * (1f - u);
        }

        /// <summary>
        /// Devuelve el cuerpo a su pose original si el jugador revive.
        /// </summary>
        private void Restore()
        {
            body.localPosition = restPosition;
            body.localRotation = restRotation;
            body.localScale = restScale;
            SetStarsActive(false);
            if (animator != null)
            {
                animator.speed = 1f;
            }
        }
        private void OnDestroy()
        {
            if(stars!=null)foreach(var star in stars)if(star!=null)Destroy(star.gameObject);
            if(starMaterial!=null)Destroy(starMaterial);
        }

    }
}
