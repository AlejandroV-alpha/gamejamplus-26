using UnityEngine;

namespace GameJam.Survival
{
    /// <summary>
    /// Extiende el brazo libre hacia el companero que se esta arrastrando.
    /// Corre en todos los clientes: DragTarget esta replicado, asi que todos ven el agarre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDragArm : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float blendSpeed = 6f;

        private SurvivalRevive revive;
        private Animator animator;
        private float weight;
        private bool useRight;
        private Vector3 grabPoint;

        private void Awake()
        {
            revive = GetComponent<SurvivalRevive>();
        }

        private void LateUpdate()
        {
            if (revive == null) return;
            if (animator == null || !animator.isHuman)
            {
                foreach (var candidate in GetComponentsInChildren<Animator>())
                    if (candidate.isHuman) { animator = candidate; break; }
                if (animator == null) return;
            }

            SurvivalRevive target = null;
            if (revive.Object != null && revive.Object.IsValid && revive.IsDragging)
                target = revive.DragTarget.GetComponent<SurvivalRevive>();

            if (target != null)
            {
                grabPoint = target.BodyPosition + Vector3.up * .3f;
                // Elige el brazo del lado donde esta el companero.
                useRight = Vector3.Dot(grabPoint - transform.position, transform.right) > 0f;
            }
            // Tambien estira el brazo hacia un cadaver de monstruo arrastrado.
            bool carryingRemains = false;
            if (target == null && revive.Object != null && revive.Object.IsValid
                && GameJam.Grinder.MonsterRemains.TryGetCarriedBy(revive.Object, out var remainsPoint))
            {
                grabPoint = remainsPoint;
                useRight = Vector3.Dot(grabPoint - transform.position, transform.right) > 0f;
                carryingRemains = true;
            }
            weight = Mathf.MoveTowards(weight, target != null || carryingRemains ? 1f : 0f, Time.deltaTime * blendSpeed);
            if (weight <= .001f) return;

            var upper = animator.GetBoneTransform(useRight ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var lower = animator.GetBoneTransform(useRight ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var hand = animator.GetBoneTransform(useRight ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (upper == null || lower == null || hand == null) return;

            // Hombro: apunta el antebrazo hacia el agarre.
            Quaternion aim = Quaternion.FromToRotation(lower.position - upper.position, grabPoint - upper.position);
            upper.rotation = Quaternion.Slerp(upper.rotation, aim * upper.rotation, weight);
            // Codo: estira el brazo hacia el mismo punto.
            aim = Quaternion.FromToRotation(hand.position - lower.position, grabPoint - lower.position);
            lower.rotation = Quaternion.Slerp(lower.rotation, aim * lower.rotation, weight);
        }
    }
}
