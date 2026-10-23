using System.Collections.Generic;
using System.Linq;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;

// Violet Venomwing: avispa voladora de Marte.
// La autoridad de Fusion decide vuelo, vida y daño; todos los clientes ven el estado replicado.
//  - Patrulla flotando en círculos (Idle).
//  - Al detectar a un jugador vuela rápido hacia él (FlyFast).
//  - De cerca se lanza en picada y clava el aguijón (Attack).
//  - A media distancia escupe ácido: bola en arco + charco que quema (Spit).
//  - Si le disparan, persigue a quien le disparó.
public sealed class VenomwingFusionEnemy : NetworkBehaviour, ITakeDamage
{
    public enum Mode { Patrol, Chase, Sting, Spit, Death }

    [Header("Vida y detección")]
    [SerializeField] float maxHealth = 140, detectionRange = 26, leashRange = 70;
    [SerializeField] float aggroDuration = 20f, aggroMaxRange = 140f;

    [Header("Vuelo")]
    [SerializeField] float hoverHeight = 3.2f;
    [SerializeField] float patrolRadius = 9f, patrolSpeed = 3f;
    [SerializeField] float chaseSpeed = 11f;
    [SerializeField] float turnDegreesPerSecond = 360f;

    [Header("Aguijón")]
    [SerializeField] float stingRange = 3.2f, stingHitRange = 3.6f, stingDamage = 12f, stingCooldown = 1.2f;
    [SerializeField] float stingHitTime = .5f, stingDuration = 1.2f;

    [Header("Ácido")]
    [SerializeField] float spitMinRange = 6f, spitMaxRange = 17f, spitCooldown = 4f;
    [SerializeField] float spitReleaseTime = .6f, spitDuration = 1.33f;
    [SerializeField] float spitDirectDamage = 10f, puddleDamagePerSecond = 6f, puddleRadius = 1.8f, puddleLifetime = 4f;
    [SerializeField] Material acidGlowMaterial, acidPuddleMaterial, acidBlobMaterial;

    [Header("Muerte")]
    [SerializeField] float despawnDelay = 6f;

    [Networked] public float Health { get; private set; }
    [Networked] private int ModeState { get; set; }
    [Networked] private float StateStarted { get; set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }

    Animator animator;
    Transform visual, head;
    Vector3 visualRestPos, home;
    Quaternion visualRestRot;
    Collider bodyCollider;
    GameJam.Combat.EnemyHitFeedback feedback;
    AudioSource buzz, hurtVoice;
    AudioClip[] hurtClips;
    int lastHurtClip = -1;
    float nextHurtSoundAt;

    // Quejido al recibir daño (Resources/Audio/Monsters/VenomwingHurt), 3D, en una fuente aparte del zumbido.
    void SetupHurtSound()
    {
        hurtClips = new[] { "VenomwingHurt", "VenomwingHurt2", "VenomwingHurt3" }
            .Select(n => Resources.Load<AudioClip>("Audio/Monsters/" + n)).Where(c => c != null).ToArray();
        if (hurtClips.Length == 0) return;
        hurtVoice = gameObject.AddComponent<AudioSource>();
        hurtVoice.playOnAwake = false;
        hurtVoice.volume = 1f;
        hurtVoice.spatialBlend = .8f;          // algo de 2D para que no se pierda a los costados
        hurtVoice.rolloffMode = AudioRolloffMode.Linear;
        hurtVoice.minDistance = 10f;           // volumen completo hasta 10 m
        hurtVoice.maxDistance = 60f;
        hurtVoice.dopplerLevel = 0f;
        hurtVoice.priority = 110;
    }

    void PlayHurtSound()
    {
        if (hurtVoice == null || SceneTransitionVideo.IsPresent || Time.time < nextHurtSoundAt) return;
        // Con ráfagas muy rápidas no se amontonan: como mucho uno cada 0.07 s, con tono distinto cada vez.
        nextHurtSoundAt = Time.time + .07f;
        hurtVoice.pitch = Random.Range(.9f, 1.12f);
        // Uno de los quejidos al azar, sin repetir el mismo dos veces seguidas.
        int i = Random.Range(0, hurtClips.Length);
        if (hurtClips.Length > 1 && i == lastHurtClip) i = (i + 1 + Random.Range(0, hurtClips.Length - 1)) % hurtClips.Length;
        lastHurtClip = i;
        hurtVoice.PlayOneShot(hurtClips[i], 2f);   // refuerzo: el archivo es bajito
    }

    // Zumbido de vuelo en bucle (Resources/Audio/Monsters/Venomwing), en 3D: se oye más fuerte de cerca.
    void SetupBuzz()
    {
        var clip = Resources.Load<AudioClip>("Audio/Monsters/Venomwing");
        if (clip == null) return;
        buzz = gameObject.AddComponent<AudioSource>();
        buzz.clip = clip;
        buzz.loop = true;
        buzz.playOnAwake = false;
        buzz.volume = .55f;
        buzz.spatialBlend = 1f;
        buzz.rolloffMode = AudioRolloffMode.Linear;
        buzz.minDistance = 3f;
        buzz.maxDistance = 35f;
        buzz.dopplerLevel = .3f;
        buzz.priority = 120;
        buzz.time = Random.Range(0f, clip.length * .95f);   // cada avispa empieza en otro punto
        buzz.Play();
    }

    // Más agudo cuando vuela rápido o ataca; se apaga suave al morir o durante los videos de transición.
    void UpdateBuzz()
    {
        if (buzz == null) return;
        var mode = CurrentMode;
        float wantPitch = mode == Mode.Chase || mode == Mode.Sting ? 1.18f : mode == Mode.Spit ? 1.08f : 1f;
        float wantVolume = mode == Mode.Death || SceneTransitionVideo.IsPresent ? 0f : .55f;
        buzz.pitch = Mathf.MoveTowards(buzz.pitch, wantPitch, Time.deltaTime * .8f);
        buzz.volume = Mathf.MoveTowards(buzz.volume, wantVolume, Time.deltaTime * (wantVolume > 0f ? .8f : .6f));
        if (buzz.volume <= 0f && buzz.isPlaying) buzz.Pause();
        else if (buzz.volume > 0f && !buzz.isPlaying) buzz.UnPause();
    }
    MarsFusionAvatar target, aggroTarget;
    float scanAt, aggroUntil, nextStingAt, nextSpitAt, patrolAngle, currentSpeed;
    bool hitApplied;
    string shownState;
    int shownMode = -1;

    struct Puddle { public Vector3 pos; public float until; }
    readonly List<Puddle> puddles = new List<Puddle>();
    float puddleTickAt;

    public Mode CurrentMode => (Mode)ModeState;
    public bool IsAlive => Object != null && Object.IsValid && CurrentMode != Mode.Death;
    float Size => Mathf.Max(.1f, transform.localScale.x);
    float Now => Runner.SimulationTime;
    float Elapsed => Mathf.Max(0, Now - StateStarted);

    public override void Spawned()
    {
        animator = GetComponentInChildren<Animator>();
        visual = animator != null ? animator.transform : transform;
        visualRestPos = visual.localPosition;
        visualRestRot = visual.localRotation;
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Head") head = t;
        bodyCollider = GetComponent<Collider>();
        feedback = GetComponent<GameJam.Combat.EnemyHitFeedback>();
        SetupBuzz();
        SetupHurtSound();
        home = transform.position;
        if (HasStateAuthority)
        {
            Health = maxHealth;
            ModeState = (int)Mode.Patrol;
            StateStarted = Now;
            var p = transform.position;
            p.y = GroundHeight(p) + hoverHeight;
            transform.position = p;
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            nextSpitAt = Now + 2f;
        }
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "VENOMWING", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid && CurrentMode != Mode.Death);
    }

    // ------------------------------------------------------------------ IA (autoridad)

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        Tick();
        TickPuddles();
        if (Object == null || !Object.IsValid) return;
        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    void SetMode(Mode m)
    {
        if (CurrentMode == m) return;
        ModeState = (int)m;
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
        if (Now >= scanAt) { scanAt = Now + .35f; ScanForTarget(); }
        if (mode == Mode.Sting) { TickSting(); return; }
        if (mode == Mode.Spit) { TickSpit(); return; }

        bool hasTarget = target != null && target.IsAlive
            && (Distance(target.transform.position) <= detectionRange || (target == aggroTarget && Aggro));
        if (!hasTarget) { Patrol(); return; }

        float dist = Distance(target.transform.position);
        Face(target.transform.position);
        if (dist <= stingRange * Size * .8f)
        {
            if (Now >= nextStingAt) { SetMode(Mode.Sting); return; }
            Hover(target.transform.position.y);
            return;
        }
        if (Now >= nextSpitAt && dist >= spitMinRange && dist <= spitMaxRange)
        {
            SetMode(Mode.Spit);
            return;
        }
        currentSpeed = Mathf.MoveTowards(currentSpeed, chaseSpeed, chaseSpeed * 2f * Runner.DeltaTime);
        SetMode(Mode.Chase);
        Vector3 to = Flat(target.transform.position - transform.position);
        float step = Mathf.Min(currentSpeed * Runner.DeltaTime, Mathf.Max(0f, dist - stingRange * Size * .7f));
        transform.position += to.normalized * step;
        Hover(target.transform.position.y);
    }

    void Patrol()
    {
        SetMode(Mode.Patrol);
        currentSpeed = Mathf.MoveTowards(currentSpeed, patrolSpeed, chaseSpeed * Runner.DeltaTime);
        // Vuelve a su zona si se alejó persiguiendo; si no, círculos lentos.
        Vector3 goal;
        if (Distance(home) > patrolRadius * 1.5f) goal = home;
        else
        {
            patrolAngle += currentSpeed / Mathf.Max(1f, patrolRadius) * Runner.DeltaTime;
            goal = home + new Vector3(Mathf.Cos(patrolAngle), 0f, Mathf.Sin(patrolAngle)) * patrolRadius;
        }
        Vector3 to = Flat(goal - transform.position);
        if (to.sqrMagnitude > .04f)
        {
            Face(transform.position + to);
            transform.position += to.normalized * Mathf.Min(currentSpeed * Runner.DeltaTime, to.magnitude);
        }
        Hover(null);
    }

    void TickSting()
    {
        float t = Elapsed;
        if (target != null)
        {
            if (t < stingHitTime) Face(target.transform.position);
            // Picada: baja hacia el jugador justo en el aguijonazo y vuelve a subir.
            float dive = Mathf.Sin(Mathf.Clamp01(t / (stingDuration * .8f)) * Mathf.PI);
            float flyY = GroundHeight(transform.position) + hoverHeight;
            float wanted = Mathf.Lerp(flyY, target.transform.position.y + 1.2f, dive);
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, wanted, 1f - Mathf.Exp(-10f * Runner.DeltaTime));
            Vector3 to = Flat(target.transform.position - transform.position);
            if (t > stingHitTime * .5f && t < stingHitTime && to.magnitude > 1.5f * Size)
                p += to.normalized * chaseSpeed * .6f * Runner.DeltaTime;
            transform.position = p;
        }
        if (!hitApplied && t >= stingHitTime)
        {
            hitApplied = true;
            if (target != null && target.IsAlive)
            {
                Vector3 d = target.transform.position - transform.position;
                bool inFront = Vector3.Angle(transform.forward, Flat(d)) < 70f;
                if (Flat(d).magnitude <= stingHitRange * Size && Mathf.Abs(d.y) < 4f && inFront)
                    target.TakeMonsterDamage(Mathf.CeilToInt(stingDamage));
            }
        }
        if (t >= stingDuration)
        {
            nextStingAt = Now + stingCooldown;
            SetMode(Mode.Chase);
        }
    }

    void TickSpit()
    {
        float t = Elapsed;
        if (target != null) { Face(target.transform.position); Hover(target.transform.position.y); }
        if (!hitApplied && t >= spitReleaseTime)
        {
            hitApplied = true;
            Vector3 from = MouthPosition();
            Vector3 to = target != null ? target.transform.position + Vector3.up * .3f : from + transform.forward * 10f;
            float flight = Mathf.Clamp(Vector3.Distance(from, to) / 16f, .4f, 1f);
            RPC_Spit(from, to, flight);
        }
        if (t >= spitDuration)
        {
            nextSpitAt = Now + spitCooldown;
            SetMode(Mode.Chase);
        }
    }

    // Todos ven la bola de ácido; solo la autoridad aplica el daño cuando cae.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Spit(Vector3 from, Vector3 to, float flight)
    {
        var mats = new VenomAcid.Mats { glow = acidGlowMaterial, puddle = acidPuddleMaterial, blob = acidBlobMaterial };
        System.Action<Vector3> onImpact = null;
        if (HasStateAuthority) onImpact = AcidLanded;
        VenomAcid.Launch(from, to, flight, mats, transform, puddleRadius, puddleLifetime, onImpact);
    }

    void AcidLanded(Vector3 point)
    {
        if (Object == null || !Object.IsValid) return;
        foreach (var a in Avatars())
            if ((a.transform.position + Vector3.up - point).magnitude <= 1.6f)
                a.TakeMonsterDamage(Mathf.CeilToInt(spitDirectDamage));
        puddles.Add(new Puddle { pos = point, until = Now + puddleLifetime });
    }

    void TickPuddles()
    {
        if (puddles.Count == 0 || Now < puddleTickAt) return;
        puddleTickAt = Now + .5f;
        puddles.RemoveAll(p => Now > p.until);
        foreach (var a in Avatars())
            foreach (var p in puddles)
            {
                Vector3 d = a.transform.position - p.pos;
                if (new Vector2(d.x, d.z).magnitude <= puddleRadius && Mathf.Abs(d.y) < 1.8f)
                {
                    a.TakeMonsterDamage(Mathf.Max(1, Mathf.CeilToInt(puddleDamagePerSecond * .5f)));
                    break;
                }
            }
    }

    Vector3 MouthPosition()
    {
        if (head != null) return head.position + transform.forward * .45f * Size - Vector3.up * .15f * Size;
        return transform.position + transform.forward * .9f * Size;
    }

    // ------------------------------------------------------------------ objetivo

    bool Aggro => aggroTarget != null && aggroTarget.Object != null && aggroTarget.Object.IsValid
        && aggroTarget.IsAlive && Now < aggroUntil && Distance(aggroTarget.transform.position) <= aggroMaxRange;

    IEnumerable<MarsFusionAvatar> Avatars() =>
        FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive);

    void ScanForTarget()
    {
        if (Aggro) { target = aggroTarget; return; }
        aggroTarget = null;
        target = Avatars().Where(a => Vector3.Distance(home, a.transform.position) < leashRange)
            .OrderBy(a => (a.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
    }

    void RememberAttacker(PlayerRef source)
    {
        var avatars = Avatars().ToArray();
        MarsFusionAvatar shooter = null;
        if (source != PlayerRef.None)
        {
            bool shared = Runner.GameMode == GameMode.Shared;
            shooter = avatars.FirstOrDefault(a => (shared ? a.Object.StateAuthority : a.Object.InputAuthority) == source)
                ?? avatars.FirstOrDefault(a => a.Object.InputAuthority == source || a.Object.StateAuthority == source);
        }
        if (shooter == null) shooter = avatars.OrderBy(a => (a.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
        if (shooter == null) return;
        aggroTarget = shooter;
        aggroUntil = Now + aggroDuration;
        target = shooter;
    }

    // ------------------------------------------------------------------ movimiento

    float Distance(Vector3 p) => Flat(p - transform.position).magnitude;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void Face(Vector3 point)
    {
        var d = Flat(point - transform.position);
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), turnDegreesPerSecond * Runner.DeltaTime);
    }

    // Altura de vuelo sobre el terreno; al perseguir baja un poco hacia el jugador.
    void Hover(float? playerY)
    {
        float ground = GroundHeight(transform.position);
        float wanted = ground + hoverHeight;
        if (playerY.HasValue) wanted = Mathf.Max(ground + hoverHeight * .65f, Mathf.Min(wanted, playerY.Value + hoverHeight * .8f));
        var p = transform.position;
        p.y = Mathf.Lerp(p.y, wanted, 1f - Mathf.Exp(-3f * Runner.DeltaTime));
        transform.position = p;
    }

    float GroundHeight(Vector3 point)
    {
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => point.x >= t.transform.position.x && point.z >= t.transform.position.z
            && point.x <= t.transform.position.x + t.terrainData.size.x && point.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain != null) return terrain.SampleHeight(point) + terrain.transform.position.y;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore)
                     .OrderBy(h => h.distance))
            if (!hit.collider.transform.IsChildOf(transform)) return hit.point.y;
        return point.y - hoverHeight;
    }

    // ------------------------------------------------------------------ daño recibido

    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        if (CurrentMode != Mode.Death && feedback != null) feedback.LocalHit();
        RPC_TakeDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int amount, RpcInfo info = default)
    {
        if (amount <= 0 || CurrentMode == Mode.Death) return;
        RememberAttacker(info.Source);
        float previous = Health;
        Health = Mathf.Max(0, Health - Mathf.Min(amount, maxHealth));
        RPC_DamageFeedback(Health <= 0, previous - Health);
        if (Health <= 0)
        {
            SetMode(Mode.Death);
            RPC_Killed(info.Source, Random.Range(1, int.MaxValue));
        }
    }

    // Todos: congelado, explosión y botín que vuela hacia quien la mató (y se suma a su inventario).
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Killed(PlayerRef killer, int seed)
    {
        if (feedback == null) return;
        bool shared = Runner.GameMode == GameMode.Shared;
        var killerAvatar = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .FirstOrDefault(a => a.Object != null && a.Object.IsValid && a.Runner == Runner
                && (shared ? a.Object.StateAuthority : a.Object.InputAuthority) == killer);
        if (killerAvatar == null)
            killerAvatar = Avatars().OrderBy(a => (a.transform.position - transform.position).sqrMagnitude).FirstOrDefault();
        bool mine = killer != PlayerRef.None && killer == Runner.LocalPlayer;
        feedback.Killed(mine, killerAvatar != null ? killerAvatar.transform : null, seed);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_DamageFeedback(bool lethal, float actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        PlayHurtSound();
        if (feedback != null)
        {
            var cam = Camera.main;
            feedback.Damaged(cam != null ? transform.position - cam.transform.position : -transform.forward);
        }
        var blood = GetComponent<GameJam.Combat.EnemyBloodFx>();
        if (blood == null) return;
        var c = transform.position + Vector3.up * .3f * Size;
        blood.Bleed(c, transform.forward);
        if (lethal) blood.Bleed(c, -transform.forward);
    }

    // ------------------------------------------------------------------ visual (todos)

    float deathStartedAt, fallStartY, groundAtDeath, fallSpin;

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, 1 - Mathf.Exp(-15 * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, 1 - Mathf.Exp(-15 * Time.deltaTime));
        }
        UpdateBuzz();
        if (animator == null) return;
        var mode = CurrentMode;

        if (shownMode != ModeState)
        {
            shownMode = ModeState;
            if (mode == Mode.Death)
            {
                if (bodyCollider != null) bodyCollider.enabled = false;
                deathStartedAt = Time.time + (feedback != null ? feedback.HitStopTime : 0f);
                fallStartY = transform.position.y;
                groundAtDeath = GroundHeight(transform.position);
                fallSpin = Random.value < .5f ? -1f : 1f;
            }
            else
            {
                string state = mode == Mode.Chase ? "FlyFast" : mode == Mode.Sting ? "Attack" : mode == Mode.Spit ? "Spit" : "Idle";
                float offset = mode == Mode.Sting || mode == Mode.Spit ? Elapsed : 0f;
                animator.speed = 1f;
                animator.CrossFadeInFixedTime(state, mode == Mode.Sting || mode == Mode.Spit ? .08f : .25f, 0, offset);
                shownState = state;
            }
        }

        if (mode == Mode.Death) RenderDeath();
    }

    // Deja de aletear, cae girando, rebota en la arena, queda panza arriba y se hunde.
    void RenderDeath()
    {
        if (feedback != null && feedback.IsFrozen) return;   // hit-stop: congelada un instante en blanco
        float t = Mathf.Max(0f, Time.time - deathStartedAt);
        animator.speed = Mathf.MoveTowards(animator.speed, 0f, Time.deltaTime * 1.5f);
        float fallTime = .9f;
        float k = Mathf.Clamp01(t / fallTime);
        float rootY = transform.position.y;
        float targetLocalY = (groundAtDeath + .35f * Size - rootY) / Size; // en espacio local del root
        float bounce = t > fallTime ? Mathf.Sin(Mathf.Clamp01((t - fallTime) / .35f) * Mathf.PI) * .25f * Mathf.Exp(-(t - fallTime) * 2f) : 0f;
        float y = Mathf.Lerp(0f, targetLocalY, k * k) + bounce;
        float sink = Mathf.SmoothStep(0f, 1f, (t - 3.2f) / 2.2f);
        visual.localPosition = visualRestPos + Vector3.up * (y - sink * .8f);
        float roll = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (fallTime + .3f))) * 160f * fallSpin;
        float pitch = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fallTime)) * 25f;
        visual.localRotation = Quaternion.Euler(pitch, 0f, roll) * visualRestRot;
    }
}
