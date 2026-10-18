using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

// La autoridad de Fusion decide reposo, patrulla y persecucion. El resto de clientes reproduce la pose replicada.
public sealed class GiantGolemEnemy : NetworkBehaviour, ITakeDamage
{
    public enum Mode { Idle, Walk, Chase, Attack, Throw, Super, Death }

    [SerializeField] int maxHealth = 200;
    [SerializeField] float deathDuration = 4.4f;
    [SerializeField] SphereCollider detectionSphere;
    [SerializeField] float detectionRange = 28f;
    [SerializeField] float attackRange = 6f;
    [SerializeField] float throwRange = 16f;
    [SerializeField] int attackDamage = 20;
    [SerializeField] int superDamage = 40;
    [SerializeField, Range(0f, 1f)] float superChance = 0.35f;
    [SerializeField] float superCooldown = 8f;
    [SerializeField] int throwDamage = 14;
    [SerializeField] float throwInterval = 10f;
    [SerializeField] float attackDuration = 2.67f;
    [SerializeField] float superDuration = 3.7f;
    [SerializeField] float throwDuration = 3.83f;
    [SerializeField] GameObject rockPrefab;
    [SerializeField] Transform throwHand;
    [SerializeField] Vector3 rockLocalOffset = new Vector3(0.05f, 0.12f, 0.2f);
    [SerializeField] float rockSpeed = 45f;
    [SerializeField] float walkSpeed = 2.4f;
    [SerializeField] float chaseSpeed = 3.6f;
    [SerializeField] float patrolRadius = 22f;
    [SerializeField] float restDuration = 4f;
    [SerializeField] float arriveDistance = 2f;
    [SerializeField] float turnSpeed = 70f;
    [SerializeField] float bodyRadius = 1.2f;

    [Networked] private int ModeState { get; set; }
    [Networked] private float StateStarted { get; set; }
    [Networked] public int Health { get; private set; }
    [Networked] private NetworkBool Walking { get; set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }

    static readonly int IsWalkingId = Animator.StringToHash("IsWalking");
    static readonly int ActionId = Animator.StringToHash("Action");

    Animator animator;
    Collider bodyCollider;
    Vector3 home;
    Vector3 patrolPoint;
    MarsFusionAvatar target;
    Transform hand;
    GameObject heldRock;
    bool rockLaunched;
    float scanAt;
    float nextThrowAt;
    float nextSuperAt;
    bool animatorReady;
    bool hitApplied;
    bool throwPoseStarted;
    bool deathPoseStarted;

    public Mode CurrentMode => (Mode)ModeState;
    float CurrentTime => Runner != null ? Runner.SimulationTime : Time.time;
    float Elapsed => Mathf.Max(0f, CurrentTime - StateStarted);

    public override void Spawned()
    {
        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<Collider>();
        hand = throwHand != null ? throwHand : FindDeepChild(transform, "B-hand.R");
        home = transform.position;
        EnsureDetectionSphere();
        if (HasStateAuthority)
        {
            Health = maxHealth;
            StateStarted = CurrentTime;
            ModeState = (int)Mode.Idle;
            Walking = false;
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
        }
        ApplyAnimator();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Tick();
        if (Object == null || !Object.IsValid) return;
        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, 1f - Mathf.Exp(-15f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, 1f - Mathf.Exp(-15f * Time.deltaTime));
        }
        ApplyAnimator();
    }

    void SetMode(Mode value)
    {
        if (CurrentMode == value) return;
        StateStarted = CurrentTime;
        ModeState = (int)value;
        hitApplied = false;
    }

    void Tick()
    {
        if (CurrentMode == Mode.Death)
        {
            Walking = false;
            if (bodyCollider != null)
                bodyCollider.enabled = false;
            if (Elapsed >= deathDuration && Object != null && Object.IsValid)
                Runner.Despawn(Object);
            return;
        }

        if (CurrentTime >= scanAt)
        {
            scanAt = CurrentTime + 0.35f;
            ScanForPlayer();
        }

        if (CurrentMode == Mode.Attack || CurrentMode == Mode.Throw || CurrentMode == Mode.Super)
        {
            UpdateAction();
            return;
        }

        if (HasDetectedPlayer())
        {
            float distance = FlatDistance(target.transform.position);
            Face(target.transform.position);
            if (distance <= attackRange)
            {
                Walking = false;
                if (CurrentTime >= nextSuperAt && Random.value <= superChance)
                {
                    nextSuperAt = CurrentTime + superCooldown;
                    SetMode(Mode.Super);
                }
                else
                    SetMode(Mode.Attack);
                return;
            }

            if (distance <= throwRange && CurrentTime >= nextThrowAt)
            {
                nextThrowAt = CurrentTime + throwInterval;
                Walking = false;
                SetMode(Mode.Throw);
                return;
            }

            SetMode(Mode.Chase);
            Chase();
            return;
        }

        if (CurrentMode == Mode.Chase)
            SetMode(Mode.Idle);

        if (CurrentMode == Mode.Idle)
        {
            Walking = false;
            if (Elapsed >= restDuration)
            {
                patrolPoint = PickPatrolPoint();
                SetMode(Mode.Walk);
            }
            return;
        }

        var remaining = patrolPoint - transform.position;
        remaining.y = 0f;
        if (remaining.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            Walking = false;
            SetMode(Mode.Idle);
            return;
        }

        Walking = true;
        MoveTowards(patrolPoint, walkSpeed);
    }

    void Chase()
    {
        var flat = target.transform.position - transform.position;
        flat.y = 0f;
        Face(target.transform.position);
        if (flat.sqrMagnitude <= arriveDistance * arriveDistance)
        {
            Walking = false;
            return;
        }

        // Primero gira en reposo. Cuando ya mira al jugador, empieza a caminar.
        if (Vector3.Angle(transform.forward, flat) > 12f)
        {
            Walking = false;
            return;
        }

        Walking = true;
        MoveTowards(target.transform.position, chaseSpeed);
    }

    void UpdateAction()
    {
        Walking = false;
        if (target != null)
            Face(target.transform.position);

        bool throwing = CurrentMode == Mode.Throw;
        bool super = CurrentMode == Mode.Super;
        float duration = throwing ? throwDuration : super ? superDuration : attackDuration;
        if (!throwing && !hitApplied && Elapsed >= duration * (super ? 0.55f : 0.45f))
        {
            hitApplied = true;
            Strike(super ? superDamage : attackDamage, attackRange + (super ? 3f : 1.5f));
        }

        if (Elapsed >= duration)
            SetMode(HasDetectedPlayer() ? Mode.Chase : Mode.Idle);
    }

    void Strike(int amount, float range)
    {
        if (target == null || !target.IsAlive || amount <= 0) return;
        var delta = target.transform.position - transform.position;
        delta.y = 0f;
        if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) > 70f) return;
        ((ITakeDamage)target).TakeDamage(amount);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || Object == null || !Object.IsValid)
            return;
        RPC_TakeDamage(damage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    void RPC_TakeDamage(int damage)
    {
        if (damage <= 0 || CurrentMode == Mode.Death)
            return;
        Health = Mathf.Max(0, Health - damage);
        if (Health <= 0)
            SetMode(Mode.Death);
    }

    float FlatDistance(Vector3 point)
    {
        var delta = point - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    void EnsureDetectionSphere()
    {
        if (detectionSphere == null)
        {
            var volume = new GameObject("DetectionSphere");
            volume.transform.SetParent(transform, false);
            detectionSphere = volume.AddComponent<SphereCollider>();
        }

        detectionSphere.isTrigger = true;
        detectionSphere.center = Vector3.zero;
        detectionSphere.radius = detectionRange;
    }

    Vector3 DetectionCenter => detectionSphere != null ? detectionSphere.transform.position : transform.position;

    float DetectionRadius
    {
        get
        {
            if (detectionSphere == null) return detectionRange;
            var scale = detectionSphere.transform.lossyScale;
            float scaleMax = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            return detectionSphere.radius * scaleMax;
        }
    }

    bool HasDetectedPlayer()
    {
        if (target == null || !target.IsAlive) return false;
        return (target.transform.position - DetectionCenter).sqrMagnitude <= DetectionRadius * DetectionRadius;
    }

    void ScanForPlayer()
    {
        Vector3 center = DetectionCenter;
        float radiusSqr = DetectionRadius * DetectionRadius;
        target = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(avatar => avatar != null && avatar.Object != null && avatar.Object.IsValid && avatar.Runner == Runner && avatar.IsAlive
                && (avatar.transform.position - center).sqrMagnitude <= radiusSqr)
            .OrderBy(avatar => (avatar.transform.position - center).sqrMagnitude)
            .FirstOrDefault();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.35f);
        var center = detectionSphere != null ? detectionSphere.transform.position : transform.position;
        float radius = detectionSphere != null ? detectionSphere.radius * Mathf.Max(detectionSphere.transform.lossyScale.x, detectionSphere.transform.lossyScale.y, detectionSphere.transform.lossyScale.z) : detectionRange;
        Gizmos.DrawWireSphere(center, radius);
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, throwRange);
    }

    Vector3 PickPatrolPoint()
    {
        Vector2 circle = Random.insideUnitCircle * patrolRadius;
        var point = home + new Vector3(circle.x, 0f, circle.y);
        SnapToTerrain(ref point);
        return point;
    }

    void Face(Vector3 point)
    {
        var direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f) return;
        var targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Runner.DeltaTime);
    }

    void MoveTowards(Vector3 point, float moveSpeed)
    {
        var direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.1f) return;
        direction.Normalize();

        var hits = Physics.SphereCastAll(transform.position + Vector3.up * bodyRadius, bodyRadius, direction, 2f, ~0, QueryTriggerInteraction.Ignore);
        var obstacle = hits.FirstOrDefault(hit => !(hit.collider is TerrainCollider) && !hit.collider.transform.IsChildOf(transform) && (target == null || !hit.collider.transform.IsChildOf(target.transform)));
        if (obstacle.collider != null)
        {
            direction = Vector3.ProjectOnPlane(direction, obstacle.normal);
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.Cross(Vector3.up, obstacle.normal);
            direction.Normalize();
        }

        Face(transform.position + direction);
        var step = Mathf.Min(moveSpeed * Runner.DeltaTime, Vector3.ProjectOnPlane(point - transform.position, Vector3.up).magnitude);
        var next = transform.position + direction * step;
        SnapToTerrain(ref next);
        transform.position = next;
    }

    static void SnapToTerrain(ref Vector3 point)
    {
        float x = point.x;
        float z = point.z;
        var terrain = Terrain.activeTerrains.FirstOrDefault(candidate =>
            x >= candidate.transform.position.x && z >= candidate.transform.position.z &&
            x <= candidate.transform.position.x + candidate.terrainData.size.x &&
            z <= candidate.transform.position.z + candidate.terrainData.size.z);
        if (terrain != null)
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
    }

    void ApplyAnimator()
    {
        if (animator == null) return;
        if (!animatorReady && animator.runtimeAnimatorController != null)
        {
            animatorReady = true;
            animator.SetBool(IsWalkingId, false);
        }
        animator.SetBool(IsWalkingId, Walking);
        animator.SetInteger(ActionId, ActionValue);
        if (CurrentMode == Mode.Death && !deathPoseStarted)
        {
            deathPoseStarted = true;
            animator.Play("Dying", 0, 0f);
        }
        if (CurrentMode == Mode.Throw && !throwPoseStarted)
        {
            throwPoseStarted = true;
            animator.Play("Throw", 0, 0f);
        }
        if (CurrentMode != Mode.Throw)
            throwPoseStarted = false;
        UpdateThrowRock();
    }

    void UpdateThrowRock()
    {
        if (CurrentMode != Mode.Throw)
        {
            if (!rockLaunched && heldRock != null)
                Destroy(heldRock);
            heldRock = null;
            rockLaunched = false;
            return;
        }

        if (rockPrefab == null || hand == null)
            return;

        if (heldRock == null && !rockLaunched)
        {
            heldRock = Instantiate(rockPrefab, hand);
            heldRock.transform.localPosition = rockLocalOffset;
            heldRock.transform.localRotation = Quaternion.identity;
            foreach (var body in heldRock.GetComponentsInChildren<Rigidbody>())
                body.isKinematic = true;
        }
    }

    public void ThrowRock()
    {
        if (rockLaunched || heldRock == null)
            return;
        LaunchRock();
    }

    void LaunchRock()
    {
        rockLaunched = true;
        var rock = heldRock;
        heldRock = null;
        rock.transform.SetParent(null, true);

        Vector3 aim = transform.position + transform.forward * throwRange + Vector3.up * 1.5f;
        if (target != null)
            aim = target.transform.position + Vector3.up * 1.4f;
        Vector3 velocity = (aim - rock.transform.position);
        if (velocity.sqrMagnitude < 0.01f)
            velocity = transform.forward;
        velocity = velocity.normalized * rockSpeed;
        velocity.y += 3f;

        var projectile = rock.AddComponent<GiantGolemRock>();
        projectile.Launch(velocity, throwDamage, HasStateAuthority, GetComponentsInChildren<Collider>());
    }

    static Transform FindDeepChild(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeepChild(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    int ActionValue
    {
        get
        {
            if (CurrentMode == Mode.Death) return 5;
            if (CurrentMode == Mode.Attack) return 2;
            if (CurrentMode == Mode.Throw) return 3;
            if (CurrentMode == Mode.Super) return 4;
            if (CurrentMode == Mode.Walk || (CurrentMode == Mode.Chase && Walking)) return 1;
            return 0;
        }
    }
}

public sealed class GiantGolemRock : MonoBehaviour
{
    int damage;
    bool canDamage;

    public void Launch(Vector3 velocity, int amount, bool dealsDamage, Collider[] ignored)
    {
        damage = amount;
        canDamage = dealsDamage;
        var collider = gameObject.GetComponent<Collider>();
        if (collider == null)
            collider = gameObject.AddComponent<SphereCollider>();
        if (ignored != null)
        {
            foreach (var body in ignored)
                if (body != null) Physics.IgnoreCollision(collider, body);
        }

        var rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.linearVelocity = velocity;
        Destroy(gameObject, 8f);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (canDamage)
        {
            var avatar = collision.collider.GetComponentInParent<MarsFusionAvatar>();
            if (avatar != null && avatar.IsAlive)
            {
                ((ITakeDamage)avatar).TakeDamage(damage);
                canDamage = false;
            }
        }
        Destroy(gameObject, 1.5f);
    }
}
