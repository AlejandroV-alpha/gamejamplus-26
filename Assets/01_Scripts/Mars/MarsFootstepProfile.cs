using ModularFootstepSystem;
using UnityEngine;

namespace GameJam.Mars
{
    [CreateAssetMenu(menuName = "GameJam/Environment/Footstep profile")]
    public sealed class MarsFootstepProfile : ScriptableObject
    {
        public FootprintDecalSettings leftPrint, rightPrint;
        public FootstepSoundData leftSound, rightSound;
        public Material leftMark, rightMark;
        public Vector2 footprintSize = new Vector2(.28f, .48f);
        [Range(.05f, 1)] public float volume = .3f;
        [Min(1)] public float lifetime = 14;
    }
}
