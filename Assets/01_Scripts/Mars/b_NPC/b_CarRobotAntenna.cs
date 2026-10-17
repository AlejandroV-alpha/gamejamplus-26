using System.Collections.Generic;
using UnityEngine;

namespace GameJam.Mars
{
    [DisallowMultipleComponent]
    public sealed class b_CarRobotAntenna : MonoBehaviour
    {
        [Tooltip("Upper assembly pieces from ModelCarParts; they move together.")]
        public string[] antennaParts = { "model_part3", "model_part6", "model_part7" };
        [Range(0f, 45f)] public float scanAngle = 18f;
        [Range(0.01f, 1f)] public float scanFrequency = 0.12f;
        [Range(0f, 8f)] public float swayAngle = 1.5f;
        private Transform pivot;
        private Quaternion restRotation;
        private Vector3 upAxis, tiltAxis;

        private void Start()
        {
            var parts = new List<Transform>();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (!System.Array.Exists(antennaParts, name => name == child.name)) continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer == null) continue;
                parts.Add(child);
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds)
            {
                Debug.LogWarning("b_CarRobotAntenna: revisa Antenna Parts; no se encontro la estructura superior.", this);
                enabled = false;
                return;
            }
            // One shared pivot keeps the connected upper pieces together.
            pivot = new GameObject("b_efecto_antenaExploracion").transform;
            pivot.SetParent(transform, false);
            pivot.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var part in parts) part.SetParent(pivot, true);
            restRotation = pivot.localRotation;
            upAxis = transform.InverseTransformDirection(Vector3.up).normalized;
            tiltAxis = Vector3.Cross(upAxis, Vector3.right);
            if (tiltAxis.sqrMagnitude < 0.01f) tiltAxis = Vector3.forward;
            tiltAxis.Normalize();
        }

        private void LateUpdate()
        {
            if (pivot == null) return;
            float phase = Time.time * scanFrequency * Mathf.PI * 2f;
            float scan = Mathf.Sin(phase) * scanAngle;
            float sway = Mathf.Sin(phase * 2.3f) * swayAngle;
            pivot.localRotation = Quaternion.AngleAxis(scan, upAxis) *
                Quaternion.AngleAxis(sway, tiltAxis) * restRotation;
        }

        private void OnDisable()
        {
            if (pivot != null) pivot.localRotation = restRotation;
        }
    }
}
