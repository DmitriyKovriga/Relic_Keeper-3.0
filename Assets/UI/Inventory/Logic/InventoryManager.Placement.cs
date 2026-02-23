using System.Collections.Generic;
using Scripts.Items;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        /// <summary>Поставить уже взятый предмет в слот. sourceAnchorForSwap — куда положить вытесненный предмет при свопе (-1 если не своп).</summary>
        public bool PlaceItemAt(InventoryItem item, int toIndex, int sourceAnchorForSwap = -1)
        {
            if (item == null || item.Data == null) return false;

            if (sourceAnchorForSwap >= 0 && toIndex == sourceAnchorForSwap)
            {
                PlaceItemAtSlotInternal(item, toIndex);
                TriggerUIUpdate();
                return true;
            }

            if (toIndex == CRAFT_SLOT_INDEX)
            {
                InventoryItem prevCraft = CraftingSlotItem;
                if (prevCraft != null && sourceAnchorForSwap >= 0)
                {
                    int dest = FindSlotForDisplacedItem(prevCraft, -1, -1, 0, 0, sourceAnchorForSwap);
                    if (dest < 0) return false;
                    CraftingSlotItem = item;
                    PlaceItemAtSlotInternal(prevCraft, dest);
                }
                else
                {
                    CraftingSlotItem = item;
                }

                TriggerUIUpdate();
                return true;
            }

            if (toIndex >= EQUIP_OFFSET)
            {
                int local = toIndex - EQUIP_OFFSET;
                if (local < 0 || local >= EquipmentItems.Length) return false;
                if ((int)item.Data.Slot != local) return false;

                InventoryItem prevEquip = EquipmentItems[local];
                if (prevEquip != null && sourceAnchorForSwap >= 0)
                {
                    int dest = FindSlotForDisplacedItem(prevEquip, -1, -1, 0, 0, sourceAnchorForSwap);
                    if (dest < 0) return false;
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                    PlaceItemAtSlotInternal(prevEquip, dest);
                }
                else
                {
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                }

                TriggerUIUpdate();
                return true;
            }

            var uniqueInArea = GetUniqueItemsInBackpackArea(item, toIndex);
            if (uniqueInArea.Count > 1) return false;
            if (uniqueInArea.Count == 0)
            {
                if (!_backpack.CanPlace(item, toIndex)) return false;
                if (!_backpack.Place(item, toIndex)) return false;
                SyncFromBackpack();
                TriggerUIUpdate();
                return true;
            }

            InventoryItem other = null;
            foreach (var x in uniqueInArea)
            {
                other = x;
                break;
            }

            if (other == null || other == item)
            {
                if (!_backpack.CanPlace(item, toIndex)) return false;
                if (!_backpack.Place(item, toIndex)) return false;
                SyncFromBackpack();
                TriggerUIUpdate();
                return true;
            }

            _backpack.GetItemAt(toIndex, out _, out int otherRoot);
            InventoryItem displaced = _backpack.Take(otherRoot);
            if (displaced == null) return false;

            if (!_backpack.Place(item, toIndex))
            {
                _backpack.Place(displaced, otherRoot);
                SyncFromBackpack();
                return false;
            }

            int destRoot = (sourceAnchorForSwap >= 0 && _backpack.CanPlace(displaced, sourceAnchorForSwap))
                ? sourceAnchorForSwap
                : _backpack.FindFirstEmptyRoot(displaced, -1);
            if (destRoot < 0 || !_backpack.Place(displaced, destRoot))
            {
                _backpack.Take(toIndex);
                _backpack.Place(displaced, otherRoot);
                SyncFromBackpack();
                return false;
            }

            SyncFromBackpack();
            TriggerUIUpdate();
            return true;
        }

        private bool GridRectsOverlap(int anchor1, int w1, int h1, int anchor2, int w2, int h2)
        {
            int r1 = anchor1 / _cols;
            int c1 = anchor1 % _cols;
            int r2 = anchor2 / _cols;
            int c2 = anchor2 % _cols;
            return r1 < r2 + h2 && r2 < r1 + h1 && c1 < c2 + w2 && c2 < c1 + w1;
        }

        private int FindSlotForDisplacedItem(InventoryItem displaced, int displacedCurrentAnchor, int excludeAnchor, int excludeW, int excludeH, int preferredAnchor)
        {
            if (displaced?.Data == null || _backpack == null) return -1;
            GetBackpackItemSize(displaced, out int w, out int h);
            bool hasExclude = excludeAnchor >= 0;

            if (preferredAnchor >= 0 && preferredAnchor < _backpack.Length)
            {
                if ((!hasExclude || !GridRectsOverlap(preferredAnchor, w, h, excludeAnchor, excludeW, excludeH)) &&
                    _backpack.CanPlace(displaced, preferredAnchor))
                    return preferredAnchor;
            }

            for (int row = 0; row <= _rows - h; row++)
            {
                for (int col = 0; col <= _cols - w; col++)
                {
                    int anchor = row * _cols + col;
                    if (hasExclude && GridRectsOverlap(anchor, w, h, excludeAnchor, excludeW, excludeH)) continue;
                    if (_backpack.CanPlace(displaced, anchor)) return anchor;
                }
            }

            return -1;
        }

        private void PlaceItemAtSlotInternal(InventoryItem item, int index)
        {
            if (item == null || item.Data == null) return;
            if (index == CRAFT_SLOT_INDEX)
            {
                CraftingSlotItem = item;
                return;
            }

            if (index >= EQUIP_OFFSET)
            {
                int local = index - EQUIP_OFFSET;
                if (local >= 0 && local < EquipmentItems.Length)
                {
                    EquipmentItems[local] = item;
                    OnItemEquipped?.Invoke(item);
                }

                return;
            }

            if (index >= 0 && index < Items.Length)
                PlaceItemAtAnchor(index, item);
        }

        public bool CanPlaceItemAt(InventoryItem item, int targetIndex)
        {
            if (item == null || item.Data == null) return false;
            if (targetIndex == CRAFT_SLOT_INDEX) return true;
            if (targetIndex >= EQUIP_OFFSET) return true;
            return _backpack != null && _backpack.CanPlace(item, targetIndex);
        }

        public bool IsSlotOccupied(int slotIndex, out InventoryItem occupier)
        {
            occupier = null;
            if (slotIndex == CRAFT_SLOT_INDEX)
            {
                occupier = CraftingSlotItem;
                return CraftingSlotItem != null;
            }

            if (slotIndex >= EQUIP_OFFSET)
            {
                int local = slotIndex - EQUIP_OFFSET;
                if (local >= 0 && local < EquipmentItems.Length)
                {
                    occupier = EquipmentItems[local];
                    return occupier != null;
                }

                return false;
            }

            if (slotIndex < 0 || slotIndex >= (_backpack?.Length ?? 0)) return false;
            occupier = _backpack.GetItemAt(slotIndex);
            return occupier != null;
        }

        private bool IsItemCoveringSlot(int anchorIndex, InventoryItem item, int targetSlot)
        {
            if (item == null || item.Data == null) return false;
            GetBackpackItemSize(item, out int w, out int h);
            int startRow = anchorIndex / _cols;
            int startCol = anchorIndex % _cols;
            int targetRow = targetSlot / _cols;
            int targetCol = targetSlot % _cols;
            return targetRow >= startRow && targetRow < startRow + h && targetCol >= startCol && targetCol < startCol + w;
        }

        public InventoryItem GetItemAt(int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (slotIndex == CRAFT_SLOT_INDEX)
            {
                anchorIndex = CRAFT_SLOT_INDEX;
                return CraftingSlotItem;
            }

            if (slotIndex >= EQUIP_OFFSET)
            {
                anchorIndex = slotIndex;
                return GetItem(slotIndex);
            }

            if (_backpack == null || slotIndex < 0 || slotIndex >= _backpack.Length) return null;
            _backpack.GetItemAt(slotIndex, out InventoryItem backpackItem, out anchorIndex);
            return backpackItem;
        }

        /// <summary>Количество слотов рюкзака (для итерации в UI).</summary>
        public int BackpackSlotCount => _backpack?.Length ?? 0;

        /// <summary>Первый свободный корень в рюкзаке для item. -1 если нет места.</summary>
        public int FindFreeBackpackAnchor(InventoryItem item, int excludeAnchor = -1)
        {
            return _backpack?.FindFirstEmptyRoot(item, excludeAnchor) ?? -1;
        }

        /// <summary>Снять предмет с экипировки в первый свободный слот рюкзака. Для дебаггера/очистки.</summary>
        public bool UnequipToBackpack(int equipSlotIndex)
        {
            if (equipSlotIndex < 0 || equipSlotIndex >= EquipmentItems.Length) return true;
            InventoryItem item = EquipmentItems[equipSlotIndex];
            if (item == null) return true;

            int root = FindFreeBackpackAnchor(item, -1);
            if (root < 0) return false;
            EquipmentItems[equipSlotIndex] = null;
            OnItemUnequipped?.Invoke(item);
            _backpack.Place(item, root);
            SyncFromBackpack();
            TriggerUIUpdate();
            return true;
        }

        /// <summary>Очистить весь рюкзак (все уникальные предметы удаляются из сетки). Для дебаггера.</summary>
        public void ClearBackpack()
        {
            if (_backpack == null) return;
            var seen = new HashSet<InventoryItem>();
            for (int i = 0; i < _backpack.Length; i++)
            {
                _backpack.GetItemAt(i, out InventoryItem it, out _);
                if (it != null && !seen.Contains(it))
                {
                    seen.Add(it);
                    _backpack.Remove(it);
                }
            }

            SyncFromBackpack();
            TriggerUIUpdate();
        }
    }
}
