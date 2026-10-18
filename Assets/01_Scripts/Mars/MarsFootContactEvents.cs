using UnityEngine;

namespace GameJam.Mars
{
    // Lives beside the Animator; only the dominant locomotion clip may emit a contact.
    public sealed class MarsFootContactEvents : MonoBehaviour
    {
        private MarsFusionFootsteps footsteps;
        private void Awake() => footsteps = GetComponentInParent<MarsFusionFootsteps>();
        public void OnMarsFootContact(AnimationEvent contact)
        {
            if (footsteps != null && contact.animatorClipInfo.weight > .5f)
                footsteps.QueueContact(contact.intParameter == 1);
        }
    }
}
