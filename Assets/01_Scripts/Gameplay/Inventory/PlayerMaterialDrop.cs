using System;
using Fusion;
using GameJam.Collectibles;
using GameJam.Combat;
using GameJam.Multiplayer;
using GameJam.Survival;
using UnityEngine;

namespace GameJam.Inventory
{
    /// <summary>
    /// Suelta una unidad del inventario como objeto de red.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Inventory))]
    public sealed class PlayerMaterialDrop : NetworkBehaviour
    {
        private const float PlayerClearance = 0.65f;
        private const float GroundOffset = 0.02f;
        private const float GroundProbeHeight = 2.5f;
        private const float GroundProbeDistance = 6f;

        [SerializeField, Min(0.5f)] private float spawnDistance = 1.2f;
        [SerializeField] private LayerMask placementLayers = ~0;

        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private readonly Collider[] clearanceHits = new Collider[32];
        private Inventory inventory;
        private MarsFusionAvatar avatar;
        private PlayerInteractionAction interactionAction;
        private PlayerHurtReaction hurtReaction;
        private SurvivalRevive revive;
        private MarsFusionSession session;
        private bool isDropping;

        /// <summary>
        /// Obtiene las dependencias locales del astronauta.
        /// </summary>
        private void Awake()
        {
            CacheDependencies();
        }

        /// <summary>
        /// Actualiza las dependencias al entrar en la simulacion.
        /// </summary>
        public override void Spawned()
        {
            CacheDependencies();
        }

        /// <summary>
        /// Intenta soltar una unidad del material indicado.
        /// </summary>
        public bool TryDropOne(MaterialData material)
        {
            if (!CanDrop(material))
            {
                return false;
            }

            NetworkObject pickupPrefab = material.prefab.GetComponent<NetworkObject>();
            MaterialPickup pickupComponent = material.prefab.GetComponent<MaterialPickup>();
            BoxCollider pickupCollider = material.prefab.GetComponent<BoxCollider>();
            if (pickupPrefab == null || pickupComponent == null || pickupCollider == null)
            {
                Debug.LogWarning("[Inventory Drop] El prefab necesita NetworkObject, MaterialPickup y BoxCollider.", material);
                return false;
            }

            Vector3 spawnPosition;
            Quaternion spawnRotation;
            if (!TryFindSpawnPose(pickupCollider, out spawnPosition, out spawnRotation))
            {
                Debug.LogWarning("[Inventory Drop] No existe una ubicacion segura delante del astronauta.", this);
                return false;
            }

            isDropping = true;
            NetworkObject spawnedObject = null;

            try
            {
                NetworkSpawnStatus status = Runner.TrySpawn(
                    pickupPrefab,
                    out spawnedObject,
                    spawnPosition,
                    spawnRotation,
                    null,
                    InitializeDroppedPickup
                );

                if (status != NetworkSpawnStatus.Spawned || spawnedObject == null)
                {
                    return false;
                }

                if (!inventory.RemoveMaterial(material, 1))
                {
                    Runner.Despawn(spawnedObject);
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                if (spawnedObject != null && spawnedObject.IsValid)
                {
                    Runner.Despawn(spawnedObject);
                }

                Debug.LogException(exception, this);
                return false;
            }
            finally
            {
                isDropping = false;
            }
        }

        /// <summary>
        /// Guarda una unidad antes de publicar el objeto en red.
        /// </summary>
        private void InitializeDroppedPickup(NetworkRunner networkRunner, NetworkObject spawnedObject)
        {
            MaterialPickup pickup = spawnedObject.GetComponent<MaterialPickup>();
            if (pickup != null)
            {
                pickup.DroppedAmount = 1;
            }
        }

        /// <summary>
        /// Comprueba autoridad, estado y cantidad disponible.
        /// </summary>
        private bool CanDrop(MaterialData material)
        {
            if (isDropping || material == null || material.prefab == null)
            {
                return false;
            }

            if (Object == null || !Object.IsValid || !HasStateAuthority || Runner == null || !Runner.IsRunning)
            {
                return false;
            }

            if (inventory == null || !inventory.StateBufferIsValid || avatar == null || !avatar.IsAlive)
            {
                return false;
            }

            if (avatar.TabletOpen || (interactionAction != null && interactionAction.IsBusy))
            {
                return false;
            }

            if ((hurtReaction != null && hurtReaction.IsReacting) ||
                (revive != null && (revive.IsRecovering || revive.IsLeavingRescue || revive.IsEmoting)))
            {
                return false;
            }

            if ((session != null && session.MenuOpen) || SceneTransitionVideo.IsBlockingInput ||
                PortalWhiteTransition.IsBlockingInput || LobbyDepartureEffects.IsBlockingInput ||
                GameJam.Crafting.CraftingBox.IsBlockingInput)
            {
                return false;
            }

            return inventory.GetMaterialAmount(material) > 0;
        }

        /// <summary>
        /// Busca suelo y espacio libre delante del astronauta.
        /// </summary>
        private bool TryFindSpawnPose(BoxCollider pickupCollider, out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
            {
                forward = Vector3.forward;
            }

            spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 scaledSize = Vector3.Scale(pickupCollider.size, pickupCollider.transform.lossyScale);
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(scaledSize.x) * 0.5f,
                Mathf.Abs(scaledSize.y) * 0.5f,
                Mathf.Abs(scaledSize.z) * 0.5f
            );
            float horizontalRadius = Mathf.Sqrt(halfExtents.x * halfExtents.x + halfExtents.z * halfExtents.z);
            float safeDistance = Mathf.Max(spawnDistance, horizontalRadius + PlayerClearance);
            Vector3 target = transform.position + forward * safeDistance;
            Vector3 rayOrigin = target + Vector3.up * GroundProbeHeight;

            RaycastHit groundHit;
            if (!TryFindGround(rayOrigin, out groundHit))
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            Vector3 scaledCenter = Vector3.Scale(pickupCollider.center, pickupCollider.transform.lossyScale);
            spawnPosition = groundHit.point + Vector3.up * (halfExtents.y - scaledCenter.y + GroundOffset);
            Vector3 worldCenter = spawnPosition + spawnRotation * scaledCenter;

            if (!HasClearance(worldCenter, halfExtents, spawnRotation, groundHit.collider))
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Selecciona el impacto de suelo mas cercano que no pertenece al jugador.
        /// </summary>
        private bool TryFindGround(Vector3 rayOrigin, out RaycastHit groundHit)
        {
            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                groundHits,
                GroundProbeDistance,
                placementLayers,
                QueryTriggerInteraction.Ignore
            );
            float nearestDistance = float.MaxValue;
            int nearestIndex = -1;

            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = groundHits[index].collider;
                if (candidate == null || candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                if (groundHits[index].distance < nearestDistance)
                {
                    nearestDistance = groundHits[index].distance;
                    nearestIndex = index;
                }
            }

            if (nearestIndex < 0)
            {
                groundHit = new RaycastHit();
                return false;
            }

            groundHit = groundHits[nearestIndex];
            return true;
        }

        /// <summary>
        /// Comprueba que el volumen del objeto no invada otros colliders.
        /// </summary>
        private bool HasClearance(Vector3 center, Vector3 halfExtents, Quaternion rotation, Collider groundCollider)
        {
            Vector3 testedExtents = halfExtents * 0.95f;
            int hitCount = Physics.OverlapBoxNonAlloc(
                center,
                testedExtents,
                clearanceHits,
                rotation,
                placementLayers,
                QueryTriggerInteraction.Ignore
            );

            for (int index = 0; index < hitCount; index++)
            {
                Collider candidate = clearanceHits[index];
                if (candidate == null || candidate == groundCollider || candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Obtiene los componentes usados por la operacion.
        /// </summary>
        private void CacheDependencies()
        {
            inventory = GetComponent<Inventory>();
            avatar = GetComponent<MarsFusionAvatar>();
            interactionAction = GetComponent<PlayerInteractionAction>();
            hurtReaction = GetComponent<PlayerHurtReaction>();
            revive = GetComponent<SurvivalRevive>();
            session = FindAnyObjectByType<MarsFusionSession>();
        }
    }
}
