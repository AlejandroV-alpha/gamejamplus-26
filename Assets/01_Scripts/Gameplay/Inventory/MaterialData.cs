using UnityEngine;

namespace GameJam.Inventory
{
    /// <summary>
    /// Define los datos compartidos de un tipo de material.
    /// </summary>
    [CreateAssetMenu(fileName = "MaterialData", menuName = "GameJam/Inventory/Material")]
    public sealed class MaterialData : ScriptableObject
    {
        [Tooltip("Identificador unico y estable. No cambiar despues de asignarlo.")]
        public string id;
        public string materialName;
        [TextArea]
        public string description;
        public Sprite icon;
        public GameObject prefab;
    }
}
