using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

// El Cuernudo: enemigo grande de Marte.
// La autoridad de Fusion decide IA, vida y golpes; todos los clientes reproducen el estado replicado.
// Ataques: zarpazo cuerpo a cuerpo (Attack) y salto con golpe doble a media distancia (Attack2).
// Todas las distancias y velocidades se multiplican por el tamaño (localScale.x).
public sealed class ElCuernudoFusionEnemy : NetworkBehaviour, ITakeDamage
{
    public enum Mode { Idle, Run, Swipe, Leap, Death, Walk, SwipeLeft }

    [Header("Vida y detección")]
    [SerializeField] float maxHealth = 450, detectionRange = 30, leashRange = 70;
    [SerializeField, Tooltip("Segundos que persigue a quien le disparó, aunque esté lejos o fuera de su zona.")] float aggroDuration = 20f;
    [SerializeField, Tooltip("Distancia máxima a la que sigue persiguiendo al atacante.")] float aggroMaxRange = 140f;

    [Header("Movimiento (por unidad de tamaño)")]
    [SerializeField, Tooltip("m/s persiguiendo, se multiplica por el tamaño.")] float chaseSpeed = 2.6f;
    [SerializeField, Min(.1f)] float acceleration = 4f;
    [SerializeField, Tooltip("Velocidad a la que el clip Run no patina (por unidad de tamaño).")] float runClipSpeed = 2.5f;
    [SerializeField, Tooltip("Velocidad a la que el clip Walk no patina (por unidad de tamaño).")] float walkClipSpeed = .45f;
    [SerializeField, Tooltip("Radio (por unidad de tamaño) en el que deambula caminando cuando no ve al jugador.")] float wanderRadius = 7f;

    [Header("Zarpazo")]
    [SerializeField] float swipeDamage = 25;
    [SerializeField, Tooltip("Por unidad de tamaño.")] float swipeRange = 1.9f;
    [SerializeField, Min(.3f)] float swipeCooldown = .5f;
    [SerializeField, Tooltip("Tiempos del clip a velocidad normal; se dividen por Swipe Speed.")] float swipeHitTime = .63f, swipeDuration = 1.53f;
    [SerializeField, Range(1f, 2.5f), Tooltip("Velocidad de los zarpazos (1 = clip original).")] float swipeSpeed = 1.75f;

    [Header("Salto")]
    [SerializeField] float leapDamage = 40;
    [SerializeField, Tooltip("Por unidad de tamaño.")] float leapMinRange = 2.6f, leapMaxRange = 5.5f, leapHitRadius = 1.6f;
    [SerializeField, Min(1f)] float leapCooldown = 6f;
    [SerializeField] float leapTakeoff = .43f, leapLand = .9f, leapDuration = 1.93f;

    [Header("Camuflaje")]
    [SerializeField, Tooltip("Segundos entre camuflajes (cuenta desde que se revela).")] float cloakCooldown = 8f;
    [SerializeField, Tooltip("Máximo tiempo invisible antes de revelarse solo.")] float cloakMaxDuration = 12f;
    [SerializeField, Tooltip("Solo se camufla si el jugador está más lejos que esto (por unidad de tamaño).")] float cloakMinDistance = 3.5f;
    [SerializeField, Tooltip("Multiplica la velocidad de persecución mientras está invisible (casi aparece al lado del jugador).")] float cloakSpeedMultiplier = 4f;
    [SerializeField, Tooltip("Multiplica la aceleración mientras está invisible: arranca de golpe.")] float cloakAccelerationMultiplier = 6f;

    [Header("Muerte")]
    [SerializeField, Min(1f)] float despawnDelay = 7f;
    [SerializeField, Tooltip("Máximo de segundos que el ragdoll cae libre antes de congelarse y hundirse (si queda quieto antes, se hunde antes).")] float deathSinkStart = 4.2f;
    [SerializeField, Min(.1f)] float deathSinkTime = 2.5f;
    [SerializeField, Tooltip("Velocidad horizontal (m/s) con la que el ragdoll sale despedido por el golpe final.")] float deathPushSpeed = 5f;
    [SerializeField, Tooltip("Velocidad hacia arriba (m/s) del golpe final.")] float deathPushUp = 2.5f;

    [Networked] public float Health { get; private set; }
    [Networked] private int ModeState { get; set; }
    [Networked] private float StateStarted { get; set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }
    [Networked] public NetworkBool Cloaked { get; private set; }
    [Networked] private Vector3 DeathPush { get; set; }
    [Networked] private float CloakStarted { get; set; }

    Animator animator;
    Transform visual;
    Quaternion visualRestRotation;
    Vector3 visualRestPosition, home, leapStart, leapEnd;
    Collider bodyCollider;
    MarsFusionAvatar target;
    float scanAt, nextAttackAt, nextLeapAt, currentSpeed, visualSpeed;
    bool hitApplied;
    MarsFusionAvatar aggroTarget;
    float aggroUntil;
    ElCuernudoCloakFX cloakFx;
    float cloakReadyAt;
    int shown = -1;
    Vector3 lastRenderPosition, wanderPoint;
    bool hasWanderPoint;
    float wanderGiveUpAt;

    public Mode CurrentMode => (Mode)ModeState;
    public bool IsAlive => Object != null && Object.IsValid && CurrentMode != Mode.Death;
    float Size => Mathf.Max(.1f, transform.localScale.x);
    float Now => Runner.SimulationTime;
    float Elapsed => Mathf.Max(0, Now - StateStarted);

    AudioSource monsterVoice;

    void SetupMonsterVoice()
    {
        var clip = Resources.Load<AudioClip>("Audio/Monsters/ElCuernudo");
        if (clip == null) return;
        monsterVoice = gameObject.AddComponent<AudioSource>();
        monsterVoice.playOnAwake = false;
        monsterVoice.clip = clip;
        monsterVoice.volume = .7f;
        monsterVoice.spatialBlend = 1f;
        monsterVoice.rolloffMode = AudioRolloffMode.Linear;
        monsterVoice.minDistance = 4f;
        monsterVoice.maxDistance = 45f;
        monsterVoice.dopplerLevel = 0f;
    }

    void PlayMonsterVoice()
    {
        if (monsterVoice != null && !monsterVoice.isPlaying && !SceneTransitionVideo.IsPresent) monsterVoice.Play();
    }

    public override void Spawned()
    {
        SetupMonsterVoice();
        animator = GetComponentInChildren<Animator>();
        visual = animator != null ? animator.transform : transform;
        visualRestPosition = visual.localPosition;
        visualRestRotation = visual.localRotation;
        bodyCollider = GetComponent<Collider>();
        home = transform.position;
        lastRenderPosition = transform.position;
        cloakFx = GetComponent<ElCuernudoCloakFX>();
        if (cloakFx == null) cloakFx = gameObject.AddComponent<ElCuernudoCloakFX>();
        cloakFx.Init(Size);
        if (HasStateAuthority)
        {
            cloakReadyAt = Now + 3f;
            Health = maxHealth;
            ModeState = (int)Mode.Idle;
            StateStarted = Now;
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            nextLeapAt = Now + 2f;
        }
        RenderState();
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "EL CUERNUDO", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid && CurrentMode != Mode.Death && !Cloaked);
    }

    // ------------------------------------------------------------------ IA (autoridad)

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Tick();
        if (Object == null || !Object.IsValid) return;
        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    void SetMode(Mode value)
    {
        if (CurrentMode == value) return;
        // Sale del camuflaje de golpe justo cuando ataca (o muere).
        if (IsSwipe(value) || value == Mode.Leap || value == Mode.Death) Reveal();
        ModeState = (int)value;
        StateStarted = Now;
        hitApplied = false;
    }

    void Tick()
    {
        var mode = CurrentMode;
        if (mode == Mode.Death)
        {
            if (Elapsed > despawnDelay) Runner.Despawn(Object);
            return;
        }

        if (Now >= scanAt)
        {
            scanAt = Now + .35f;
            ScanForTarget();
        }

        if (IsSwipe(mode)) { TickSwipe(); return; }
        if (mode == Mode.Leap) { TickLeap(); return; }

        bool hasTarget = target != null && target.IsAlive
            && (Distance(target.transform.position) <= detectionRange || (target == aggroTarget && Aggro));
        if (Cloaked && (!hasTarget || Now - CloakStarted > cloakMaxDuration)) Reveal();
        if (!hasTarget) { Wander(); return; }
        if (!Cloaked && Now >= cloakReadyAt && Distance(target.transform.position) > cloakMinDistance * Size)
        {
            Cloaked = true;
            CloakStarted = Now;
        }

        // Detectó al jugador: corre hacia él.
        float distance = Distance(target.transform.position);
        Face(target.transform.position);
        // Invisible no salta: se acerca a toda velocidad y aparece al lado con el zarpazo.
        bool leapReady = !Cloaked && Now >= nextLeapAt && Now >= nextAttackAt;
        if (leapReady && distance >= leapMinRange * Size && distance <= leapMaxRange * Size)
        {
            SetMode(Mode.Leap);
            return;
        }
        float r = swipeRange * Size;
        if (distance <= r)
        {
            currentSpeed = 0f;
            if (Now >= nextAttackAt || Cloaked) StartSwipe(); // invisible: ataca apenas llega
            else SetMode(Mode.Idle);
            return;
        }
        float desired, accel;
        if (Cloaked)
        {
            // Invisible: embestida a toda velocidad, solo frena en el último tramo para no pasarse.
            desired = chaseSpeed * Size * cloakSpeedMultiplier * Mathf.Clamp((distance - r) / (r * 2f) + .3f, .35f, 1f);
            accel = acceleration * Size * cloakAccelerationMultiplier;
        }
        else
        {
            // Frena un poco al llegar, pero sin dejar de correr.
            desired = chaseSpeed * Size * Mathf.Clamp((distance - r * .8f) / (r * 1.5f) + .45f, .6f, 1f);
            accel = acceleration * Size;
        }
        currentSpeed = Mathf.MoveTowards(currentSpeed, desired, accel * Runner.DeltaTime);
        SetMode(Mode.Run);
        // Nunca avanza más de lo que falta para quedar a distancia de zarpazo.
        float maxStep = Mathf.Max(0f, distance - r * .9f) / Mathf.Max(.0001f, Runner.DeltaTime);
        MoveTowards(target.transform.position, Mathf.Min(currentSpeed, maxStep));
    }

    void Reveal()
    {
        if (!HasStateAuthority || !Cloaked) return;
        Cloaked = false;
        cloakReadyAt = Now + cloakCooldown;
    }

    // Sin jugador a la vista: camina sin parar entre puntos al azar alrededor de su zona.
    void Wander()
    {
        bool needPoint = !hasWanderPoint || Distance(wanderPoint) < .8f * Size || Now >= wanderGiveUpAt;
        if (needPoint) PickWanderPoint();
        float desired = walkClipSpeed * Size;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desired, acceleration * Size * Runner.DeltaTime);
        SetMode(Mode.Walk);
        MoveTowards(wanderPoint, currentSpeed);
    }

    void PickWanderPoint()
    {
        // Prefiere puntos por delante para que el paseo se vea natural y no zigzaguee.
        for (int i = 0; i < 8; i++)
        {
            float angle = Random.Range(-100f, 100f);
            float dist = Random.Range(.4f, 1f) * wanderRadius * Size;
            var dir = Quaternion.Euler(0, angle, 0) * transform.forward;
            var candidate = transform.position + dir * dist;
            if (Vector3.Distance(candidate, home) > wanderRadius * Size)
                candidate = home + (candidate - home).normalized * wanderRadius * Size * Random.Range(.3f, .9f);
            candidate.y = GroundHeight(candidate);
            wanderPoint = candidate;
            if (i > 3 || Vector3.Distance(candidate, transform.position) > 2f * Size) break;
        }
        hasWanderPoint = true;
        wanderGiveUpAt = Now + Distance(wanderPoint) / Mathf.Max(.1f, walkClipSpeed * Size) + 4f;
    }

    bool IsSwipe(Mode m) => m == Mode.Swipe || m == Mode.SwipeLeft;
    bool nextSwipeLeft;

    // Combo: alterna mano derecha y mano izquierda.
    void StartSwipe()
    {
        SetMode(nextSwipeLeft ? Mode.SwipeLeft : Mode.Swipe);
        nextSwipeLeft = !nextSwipeLeft;
    }

    void TickSwipe()
    {
        float speed = Mathf.Max(.1f, swipeSpeed);
        if (Elapsed < swipeHitTime / speed * .8f && target != null) Face(target.transform.position);
        if (!hitApplied && Elapsed >= swipeHitTime / speed)
        {
            hitApplied = true;
            if (InFrontAndClose(swipeRange * Size + .8f, 85f)) target.TakeMonsterDamage(Mathf.CeilToInt(swipeDamage));
        }
        if (Elapsed >= swipeDuration / speed)
        {
            nextAttackAt = Now + swipeCooldown;
            currentSpeed = 0f;
            SetMode(Mode.Idle);
        }
    }

    void TickLeap()
    {
        float t = Elapsed;
        if (t < leapTakeoff)
        {
            // Durante la carga apunta y calcula dónde caer (justo delante del jugador).
            if (target != null)
            {
                Face(target.transform.position);
                var to = target.transform.position - transform.position; to.y = 0;
                leapStart = transform.position;
                leapEnd = transform.position + to.normalized * Mathf.Max(0f, to.magnitude - swipeRange * Size * .7f);
                leapEnd.y = GroundHeight(leapEnd);
            }
            else { leapStart = leapEnd = transform.position; }
        }
        else if (t < leapLand)
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(leapTakeoff, leapLand, t));
            var p = Vector3.Lerp(leapStart, leapEnd, k);
            p.y = Mathf.Lerp(leapStart.y, leapEnd.y, k);
            transform.position = p;
        }
        else if (!hitApplied)
        {
            hitApplied = true;
            transform.position = leapEnd;
            var impact = transform.position + transform.forward * .9f * Size;
            if (target != null && target.IsAlive)
            {
                var d = target.transform.position - impact; d.y = 0;
                if (d.magnitude <= leapHitRadius * Size) target.TakeMonsterDamage(Mathf.CeilToInt(leapDamage));
            }
        }
        if (t >= leapDuration)
        {
            nextLeapAt = Now + leapCooldown;
            nextAttackAt = Now + .6f;
            currentSpeed = 0f;
            SetMode(Mode.Idle);
        }
    }

    bool InFrontAndClose(float range, float maxAngle)
    {
        if (target == null || !target.IsAlive) return false;
        var delta = target.transform.position - transform.position; delta.y = 0;
        return delta.magnitude <= range && Vector3.Angle(transform.forward, delta) <= maxAngle;
    }

    bool Aggro => aggroTarget != null && aggroTarget.Object != null && aggroTarget.Object.IsValid
        && aggroTarget.IsAlive && Now < aggroUntil && Distance(aggroTarget.transform.position) <= aggroMaxRange;

    void ScanForTarget()
    {
        if (Aggro) { target = aggroTarget; return; }
        aggroTarget = null;
        target = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive
                && Vector3.Distance(home, a.transform.position) < leashRange)
            .OrderBy(a => (a.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    float Distance(Vector3 point)
    {
        var d = point - transform.position; d.y = 0;
        return d.magnitude;
    }

    void Face(Vector3 point)
    {
        var d = point - transform.position; d.y = 0;
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 200 * Runner.DeltaTime);
    }

    void MoveTowards(Vector3 point, float moveSpeed)
    {
        var d = point - transform.position; d.y = 0;
        if (d.sqrMagnitude < .1f) return;
        d.Normalize();
        float radius = .45f * Size;
        var hits = Physics.SphereCastAll(transform.position + Vector3.up * radius * 2f, radius, d, 1.5f * Size, ~0, QueryTriggerInteraction.Ignore);
        var obstacle = hits.FirstOrDefault(h => !(h.collider is TerrainCollider) && !h.collider.transform.IsChildOf(transform)
            && (target == null || !h.collider.transform.IsChildOf(target.transform)));
        if (obstacle.collider != null)
        {
            d = Vector3.ProjectOnPlane(d, obstacle.normal); d.y = 0;
            if (d.sqrMagnitude < .01f) d = Vector3.Cross(Vector3.up, obstacle.normal);
            d.Normalize();
        }
        Face(transform.position + d);
        var next = transform.position + d * moveSpeed * Runner.DeltaTime;
        next.y = GroundHeight(next);
        transform.position = next;
    }

    float GroundHeight(Vector3 point)
    {
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => point.x >= t.transform.position.x && point.z >= t.transform.position.z
            && point.x <= t.transform.position.x + t.terrainData.size.x && point.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain != null) return terrain.SampleHeight(point) + terrain.transform.position.y;
        float best = float.MaxValue, y = point.y;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 4f, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
            best = hit.distance; y = hit.point.y;
        }
        return y;
    }

    // ------------------------------------------------------------------ Daño recibido

    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        RPC_TakeDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int amount, RpcInfo info = default)
    {
        if (amount <= 0 || CurrentMode == Mode.Death) return;
        RememberAttacker(info.Source);
        float previous = Health;
        Reveal(); // un disparo lo saca del camuflaje
        Health = Mathf.Max(0, Health - Mathf.Min(amount, maxHealth));
        RPC_DamageFeedback(Health <= 0, previous - Health);
        if (Health <= 0)
        {
            // Sale despedido alejándose del jugador más cercano (normalmente quien lo mató).
            Vector3 away = -transform.forward;
            ScanForTarget();
            if (target != null)
            {
                away = transform.position - target.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < .01f) away = -transform.forward;
            }
            DeathPush = away.normalized * deathPushSpeed + Vector3.up * deathPushUp;
            SetMode(Mode.Death);
        }
    }

    // Quien le disparó pasa a ser su objetivo: lo detecta y lo persigue aunque esté lejos.
    void RememberAttacker(PlayerRef source)
    {
        MarsFusionAvatar shooter = null;
        var avatars = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive).ToArray();
        if (source != PlayerRef.None)
        {
            bool shared = Runner.GameMode == GameMode.Shared;
            shooter = avatars.FirstOrDefault(a => (shared ? a.Object.StateAuthority : a.Object.InputAuthority) == source)
                ?? avatars.FirstOrDefault(a => a.Object.InputAuthority == source || a.Object.StateAuthority == source);
        }
        // Si no se sabe quién fue, el jugador vivo más cercano.
        if (shooter == null)
            shooter = avatars.OrderBy(a => (a.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
        if (shooter == null) return;
        aggroTarget = shooter;
        aggroUntil = Now + aggroDuration;
        target = shooter;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DamageFeedback(bool lethal, float actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        var blood = GetComponent<GameJam.Combat.EnemyBloodFx>();
        if (blood == null) return;
        var chest = transform.position + Vector3.up * 1.1f * Size;
        blood.Bleed(chest, transform.forward);
        if (lethal) blood.Bleed(chest, -transform.forward);
    }

    // ------------------------------------------------------------------ Visual (todos los clientes)

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, 1 - Mathf.Exp(-15 * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, 1 - Mathf.Exp(-15 * Time.deltaTime));
        }
        if (cloakFx != null)
        {
            if (CurrentMode == Mode.Death) cloakFx.SetCloaked(false);
            else cloakFx.SetCloaked(Cloaked);
        }
        RenderState();
    }

    static string StateName(Mode m)
    {
        switch (m)
        {
            case Mode.Run: return "Run";
            case Mode.Walk: return "Walk";
            case Mode.Swipe: return "Attack";
            case Mode.SwipeLeft: return "AttackL";
            case Mode.Leap: return "Attack2";
            default: return "Idle";
        }
    }

    void RenderState()
    {
        if (animator == null) return;
        var mode = CurrentMode;

        if (shown != ModeState)
        {
            shown = ModeState;
            if (IsSwipe(mode) || mode == Mode.Leap) PlayMonsterVoice();
            if (mode == Mode.Death)
            {
                if (monsterVoice != null) monsterVoice.Stop();
                if (bodyCollider != null) bodyCollider.enabled = false;
                visual.localPosition = visualRestPosition;
                visual.localRotation = visualRestRotation;
                if (ragdoll == null) ragdoll = GetComponent<ElCuernudoRagdoll>();
                if (ragdoll == null) ragdoll = gameObject.AddComponent<ElCuernudoRagdoll>();
                ragdoll.Activate(Size, DeathPush);
            }
            else
            {
                float offset = IsSwipe(mode) ? Elapsed * swipeSpeed : mode == Mode.Leap ? Elapsed : 0f;
                bool moving = mode == Mode.Run || mode == Mode.Walk;
                animator.CrossFadeInFixedTime(StateName(mode), moving ? .25f : .1f, 0, offset);
            }
        }

        if (mode == Mode.Death)
        {
            // Ragdoll: cae con física; cuando queda quieto se congela y se hunde despacio en la arena.
            if (ragdoll != null && ragdoll.IsActive)
            {
                if (!ragdoll.IsFrozen && ragdoll.HasSettled(1.8f, deathSinkStart))
                {
                    ragdoll.Freeze();
                    sinkStartedAt = Time.time;
                }
                if (ragdoll.IsFrozen)
                {
                    float sink = Mathf.SmoothStep(0f, 1f, (Time.time - sinkStartedAt) / deathSinkTime);
                    visual.localPosition = visualRestPosition + Vector3.down * 1.2f * sink;
                }
            }
            return;
        }

        visual.localPosition = visualRestPosition + Vector3.up * SlopeFootOffset();
        visual.localRotation = visualRestRotation;
        MatchRunSpeed(mode);
    }

    float slopeOffset;
    ElCuernudoRagdoll ragdoll;
    float sinkStartedAt;

    // En lomas el centro queda apoyado pero el pie de la bajada flota: baja el cuerpo hacia el suelo
    // más bajo bajo los pies (adelante/atrás/costados). Devuelve el desplazamiento en espacio local.
    float SlopeFootOffset()
    {
        float s = Size;
        Vector3 p = transform.position;
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized * .35f * s;
        Vector3 r = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized * .22f * s;
        float center = GroundHeight(p);
        float low = Mathf.Min(GroundHeight(p + f), GroundHeight(p - f), GroundHeight(p + r), GroundHeight(p - r));
        float wanted = Mathf.Clamp((low - center) * .8f, -.6f * s, 0f) / s;
        slopeOffset = Mathf.Lerp(slopeOffset, wanted, 1f - Mathf.Exp(-12f * Time.deltaTime));
        return slopeOffset;
    }

    float deathSlopePitch, deathGroundOffset;

    // Cae hacia adelante y queda tendido ~1.6 m (por unidad de tamaño): mide el suelo donde va a quedar
    // la cabeza y donde quedan los pies para inclinar el cuerpo con el terreno y que no flote en bajadas.
    void FitDeathToSlope()
    {
        float s = Size;
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float center = GroundHeight(transform.position);
        float front = GroundHeight(transform.position + fwd * 1.6f * s);
        float back = GroundHeight(transform.position - fwd * .4f * s);
        float run = 2f * s;
        deathSlopePitch = Mathf.Clamp(Mathf.Atan2(back - front, run) * Mathf.Rad2Deg, -25f, 25f);
        // Baja al punto más bajo para que nada quede colgando (en local, dividido por la escala).
        deathGroundOffset = (Mathf.Min(center, front, back) - center) / s;
    }

    // El ritmo de las piernas sigue a la velocidad real para que no patine.
    void MatchRunSpeed(Mode mode)
    {
        if (Time.deltaTime <= 0f) return;
        var delta = transform.position - lastRenderPosition; delta.y = 0;
        lastRenderPosition = transform.position;
        visualSpeed = Mathf.Lerp(visualSpeed, delta.magnitude / Time.deltaTime, 1f - Mathf.Exp(-10f * Time.deltaTime));
        // Invisible puede ir muy rápido: las piernas se aceleran más para que las huellas sigan el ritmo.
        if (mode == Mode.Run) animator.speed = Mathf.Clamp(visualSpeed / (runClipSpeed * Size), .7f, Cloaked ? 3.2f : 1.4f);
        else if (mode == Mode.Walk) animator.speed = Mathf.Clamp(visualSpeed / (walkClipSpeed * Size), .5f, 1.6f);
        else if (IsSwipe(mode)) animator.speed = swipeSpeed;
        else animator.speed = 1f;
    }
}
