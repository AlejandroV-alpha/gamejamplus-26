using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Estallido verde al curarse con la estacion medica: circulo de curacion en el suelo,
    /// explosion verde en el pecho y aura ascendente que sigue al astronauta, con un acorde brillante.
    /// Usa efectos del pack Hovl agrupados en Resources/VFX/HealPickupFx. Lo ven todos los jugadores.
    /// </summary>
    public sealed class PlayerHealFx : MonoBehaviour
    {
        private const float CircleScale = .32f, BurstStop = .45f, AuraStop = 1.4f, CircleStop = 1.6f, Life = 3.5f;
        private static GameObject prefab;
        private static AudioClip chime;

        private Transform follow;
        private float born;
        private ParticleSystem[] burst, aura, circle;
        private bool burstStopped, auraStopped, circleStopped;

        public static void Play(Transform player, bool local)
        {
            if (player == null) return;
            if (prefab == null) prefab = Resources.Load<GameObject>("VFX/HealPickupFx");
            if (prefab == null) return;
            var go = Instantiate(prefab, player.position, Quaternion.identity);
            var fx = go.AddComponent<PlayerHealFx>();
            fx.follow = player;
            fx.born = Time.time;
            fx.Setup(local);
        }

        private void Setup(bool local)
        {
            foreach (Transform child in transform)
            {
                var systems = child.GetComponentsInChildren<ParticleSystem>(true);
                if (child.name.Contains("Healing circle")) { circle = systems; child.localScale = Vector3.one * CircleScale; }
                else if (child.name.Contains("Green hit")) burst = systems;
                else aura = systems;
            }
            if (chime == null) chime = BuildChime();
            var audio = gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = local ? 0f : 1f;
            audio.minDistance = 3f;
            audio.maxDistance = 30f;
            audio.PlayOneShot(chime, local ? .55f : .8f);
            Destroy(gameObject, Life);
        }

        private void LateUpdate()
        {
            // Todo el efecto acompaña al jugador si se mueve mientras dura.
            if (follow != null) transform.position = follow.position;
            float age = Time.time - born;
            if (!burstStopped && age > BurstStop) { Stop(burst); burstStopped = true; }
            if (!auraStopped && age > AuraStop) { Stop(aura); auraStopped = true; }
            if (!circleStopped && age > CircleStop) { Stop(circle); circleStopped = true; }
        }

        private static void Stop(ParticleSystem[] systems)
        {
            if (systems == null) return;
            foreach (var ps in systems)
                if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        // Arpegio ascendente brillante (do-mi-sol-do) con brillo suave: suena a "curado".
        private static AudioClip BuildChime()
        {
            const int rate = 44100;
            int length = (int)(rate * 1.1f);
            var data = new float[length];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * .07f * rate);
                for (int i = 0; start + i < length; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Min(1f, t / .01f) * Mathf.Exp(-t * 4.5f);
                    float w = 2f * Mathf.PI * notes[n] * t;
                    data[start + i] += (Mathf.Sin(w) + .3f * Mathf.Sin(w * 2f) + .12f * Mathf.Sin(w * 3f)) * env * .22f;
                }
            }
            float peak = .001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            for (int i = 0; i < length; i++) data[i] = data[i] / peak * .8f;
            var clip = AudioClip.Create("HealChime", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
