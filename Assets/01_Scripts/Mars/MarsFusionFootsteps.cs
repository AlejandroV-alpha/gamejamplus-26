using Fusion.Addons.SimpleKCC;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameJam.Mars
{
    // Cosmetic contacts come from each visible avatar's animation; no per-step RPCs.
    public sealed class MarsFusionFootsteps : MonoBehaviour
    {
        public GameObject decalPrefab;
        public Transform leftFoot, rightFoot;
        private SimpleKCC kcc;
        private AudioSource audioSource;
        private int pendingFoot = -1;
        private float lastContact = -1;
        public int StepsCreated { get; private set; }
        private void Awake()
        {
            kcc = GetComponent<SimpleKCC>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; audioSource.spatialBlend = 1;
            audioSource.rolloffMode = AudioRolloffMode.Linear; audioSource.minDistance = 1.5f; audioSource.maxDistance = 20;
        }
        public void QueueContact(bool right)
        {
            var avatar=GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();
            if(avatar!=null && avatar.Object!=null && avatar.Object.IsValid && !avatar.IsAlive) return;
            if(GetComponent<GameJam.Multiplayer.PlayerInteractionAction>()?.IsBusy ?? false) return;
            if(GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false) return;
            if (Time.time - lastContact < .22f || kcc == null || kcc.Object == null ||
                !kcc.Object.IsValid || !kcc.IsGrounded || kcc.RealSpeed < .2f) return;
            pendingFoot = right ? 1 : 0;
            lastContact = Time.time;
        }
        private void LateUpdate()
        {
            if (pendingFoot < 0) return;
            var avatar=GetComponent<GameJam.Multiplayer.MarsFusionAvatar>();
            if((avatar!=null && avatar.Object!=null && avatar.Object.IsValid && !avatar.IsAlive)
                || (GetComponent<GameJam.Multiplayer.PlayerInteractionAction>()?.IsBusy ?? false)
                || (GetComponent<GameJam.Combat.PlayerHurtReaction>()?.IsReacting ?? false)) {pendingFoot=-1;return;}
            bool right = pendingFoot == 1;
            pendingFoot = -1;
            if (kcc == null || !kcc.IsGrounded) return;
            var foot = right ? rightFoot : leftFoot;
            if (foot == null || decalPrefab == null) return;
            if (!Physics.Raycast(foot.position + Vector3.up * .35f, Vector3.down, out var hit, .9f, 1, QueryTriggerInteraction.Ignore)) return;
            var surface = hit.collider.GetComponent<MarsFootstepSurface>();
            if (surface == null || surface.profile == null) return;
            var profile = surface.profile;
            var sound = right ? profile.rightSound : profile.leftSound;
            if (sound != null && sound.StepSounds.Count > 0)
            {
                // One voice per avatar: long sample tails cannot stack into a second step.
                audioSource.clip = sound.StepSounds[Random.Range(0, sound.StepSounds.Count)];
                audioSource.volume = profile.volume;
                audioSource.Play();
            }
            if (!surface.AllowsFootprint(hit.point)) return;
            var settings = right ? profile.rightPrint : profile.leftPrint;
            if (settings == null) return;
            var instance = MarsFootprintPool.GetShared(decalPrefab).Get;
            var projector = instance.GetComponent<DecalProjector>();
            projector.material = settings.Material;
            projector.size = new Vector3(.25f, .36f, .45f);
            projector.pivot = settings.Pivot;
            projector.drawDistance = 35;
            projector.enabled = false;
            var mark = instance.GetComponentInChildren<MeshRenderer>();
            mark.sharedMaterial = right ? profile.rightMark : profile.leftMark;
            mark.transform.localScale = new Vector3(profile.footprintSize.x, profile.footprintSize.y, 1);
            instance.transform.position = hit.point + hit.normal * .012f;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal).normalized;
            instance.transform.rotation = Quaternion.LookRotation(hit.normal, forward);
            instance.GetComponent<MarsFootprintFade>().Restart(profile.lifetime);
            StepsCreated++;
        }
    }
}
