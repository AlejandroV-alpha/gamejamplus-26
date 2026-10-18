
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace GameJam.Inventory
{
    /// <summary>
    /// Almacena y sincroniza las cantidades de materiales de un jugador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Inventory : NetworkBehaviour
    {
        private const int MaterialTypeCount = 5;

        [SerializeField]
        [Tooltip("Los cinco tipos de material. Conservar el mismo orden en todos los clientes.")]
        private MaterialData[] materials = new MaterialData[MaterialTypeCount];

        // Fusion reemplaza esta propiedad por acceso al estado replicado.
        [Networked, Capacity(MaterialTypeCount)]
        private NetworkArray<int> MaterialAmounts { get; }

        /// <summary>
        /// Muestra las cantidades iniciales del inventario local.
        /// </summary>
        public override void Spawned()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            Debug.Log("[Inventory] Inventario inicial:", this);

            foreach (MaterialData material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                int amount = GetMaterialAmount(material);

                Debug.Log(
                    $"[Inventory] {material.materialName}: {amount}",
                    this
                );
            }
        }

        /// <summary>
        /// Agrega materiales si la cantidad es valida y esta instancia tiene autoridad.
        /// </summary>
        public bool AddMaterial(MaterialData material, int amount)
        {
            if (!CanModify(amount))
            {
                return false;
            }

            int materialIndex = GetMaterialIndex(material);
            if (materialIndex < 0)
            {
                return false;
            }

            int currentAmount = MaterialAmounts.Get(materialIndex);
            if (amount > int.MaxValue - currentAmount)
            {
                return false;
            }

            int newAmount = currentAmount + amount;

            MaterialAmounts.Set(materialIndex, newAmount);

            Debug.Log(
                $"[Inventory] Recogido: {material.materialName} +{amount} | Total: {newAmount}",
                this
            );

            return true;
        }

        /// <summary>
        /// Quita materiales con autoridad solo cuando existe la cantidad solicitada.
        /// </summary>
        public bool RemoveMaterial(MaterialData material, int amount)
        {
            if (!CanModify(amount))
            {
                return false;
            }

            int materialIndex = GetMaterialIndex(material);
            if (materialIndex < 0)
            {
                return false;
            }

            int currentAmount = MaterialAmounts.Get(materialIndex);
            if (currentAmount < amount)
            {
                return false;
            }

            int newAmount = currentAmount - amount;

            MaterialAmounts.Set(materialIndex, newAmount);

            Debug.Log(
                $"[Inventory] Consumido: {material.materialName} -{amount} | Restante: {newAmount}",
                this
            );

            return true;
        }

        /// <summary>
        /// Comprueba todos los costes sin modificar ninguna cantidad.
        /// </summary>
        public bool HasMaterials(Dictionary<MaterialData, int> costs)
        {
            int[] remainingAmounts;
            return TryCalculateRemainingAmounts(costs, out remainingAmounts);
        }

        /// <summary>
        /// Consume todos los costes juntos o conserva el inventario completo.
        /// </summary>
        public bool TryRemoveMaterials(Dictionary<MaterialData, int> costs)
        {
            if (!CanModify(1))
            {
                return false;
            }

            int[] remainingAmounts;
            if (!TryCalculateRemainingAmounts(costs, out remainingAmounts))
            {
                return false;
            }

            // No hay esperas ni callbacks entre la validacion y la escritura.
            for (int index = 0; index < MaterialTypeCount; index++)
            {
                MaterialAmounts.Set(index, remainingAmounts[index]);
            }

            return true;
        }

        /// <summary>
        /// Calcula los saldos finales y agrupa referencias con el mismo ID.
        /// </summary>
        private bool TryCalculateRemainingAmounts(Dictionary<MaterialData, int> costs, out int[] remainingAmounts)
        {
            remainingAmounts = null;
            if (!IsReady())
            {
                return false;
            }

            if (costs == null || costs.Count == 0)
            {
                return false;
            }

            remainingAmounts = new int[MaterialTypeCount];
            for (int index = 0; index < MaterialTypeCount; index++)
            {
                remainingAmounts[index] = MaterialAmounts.Get(index);
            }

            foreach (KeyValuePair<MaterialData, int> cost in costs)
            {
                int materialIndex = GetMaterialIndex(cost.Key);
                if (materialIndex < 0 || cost.Value <= 0)
                {
                    return false;
                }

                if (remainingAmounts[materialIndex] < cost.Value)
                {
                    return false;
                }

                remainingAmounts[materialIndex] -= cost.Value;
            }

            return true;
        }

        /// <summary>
        /// Comprueba si existe una cantidad positiva del material solicitado.
        /// </summary>
        public bool HasMaterial(MaterialData material, int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            return GetMaterialAmount(material) >= amount;
        }

        /// <summary>
        /// Consulta la cantidad replicada o devuelve cero si no esta disponible.
        /// </summary>
        public int GetMaterialAmount(MaterialData material)
        {
            if (!IsReady())
            {
                return 0;
            }

            int materialIndex = GetMaterialIndex(material);
            if (materialIndex < 0)
            {
                return 0;
            }

            return MaterialAmounts.Get(materialIndex);
        }

        /// <summary>
        /// Devuelve una copia de los materiales con cantidades mayores que cero.
        /// </summary>
        public Dictionary<MaterialData, int> GetContents()
        {
            Dictionary<MaterialData, int> contents = new Dictionary<MaterialData, int>();

            if (materials == null)
            {
                return contents;
            }

            foreach (MaterialData material in materials)
            {
                int amount = GetMaterialAmount(material);

                if (amount > 0)
                {
                    contents[material] = amount;
                }
            }

            return contents;
        }

        /// <summary>
        /// Valida la cantidad y la autoridad antes de modificar el estado.
        /// </summary>
        private bool CanModify(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            if (!IsReady())
            {
                return false;
            }

            return HasStateAuthority;
        }

        /// <summary>
        /// Comprueba que el objeto tenga un estado de red disponible.
        /// </summary>
        private bool IsReady()
        {
            if (Object == null)
            {
                return false;
            }

            if (!Object.IsValid)
            {
                return false;
            }

            return StateBufferIsValid;
        }

        /// <summary>
        /// Localiza el contador de un material mediante su identificador estable.
        /// </summary>
        private int GetMaterialIndex(MaterialData material)
        {
            if (material == null)
            {
                return -1;
            }

            if (string.IsNullOrWhiteSpace(material.id))
            {
                return -1;
            }

            if (materials == null)
            {
                return -1;
            }

            if (materials.Length != MaterialTypeCount)
            {
                return -1;
            }

            for (int index = 0; index < materials.Length; index++)
            {
                MaterialData registeredMaterial = materials[index];

                if (registeredMaterial == null)
                {
                    continue;
                }

                if (registeredMaterial.id == material.id)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
