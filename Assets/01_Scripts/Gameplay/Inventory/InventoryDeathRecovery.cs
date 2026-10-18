using Fusion;
using UnityEngine;
using GameJam.Multiplayer;
using GameJam.Collectibles;

namespace GameJam.Inventory
{
    // Recovered ship parts must not be permanently lost when their carrier dies.
    public sealed class InventoryDeathRecovery : NetworkBehaviour
    {
        private Inventory inventory;
        private MarsFusionAvatar avatar;
        private bool dropped;
        private Vector3 lastSafePosition;
        public override void Spawned()
        {
            inventory=GetComponent<Inventory>(); avatar=GetComponent<MarsFusionAvatar>();
            lastSafePosition=transform.position;
        }
        public override void FixedUpdateNetwork()
        {
            if(!HasStateAuthority || inventory==null || avatar==null) return;
            if(avatar.IsAlive)
            {
                var zone=ExplorationZone.ForPlayer(this);
                if(zone==null || zone.Contains(transform.position)) lastSafePosition=transform.position;
                dropped=false; return;
            }
            if(dropped) return;
            dropped=true;
            int i=0;
            foreach(var entry in inventory.GetContents())
            {
                var prefab=entry.Key.prefab!=null?entry.Key.prefab.GetComponent<NetworkObject>():null;
                if(prefab==null) continue;
                float angle=i++*2.4f;
                Vector3 position=lastSafePosition+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.6f;
                var item=Runner.Spawn(prefab,position,Quaternion.identity);
                item.GetComponent<MaterialPickup>().DroppedAmount=entry.Value;
                inventory.RemoveMaterial(entry.Key,entry.Value);
            }
        }
    }
}
