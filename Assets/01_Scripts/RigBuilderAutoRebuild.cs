using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace GameJam.Animation
{
    /// <summary>
    /// Rebuilds the animation rig after the Animator has created its controller graph.
    /// RigBuilder.OnEnable builds too early, so constraints stay inactive until the component is toggled.
    /// </summary>
    public sealed class RigBuilderAutoRebuild : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var rigBuilder = GetComponent<RigBuilder>();
            if (rigBuilder == null)
                rigBuilder = GetComponentInChildren<RigBuilder>(true);

            if (rigBuilder == null)
            {
                Debug.LogWarning("RigBuilderAutoRebuild could not find a RigBuilder.", this);
                yield break;
            }

            yield return null;

            rigBuilder.enabled = false;
            rigBuilder.enabled = true;
        }
    }
}
