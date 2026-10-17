using UnityEngine;

namespace GameJam.Tablet
{
    [CreateAssetMenu(menuName = "GameJam/Tablet Item")]
    public sealed class TabletItemDefinition : ScriptableObject
    {
        public string itemId = "mission-tablet";
        public string displayName = "Tablet de misión";
        public GameObject heldPrefab;
        public TabletRecipeCatalog catalog;
        public TabletUISkin skin;
    }
}
