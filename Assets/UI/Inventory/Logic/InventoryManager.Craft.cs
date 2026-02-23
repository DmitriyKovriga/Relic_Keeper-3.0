using Scripts.Items;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        /// <summary>Установить предмет в слот крафта (для переноса из склада и т.п.).</summary>
        public void SetCraftingSlotItem(InventoryItem item)
        {
            CraftingSlotItem = item;
            TriggerUIUpdate();
        }

        private bool HandleMoveFromCraftSlot(int toIndex)
        {
            if (CraftingSlotItem == null) return false;
            var item = CraftingSlotItem;

            if (toIndex >= EQUIP_OFFSET)
            {
                int localEquipIndex = toIndex - EQUIP_OFFSET;
                if ((int)item.Data.Slot != localEquipIndex) return false;

                var currentEquipped = EquipmentItems[localEquipIndex];
                CraftingSlotItem = null;
                EquipmentItems[localEquipIndex] = item;
                if (currentEquipped != null)
                {
                    CraftingSlotItem = currentEquipped;
                    OnItemUnequipped?.Invoke(currentEquipped);
                }

                OnItemEquipped?.Invoke(item);
                return true;
            }

            if (!CanPlaceItemAt(item, toIndex)) return false;
            CraftingSlotItem = null;
            PlaceItemAtAnchor(toIndex, item);
            return true;
        }

        private bool HandleMoveToCraftSlot(int fromIndex)
        {
            var itemFrom = GetItemAt(fromIndex, out int fromAnchor);
            if (itemFrom == null) return false;

            var previousCraft = CraftingSlotItem;
            if (fromIndex >= EQUIP_OFFSET)
            {
                int localIndex = fromIndex - EQUIP_OFFSET;
                EquipmentItems[localIndex] = previousCraft;
                if (previousCraft != null) OnItemUnequipped?.Invoke(itemFrom);
                CraftingSlotItem = itemFrom;
                OnItemEquipped?.Invoke(previousCraft);
                return true;
            }

            ClearItemAtAnchor(fromAnchor, itemFrom);
            if (previousCraft != null) PlaceItemAtAnchor(fromAnchor, previousCraft);
            CraftingSlotItem = itemFrom;
            return true;
        }
    }
}
