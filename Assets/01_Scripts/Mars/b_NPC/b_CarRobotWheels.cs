using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Mars
{
    [DisallowMultipleComponent]
    public sealed class b_CarRobotWheels : MonoBehaviour
    {
        [Tooltip("Pieces identified as wheels in ModelCarParts.fbx.")]
        public string[] wheelNames = { "model_part0", "model_part1", "model_part4", "model_part5", "model_part8", "model_part10" };
        public Vector3 localAxle = Vector3.up;
        public bool automaticAxle = true;
        public float spinMultiplier = 1f;
        public LayerMask groundLayers = ~0;
        [Min(0.05f)] public float maxStepHeight = 0.5f;
        [Min(0.1f)] public float groundProbeDistance = 4f;
        [Min(0f)] public float groundClearance = 0.03f;
        public Material dustMaterial;
        [Range(0f, 30f)] public float dustPerMeter = 9f;
        private ParticleSystem dust;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly List<Wheel> wheels = new List<Wheel>();
        private Vector3 lastPosition;
        private sealed class Wheel
        {
            public Transform transform;
            public Vector3 center;
            public float radius;
            public Vector3 axle;
            public float dustBudget;
        }

        private void Start()
        {
            lastPosition = transform.position;
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (!System.Array.Exists(wheelNames, name => child.name == name)) continue;
                var mesh = child.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                Bounds bounds = mesh.sharedMesh.bounds;
                Vector3 e = bounds.extents;
                Vector3 axis = automaticAxle
                    ? (e.x <= e.y && e.x <= e.z ? Vector3.right : e.y <= e.z ? Vector3.up : Vector3.forward)
                    : localAxle.normalized;
                Vector3 radial = Vector3.one - new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z));
                float radius = Mathf.Max(child.TransformVector(Vector3.right * e.x * radial.x).magnitude,
                    Mathf.Max(child.TransformVector(Vector3.up * e.y * radial.y).magnitude,
                        child.TransformVector(Vector3.forward * e.z * radial.z).magnitude));
                if (radius <= 0.001f) continue;
                wheels.Add(new Wheel { transform = child, center = bounds.center, radius = radius, axle = axis });
            }
            if (wheels.Count == 0)
                Debug.LogWarning("b_CarRobotWheels: no se encontraron las llantas de ModelCarParts. Revisa el modelo y Wheel Names.", this);
            if (dustMaterial != null) CreateDust();
        }

        private bool GroundAt(Vector3 center, float radius, out RaycastHit ground)
        {
            ground = default;
            int count = Physics.RaycastNonAlloc(center + Vector3.up * maxStepHeight, Vector3.down,
                hits, maxStepHeight + radius + groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform) ||
                    hit.normal.y < 0.55f || hit.distance >= closest) continue;
                closest = hit.distance;
                ground = hit;
            }
            return closest < float.PositiveInfinity;
        }

        public bool TryGroundPosition(Vector3 desired, out Vector3 grounded)
        {
            grounded = desired;
            Vector3 delta = desired - transform.position;
            float correction = float.NegativeInfinity;
            foreach (var wheel in wheels)
            {
                Vector3 center = wheel.transform.TransformPoint(wheel.center) + delta;
                if (GroundAt(center, wheel.radius, out var hit))
                    correction = Mathf.Max(correction, hit.point.y + wheel.radius + groundClearance - center.y);
            }
            if (float.IsNegativeInfinity(correction)) return false;
            grounded.y += correction;
            return true;
        }

        private void CreateDust()
        {
            var obj = new GameObject("b_efecto_tierraLlantas");
            obj.transform.SetParent(transform, false);
            dust = obj.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.playOnAwake = false;
            var emission = dust.emission;
            emission.enabled = false;
            var shape = dust.shape;
            shape.enabled = false;
            var fade = dust.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            dust.Play();
        }

        private void LateUpdate()
        {
            Vector3 displacement = transform.position - lastPosition;
            lastPosition = transform.position;
            // Ignore teleports; only animate actual travel, including reverse travel.
            if (displacement.sqrMagnitude > 100f) return;
            foreach (var wheel in wheels)
            {
                if (wheel.transform == null) continue;
                Vector3 axle = wheel.transform.TransformDirection(wheel.axle);
                Vector3 rollingDirection = Vector3.Cross(axle, Vector3.up).normalized;
                float distance = Vector3.Dot(displacement, rollingDirection);
                float angle = distance / wheel.radius * Mathf.Rad2Deg * spinMultiplier;
                Vector3 center = wheel.transform.TransformPoint(wheel.center);
                // Rotate around the mesh center even when exported pivots share the body origin.
                wheel.transform.RotateAround(center, axle, angle);
                if (dust == null) continue;
                float travel = Vector3.ProjectOnPlane(displacement, Vector3.up).magnitude;
                if (travel < 0.0001f || !GroundAt(center, wheel.radius, out var ground) ||
                    Mathf.Abs(center.y - wheel.radius - ground.point.y) > groundClearance + 0.2f) continue;
                wheel.dustBudget = Mathf.Min(5f, wheel.dustBudget + travel * dustPerMeter);
                while (wheel.dustBudget >= 1f)
                {
                    wheel.dustBudget--;
                    dust.Emit(new ParticleSystem.EmitParams
                    {
                        position = ground.point + ground.normal * 0.08f,
                        velocity = -Vector3.ProjectOnPlane(displacement, Vector3.up).normalized * 0.4f + Vector3.up * 0.25f,
                        startSize = Random.Range(0.25f, 0.6f),
                        startLifetime = Random.Range(0.6f, 1.2f),
                        startColor = new Color(0.7f, 0.4f, 0.23f, 0.45f),
                        rotation = Random.Range(0f, 360f)
                    }, 1);
                }
            }
        }

        private void OnDisable()
        {
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnEnable()
        {
            lastPosition = transform.position;
            if (dust != null) dust.Play();
        }
    }
}
