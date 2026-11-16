using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace GameJam.Inventory
{
    /// <summary>The quickbar is the entire replicated inventory: one unit per slot.</summary>
    [DisallowMultipleComponent]
    public sealed class Inventory : NetworkBehaviour
    {
        private const int MaterialTypeCount = 10;
        public const int MaximumSlots = 10;
        [SerializeField, Range(1, MaximumSlots), Tooltip("Capacidad total. Una unidad por espacio, sin pilas. Se aplica al iniciar la partida.")]
        private int slotCount = 6;
        [SerializeField, Tooltip("Mismo orden de materiales en todos los clientes.")]
        private MaterialData[] materials = new MaterialData[MaterialTypeCount];
        [SerializeField, Tooltip("Objetos que ocupan espacios al aparecer el jugador.")]
        private MaterialData[] startingItems = System.Array.Empty<MaterialData>();

        // Zero is empty; the remaining values are stable material indices plus one.
        [Networked, Capacity(MaximumSlots)] private NetworkArray<int> SlotMaterials { get; }
        [Networked] private int ActiveSlotCount { get; set; }
        [Networked] public int SelectedSlot { get; private set; }
        public MaterialData SelectedItem => GetSlotMaterial(SelectedSlot);
        public bool HasItem(CarriedItemKind kind)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var item = GetSlotMaterial(i);
                if (item != null && item.kind == kind) return true;
            }
            return false;
        }
        public bool SelectSlot(int slot)
        {
            if (!CanModify(1) || slot < 0 || slot >= SlotCount) return false;
            SelectedSlot = slot;
            return true;
        }
        public int SlotCount => IsReady() && ActiveSlotCount > 0 ? Mathf.Clamp(ActiveSlotCount, 1, MaximumSlots) : Mathf.Clamp(slotCount, 1, MaximumSlots);
        public IReadOnlyList<MaterialData> RegisteredMaterials => materials;

        public void GrantExhibitionKit()
        {
            if (!CanModify(1) || materials == null) return;
            for (int i = 0; i < SlotCount; i++) SlotMaterials.Set(i, 0);

            int slotIndex = 0;
            // First place PrimaryWeapon and Radio if present in materials
            for (int i = 0; i < materials.Length && slotIndex < SlotCount; i++)
            {
                if (materials[i] != null && materials[i].kind == CarriedItemKind.PrimaryWeapon)
                {
                    SlotMaterials.Set(slotIndex++, i + 1);
                    break;
                }
            }
            for (int i = 0; i < materials.Length && slotIndex < SlotCount; i++)
            {
                if (materials[i] != null && materials[i].kind == CarriedItemKind.Radio)
                {
                    SlotMaterials.Set(slotIndex++, i + 1);
                    break;
                }
            }
            // Fill remaining slots with distinct crafting materials
            for (int i = 0; i < materials.Length && slotIndex < SlotCount; i++)
            {
                if (materials[i] != null && materials[i].kind == CarriedItemKind.Material)
                {
                    SlotMaterials.Set(slotIndex++, i + 1);
                }
            }
            SelectedSlot = 0;
        }
        public int UsedSlots
        {
            get { int count=0; if(IsReady()) for(int i=0;i<SlotCount;i++) if(SlotMaterials.Get(i)!=0) count++; return count; }
        }
        public int FreeSlots => SlotCount - UsedSlots;
        public override void Spawned()
        {
            if (!HasStateAuthority) return;
            ActiveSlotCount = Mathf.Clamp(slotCount,1,MaximumSlots);
            SelectedSlot = 0;
            foreach (var item in startingItems) if (item != null && !AddMaterial(item, 1))
                Debug.LogWarning("[Loadout] No cabe el objeto inicial: " + item.name, this);
        }
        public MaterialData GetSlotMaterial(int slot)
        {
            if(!IsReady() || slot<0 || slot>=SlotCount) return null;
            int index=SlotMaterials.Get(slot)-1;
            return materials!=null && index>=0 && index<materials.Length ? materials[index] : null;
        }
        public bool CanAddMaterial(MaterialData material,int amount)
        {
            return IsReady() && amount>0 && GetMaterialIndex(material)>=0 && amount<=FreeSlots;
        }
        public bool AddMaterial(MaterialData material,int amount)
        {
            if(!CanModify(amount) || !CanAddMaterial(material,amount)) return false;
            int id=GetMaterialIndex(material)+1;
            for(int i=0;i<SlotCount && amount>0;i++) if(SlotMaterials.Get(i)==0){SlotMaterials.Set(i,id);amount--;}
            return true;
        }
        public bool RemoveSlot(int slot)
        {
            if(!CanModify(1) || GetSlotMaterial(slot)==null) return false;
            SlotMaterials.Set(slot,0);return true;
        }
        public bool RemoveMaterial(MaterialData material,int amount)
        {
            if(!CanModify(amount) || GetMaterialAmount(material)<amount) return false;
            int id=GetMaterialIndex(material)+1;
            for(int i=0;i<SlotCount && amount>0;i++) if(SlotMaterials.Get(i)==id){SlotMaterials.Set(i,0);amount--;}
            return true;
        }
        public bool HasMaterial(MaterialData material,int amount) => amount>0 && GetMaterialAmount(material)>=amount;
        public int GetMaterialAmount(MaterialData material)
        {
            if(!IsReady()) return 0;
            int index=GetMaterialIndex(material); if(index<0)return 0;
            int count=0;for(int i=0;i<SlotCount;i++)if(SlotMaterials.Get(i)==index+1)count++;
            return count;
        }
        public Dictionary<MaterialData,int> GetContents()
        {
            var contents=new Dictionary<MaterialData,int>();
            for(int i=0;i<SlotCount;i++){var material=GetSlotMaterial(i);if(material==null)continue;contents.TryGetValue(material,out int count);contents[material]=count+1;}
            return contents;
        }
        public bool HasMaterials(Dictionary<MaterialData,int> costs) => TryCalculateRemainingAmounts(costs,out _);
        public bool TryRemoveMaterials(Dictionary<MaterialData,int> costs)
        {
            if(!CanModify(1) || !TryCalculateRemainingAmounts(costs,out int[] remaining))return false;
            // Validate all costs before changing any slot. Keep the other items in place.
            var remove=new int[MaterialTypeCount];
            for(int i=0;i<SlotCount;i++){int id=SlotMaterials.Get(i)-1;if(id>=0)remove[id]++;}
            for(int i=0;i<MaterialTypeCount;i++)remove[i]-=remaining[i];
            for(int i=0;i<SlotCount;i++){int id=SlotMaterials.Get(i)-1;if(id>=0 && remove[id]>0){SlotMaterials.Set(i,0);remove[id]--;}}
            return true;
        }
        private bool TryCalculateRemainingAmounts(Dictionary<MaterialData,int> costs,out int[] remaining)
        {
            remaining=null;
            if(!IsReady() || costs==null || costs.Count==0)return false;
            remaining=new int[MaterialTypeCount];
            for(int i=0;i<SlotCount;i++){int id=SlotMaterials.Get(i)-1;if(id>=0 && id<MaterialTypeCount)remaining[id]++;}
            foreach(var cost in costs){int id=GetMaterialIndex(cost.Key);if(id<0 || cost.Value<=0 || remaining[id]<cost.Value)return false;remaining[id]-=cost.Value;}
            return true;
        }
        private bool CanModify(int amount) => amount>0 && IsReady() && HasStateAuthority;
        private bool IsReady() => Object!=null && Object.IsValid && StateBufferIsValid;
        private int GetMaterialIndex(MaterialData material)
        {
            if(material==null || string.IsNullOrWhiteSpace(material.id) || materials==null || materials.Length!=MaterialTypeCount)return -1;
            for(int i=0;i<materials.Length;i++)if(materials[i]!=null && materials[i].id==material.id)return i;
            return -1;
        }
        private void OnValidate(){slotCount=Mathf.Clamp(slotCount,1,MaximumSlots);}
    }
}
