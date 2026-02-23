using Scripts.Items;
using UnityEngine;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public bool TryMoveOrSwap(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return true;

            InventoryItem itemFrom = GetItemAt(fromIndex, out int fromAnchor);
            if (itemFrom == null)
            {
                Debug.LogError($"[InventoryManager] Error: Item at {fromIndex} is null!");
                return false;
            }

            if (itemFrom == GetItem(toIndex)) return true;

            bool success = false;
            if (fromIndex == CRAFT_SLOT_INDEX)
            {
                success = HandleMoveFromCraftSlot(toIndex);
            }
            else if (toIndex == CRAFT_SLOT_INDEX)
            {
                success = HandleMoveToCraftSlot(fromIndex);
            }
            else if (toIndex >= EQUIP_OFFSET)
            {
                success = TryEquipItem(fromAnchor, toIndex, itemFrom);
            }
            else if (fromIndex >= EQUIP_OFFSET)
            {
                success = TryUnequipItem(fromIndex, toIndex, itemFrom);
            }
            else
            {
                success = HandleBackpackMove(fromAnchor, toIndex, itemFrom);
            }

            if (success) TriggerUIUpdate();
            return success;
        }

        private bool TryEquipItem(int fromAnchor, int equipGlobalIndex, InventoryItem itemToEquip)
        {
            int localEquipIndex = equipGlobalIndex - EQUIP_OFFSET;
            if (localEquipIndex < 0 || localEquipIndex >= EquipmentItems.Length)
            {
                Debug.LogWarning($"[Inventory] Invalid equip slot: {equipGlobalIndex}");
                return false;
            }

            InventoryItem currentEquipped = EquipmentItems[localEquipIndex];
            int requiredSlotType = localEquipIndex;
            int itemSlotType = (int)itemToEquip.Data.Slot;
            if (itemSlotType != requiredSlotType)
            {
                Debug.LogWarning($"[Inventory] Wrong slot type. Item: {itemSlotType}, Slot: {requiredSlotType}");
                return false;
            }

            ClearItemAtAnchor(fromAnchor, itemToEquip);

            if (currentEquipped != null && currentEquipped.Data != null)
            {
                if (CanPlaceItemAt(currentEquipped, fromAnchor))
                {
                    PlaceItemAtAnchor(fromAnchor, currentEquipped);
                    OnItemUnequipped?.Invoke(currentEquipped);
                }
                else
                {
                    Debug.LogWarning("[Inventory] Swap failed (no space). Reverting.");
                    PlaceItemAtAnchor(fromAnchor, itemToEquip);
                    return false;
                }
            }

            EquipmentItems[localEquipIndex] = itemToEquip;
            OnItemEquipped?.Invoke(itemToEquip);
            return true;
        }

        private bool TryUnequipItem(int equipGlobalIndex, int backpackIndex, InventoryItem itemToUnequip)
        {
            InventoryItem itemInBackpack = GetItemAt(backpackIndex, out int backpackAnchor);
            if (itemInBackpack == null)
            {
                if (CanPlaceItemAt(itemToUnequip, backpackIndex))
                {
                    int localEquip = equipGlobalIndex - EQUIP_OFFSET;
                    if (localEquip < 0 || localEquip >= EquipmentItems.Length) return false;
                    EquipmentItems[localEquip] = null;
                    PlaceItemAtAnchor(backpackIndex, itemToUnequip);
                    OnItemUnequipped?.Invoke(itemToUnequip);
                    return true;
                }

                return false;
            }

            return TryEquipItem(backpackAnchor, equipGlobalIndex, itemInBackpack);
        }

        private void ClearItemAtAnchor(int anchorIndex, InventoryItem item)
        {
            if (item?.Data == null || _backpack == null || anchorIndex < 0 || anchorIndex >= _backpack.Length) return;
            _backpack.Remove(item);
            SyncFromBackpack();
        }

        private void PlaceItemAtAnchor(int anchorIndex, InventoryItem item)
        {
            if (item?.Data == null || _backpack == null || anchorIndex < 0 || anchorIndex >= _backpack.Length) return;
            _backpack.Place(item, anchorIndex);
            SyncFromBackpack();
        }

        /// <summary>Удалить предмет из рюкзака по любому слоту (очищает все ячейки многоклеточного предмета).</summary>
        public void RemoveItemAtSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= Items.Length) return;
            InventoryItem item = GetItemAt(slotIndex, out int anchorIndex);
            if (item == null) return;
            ClearItemAtAnchor(anchorIndex, item);
        }

        /// <summary>Поставить предмет в рюкзак по якорному слоту (заполняет все ячейки).</summary>
        public void PlaceItemAt(int anchorIndex, InventoryItem item)
        {
            if (anchorIndex < 0 || anchorIndex >= Items.Length || item?.Data == null) return;
            PlaceItemAtAnchor(anchorIndex, item);
        }

        private int CountUniqueItemsInArea(int targetIndex, InventoryItem item)
        {
            return GetUniqueItemsInBackpackArea(item, targetIndex).Count;
        }

        private bool GetSingleItemInArea(int targetIndex, InventoryItem item, out InventoryItem foundItem, out int foundAnchor)
        {
            foundItem = null;
            foundAnchor = -1;
            var unique = GetUniqueItemsInBackpackArea(item, targetIndex);
            if (unique.Count != 1) return false;

            foreach (var u in unique)
            {
                foundItem = u;
                _backpack.GetItemAt(targetIndex, out _, out foundAnchor);
                return true;
            }

            return false;
        }

        private bool HandleBackpackMove(int fromIndex, int toIndex, InventoryItem itemA)
        {
            int uniqueCount = CountUniqueItemsInArea(toIndex, itemA);
            if (uniqueCount > 1) return false;

            if (uniqueCount == 0)
            {
                if (!CanPlaceItemAt(itemA, toIndex)) return false;
                ClearItemAtAnchor(fromIndex, itemA);
                PlaceItemAtAnchor(toIndex, itemA);
                OnInventoryChanged?.Invoke();
                return true;
            }

            if (!GetSingleItemInArea(toIndex, itemA, out InventoryItem itemB, out int indexB))
                return false;

            if (itemB == itemA)
            {
                ClearItemAtAnchor(fromIndex, itemA);
                if (CanPlaceItemAt(itemA, toIndex))
                {
                    PlaceItemAtAnchor(toIndex, itemA);
                    OnInventoryChanged?.Invoke();
                    return true;
                }

                PlaceItemAtAnchor(fromIndex, itemA);
                return false;
            }

            if (fromIndex == indexB) return false;
            InventoryItem itemTarget = GetItem(indexB);
            if (itemTarget == null) return false;
            if (!CanPlaceItemAt(itemA, indexB)) return false;
            if (!CanPlaceItemAt(itemTarget, fromIndex)) return false;

            ClearItemAtAnchor(fromIndex, itemA);
            ClearItemAtAnchor(indexB, itemTarget);
            PlaceItemAtAnchor(indexB, itemA);
            PlaceItemAtAnchor(fromIndex, itemTarget);
            OnInventoryChanged?.Invoke();
            return true;
        }
    }
}
