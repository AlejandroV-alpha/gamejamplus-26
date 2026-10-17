using UnityEngine;

namespace GameJam.Mars
{
    [DisallowMultipleComponent]
    public sealed class b_CarRobotPatrol : MonoBehaviour
    {
        public Transform[] points = new Transform[0];
        [Min(0f)] public float speed = 1.5f;
        [Min(0f)] public float turnSpeed = 100f;
        [Min(0f)] public float pauseSeconds = 1.5f;
        [Min(0.05f)] public float arrivalDistance = 0.25f;
        public Terrain terrain;
        public bool followTerrain = true;
        [Tooltip("Correction if the model's front does not face local +Z.")]
        public float headingOffset;
        private int index;
        private float pause;
        private Quaternion modelTilt;
        private b_CarRobotWheels wheelController;

        private void Start()
        {
            if (GetComponent<b_CarRobotWheels>() == null)
                gameObject.AddComponent<b_CarRobotWheels>();
            wheelController = GetComponent<b_CarRobotWheels>();
            if (terrain == null) terrain = Terrain.activeTerrain;
            if (points == null || points.Length < 2)
            {
                Debug.LogWarning("b_CarRobotPatrol: asigna al menos dos puntos de ruta.", this);
                enabled = false;
                return;
            }
            modelTilt = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f)) * transform.rotation;
        }

        private float GroundHeight(Vector3 position)
        {
            if (terrain == null || terrain.terrainData == null) return position.y;
            return terrain.SampleHeight(position) + terrain.transform.position.y;
        }

        private void Update()
        {
            if (followTerrain && wheelController != null &&
                wheelController.TryGroundPosition(transform.position, out var grounded))
                transform.position = grounded;
            if (pause > 0f) { pause -= Time.deltaTime; return; }
            var target = points[index];
            if (target == null) { index = (index + 1) % points.Length; return; }
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= arrivalDistance * arrivalDistance)
            {
                index = (index + 1) % points.Length;
                pause = pauseSeconds;
                return;
            }
            Quaternion heading = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, headingOffset, 0f) * modelTilt;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, turnSpeed * Time.deltaTime);
            Vector3 next = Vector3.MoveTowards(transform.position,
                new Vector3(target.position.x, transform.position.y, target.position.z), speed * Time.deltaTime);
            if (followTerrain && wheelController != null)
            {
                if (!wheelController.TryGroundPosition(next, out var surfacePosition)) return;
                next = surfacePosition;
            }
            transform.position = next;
        }

#if UNITY_EDITOR
        [ContextMenu("b_Crear ruta de 4 puntos")]
        private void CreateRoute()
        {
            if (Application.isPlaying) return;
            if (points != null && System.Array.Exists(points, point => point != null))
            {
                Debug.LogWarning("La ruta ya tiene puntos. Muevelos desde la jerarquia.", this);
                return;
            }
            UnityEditor.Undo.RecordObject(this, "Crear patrulla");
            var root = new GameObject("b_CarRobot_Ruta");
            UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Crear ruta");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.transform.position = transform.position;
            Vector3[] offsets = { Vector3.forward * 6f, new Vector3(6f, 0f, 6f),
                Vector3.right * 6f, Vector3.zero };
            points = new Transform[4];
            if (terrain == null) terrain = Terrain.activeTerrain;
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("b_Punto_0" + (i + 1));
                UnityEditor.Undo.RegisterCreatedObjectUndo(point, "Crear punto");
                point.transform.SetParent(root.transform, false);
                Vector3 position = transform.position + offsets[i];
                if (terrain != null) position.y = GroundHeight(position);
                point.transform.position = position;
                points[i] = point.transform;
            }
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (points == null || points.Length == 0) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] == null) continue;
                Gizmos.DrawWireSphere(points[i].position, 0.4f);
                var next = points[(i + 1) % points.Length];
                if (next != null) Gizmos.DrawLine(points[i].position, next.position);
            }
        }
    }
}
