using UnityEngine;
using UnityEngine.Events;

namespace JungleNightmare {
    [RequireComponent(typeof(Animator))]
    public sealed class FrostActor : MonoBehaviour {
        public FrostGroundFX groundFX;
        [Tooltip("Disable when your movement controller calls SetMoveSpeed.")]
        public bool measureMovement = true;
        public float attackCooldown = 2.3f;
        public float runningSpeed = 1.8f;
        public UnityEvent onAttackImpact = new UnityEvent();
        Animator animator;
        Vector3 previousPosition;
        float nextAttack;
        public bool IsAttacking { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsReacting { get; private set; }
        void Awake() { animator = GetComponent<Animator>(); previousPosition = transform.position; }
        void OnEnable() { previousPosition = transform.position; }
        void Update() {
            if (measureMovement && Time.deltaTime > 0) {
                Vector3 d = transform.position - previousPosition; d.y = 0;
                SetMoveSpeed(d.magnitude / Time.deltaTime);
            }
            previousPosition = transform.position;
            IsAttacking = InState("Attack") || InState("Frost_Lunge");
            IsReacting = InState("Hit");
            if (groundFX) groundFX.suppressSteps = IsDead || IsReacting || IsAttacking;
        }
        bool InState(string state) => animator.GetCurrentAnimatorStateInfo(0).IsName(state) ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state));
        public void SetSpeed(float normalizedSpeed) => SetMoveSpeed(Mathf.Clamp01(normalizedSpeed) * runningSpeed);
        public void SetMoveSpeed(float metresPerSecond) {
            if (!animator) animator = GetComponent<Animator>();
            float speed = IsDead ? 0 : Mathf.Max(0, metresPerSecond);
            animator.SetFloat("Speed", speed);
            if (groundFX) groundFX.movementSpeed = speed;
        }
        public void Attack() {
            if (!isActiveAndEnabled || IsDead || IsReacting || Time.time < nextAttack || IsAttacking) return;
            nextAttack = Time.time + attackCooldown;
            IsAttacking = true;
            animator.SetTrigger("Attack");
        }
        public void ReceiveDamage() {
            if (!isActiveAndEnabled || IsDead) return;
            animator.ResetTrigger("Attack");animator.SetTrigger("Hit");IsAttacking=false;IsReacting=true;
            if(groundFX)groundFX.suppressSteps=true;
        }
        public void Die() {
            if (!isActiveAndEnabled || IsDead) return;
            IsDead=true;IsAttacking=false;animator.ResetTrigger("Attack");animator.ResetTrigger("Hit");
            animator.SetBool("Dead",true);SetMoveSpeed(0);
            if(groundFX)groundFX.suppressSteps=true;
        }
        // Animation event at the contact frame. Connect gameplay damage to onAttackImpact.
        public void FrostImpact() {
            if(IsDead || IsReacting) return;
            if (groundFX) groundFX.AttackBurst();
            onAttackImpact.Invoke();
        }
    }
}

// Integrated into gamejamplus-26.
