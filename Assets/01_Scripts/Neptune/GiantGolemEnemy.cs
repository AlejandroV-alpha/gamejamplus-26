using System.Linq;
using FirstGearGames.SmoothCameraShaker;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

// La autoridad de Fusion decide reposo, patrulla y persecucion. El resto de clientes reproduce la pose replicada.
public sealed class GiantGolemEnemy : NetworkBehaviour, ITakeDamage, IStateAuthorityChanged
{
    public enum Mode { Idle, Walk, Chase, Attack, Throw, Super, Death }

    [SerializeField] int maxHealth = 200;
    [SerializeField] float deathDuration = 4.4f;
    [SerializeField] SphereCollider detectionSphere;
    [SerializeField] SphereCollider swipeSphere;
    [SerializeField] float detectionRange = 28f;
    [SerializeField] float attackRange = 6f;
    [SerializeField] float swipeRange = 3.5f;
    [SerializeField] float throwRange = 16f;
    [SerializeField] int attackDamage = 20;
    [SerializeField] int superDamage = 40;
    [SerializeField, Range(0f, 1f)] float superChance = 0.65f;
    [SerializeField] float superCooldown = 5f;
    [SerializeField] int throwDamage = 14;
    [SerializeField] float throwInterval = 10f;
    [SerializeField] float attackDuration = 2.67f;
    [SerializeField] float superDuration = 3.7f;
    [SerializeField] float throwDuration = 3.83f;
    [SerializeField] GameObject rockPrefab;
    [SerializeField] Transform throwHand;
    [SerializeField] Vector3 rockLocalOffset = new Vector3(0.05f, 0.12f, 0.2f);
    [SerializeField] float rockSpeed = 45f;
    [SerializeField] AudioClip rockGroundClip;
    [SerializeField] AudioClip rockImpactClip;
    [SerializeField] float walkSpeed = 2.4f;
    [SerializeField] float chaseSpeed = 3.6f;
    [SerializeField] float patrolRadius = 22f;
    [SerializeField] bool patrolWholeTerrain = true;
    [SerializeField, Range(5f, 45f)] float maximumPatrolSlope = 35f;
    Terrain patrolTerrain;
    ExplorationZone patrolZone;
    int patrolSector;
    float progressCheckAt;
    Vector3 progressPosition;
    readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    [SerializeField] float restDuration = 4f;
    [SerializeField] float arriveDistance = 2f;
    [SerializeField] float turnSpeed = 70f;
    [SerializeField] float bodyRadius = 1.2f;
    [SerializeField] ShakeData stepShake;
    [SerializeField] AudioClip[] stepClips;
    [SerializeField] AudioClip jumpClip;
    [SerializeField] AudioClip landClip;
    [SerializeField] AudioClip swipeClip;
    [SerializeField] AudioClip[] roarClips;
    [SerializeField] float roarGap = 8f;
    [SerializeField] GameObject groundBurstEffect;
    [SerializeField] Transform effectPoint;
    [SerializeField] float groundEffectLifetime = 3.5f;
    [SerializeField] float stepVolume = 1f;
    [SerializeField] float stepRange = 40f;

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
    float nextRoarAt;
    int roarIndex;
    bool animatorReady;
    bool hitApplied;
    bool throwPoseStarted;
    bool superPoseStarted;
    bool attackPoseStarted;
    bool deathPoseStarted;
    AudioSource stepAudio;

    public Mode CurrentMode => (Mode)ModeState;
    float CurrentTime => Runner != null ? Runner.SimulationTime : Time.time;
    float Elapsed => Mathf.Max(0f, CurrentTime - StateStarted);

    public override void Spawned()
    {
        animator = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<Collider>();
        hand = throwHand != null ? throwHand : FindDeepChild(transform, "B-hand.R");
        if (effectPoint == null)
            effectPoint = FindDeepChild(transform, "B-root");
        patrolZone = ExplorationZone.ForPlayer(this);
        patrolTerrain = patrolZone != null ? patrolZone.terrain : null;
        if (patrolTerrain == null)
            patrolTerrain = Terrain.activeTerrains.FirstOrDefault(t => t.gameObject.scene == gameObject.scene);
        Vector3 grounded = transform.position;
        SnapToTerrain(ref grounded);
        transform.position = grounded;
        home = transform.position;
        progressPosition = home;
        progressCheckAt = CurrentTime + 4f;
        patrolSector = Random.Range(0, 9);
        nextRoarAt = Time.time + roarGap;
        EnsureDetectionSphere();
        EnsureSwipeSphere();
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
        UpdateRoar();
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
            if (super)
                Strike(superDamage, attackRange + 3f);
            else if (IsInsideSwipeSphere(target))
                target.TakeMonsterDamage(attackDamage);
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
        target.TakeMonsterDamage(amount);
    }

    public void FirstStep()
    {
        PlayStep();
    }

    public void SecondStep()
    {
        PlayStep();
    }

    public void StartJump()
    {
        PlayGolemClip(jumpClip, 1f);
    }

    public void SwipSound()
    {
        PlayGolemClip(swipeClip, 1f);
    }

    void UpdateRoar()
    {
        bool calm = CurrentMode == Mode.Idle || CurrentMode == Mode.Walk || CurrentMode == Mode.Chase;
        if (!calm || roarClips == null || roarClips.Length == 0)
            return;
        if (Time.time < nextRoarAt)
            return;
        var clip = roarClips[roarIndex % roarClips.Length];
        roarIndex++;
        if (clip == null)
        {
            nextRoarAt = Time.time + roarGap;
            return;
        }
        PlayGolemClip(clip, 1f);
        nextRoarAt = Time.time + clip.length + roarGap;
    }

    public void GroundHit()
    {
        PlayGolemClip(landClip, 1f);
        ShakeView(0.8f);
        SpawnGroundEffects();
    }

    void SpawnGroundEffects()
    {
        var origin = effectPoint != null ? effectPoint : transform;
        SpawnEffect(groundBurstEffect, origin.position, transform.rotation);
    }

    void SpawnEffect(GameObject prefab, Vector3 point, Quaternion rotation)
    {
        if (prefab == null)
            return;
        var effect = Instantiate(prefab, point, rotation);
        Destroy(effect, groundEffectLifetime);
    }

    void PlayStep()
    {
        if (stepClips != null && stepClips.Length > 0)
            PlayGolemClip(stepClips[Random.Range(0, stepClips.Length)], Random.Range(0.62f, 0.78f));
        ShakeView(0.35f);
    }

    void PlayGolemClip(AudioClip clip, float pitch)
    {
        if (clip == null)
            return;
        if (stepAudio == null)
        {
            stepAudio = gameObject.AddComponent<AudioSource>();
            stepAudio.spatialBlend = 1f;
            stepAudio.rolloffMode = AudioRolloffMode.Linear;
            stepAudio.minDistance = 5f;
            stepAudio.maxDistance = stepRange;
            stepAudio.playOnAwake = false;
            stepAudio.dopplerLevel = 0f;
        }
        stepAudio.pitch = pitch;
        stepAudio.PlayOneShot(clip, stepVolume);
    }

    void ShakeView(float amount)
    {
        if (stepShake == null)
            return;
        var view = Camera.main;
        if (view == null)
            return;
        float distance = Vector3.Distance(view.transform.position, transform.position);
        float strength = (1f - Mathf.Clamp01(distance / stepRange)) * amount;
        if (strength <= 0.05f)
            return;
        if (view.GetComponent<CameraShaker>() == null)
            view.gameObject.AddComponent<CameraShaker>();
        var instance = CameraShakerHandler.Shake(stepShake);
        if (instance != null)
            instance.MultiplyMagnitude(strength, 0f, false);
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

    void EnsureSwipeSphere()
    {
        if (swipeSphere == null)
        {
            var volume = new GameObject("SwipeDetectionSphere");
            volume.transform.SetParent(transform, false);
            volume.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            swipeSphere = volume.AddComponent<SphereCollider>();
        }

        swipeSphere.isTrigger = true;
        swipeSphere.center = Vector3.zero;
        swipeSphere.radius = swipeRange;
    }

    bool IsInsideSwipeSphere(MarsFusionAvatar avatar)
    {
        if (avatar == null || !avatar.IsAlive || swipeSphere == null)
            return false;
        var scale = swipeSphere.transform.lossyScale;
        float radius = swipeSphere.radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        return (avatar.transform.position - swipeSphere.transform.position).sqrMagnitude <= radius * radius;
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
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        if (swipeSphere != null)
        {
            var swipeScale = swipeSphere.transform.lossyScale;
            float swipeRadius = swipeSphere.radius * Mathf.Max(swipeScale.x, Mathf.Max(swipeScale.y, swipeScale.z));
            Gizmos.DrawWireSphere(swipeSphere.transform.position, swipeRadius);
        }
        else
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.2f, swipeRange);
        Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, throwRange);
    }

    public void StateAuthorityChanged()
    {
        if (!HasStateAuthority) return;
        // Route selection is local to the authority; start a fresh route after migration.
        progressPosition = transform.position;
        progressCheckAt = CurrentTime + 4f;
        if (CurrentMode == Mode.Walk) SetMode(Mode.Idle);
    }

    Vector3 PickPatrolPoint()
    {
        progressPosition = transform.position;
        progressCheckAt = CurrentTime + 4f;
        if (patrolWholeTerrain && patrolTerrain != null && patrolTerrain.terrainData != null)
        {
            var origin = patrolTerrain.transform.position;
            var size = patrolTerrain.terrainData.size;
            float margin = Mathf.Max(12f, bodyRadius * 3f);
            // Step four is coprime with nine: every sector is visited before repeating.
            for (int sectorTry = 0; sectorTry < 9; sectorTry++)
            {
                patrolSector = (patrolSector + 4) % 9;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float u = (patrolSector % 3 + Random.Range(.12f, .88f)) / 3f;
                    float v = (patrolSector / 3 + Random.Range(.12f, .88f)) / 3f;
                    var candidate = origin + new Vector3(Mathf.Lerp(margin, size.x - margin, u), 0,
                        Mathf.Lerp(margin, size.z - margin, v));
                    if (!IsWalkableTerrain(candidate)) continue;
                    SnapToTerrain(ref candidate);
                    return candidate;
                }
            }
            return transform.position;
        }
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

    bool IsWalkableTerrain(Vector3 point)
    {
        if (patrolZone != null && !patrolZone.Contains(point)) return false;
        if (patrolTerrain == null || patrolTerrain.terrainData == null) return true;
        Vector3 p = point - patrolTerrain.transform.position;
        var size = patrolTerrain.terrainData.size;
        float margin = Mathf.Max(3f, bodyRadius);
        return p.x >= margin && p.z >= margin && p.x <= size.x - margin && p.z <= size.z - margin
            && patrolTerrain.terrainData.GetSteepness(p.x / size.x, p.z / size.z) <= maximumPatrolSlope;
    }

    void MoveTowards(Vector3 point, float moveSpeed)
    {
        if (CurrentMode == Mode.Walk && CurrentTime >= progressCheckAt)
        {
            if ((transform.position - progressPosition).sqrMagnitude < .25f)
            { patrolPoint = PickPatrolPoint(); return; }
            progressPosition = transform.position;
            progressCheckAt = CurrentTime + 4f;
        }
        var direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.1f) return;
        direction.Normalize();

        int hitCount = Physics.SphereCastNonAlloc(transform.position + Vector3.up * bodyRadius, bodyRadius,
            direction, obstacleHits, 2f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit obstacle = default;
        float closest = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            var hit = obstacleHits[i];
            if (hit.collider is TerrainCollider || hit.collider.transform.IsChildOf(transform)
                || (target != null && hit.collider.transform.IsChildOf(target.transform)) || hit.distance >= closest) continue;
            obstacle = hit; closest = hit.distance;
        }
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
        if (!IsWalkableTerrain(next))
        {
            if (CurrentMode == Mode.Walk) patrolPoint = PickPatrolPoint();
            return;
        }
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
        if (CurrentMode == Mode.Super && !superPoseStarted)
        {
            superPoseStarted = true;
            animator.Play("Mutant Jump Attack", 0, 0f);
        }
        if (CurrentMode != Mode.Super)
            superPoseStarted = false;
        if (CurrentMode == Mode.Attack && !attackPoseStarted)
        {
            attackPoseStarted = true;
            animator.Play("Attack", 0, 0f);
        }
        if (CurrentMode != Mode.Attack)
            attackPoseStarted = false;
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
        projectile.Launch(velocity, throwDamage, HasStateAuthority, GetComponentsInChildren<Collider>(), rockGroundClip, rockImpactClip);
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
    bool groundSoundPlayed;
    AudioClip groundClip;
    AudioClip impactClip;

    public void Launch(Vector3 velocity, int amount, bool dealsDamage, Collider[] ignored, AudioClip ground, AudioClip impact)
    {
        damage = amount;
        canDamage = dealsDamage;
        groundClip = ground;
        impactClip = impact;
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
                avatar.TakeMonsterDamage(damage);
                canDamage = false;
            }
        }

        if (!groundSoundPlayed && IsGround(collision))
        {
            groundSoundPlayed = true;
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            RockGroundAudio.PlayAt(point, groundClip, impactClip);
        }
        Destroy(gameObject, 1.5f);
    }

    static bool IsGround(Collision collision)
    {
        if (collision.collider.GetComponentInParent<MarsFusionAvatar>() != null)
            return false;
        if (collision.collider is TerrainCollider)
            return true;
        return collision.contactCount > 0 && collision.GetContact(0).normal.y > 0.45f;
    }
}

public sealed class RockGroundAudio : MonoBehaviour
{
    public static void PlayAt(Vector3 point, AudioClip rolling, AudioClip impact)
    {
        if (rolling == null && impact == null)
            return;
        var host = new GameObject("RockGroundAudio");
        host.transform.position = point;
        var source = host.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 4f;
        source.maxDistance = 40f;
        source.playOnAwake = false;
        float duration = 0.1f;
        if (rolling != null)
        {
            source.PlayOneShot(rolling);
            duration = Mathf.Max(duration, rolling.length);
        }
        if (impact != null)
        {
            source.PlayOneShot(impact);
            duration = Mathf.Max(duration, impact.length);
        }
        Destroy(host, duration + 0.1f);
    }
}
