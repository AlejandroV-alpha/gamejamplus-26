using UnityEngine;

namespace GameJam.Multiplayer
{
    /// <summary>Slowly spins (and optionally bobs) a decorative object, like the lobby holograms.</summary>
    public sealed class LobbySpin : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new(0f, 12f, 0f);
        public Space space = Space.Self;
        [Tooltip("Vertical float distance in meters; 0 disables bobbing.")]
        public float bobHeight;
        public float bobSpeed = 1.5f;
        private Vector3 restPosition;

        private void Awake() => restPosition = transform.localPosition;

        private void Update()
        {
            transform.Rotate(degreesPerSecond * Time.deltaTime, space);
            if (bobHeight > 0f)
                transform.localPosition = restPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }
    }
}
