using System.Collections.Generic;
using UnityEngine;
using State = GusanoExcavadorFusion.State;

/// <summary>
/// Da vida a la boca del gusano encima de su animacion: los colmillos tiemblan y castañetean,
/// las tres mandibulas se abren y cierran, y los pliegues y ganchos se agitan.
/// Mas intenso al atacar o al tener atrapado a un jugador.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class GusanoTeethMotion : MonoBehaviour
{
    sealed class Part
    {
        public Transform t;
        public Quaternion rest, applied;
        public float phase, amplitude, speed;
        public Vector3 axis;
        public bool jaw;
    }

    [SerializeField] float idleIntensity = .35f;
    [SerializeField] float crawlIntensity = .6f;
    [SerializeField] float attackIntensity = 1.4f;

    readonly List<Part> parts = new List<Part>();
    GusanoExcavadorFusion worm;
    float intensity;

    void Awake()
    {
        worm = GetComponentInParent<GusanoExcavadorFusion>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("Mandibula_")) Add(t, 22f, 3.2f, Vector3.right, true);
            else if (n.StartsWith("Colmillo_") || n.StartsWith("Punta_marfil_")) Add(t, 9f, 9f, Random.onUnitSphere, false);
            else if (n.StartsWith("Pliegue_boca_") || n.StartsWith("Nodulo_labio_")) Add(t, 6f, 4f, Vector3.right, false);
            else if (n.StartsWith("Gancho_lateral_")) Add(t, 14f, 2.2f, Vector3.forward, false);
        }
    }

    void Add(Transform t, float amplitude, float speed, Vector3 axis, bool jaw)
    {
        parts.Add(new Part
        {
            t = t, rest = t.localRotation, applied = t.localRotation,
            phase = Random.value * Mathf.PI * 2f, amplitude = amplitude, speed = speed * Random.Range(.85f, 1.15f),
            axis = axis.normalized, jaw = jaw,
        });
    }

    void LateUpdate()
    {
        State state = worm != null ? worm.CurrentState : State.Crawl;
        float target = state == State.Attack || state == State.Hold ? attackIntensity
            : state == State.Crawl ? crawlIntensity
            : state == State.Hidden ? 0f : idleIntensity;
        intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        float time = Time.time;

        foreach (Part p in parts)
        {
            // Si la animacion no toco este hueso, se parte de la pose de reposo para no acumular giros.
            Quaternion baseRotation = p.t.localRotation == p.applied ? p.rest : p.t.localRotation;
            float wave;
            if (p.jaw)
            {
                // Mandibulas: abren despacio y cierran de golpe, como un mordisco.
                float cycle = Mathf.Repeat(time * p.speed * .5f + p.phase, 1f);
                wave = cycle < .7f ? Mathf.SmoothStep(0f, 1f, cycle / .7f) : 1f - Mathf.SmoothStep(0f, 1f, (cycle - .7f) / .3f);
            }
            else
            {
                wave = Mathf.Sin(time * p.speed + p.phase) * .7f + Mathf.Sin(time * p.speed * 2.3f + p.phase) * .3f;
            }
            p.applied = baseRotation * Quaternion.AngleAxis(wave * p.amplitude * intensity, p.axis);
            p.t.localRotation = p.applied;
        }
    }
}
