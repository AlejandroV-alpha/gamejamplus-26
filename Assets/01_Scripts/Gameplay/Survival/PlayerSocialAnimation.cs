using Fusion;
using GameJam.Combat;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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
        private Transform model, leftArm, rightArm, leftForearm, rightForearm;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private AnimationClip currentClip, kneel, getUp;
        private Gun gun;
        private bool hidGun, gunWasActive, originalRootMotion;
        private static readonly HumanBodyBones[] SupportBones = { HumanBodyBones.Hips, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        private float rescueStarted = -1;
        private NetworkId rescueId;

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
            gun = GetComponentInChildren<Gun>(true);
            if (animator == null || animator.transform == transform) return;
            model = animator.transform;
            restPosition = model.localPosition;
            restRotation = model.localRotation;
            kneel = LoadClip("PlayerSocial/Kneel");
            getUp = Resources.Load<AnimationClip>("Animations/StormStandUp");
            if (getUp == null) getUp = LoadClip("Animations/StormStandUpSource");
            if (animator.isHuman)
            {
                leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            }
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
                clip = getUp;
                if (clip != null) time = clip.length - revive.GetUpDeadline.RemainingTime(revive.Runner).GetValueOrDefault();
            }
            else if (target != null)
            {
                if (rescueStarted < 0 || rescueId != target.Object.Id)
                { rescueStarted = Time.time; rescueId = target.Object.Id; }
                clip = kneel;
                if (clip != null) time = Mathf.Min(Time.time - rescueStarted, Mathf.Max(0, clip.length - .03f));
            }
            else if (revive.IsEmoting)
            {
                clip = EmoteClip(revive.Emote);
                if (clip != null) time = clip.length - revive.EmoteDeadline.RemainingTime(revive.Runner).GetValueOrDefault();
            }
            if (target == null) rescueStarted = -1;
            if (clip == null) { StopPose(); return; }
            if (currentClip != clip || !graph.IsValid())
            {
                StopPose();
                currentClip = clip;
                originalRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false;
                graph = PlayableGraph.Create("Player emotes and cooperative revival");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(true);
                playable.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(graph, "Social pose", animator);
                output.SetSourcePlayable(playable);
                graph.Play();
                if (gun != null)
                {
                    gunWasActive = gun.gameObject.activeSelf;
                    gun.gameObject.SetActive(false);
                    hidGun = true;
                }
            }
            model.localPosition = restPosition;
            model.localRotation = restRotation;
            playable.SetTime(Mathf.Clamp((float)time, 0, Mathf.Max(0, clip.length - .001f)));
            graph.Evaluate(0);
            if (revive.IsRecovering) GroundPose();
            if (target != null)
            {
                float pump = Mathf.Sin((Time.time - rescueStarted) * Mathf.PI * 4f) * 8f;
                if (leftArm != null) leftArm.localRotation *= Quaternion.Euler(-40 + pump, 0, -18);
                if (rightArm != null) rightArm.localRotation *= Quaternion.Euler(-40 + pump, 0, 18);
                if (leftForearm != null) leftForearm.localRotation *= Quaternion.Euler(-35, 0, 0);
                if (rightForearm != null) rightForearm.localRotation *= Quaternion.Euler(-35, 0, 0);
            }
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
            if (hidGun && gun != null) gun.gameObject.SetActive(gunWasActive);
            hidGun = false;
        }
        private void OnDisable() => StopPose();
        private void OnDestroy() => StopPose();
    }
}
