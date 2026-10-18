using System;
using UnityEngine;

namespace GameJam.Combat
{
    /// <summary>
    /// Bala de energia visual: viaja de la boca al impacto con estela y avisa al llegar.
    /// </summary>
    public sealed class EnergyBolt : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float speed = 140f;
        [SerializeField, Min(0f)] private float lingerAfterImpact = 0.25f;

        private Vector3 target;
        private Action onArrive;
        private bool arrived;

        /// <summary>
        /// Inicia el vuelo hacia el punto final; onArrive se ejecuta una sola vez al llegar.
        /// </summary>
        public void Launch(Vector3 origin, Vector3 endpoint, Action arrive)
        {
            transform.position = origin;
            Vector3 direction = endpoint - origin;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
            target = endpoint;
            onArrive = arrive;
            arrived = false;
        }

        /// <summary>
        /// Avanza la bala y deja que la estela se desvanezca tras el impacto.
        /// </summary>
        private void Update()
        {
            if (arrived)
            {
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            if ((transform.position - target).sqrMagnitude > 0.0001f)
            {
                return;
            }

            arrived = true;
            onArrive?.Invoke();
            onArrive = null;
            foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            foreach (Light boltLight in GetComponentsInChildren<Light>())
            {
                boltLight.enabled = false;
            }
            Destroy(gameObject, lingerAfterImpact);
        }
    }
}
