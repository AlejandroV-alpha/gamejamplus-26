using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace GameJam.Multiplayer
{
    public sealed class LobbyDepartureEffects : MonoBehaviour
    {
        public LobbyAlert alert;
        private static LobbyDepartureEffects current;
        private readonly List<(Transform part, Vector3 closed)> doorParts = new();
        private GameObject portal;
        private AudioSource alarm;
        private AudioClip alarmClip;
        private bool localEntered;
        private bool mustExitPortal;
        private float openAmount, nextEnterSend;
        private readonly Vector3 portalCenter = new(-3f, 1.45f, 6.4f);
        public static bool IsBlockingInput => current != null && current.alert != null
            && current.alert.DepartureActive && (current.alert.DeparturePhase == 2
                || current.alert.DeparturePhase == 3 || current.localEntered);

        private void Start()
        {
            current = this;
            foreach (var part in FindObjectsByType<Transform>())
                if (part.name == "Puerta_A_Hoja" || part.name == "Puerta_A_Barra"
                    || part.name.StartsWith("Puerta_A_Ventanilla") || part.name.StartsWith("Puerta_A_Relieve")
                    || part.name == "Puerta_A_Junta") doorParts.Add((part, part.position));
            portal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portal.name = "j_DeparturePortal";
            portal.transform.SetParent(transform, false);
            portal.transform.SetPositionAndRotation(portalCenter, Quaternion.identity);
            portal.transform.localScale = new Vector3(1.95f,2.7f,1);
            Destroy(portal.GetComponent<Collider>());
            if (alert.portalMaterial != null) portal.GetComponent<Renderer>().sharedMaterial = alert.portalMaterial;
            portal.SetActive(false);

            // A small synthesized rising/falling emergency siren, with soft attack/release.
            const int rate = 22050;
            var samples = new float[rate * 2];
            float phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float sweep = .5f - .5f * Mathf.Cos(t * Mathf.PI * 2);
                phase += 2 * Mathf.PI * (310 + 430 * sweep) / rate;
                float envelope = Mathf.Min(1,t*25) * Mathf.Min(1,(2-t)*25);
                samples[i] = Mathf.Sin(phase) * envelope * .55f;
            }
            alarmClip = AudioClip.Create("Emergency wah siren", samples.Length, 1, rate, false);
            alarmClip.SetData(samples,0);
            alarm = gameObject.AddComponent<AudioSource>();
            alarm.clip = alarmClip;
            alarm.loop = true;
            alarm.playOnAwake = false;
            alarm.spatialBlend = 0;
            alarm.volume = .2f;
        }

        public bool ContainsPortal(Vector3 position)
        {
            return openAmount > .85f && Mathf.Abs(position.x-portalCenter.x) < 1.15f
                && position.y > -.5f && position.y < 3f
                && Mathf.Abs(position.z-portalCenter.z) < .65f;
        }

        public static void ReleaseLocalPortalWait()
        {
            if (current == null) return;
            current.localEntered = false;
            current.mustExitPortal = true;
        }

        private void Update()
        {
            if (alert == null || !alert.DepartureActive) return;
            int phase = alert.DeparturePhase;
            if (phase < 4 && !alarm.isPlaying) alarm.Play();
            if (phase >= 4 && alarm.isPlaying) alarm.Stop();
            alarm.volume = phase == 2 ? .035f : .2f;
            if (phase < 4) return;
            openAmount = Mathf.SmoothStep(0,1,1-Mathf.Clamp01(alert.PhaseRemaining/1.4f));
            foreach (var (part, closed) in doorParts)
                if (part != null) part.position = closed + Vector3.up * (3.2f * openAmount);
            portal.SetActive(true);
            var runner = alert.Runner;
            var player = runner != null ? runner.GetPlayerObject(runner.LocalPlayer) : null;
            bool inside = player != null && ContainsPortal(player.transform.position);
            if (!inside) mustExitPortal = false;
            if (phase == 4 && inside && !mustExitPortal)
            {
                if (!localEntered)
                {
                    localEntered = true;
                    PortalWhiteTransition.Begin(alert.DestinationSceneIndex, runner);
                }
                // Acknowledge entry only once this client is covered. The last
                // acknowledgement can then load the shared scene without a flash.
                if (PortalWhiteTransition.IsFullyWhite && Time.unscaledTime >= nextEnterSend)
                {
                    nextEnterSend = Time.unscaledTime + .5f;
                    alert.EnterPortal();
                }
            }
        }

        public static void ApplyCameraShake(Camera camera)
        {
            if (current == null || current.alert == null || !current.alert.DepartureActive) return;
            int phase = current.alert.DeparturePhase;
            float strength = phase == 1 ? .035f : phase == 3 ? .22f * Mathf.Clamp01(current.alert.PhaseRemaining/1.25f) : 0;
            if (strength <= 0) return;
            float t = Time.unscaledTime * 20;
            float x = Mathf.PerlinNoise(t,7)-.5f;
            float y = Mathf.PerlinNoise(3,t)-.5f;
            camera.transform.position += camera.transform.right * (x*strength) + camera.transform.up * (y*strength);
            camera.transform.rotation *= Quaternion.Euler(y*strength*12,x*strength*7,x*strength*16);
        }

        private void OnGUI()
        {
            if (!localEntered || PortalWhiteTransition.IsBlockingInput || alert == null || alert.DeparturePhase != 4) return;
            GUI.Box(new Rect(Screen.width/2f-220,Screen.height-90,440,55),
                "EN EL PORTAL\nEsperando a que cruce el resto de la tripulación...");
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
            if (alarm != null) alarm.Stop();
            if (alarmClip != null) Destroy(alarmClip);
            if (portal != null) Destroy(portal);
        }
    }
}
