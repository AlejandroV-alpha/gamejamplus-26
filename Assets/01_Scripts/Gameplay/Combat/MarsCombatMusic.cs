
using UnityEngine;

namespace GameJam.Combat
{
    [DisallowMultipleComponent]
    public sealed class MarsCombatMusic : MonoBehaviour
    {
        [SerializeField] private AudioSource exploration;
        [SerializeField] private AudioSource combat;
        [SerializeField, Min(1f)] private float secondsAfterLastHit = 9f;
        [SerializeField, Min(.1f)] private float fadeSeconds = 1.25f;

        private static MarsCombatMusic active;
        private float combatUntil;
        private float blend;
        private float explorationVolume;
        private float combatVolume;

        private void Awake()
        {
            explorationVolume = exploration.volume;
            combatVolume = combat.volume;
            combat.volume = 0f;
        }

        private void OnEnable() => active = this;

        private void OnDisable()
        {
            if (active == this) active = null;
            combatUntil = 0f;
            blend = 0f;
            exploration.volume = explorationVolume;
            combat.volume = 0f;
            combat.Stop();
        }

        public static void ReportHit()
        {
            if (active != null)
                active.combatUntil = Time.unscaledTime + active.secondsAfterLastHit;
        }

        private void Update()
        {
            bool fighting = Time.unscaledTime < combatUntil;
            if (fighting && !combat.isPlaying) combat.Play();

            blend = Mathf.MoveTowards(blend, fighting ? 1f : 0f, Time.unscaledDeltaTime / fadeSeconds);
            exploration.volume = explorationVolume * (1f - blend);
            combat.volume = combatVolume * blend;

            if (!fighting && blend == 0f && combat.isPlaying) combat.Stop();
        }
    }
}

