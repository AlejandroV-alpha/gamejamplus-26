using System;
using System.Linq;
using Fusion;
using UnityEngine;
using GameJam.Multiplayer;

// Fusion state authority controls AI and damage; all clients render replicated state.
public sealed class ReksaiFusionEnemy : NetworkBehaviour, ITakeDamage
{
    public enum Mode { Idle, Run, Attack1, Attack2, Burrow, Hidden, Emerge, Death }
    [SerializeField] float maxHealth = 160, detectionRange = 28, leashRange = 65, speed = 6.5f, attackRange = 3.8f, damage = 18, burrowCooldown = 18;
    [SerializeField, Min(.1f)] float attackSpeed = 1.5f;
    [SerializeField, Min(0f)] float attackRecovery = .35f;
    [SerializeField, Min(1f)] float undergroundSpeed = 20f;
    [SerializeField, Min(.5f)] float undergroundMaxDuration = 1.8f;
    [SerializeField, Min(1.5f)] float emergeDistance = 2.2f;

    [Networked] private int ModeState { get; set; }
    [Networked] private float StateStarted { get; set; }
    [Networked] public float Health { get; private set; }
    [Networked] private Vector3 SyncedPosition { get; set; }
    [Networked] private Quaternion SyncedRotation { get; set; }

    Animator animator;
    Transform visual;
    Vector3 visualRest, home;
    Collider bodyCollider;

    MarsFusionAvatar targetFusion;
    Transform targetTransform;

    float scanAt, attackAt, burrowAt;
    bool hitApplied, alternate;
    int shown = -1;

    public Mode CurrentMode => (Mode)ModeState;
    float CurrentTime => Runner.SimulationTime;
    float Elapsed => Mathf.Max(0, CurrentTime - StateStarted);

    AudioSource monsterVoice;

    void SetupMonsterVoice()
    {
        var clip = Resources.Load<AudioClip>("Audio/Monsters/Reksai");
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
        animator = GetComponentInChildren<Animator>();
        visual = animator != null ? animator.transform : transform;
        visualRest = visual.localPosition;
        bodyCollider = GetComponent<Collider>();
        home = transform.position;
        if (HasStateAuthority)
        {
            Health = maxHealth;
            StateStarted = CurrentTime;
            ModeState = (int)Mode.Idle;
            SyncedPosition = transform.position;
            SyncedRotation = transform.rotation;
            burrowAt = CurrentTime + burrowCooldown;
        }
        RenderState();
        GameJam.Combat.EnemyCombatUI.Attach(gameObject, "REK’SAI", () => Health, maxHealth,
            () => Object != null && Object.IsValid && StateBufferIsValid
                && CurrentMode != Mode.Hidden && CurrentMode != Mode.Burrow && CurrentMode != Mode.Death);
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
        TickServer();
        if (Object == null || !Object.IsValid) return;
        SyncedPosition = transform.position;
        SyncedRotation = transform.rotation;
    }

    public override void Render()
    {
        if (!HasStateAuthority)
        {
            transform.position = Vector3.Lerp(transform.position, SyncedPosition, 1-Mathf.Exp(-15*Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, SyncedRotation, 1-Mathf.Exp(-15*Time.deltaTime));
        }
        RenderState();
    }

    void SetMode(Mode value)
    {
        if (CurrentMode == value) return;
        StateStarted = CurrentTime;
        ModeState = (int)value;
        hitApplied = false;
    }

    bool HasAliveTarget()
    {
        if (targetTransform == null) return false;
        if (targetFusion != null) return targetFusion.IsAlive;
        return true;
    }

    void TickServer()
    {
        var state = CurrentMode;
        if (state == Mode.Death)
        {
            if (Elapsed > 4)
            {
                Runner.Despawn(Object);
            }
            return;
        }

        if (CurrentTime >= scanAt)
        {
            scanAt = CurrentTime + .35f;
            ScanForTargets();
        }

        if (state == Mode.Burrow)
        {
            if (Elapsed >= ClipLength("spell2")) SetMode(Mode.Hidden);
            return;
        }

        if (state == Mode.Hidden)
        {
            bool close = false;
            if (HasAliveTarget() && TryFindEmergencePoint(out var destination))
            {
                MoveTowards(destination, undergroundSpeed);
                var remaining = destination - transform.position; remaining.y = 0; close = remaining.sqrMagnitude <= .25f;
                if (close) Face(targetTransform.position);
            }
            if ((close && Elapsed >= .35f) || Elapsed >= undergroundMaxDuration) SetMode(Mode.Emerge);
            return;
        }

        if (state == Mode.Emerge)
        {
            if (!hitApplied && Elapsed >= ClipLength("spell2knockup") * .48f)
            {
                hitApplied = true;
                Strike(damage * .7f, attackRange + .7f);
            }
            if (Elapsed >= ClipLength("spell2knockup"))
            {
                attackAt = CurrentTime + .5f;
                SetMode(Mode.Idle);
            }
            return;
        }

        if (state == Mode.Attack1 || state == Mode.Attack2)
        {
            if (HasAliveTarget()) Face(targetTransform.position);
            var duration = ClipLength(state == Mode.Attack1 ? "attack1" : "attack2") / Mathf.Max(.1f, attackSpeed);
            if (!hitApplied && Elapsed >= duration * .42f)
            {
                hitApplied = true;
                Strike(damage, attackRange + .2f);
            }
            if (Elapsed >= duration)
            {
                attackAt = CurrentTime + attackRecovery;
                SetMode(Mode.Idle);
            }
            return;
        }

        if (!HasAliveTarget() || Vector3.Distance(transform.position, targetTransform.position) > detectionRange)
        {
            if (Vector3.Distance(transform.position, home) > 1)
            {
                SetMode(Mode.Run);
                MoveTowards(home, speed * .6f);
            }
            else SetMode(Mode.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, targetTransform.position);
        if (CurrentTime >= burrowAt && distance > 5 && distance < 22)
        {
            burrowAt = CurrentTime + burrowCooldown;
            SetMode(Mode.Burrow);
            return;
        }

        if (distance <= attackRange)
        {
            if (CurrentTime >= attackAt)
            {
                alternate = !alternate;
                SetMode(alternate ? Mode.Attack1 : Mode.Attack2);
            }
            else SetMode(Mode.Idle);
        }
        else
        {
            SetMode(Mode.Run);
            MoveTowards(targetTransform.position, speed);
        }
    }

    void ScanForTargets()
    {
        targetFusion = FindObjectsByType<MarsFusionAvatar>(FindObjectsSortMode.None)
            .Where(a => a.Object != null && a.Object.IsValid && a.Runner == Runner && a.IsAlive
                && Vector3.Distance(home,a.transform.position) < leashRange)
            .OrderBy(a => (a.transform.position-transform.position).sqrMagnitude).FirstOrDefault();
        targetTransform = targetFusion != null ? targetFusion.transform : null;
    }

    void Face(Vector3 point)
    {
        var d = point - transform.position; d.y = 0;
        if (d.sqrMagnitude > .01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), 240 * Runner.DeltaTime);
    }

    void MoveTowards(Vector3 point, float moveSpeed)
    {
        var d = point - transform.position; d.y = 0;
        if (d.sqrMagnitude < .1f) return;
        d.Normalize();
        if (CurrentMode != Mode.Hidden)
        {
            var hits = Physics.SphereCastAll(transform.position + Vector3.up * .9f, .45f, d, 1.5f, ~0, QueryTriggerInteraction.Ignore);
            var obstacle = hits.FirstOrDefault(h => !(h.collider is TerrainCollider) && !h.collider.transform.IsChildOf(transform) && (targetTransform == null || !h.collider.transform.IsChildOf(targetTransform)));
            if (obstacle.collider != null)
            {
                d = Vector3.ProjectOnPlane(d, obstacle.normal); d.y = 0;
                if (d.sqrMagnitude < .01f) d = Vector3.Cross(Vector3.up, obstacle.normal);
                d.Normalize();
            }
        }
        Face(transform.position + d);
        var next = transform.position + d * Mathf.Min(moveSpeed * Runner.DeltaTime, Vector3.ProjectOnPlane(point - transform.position, Vector3.up).magnitude);
        var terrain = Terrain.activeTerrains.FirstOrDefault(t => next.x >= t.transform.position.x && next.z >= t.transform.position.z && next.x <= t.transform.position.x + t.terrainData.size.x && next.z <= t.transform.position.z + t.terrainData.size.z);
        if (terrain != null) next.y = terrain.SampleHeight(next) + terrain.transform.position.y;
        transform.position = next;
    }

    bool TryFindEmergencePoint(out Vector3 point)
    {
        if (targetTransform == null) { point = transform.position; return false; }
        var direction = transform.position - targetTransform.position; direction.y = 0;
        if (direction.sqrMagnitude < .01f) direction = -targetTransform.forward;
        direction.Normalize();
        for (int ring = 0; ring < 2; ring++)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i == 0 ? 0 : ((i + 1) / 2) * 45 * (i % 2 == 1 ? 1 : -1);
                var candidate = targetTransform.position + Quaternion.Euler(0, angle, 0) * direction * (emergeDistance + ring * 1.2f);
                var terrain = Terrain.activeTerrains.FirstOrDefault(t => candidate.x >= t.transform.position.x && candidate.z >= t.transform.position.z && candidate.x <= t.transform.position.x + t.terrainData.size.x && candidate.z <= t.transform.position.z + t.terrainData.size.z);
                if (terrain != null) candidate.y = terrain.SampleHeight(candidate) + terrain.transform.position.y;
                bool blocked = Physics.OverlapCapsule(candidate + Vector3.up * .8f, candidate + Vector3.up * 1.7f, .85f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !(c is TerrainCollider) && !c.transform.IsChildOf(transform));
                if (!blocked) { point = candidate; return true; }
            }
        }
        point = transform.position; return false;
    }

    void Strike(float amount, float range)
    {
        if (!HasAliveTarget()) return;
        var delta = targetTransform.position - transform.position; delta.y = 0;
        if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) > 85) return;
        var from = transform.position + Vector3.up; var to = targetTransform.position + Vector3.up;
        var blocked = Physics.RaycastAll(from, (to - from).normalized, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore)
            .Any(h => !h.collider.transform.IsChildOf(transform) && !h.collider.transform.IsChildOf(targetTransform) && !(h.collider is TerrainCollider));
        if (blocked) return;

        if (targetFusion != null)
        {
            targetFusion.TakeMonsterDamage(Mathf.CeilToInt(amount));
        }
    }

    public void TakeDamage(int amount)
    {
        if (Object == null || !Object.IsValid || amount <= 0) return;
        RPC_TakeDamage(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_TakeDamage(int amount)
    {
        if (amount <= 0 || CurrentMode == Mode.Death || CurrentMode == Mode.Hidden || CurrentMode == Mode.Burrow) return;
        float previousHealth = Health;
        Health = Mathf.Max(0, Health-Mathf.Min(amount,maxHealth));
        RPC_DamageFeedback(Health<=0, previousHealth - Health);
        Debug.Log("[Reksai Fusion] Health="+Health, this);
        if (Health <= 0) SetMode(Mode.Death);
    }

    [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
    private void RPC_DamageFeedback(bool lethal, float actualDamage)
    {
        GetComponent<GameJam.Combat.EnemyCombatUI>()?.Damage(actualDamage);
        var blood=GetComponent<GameJam.Combat.EnemyBloodFx>();
        if(blood==null)return;
        blood.Bleed(transform.position+Vector3.up*1.2f,transform.forward);
        if(lethal)blood.Bleed(transform.position+Vector3.up*1.2f,-transform.forward);
    }

    string Suffix(Mode state)
    {
        switch (state)
        {
            case Mode.Run: return "runhaste";
            case Mode.Attack1: return "attack1";
            case Mode.Attack2: return "attack2";
            case Mode.Burrow: return "spell2";
            case Mode.Emerge: return "spell2knockup";
            case Mode.Death: return "death";
            default: return "idle1base";
        }
    }

    float ClipLength(string suffix)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return 1f;
        var c = animator.runtimeAnimatorController.animationClips.FirstOrDefault(a => a.name.EndsWith(suffix, StringComparison.Ordinal));
        return c != null ? c.length : 1f;
    }

    float PlaybackSpeed(Mode state) => state == Mode.Attack1 || state == Mode.Attack2 ? Mathf.Max(.1f, attackSpeed) : state == Mode.Run ? 1.35f : 1f;

    void RenderState()
    {
        if (animator == null) return;
        var state = CurrentMode; float elapsed = Elapsed; animator.speed = PlaybackSpeed(state);
        int currentModeVal = ModeState;
        if (shown != currentModeVal)
        {
            shown = currentModeVal;
            if (state == Mode.Attack1 || state == Mode.Attack2 || state == Mode.Emerge) PlayMonsterVoice();
            else if ((state == Mode.Death || state == Mode.Hidden || state == Mode.Burrow) && monsterVoice != null) monsterVoice.Stop();
            var suffix = Suffix(state);
            var clip = animator.runtimeAnimatorController.animationClips.FirstOrDefault(a => a.name.EndsWith(suffix, StringComparison.Ordinal));
            if (clip != null) animator.CrossFadeInFixedTime(Animator.StringToHash("Base Layer." + clip.name.Split('|').Last()), .10f, 0, Mathf.Min(elapsed * PlaybackSpeed(state), Mathf.Max(0, clip.length - .01f)));
        }
        float depth = state == Mode.Hidden ? 4 : state == Mode.Burrow ? 4 * Mathf.SmoothStep(0, 1, elapsed / ClipLength("spell2")) : state == Mode.Emerge ? 4 * (1 - Mathf.SmoothStep(0, 1, elapsed / ClipLength("spell2knockup"))) : 0;
        visual.localPosition = visualRest + Vector3.down * depth;
        if (bodyCollider != null) bodyCollider.enabled = state != Mode.Hidden && state != Mode.Burrow && state != Mode.Death;
    }
}


