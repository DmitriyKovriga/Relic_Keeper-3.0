using System.Collections.Generic;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public InventorySaveData GetSaveData()
        {
            var data = new InventorySaveData();

            // Backpack: save each unique item by its root anchor.
            if (_backpack != null)
            {
                for (int i = 0; i < _backpack.Length; i++)
                {
                    _backpack.GetItemAt(i, out InventoryItem item, out int root);
                    if (item != null && item.Data != null && root == i)
                        data.Items.Add(item.GetSaveData(i));
                }
            }

            // Equipment: encoded as EQUIP_OFFSET + local index.
            for (int i = 0; i < EquipmentItems.Length; i++)
            {
                if (EquipmentItems[i] != null && EquipmentItems[i].Data != null)
                    data.Items.Add(EquipmentItems[i].GetSaveData(EQUIP_OFFSET + i));
            }

            if (CraftingSlotItem != null && CraftingSlotItem.Data != null)
                data.CraftingSlotItem = CraftingSlotItem.GetSaveData(CRAFT_SLOT_INDEX);

            data.OrbCounts = new List<OrbCountEntry>(_orbCounts);
            return data;
        }

        public void LoadState(InventorySaveData data, ItemDatabaseSO itemDB)
        {
            // 1) Clear current state.
            for (int i = 0; i < EquipmentItems.Length; i++)
            {
                if (EquipmentItems[i] == null) continue;
                OnItemUnequipped?.Invoke(EquipmentItems[i]);
                EquipmentItems[i] = null;
            }

            if (_backpack != null)
            {
                var toRemove = new List<InventoryItem>();
                for (int i = 0; i < _backpack.Length; i++)
                {
                    _backpack.GetItemAt(i, out InventoryItem it, out int root);
                    if (it != null && root == i) toRemove.Add(it);
                }

                foreach (var it in toRemove)
                    _backpack.Remove(it);
            }

            SyncFromBackpack();
            CraftingSlotItem = null;
            _orbCounts.Clear();
            if (data.OrbCounts != null) _orbCounts.AddRange(data.OrbCounts);

            // 2) Restore backpack/equipment.
            var claimedBackpack = new HashSet<int>();
            foreach (var itemData in data.Items)
            {
                InventoryItem newItem = InventoryItem.LoadFromSave(itemData, itemDB);
                if (newItem == null) continue;

                if (itemData.SlotIndex >= EQUIP_OFFSET)
                {
                    int equipIndex = itemData.SlotIndex - EQUIP_OFFSET;
                    if (equipIndex < EquipmentItems.Length)
                    {
                        EquipmentItems[equipIndex] = newItem;
                        OnItemEquipped?.Invoke(newItem);
                    }
                    continue;
                }

                if (itemData.SlotIndex == CRAFT_SLOT_INDEX) continue;

                int anchor = itemData.SlotIndex;
                if (anchor < 0 || anchor >= (_backpack?.Length ?? 0)) continue;

                GetBackpackItemSize(newItem, out int w, out int h);
                bool anyClaimed = false;
                for (int r = 0; r < h && !anyClaimed; r++)
                {
                    for (int c = 0; c < w; c++)
                    {
                        if (!claimedBackpack.Contains(anchor + r * _cols + c)) continue;
                        anyClaimed = true;
                        break;
                    }
                }

                if (anyClaimed) continue;
                _backpack.Place(newItem, anchor);

                for (int r = 0; r < h; r++)
                {
                    for (int c = 0; c < w; c++)
                        claimedBackpack.Add(anchor + r * _cols + c);
                }
            }

            SyncFromBackpack();

            // 3) Restore craft slot.
            if (data.CraftingSlotItem != null)
            {
                var craftItem = InventoryItem.LoadFromSave(data.CraftingSlotItem, itemDB);
                if (craftItem != null) CraftingSlotItem = craftItem;
            }

            TriggerUIUpdate();
        }
    }
}
