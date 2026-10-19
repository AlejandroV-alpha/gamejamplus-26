using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using JungleNightmare;
using UnityEngine;

// Enemigo de Neptuno: la autoridad de Fusion decide IA, vida y golpes;
// todos los clientes reproducen ataque, dolor y muerte con FrostActor.
public sealed class JungleNightmareFusionEnemy : NetworkBehaviour, ITakeDamage
{
    [SerializeField] float maxHealth = 120, detectionRange = 24, leashRange = 50, attackRange = 3f, damage = 15;
    [SerializeField, Tooltip("m/s persiguiendo (se multiplica por el tamaño).")] float chaseSpeed = 2.6f;
    [SerializeField] float patrolSpeed = 1.2f;
    [SerializeField, Min(.1f)] float acceleration = 5f;
    [SerializeField, Tooltip("Velocidad real a la que el clip Run no patina (por unidad de tamaño).")] float runClipSpeed = 1.8f;
    [SerializeField, Min(0.5f)] float attackCooldown = 2.3f;
    [SerializeField, Min(0f)] float attackImpactDelay = 0.87f;
    [SerializeField, Min(1f)] float despawnDelay = 5f;
    [SerializeField, Tooltip("Escala minima para que el golpe congele (el grande es 2.3, el mediano 1.5).")]
    float freezeMinScale = 2f;

    [Networked] public float Health { get; private set; }
    [Networked] public NetworkBool Dead { get; private set; }
    [Networked] private int AttackCount { get; set; }
    [Networked] private int HitCount { get; set; }
    [Networked] private float DeathTime { get; set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }

    FrostActor actor;
    Collider bodyCollider;
    Vector3 home;
    MarsFusionAvatar target;
    float scanAt, nextAttackAt, impactAt = -1;
    int shownAttacks, shownHits;
    bool shownDead;

    public bool IsAlive => Object != null && Object.IsValid && !Dead;

    AudioSource monsterVoice;

    void SetupMonsterVoice()
    {
        var clip = Resources.Load<AudioClip>("Audio/Monsters/JungleNightmare");
        if (clip == null) return;
        monsterVoice = gameObject.AddComponent<AudioSource>();
        monsterVoice.playOnAwake = false;
        monsterVoice.loop = false;
        monsterVoice.clip = clip;
        monsterVoice.volume = .6f;
        monsterVoice.spatialBlend = 1f;
        monsterVoice.rolloffMode = AudioRolloffMode.Linear;
        monsterVoice.minDistance = 3f;
        monsterVoice.maxDistance = 35f;
        monsterVoice.dopplerLevel = 0f;
        monsterVoice.priority = 128;
    }

    void PlayMonsterVoice()
    {
        if (monsterVoice != null && !monsterVoice.isPlaying
            && !SceneTransitionVideo.IsPresent) monsterVoice.Play();
    }

    public override void Spawned()
    {
        SetupMonsterVoice();
        actor = GetComponentInChildren<FrostActor>();
        bodyCollider = GetComponent<Collider>();
        // Las variantes grandes pegan mas fuerte, aguantan mas y alcanzan mas lejos.
        float size = Mathf.Max(.1f, transform.localScale.x);
        maxHealth *= size; damage *= size; attackRange *= size;
        home = transform.position;
        if (HasStateAuthority)
        {
            Health = maxHealth;
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
        }
        shownAttacks = AttackCount;
        shownHits = HitCount;
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "JUNGLE NIGHTMARE", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid && !Dead);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        if (Dead)
        {
            if (Runner.SimulationTime - DeathTime > despawnDelay) Runner.Despawn(Object);
            return;
        }

        float now = Runner.SimulationTime;
        if (now >= scanAt)
        {
            scanAt = now + .35f;
            ScanForTarget();
        }

        // El golpe llega a mitad del zarpazo, igual que el evento del clip Attack.
        if (impactAt > 0 && now >= impactAt)
        {
            impactAt = -1;
            Strike();
        }

        bool busy = actor != null && (actor.IsAttacking || actor.IsReacting);
        float size = Mathf.Max(.1f, transform.localScale.x);
        Vector3? destination = null;
        float desiredSpeed = 0f;
        if (target == null || !target.IsAlive || Distance(target.transform.position) > detectionRange)
        {
            if (!busy && Distance(home) > 1f) { destination = home; desiredSpeed = patrolSpeed * size; }
        }
        else
        {
            float distance = Distance(target.transform.position);
            Face(target.transform.position);
            if (distance <= attackRange)
            {
                if (!busy && now >= nextAttackAt)
                {
                    nextAttackAt = now + attackCooldown;
                    impactAt = now + attackImpactDelay;
                    AttackCount++;
                }
            }
            else if (!busy)
            {
                destination = target.transform.position;
                // Frena al acercarse para llegar al zarpazo sin pasarse.
                desiredSpeed = chaseSpeed * size * Mathf.Clamp01((distance - attackRange * .8f) / (attackRange * 1.5f) + .35f);
            }
        }

        // Acelera y frena suave: sin arrancones ni paradas en seco.
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, acceleration * size * Runner.DeltaTime);
        if (destination.HasValue && currentSpeed > .01f) MoveTowards(destination.Value, currentSpeed);
        else if (currentSpeed > .01f) MoveTowards(transform.position + transform.forward * 5f, currentSpeed);

        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, 1 - Mathf.Exp(-15 * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, 1 - Mathf.Exp(-15 * Time.deltaTime));
        }
        if (actor == null) return;

        // Los contadores replicados disparan la animacion una vez en cada cliente.
        if (Dead && !shownDead)
        {
            shownDead = true;
            if (monsterVoice != null) monsterVoice.Stop();
            deathStartedAt = Time.time;
            deathScale = transform.localScale;
            var anim = actor.GetComponent<Animator>();
            if (anim != null) anim.speed = 1f;
            actor.Die();
            if (bodyCollider != null) bodyCollider.enabled = false;
            Burst(.6f);
        }
        if (Dead)
        {
            PlayFrozenDeath(Time.time - deathStartedAt);
            return;
        }
        MatchRunAnimation();
        if (HitCount != shownHits)
        {
            shownHits = HitCount;
            actor.ReceiveDamage();
        }
        if (AttackCount != shownAttacks)
        {
            shownAttacks = AttackCount;
            PlayMonsterVoice();
            actor.Attack();
        }
    }

    // ---- Caminata: el ritmo de las patas sigue a la velocidad real para que no patine ----
    float currentSpeed, visualSpeed;
    Vector3 lastRenderPosition;
    Animator bodyAnimator;

    void MatchRunAnimation()
    {
        if (bodyAnimator == null) { bodyAnimator = actor.GetComponent<Animator>(); lastRenderPosition = transform.position; }
        if (Time.deltaTime <= 0f || bodyAnimator == null) return;
        Vector3 delta = transform.position - lastRenderPosition; delta.y = 0f;
        lastRenderPosition = transform.position;
        visualSpeed = Mathf.Lerp(visualSpeed, delta.magnitude / Time.deltaTime, 1f - Mathf.Exp(-10f * Time.deltaTime));

        if (actor.IsAttacking || actor.IsReacting || visualSpeed < .2f)
        {
            bodyAnimator.speed = 1f;
            return;
        }
        float size = Mathf.Max(.1f, transform.localScale.x);
        bodyAnimator.speed = Mathf.Clamp(visualSpeed / (runClipSpeed * size), .55f, 1.5f);
    }

    // ---- Muerte: cae con su animacion, se congela poco a poco y estalla en hielo ----
    const float FreezeTime = 2.2f, ShatterAt = 2.6f, ShatterTime = .35f;
    static readonly int TintId = Shader.PropertyToID("_Tint");
    static readonly Color FrozenTint = new Color(.55f, .85f, 1.4f, 1f);
    float deathStartedAt;
    Vector3 deathScale;
    bool shattered;
    Renderer[] bodyRenderers;
    MaterialPropertyBlock tintBlock;

    void PlayFrozenDeath(float t)
    {
        // 1. Mientras dura la animacion Death, el cuerpo se cubre de escarcha.
        if (bodyRenderers == null)
        {
            bodyRenderers = GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer)).ToArray();
            tintBlock = new MaterialPropertyBlock();
        }
        float frost = Mathf.SmoothStep(0f, 1f, t / FreezeTime);
        foreach (var r in bodyRenderers)
        {
            r.GetPropertyBlock(tintBlock);
            tintBlock.SetColor(TintId, Color.Lerp(Color.white, FrozenTint, frost));
            r.SetPropertyBlock(tintBlock);
        }

        // 2. Ya congelado, tiembla un instante y se rompe en pedazos de hielo.
        if (t > FreezeTime && t < ShatterAt)
        {
            float shake = Mathf.Sin(t * 90f) * .04f * Mathf.InverseLerp(FreezeTime, ShatterAt, t);
            transform.localScale = deathScale * (1f + shake);
        }
        if (t >= ShatterAt)
        {
            if (!shattered)
            {
                shattered = true;
                Burst(1.8f);
            }
            float u = Mathf.Clamp01((t - ShatterAt) / ShatterTime);
            transform.localScale = deathScale * Mathf.Lerp(1.08f, 0f, u * u);
        }
    }

    void Burst(float power)
    {
        var fx = GetComponentInChildren<FrostGroundFX>();
        if (fx == null) return;
        float size = Mathf.Max(.5f, deathScale.x);
        var center = transform.position + Vector3.up * .6f * size;
        FrostHitBurst.Spawn(center, null, fx.dustMaterial, fx.shardMaterial, Mathf.Min(2.2f, size * .7f * power));
    }

    void ScanForTarget()
    {
        target = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive
                && Vector3.Distance(home, a.transform.position) < leashRange)
            .OrderBy(a => (a.transform.position - transform.position).sqrMagnitude)
            .FirstOrDefault();
    }

    void Strike()
    {
        if (target == null || !target.IsAlive) return;
        var delta = target.transform.position - transform.position; delta.y = 0;
        if (delta.magnitude > attackRange + .5f || Vector3.Angle(transform.forward, delta) > 80) return;
        target.TakeMonsterDamage(Mathf.CeilToInt(damage));
        // Solo los grandes congelan; los medianos solo hacen dano.
        if (transform.localScale.x >= freezeMinScale) RPC_FrostHit(target.Object);
    }

    // Todos los clientes ven el estallido de frio sobre el jugador golpeado.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_FrostHit(NetworkObject victim)
    {
        if (victim == null) return;
        var fx = GetComponentInChildren<FrostGroundFX>();
        if (fx == null) return;
        var chest = victim.transform.position + Vector3.up * 1.1f;
        FrostHitBurst.Spawn(chest, victim.transform, fx.dustMaterial, fx.shardMaterial, Mathf.Clamp(transform.localScale.x, 1f, 1.6f));
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
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 300 * Runner.DeltaTime);
    }

    void MoveTowards(Vector3 point, float moveSpeed)
    {
        var d = point - transform.position; d.y = 0;
        if (d.sqrMagnitude < .1f) return;
        d.Normalize();
        var hits = Physics.SphereCastAll(transform.position + Vector3.up * .8f, .4f, d, 1.3f, ~0, QueryTriggerInteraction.Ignore);
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
        // Suelos de malla (hielo de Neptuno): rayo hacia abajo ignorando al propio enemigo.
        float best = float.MaxValue, y = point.y;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
            best = hit.distance; y = hit.point.y;
        }
        return y;
    }

    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        RPC_TakeDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int amount)
    {
        if (amount <= 0 || Dead) return;
        float previousHealth = Health;
        Health = Mathf.Max(0, Health - Mathf.Min(amount, maxHealth));
        RPC_DamageFeedback(Health <= 0, previousHealth - Health);
        if (Health <= 0)
        {
            Dead = true;
            DeathTime = Runner.SimulationTime;
            impactAt = -1;
        }
        else
        {
            HitCount++;
            impactAt = -1; // El dolor interrumpe el zarpazo, como en FrostActor.
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DamageFeedback(bool lethal, float actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        var blood = GetComponent<GameJam.Combat.EnemyBloodFx>();
        if (blood == null) return;
        var chest = transform.position + Vector3.up * 1f;
        blood.Bleed(chest, transform.forward);
        if (lethal) blood.Bleed(chest, -transform.forward);
    }
}
