using System.Collections;
using Fusion;
using GameJam.Multiplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using Character = Starter.ThirdPersonCharacter.Player;
using CharacterInput = Starter.ThirdPersonCharacter.PlayerInput;

namespace GameJam.Combat
{
    /// <summary>
    /// Adapta la pistola hitscan a tercera persona y autoridad compartida.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class Gun : NetworkBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private Transform aimPoint;
        [SerializeField] private Transform recoilTarget;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip gunshot;
        [SerializeField] private AudioClip gunreload;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private GameObject muzzleFlashEffect;
        [SerializeField] private EnergyBolt boltEffect;
        [SerializeField] private GameObject impactEffect;
        [SerializeField, Min(1)] private int damage = 1;
        [SerializeField, Min(1f)] private float range = 100f;
        [SerializeField, Min(1)] private int magazineSize = 10;
        [SerializeField, Min(0.02f)] private float fireInterval = 0.25f;
        [SerializeField, Min(0.01f)] private float tracerDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float muzzleFlashDuration = 1f;
        [SerializeField, Min(0.01f)] private float impactEffectDuration = 1f;
        [SerializeField, Min(0f)] private float recoilDistance = 0.08f;
        [SerializeField, Min(0f)] private float recoilAngle = 5f;
        [SerializeField, Min(0.01f)] private float recoilDuration = 0.12f;
        [SerializeField] private LayerMask hitLayers = ~0;
        [SerializeField] private bool allowFriendlyFire;

        // Fusion conserva la municion individual dentro del objeto del astronauta.
        [Networked] private int currentAmmo { get; set; }

        private Character character;
        private CharacterInput characterInput;
        private MarsFusionAvatar avatar;
        private Camera aimCamera;
        private float nextShotTime;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Coroutine tracerCoroutine;
        private Coroutine recoilCoroutine;

        /// <summary>
        /// Obtiene los controles existentes y guarda la pose local de la pistola.
        /// </summary>
        private void Awake()
        {
            character = GetComponentInParent<Character>();
            characterInput = GetComponentInParent<CharacterInput>();
            avatar = GetComponentInParent<MarsFusionAvatar>();
            restPosition = RecoilBody.localPosition;
            restRotation = RecoilBody.localRotation;
            if (tracer != null)
            {
                tracer.enabled = false;
                tracer.useWorldSpace = true;
            }
        }

        /// <summary>
        /// Inicializa el cargador exclusivamente en su propietario.
        /// </summary>
        public override void Spawned()
        {
            nextShotTime = 0f;
            if (HasStateAuthority)
            {
                currentAmmo = Mathf.Max(1, magazineSize);
            }
        }

        /// <summary>
        /// Lee disparo y recarga despues de actualizar la camara del astronauta.
        /// </summary>
        private void LateUpdate()
        {
            if (!CanUseGun())
            {
                return;
            }

            UpdateAimPoint();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Reload();
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Shoot();
            }
        }

        /// <summary>
        /// Dispara una vez, descuenta una bala y envia solo efectos a los clientes.
        /// </summary>
        public void Shoot()
        {
            if (!CanUseGun() || firePoint == null)
            {
                return;
            }

            if (Time.time < nextShotTime)
            {
                return;
            }

            if (currentAmmo <= 0)
            {
                Debug.Log("[Gun] Sin municion. Pulsa R para recargar.", this);
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            if (aimCamera == null)
            {
                Debug.LogWarning("[Gun] Falta la camara principal.", this);
                return;
            }

            Vector3 origin = firePoint.position;
            Vector3 endpoint;
            Vector3 normal;
            Collider target = FindShotImpact(origin, out endpoint, out normal);
            currentAmmo--;
            nextShotTime = Time.time + Mathf.Max(0.02f, fireInterval);
            RpcShotEffects(origin, endpoint, normal, target != null);
            if (target != null)
            {
                ApplyHitDamage(target);
            }

            Debug.Log("[Gun] Municion: " + currentAmmo + " / " + magazineSize, this);
        }

        /// <summary>
        /// Llena el cargador al instante sin consumir reservas.
        /// </summary>
        public void Reload()
        {
            if (!CanUseGun() || currentAmmo >= magazineSize)
            {
                return;
            }

            currentAmmo = Mathf.Max(1, magazineSize);
            RpcReloadEffect();
            Debug.Log("[Gun] Recarga: " + currentAmmo + " / " + magazineSize, this);
        }

        /// <summary>
        /// Consulta la municion replicada de esta pistola.
        /// </summary>
        public int GetCurrentAmmo()
        {
            if (!IsReady())
            {
                return 0;
            }

            return currentAmmo;
        }

        /// <summary>
        /// Apunta con la camara y comprueba obstrucciones hasta la boca del arma.
        /// </summary>
        private Collider FindShotImpact(Vector3 origin, out Vector3 endpoint, out Vector3 normal)
        {
            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 aimPoint = aimRay.origin + aimRay.direction * range;
            RaycastHit hit;
            if (TryGetClosestHit(aimRay.origin, aimRay.direction, range, out hit))
            {
                aimPoint = hit.point;
            }

            // Este segmento impide disparar desde una boca que atraviesa una pared.
            Vector3 shoulder = character.CameraPivot.position;
            Vector3 muzzleOffset = origin - shoulder;
            if (TryGetClosestHit(shoulder, muzzleOffset.normalized, muzzleOffset.magnitude, out hit))
            {
                endpoint = hit.point;
                normal = hit.normal;
                return hit.collider;
            }

            Collider[] overlaps = Physics.OverlapSphere(origin, 0.015f, hitLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider obstacle in overlaps)
            {
                if (!IsOwnCollider(obstacle))
                {
                    endpoint = origin;
                    normal = -aimRay.direction;
                    return obstacle;
                }
            }

            Vector3 direction = aimPoint - origin;
            float shotDistance = Mathf.Min(range, direction.magnitude);
            if (TryGetClosestHit(origin, direction.normalized, shotDistance, out hit))
            {
                endpoint = hit.point;
                normal = hit.normal;
                return hit.collider;
            }

            endpoint = origin + direction.normalized * shotDistance;
            normal = Vector3.up;
            return null;
        }

        /// <summary>
        /// Obtiene el primer impacto solido ignorando todos los colliders propios.
        /// </summary>
        private bool TryGetClosestHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit closestHit)
        {
            closestHit = default;
            bool found = false;
            float closestDistance = distance;
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, hitLayers, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (IsOwnCollider(hit.collider))
                {
                    continue;
                }
                if (hit.distance <= closestDistance)
                {
                    closestDistance = hit.distance;
                    closestHit = hit;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Identifica el cuerpo y los objetos sujetos por el astronauta propietario.
        /// </summary>
        private bool IsOwnCollider(Collider candidate)
        {
            return candidate.transform.IsChildOf(character.transform);
        }

        /// <summary>
        /// Usa rutas de dano conocidas y evita modificar enemigos remotos sin Fusion.
        /// </summary>
        private void ApplyHitDamage(Collider target)
        {
            var enemy = target.GetComponentInParent<ReksaiFusionEnemy>();
            if (enemy != null && enemy.Runner == Runner)
            {
                enemy.TakeDamage(damage);
                return;
            }
            MarsFusionAvatar targetAvatar = target.GetComponentInParent<MarsFusionAvatar>();
            if (targetAvatar != null)
            {
                if (!allowFriendlyFire || targetAvatar.Runner != Runner)
                {
                    return;
                }
                if (targetAvatar.Object == null || !targetAvatar.Object.IsValid)
                {
                    return;
                }
                if (!targetAvatar.HasHealthSource)
                {
                    Debug.LogWarning("[Gun] Objetivo sin componente de vida.", targetAvatar);
                    return;
                }

                // El avatar existente envia el dano a la autoridad del objetivo.
                ITakeDamage damageReceiver = targetAvatar;
                damageReceiver.TakeDamage(damage);
                return;
            }

            foreach (MonoBehaviour component in target.GetComponentsInParent<MonoBehaviour>())
            {
                ITakeDamage damageReceiver = component as ITakeDamage;
                if (damageReceiver == null)
                {
                    continue;
                }
                if (Runner.GameMode == GameMode.Single)
                {
                    damageReceiver.TakeDamage(damage);
                }
                else
                {
                    Debug.LogWarning("[Gun] Dano Shared pendiente: el objetivo no tiene una ruta de autoridad Fusion verificada.", component);
                }
                return;
            }
        }

        /// <summary>
        /// Reproduce el disparo en cada cliente sin aplicar dano desde el RPC visual.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcShotEffects(Vector3 origin, Vector3 endpoint, Vector3 normal, bool hasImpact)
        {
            if (audioSource != null && gunshot != null)
            {
                audioSource.PlayOneShot(gunshot);
            }
            PlayRecoil();
            ShowMuzzleFlash(origin, endpoint);
            if (boltEffect != null)
            {
                // El impacto espera a que la bala visual llegue al objetivo.
                EnergyBolt bolt = Instantiate(boltEffect, origin, Quaternion.identity);
                bolt.Launch(origin, endpoint, hasImpact ? () => ShowImpactEffect(endpoint, normal) : (System.Action)null);
                return;
            }

            ShowTracer(origin, endpoint);
            if (hasImpact)
            {
                ShowImpactEffect(endpoint, normal);
            }
        }

        /// <summary>
        /// Reproduce la recarga en todos los clientes sin cambiar su municion.
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcReloadEffect()
        {
            if (audioSource != null && gunreload != null)
            {
                audioSource.PlayOneShot(gunreload);
            }
        }

        /// <summary>
        /// Crea el destello de la boca orientado hacia el punto de impacto.
        /// </summary>
        private void ShowMuzzleFlash(Vector3 origin, Vector3 endpoint)
        {
            if (muzzleFlashEffect == null)
            {
                return;
            }

            Vector3 direction = endpoint - origin;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : firePoint.rotation;
            GameObject effect = Instantiate(muzzleFlashEffect, origin, rotation);
            Destroy(effect, muzzleFlashDuration);
        }

        /// <summary>
        /// Crea un impacto visual local y lo elimina tras su duracion.
        /// </summary>
        private void ShowImpactEffect(Vector3 position, Vector3 normal)
        {
            EnemyBloodFx.TryBleedAt(position, normal);
            if (impactEffect == null)
            {
                return;
            }

            GameObject effect = Instantiate(impactEffect, position + normal * 0.01f, Quaternion.LookRotation(normal));
            Destroy(effect, impactEffectDuration);
        }

        /// <summary>
        /// Inicia una trazadora opcional sin crear objetos de red.
        /// </summary>
        private void ShowTracer(Vector3 origin, Vector3 endpoint)
        {
            if (tracer == null)
            {
                return;
            }
            if (tracerCoroutine != null)
            {
                StopCoroutine(tracerCoroutine);
            }

            tracerCoroutine = StartCoroutine(TracerRoutine(origin, endpoint));
        }

        /// <summary>
        /// Mantiene la linea visible durante el tiempo configurado.
        /// </summary>
        private IEnumerator TracerRoutine(Vector3 origin, Vector3 endpoint)
        {
            tracer.positionCount = 2;
            tracer.SetPosition(0, origin);
            tracer.SetPosition(1, endpoint);
            tracer.enabled = true;
            yield return new WaitForSeconds(tracerDuration);
            tracer.enabled = false;
            tracerCoroutine = null;
        }

        /// <summary>
        /// Reinicia el retroceso visual sin modificar el movimiento del jugador.
        /// </summary>
        private void PlayRecoil()
        {
            if (recoilCoroutine != null)
            {
                StopCoroutine(recoilCoroutine);
            }

            recoilCoroutine = StartCoroutine(RecoilRoutine());
        }

        /// <summary>
        /// Devuelve suavemente la pistola a su pose local original.
        /// </summary>
        private IEnumerator RecoilRoutine()
        {
            Vector3 kickedPosition = restPosition - restRotation * Vector3.forward * recoilDistance;
            Quaternion kickedRotation = restRotation * Quaternion.Euler(-recoilAngle, 0f, 0f);
            float elapsed = 0f;
            while (elapsed < recoilDuration)
            {
                float progress = elapsed / recoilDuration;
                RecoilBody.localPosition = Vector3.Lerp(kickedPosition, restPosition, progress);
                RecoilBody.localRotation = Quaternion.Slerp(kickedRotation, restRotation, progress);
                elapsed += Time.deltaTime;
                yield return null;
            }

            RecoilBody.localPosition = restPosition;
            RecoilBody.localRotation = restRotation;
            recoilCoroutine = null;
        }

        /// <summary>
        /// Limpia efectos y restaura la pose cuando se desactiva la pistola.
        /// </summary>
        private void OnDisable()
        {
            StopAllCoroutines();
            tracerCoroutine = null;
            recoilCoroutine = null;
            if (tracer != null)
            {
                tracer.enabled = false;
            }
            RecoilBody.localPosition = restPosition;
            RecoilBody.localRotation = restRotation;
        }

        /// <summary>
        /// Coloca el AimPoint del rig en el impacto del mismo rayo que usa el disparo.
        /// </summary>
        private void UpdateAimPoint()
        {
            if (aimPoint == null || firePoint == null)
            {
                return;
            }

            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
            if (aimCamera == null)
            {
                return;
            }

            FindShotImpact(firePoint.position, out Vector3 endpoint, out _);
            aimPoint.position = endpoint;
        }

        private Transform RecoilBody => recoilTarget != null ? recoilTarget : transform;

        /// <summary>
        /// Valida autoridad, vida, tablet y controles antes de aceptar una accion.
        /// </summary>
        private bool CanUseGun()
        {
            if (!IsReady() || !HasStateAuthority)
            {
                return false;
            }
            if (avatar == null || !avatar.IsAlive || (avatar.GetComponent<PlayerHurtReaction>()?.IsReacting ?? false)
                || (avatar.GetComponent<PlayerInteractionAction>()?.IsBusy ?? false))
            {
                return false;
            }
            if (avatar.TabletOpen || characterInput == null)
            {
                return false;
            }
            if (character == null || character.CameraPivot == null)
            {
                return false;
            }

            return characterInput.ControlsEnabled;
        }

        /// <summary>
        /// Comprueba que la pistola pertenece a un objeto de Fusion activo.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null || !Object.IsValid)
            {
                return false;
            }

            return StateBufferIsValid;
        }
    }
}
