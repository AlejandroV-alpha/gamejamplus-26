using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using System.Collections.Generic;

namespace GameJam.Survival
{
    // Cosmetic only: the existing revival authority still decides when health returns.
    [DefaultExecutionOrder(11800)]
    [DisallowMultipleComponent]
    public sealed class PlayerSocialAnimation : MonoBehaviour
    {
        private static readonly AnimationClip[] emotes = new AnimationClip[3];
        private static readonly string[] emotePaths = { "Wave", "ThumbsUp", "Flex" };
        private SurvivalRevive revive;
        private MarsFusionAvatar avatar;
        private Animator animator;
        private Transform model;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private PlayableGraph graph;
        private AnimationClipPlayable playable, previousPlayable;
        private AnimationMixerPlayable mixer;
        private float blendStarted;
        private const float TransitionDuration = .15f;
        private AnimationClip currentClip, kneel;
        private static AnimationClip reviveGetUp, rescueStand;
        public static AnimationClip ReviveGetUpClip => reviveGetUp != null ? reviveGetUp
            : (reviveGetUp = Resources.Load<AnimationClip>("PlayerSocial/ReviveGetUp"));
        public static AnimationClip RescueStandClip => rescueStand != null ? rescueStand
            : (rescueStand = Resources.Load<AnimationClip>("PlayerSocial/ReviveStand"));
        private Renderer[] weaponRenderers;
        private readonly Dictionary<Renderer, bool> weaponVisibility = new Dictionary<Renderer, bool>();
        private bool originalRootMotion;
        private static readonly HumanBodyBones[] SupportBones = { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

        public static AnimationClip LoadClip(string path)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>(path))
                if (!clip.name.StartsWith("__preview") && clip.isHumanMotion) return clip;
            return null;
        }
        public static AnimationClip EmoteClip(byte index)
        {
            if (index < 1 || index > 3) return null;
            if (emotes[index-1] == null) emotes[index-1] = LoadClip("PlayerSocial/" + emotePaths[index-1]);
            return emotes[index-1];
        }
        private void Awake()
        {
            revive = GetComponent<SurvivalRevive>();
            avatar = GetComponent<MarsFusionAvatar>();
            animator = GetComponentInChildren<Animator>();
            var action = GetComponent<PlayerInteractionAction>();
            Transform weapon = action != null ? action.weaponVisual : null;
            if (weapon == null)
            {
                var gun = GetComponentInChildren<Gun>(true);
                if (gun != null) weapon = gun.transform;
            }
            // Gun can live on the player root. Never hide the player or its skinned model.
            weaponRenderers = weapon != null && weapon != transform && weapon.IsChildOf(transform)
                && !ModelIsInside(weapon) ? weapon.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            if (animator == null || animator.transform == transform) return;
            model = animator.transform;
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            kneel = Resources.Load<AnimationClip>("PlayerSocial/ReviveCrouch");
        }
        private void LateUpdate()
        {
            if (revive == null || revive.Object == null || !revive.Object.IsValid || avatar == null || !avatar.IsAlive
                || model == null || (GetComponent<GameJam.Environment.Mars.PlayerSandstorm>()?.IsDowned ?? false))
            { StopPose(); return; }
            var target = revive.RescueTarget;
            AnimationClip clip = null;
            double time = 0;
            if (revive.IsRecovering)
            {
                clip = ReviveGetUpClip;
                if (clip != null) time = clip.length - revive.GetUpDeadline.RemainingTime(revive.Runner).GetValueOrDefault();
            }
            else if (target != null)
            {
                clip = kneel;
                // Hold the last crouched pose until the authoritative reservation finishes.
                if (clip != null) time = Mathf.Min(target.ReviveElapsed, Mathf.Max(0, clip.length - .001f));
            }
            else if (revive.IsLeavingRescue)
            {
                clip = RescueStandClip;
                if (clip != null) time = clip.length - revive.RescueExitDeadline.RemainingTime(revive.Runner).GetValueOrDefault();
            }
            else if (revive.IsEmoting)
            {
                clip = EmoteClip(revive.Emote);
                if (clip != null) time = clip.length - revive.EmoteDeadline.RemainingTime(revive.Runner).GetValueOrDefault();
            }
            if (clip == null) { StopPose(); return; }
            PlayPose(clip, time);
            if (revive.IsRecovering || target != null || revive.IsLeavingRescue) GroundPose();
        }
        private bool ModelIsInside(Transform weapon)
        {
            return animator != null && (animator.transform == weapon || animator.transform.IsChildOf(weapon));
        }
        private void PlayPose(AnimationClip clip, double time)
        {
            if (currentClip != clip || !graph.IsValid())
            {
                if (!graph.IsValid())
                {
                    originalRootMotion = animator.applyRootMotion;
                    animator.applyRootMotion = false;
                    graph = PlayableGraph.Create("Player emotes and cooperative revival");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    mixer = AnimationMixerPlayable.Create(graph, 2);
                    var output = AnimationPlayableOutput.Create(graph, "Social pose", animator);
                    output.SetSourcePlayable(mixer);
                    graph.Play();
                    foreach (var renderer in weaponRenderers)
                    {
                        if (renderer == null) continue;
                        weaponVisibility[renderer] = renderer.enabled;
                        renderer.enabled = false;
                    }
                }
                if (previousPlayable.IsValid())
                {
                    mixer.DisconnectInput(1);
                    graph.DestroyPlayable(previousPlayable);
                }
                previousPlayable = playable;
                if (previousPlayable.IsValid())
                {
                    // Blend from the sampled crouch, including an early cancellation.
                    mixer.DisconnectInput(0);
                    graph.Connect(previousPlayable, 0, mixer, 1);
                }
                currentClip = clip;
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(true);
                playable.SetSpeed(0);
                graph.Connect(playable, 0, mixer, 0);
                blendStarted = Time.unscaledTime;
            }
            model.localPosition = restPosition;
            model.localRotation = restRotation;
            playable.SetTime(Mathf.Clamp((float)time, 0, Mathf.Max(0, clip.length - .001f)));
            float blend = previousPlayable.IsValid()
                ? Mathf.Clamp01((Time.unscaledTime - blendStarted) / TransitionDuration) : 1f;
            mixer.SetInputWeight(0, blend);
            mixer.SetInputWeight(1, 1f - blend);
            if (blend >= 1f && previousPlayable.IsValid())
            {
                mixer.DisconnectInput(1);
                graph.DestroyPlayable(previousPlayable);
                previousPlayable = default;
            }
            graph.Evaluate(0);
        }
        private void GroundPose()
        {
            if (!animator.isHuman) return;
            float offset = float.NegativeInfinity;
            foreach (var bone in SupportBones)
            {
                var support = animator.GetBoneTransform(bone);
                if (support == null) continue;
                Vector3 point = support.position;
                if (Physics.Raycast(new Vector3(point.x, transform.position.y + 1, point.z), Vector3.down,
                    out var hit, 3, 1, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                    offset = Mathf.Max(offset, hit.point.y + (bone == HumanBodyBones.Hips ? .14f : .035f) - point.y);
            }
            if (!float.IsNegativeInfinity(offset)) model.position += Vector3.up * Mathf.Clamp(offset, -1.5f, .6f);
        }
        private void StopPose()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
                if (animator != null) animator.applyRootMotion = originalRootMotion;
                if (model != null) { model.localPosition = restPosition; model.localRotation = restRotation; }
            }
            currentClip = null;
            playable = default;
            previousPlayable = default;
            foreach (var entry in weaponVisibility)
                if (entry.Key != null) entry.Key.enabled = entry.Value;
            weaponVisibility.Clear();
        }
        private void OnDisable() => StopPose();
        private void OnDestroy() => StopPose();
    }
}
