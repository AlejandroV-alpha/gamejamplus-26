using UnityEngine;

namespace GameJam.Ship
{
    // Visuals read the existing replicated repair state; late joiners see the same ship.
    public sealed class ShipRepairPresentation : MonoBehaviour
    {
        public ShipRepairStation station;
        public GameObject[] missingParts;
        private void LateUpdate()
        {
            bool installed=station!=null && station.Object!=null && station.Object.IsValid && station.IsRepaired();
            foreach(var part in missingParts)
                if(part!=null && part.activeSelf!=installed) part.SetActive(installed);
        }
    }
}
