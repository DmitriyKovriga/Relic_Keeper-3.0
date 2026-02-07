using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    /// <summary>
    /// Склад с вкладками. Сетка на вкладку: STASH_COLS x STASH_ROWS.
    /// Сохраняется/загружается через GameSaveManager.
    /// </summary>
    public class StashManager : MonoBehaviour
    {
        public static StashManager Instance { get; private set; }

        public const int STASH_COLS = 6;
        public const int STASH_ROWS = 10;
        public const int STASH_SLOTS_PER_TAB = STASH_COLS * STASH_ROWS;

        public event Action OnStashChanged;

        private List<InventoryItem[]> _tabs = new List<InventoryItem[]>();
        private int _currentTabIndex;

        public int TabCount => _tabs.Count;
        public int CurrentTabIndex => _currentTabIndex;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureAtLeastOneTab();
        }

        private void EnsureAtLeastOneTab()
        {
            if (_tabs.Count == 0)
                AddTab();
        }

        public void AddTab()
        {
            var grid = new InventoryItem[STASH_SLOTS_PER_TAB];
            _tabs.Add(grid);
            OnStashChanged?.Invoke();
        }

        public void SetCurrentTab(int index)
        {
            if (index < 0 || index >= _tabs.Count) return;
            _currentTabIndex = index;
            OnStashChanged?.Invoke();
        }

        public InventoryItem GetItem(int tabIndex, int slotIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            if (slotIndex < 0 || slotIndex >= STASH_SLOTS_PER_TAB) return null;
            return _tabs[tabIndex][slotIndex];
        }

        public InventoryItem GetItemAt(int tabIndex, int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
            {
                if (grid[i] != null && IsItemCoveringSlot(i, grid[i], slotIndex))
                {
                    anchorIndex = i;
                    return grid[i];
                }
            }
            return null;
        }

        private bool IsItemCoveringSlot(int anchorIndex, InventoryItem item, int targetSlot)
        {
            if (item?.Data == null) return false;
            int startRow = anchorIndex / STASH_COLS;
            int startCol = anchorIndex % STASH_COLS;
            int targetRow = targetSlot / STASH_COLS;
            int targetCol = targetSlot % STASH_COLS;
            return targetRow >= startRow && targetRow < startRow + item.Data.Height &&
                   targetCol >= startCol && targetCol < startCol + item.Data.Width;
        }

        public bool IsSlotOccupied(int tabIndex, int slotIndex, out InventoryItem occupier)
        {
            occupier = null;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return true;
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
            {
                if (grid[i] != null && IsItemCoveringSlot(i, grid[i], slotIndex))
                {
                    occupier = grid[i];
                    return true;
                }
            }
            return false;
        }

        public bool CanPlaceItemAt(int tabIndex, int targetSlot, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item?.Data == null) return false;
            int w = item.Data.Width;
            int h = item.Data.Height;
            int row = targetSlot / STASH_COLS;
            int col = targetSlot % STASH_COLS;
            if (col + w > STASH_COLS) return false;
            if (row + h > STASH_ROWS) return false;
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int idx = targetSlot + r * STASH_COLS + c;
                    if (IsSlotOccupied(tabIndex, idx, out var occupier) && occupier != item)
                        return false;
                }
            }
            return true;
        }

        /// <summary>Забрать предмет из склада. Предмет больше не числится в сетке — только в возвращаемой ссылке.</summary>
        public InventoryItem TakeItemFromStash(int tabIndex, int anchorSlot)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            var grid = _tabs[tabIndex];
            if (anchorSlot < 0 || anchorSlot >= grid.Length) return null;
            InventoryItem item = grid[anchorSlot];
            if (item == null) return null;
            RemoveItemFromStash(tabIndex, anchorSlot);
            return item;
        }

        /// <summary>Поставить уже взятый предмет в склад. fromAnchorForSwap — якорь слота, откуда взяли (для свопа вытесненный предмет кладётся туда).</summary>
        public bool PlaceItemInStash(InventoryItem item, int toTab, int toSlotIndex, int fromTabForSwap = -1, int fromAnchorForSwap = -1)
        {
            if (item == null || item.Data == null) return false;
            if (toTab < 0 || toTab >= _tabs.Count) return false;

            bool targetOccupied = IsSlotOccupied(toTab, toSlotIndex, out InventoryItem otherItem);
            if (targetOccupied && otherItem != null && otherItem != item)
            {
                int otherAnchor = GetAnchorSlot(toTab, otherItem);
                if (otherAnchor < 0) return false;
                if (fromTabForSwap >= 0 && fromAnchorForSwap >= 0 && CanPlaceItemAt(fromTabForSwap, fromAnchorForSwap, otherItem))
                {
                    RemoveItemFromStash(toTab, otherAnchor);
                    PlaceItem(toTab, otherAnchor, item); // по якорю освободившейся области
                    PlaceItem(fromTabForSwap, fromAnchorForSwap, otherItem);
                    OnStashChanged?.Invoke();
                    return true;
                }
                return false;
            }
            if (!CanPlaceItemAt(toTab, toSlotIndex, item)) return false;
            PlaceItem(toTab, toSlotIndex, item);
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Переместить предмет из инвентаря в склад. Предмет забирается из inv.</summary>
        public bool TryMoveFromInventory(int fromInventorySlotIndex, int toStashTab, int toStashSlot)
        {
            if (InventoryManager.Instance == null) return false;
            InventoryItem item = InventoryManager.Instance.GetItem(fromInventorySlotIndex);
            if (item == null) return false;
            if (!CanPlaceItemAt(toStashTab, toStashSlot, item)) return false;

            RemoveItemFromInventory(fromInventorySlotIndex);
            PlaceItem(toStashTab, toStashSlot, item);
            return true;
        }

        /// <summary>Переместить предмет из склада в инвентарь.</summary>
        public bool TryMoveToInventory(int fromStashTab, int fromStashSlot, int toInventorySlotIndex)
        {
            if (InventoryManager.Instance == null) return false;
            InventoryItem item = GetItemAt(fromStashTab, fromStashSlot, out int anchorStash);
            if (item == null) return false;
            if (!InventoryManager.Instance.CanPlaceItemAt(item, toInventorySlotIndex)) return false;

            RemoveItemFromStash(fromStashTab, anchorStash);
            bool placed = PlaceItemInInventory(item, toInventorySlotIndex);
            if (!placed)
            {
                PlaceItem(fromStashTab, anchorStash, item);
                return false;
            }
            return true;
        }

        /// <summary>Переместить или свопнуть предмет внутри склада (как в инвентаре: можно на себя, можно менять местами с другим предметом).</summary>
        public bool TryMoveStashToStash(int fromTab, int fromAnchorSlot, int toTab, int toSlotIndex)
        {
            InventoryItem item = GetItem(fromTab, fromAnchorSlot);
            if (item == null) return false;
            if (fromTab == toTab && fromAnchorSlot == toSlotIndex) return true;
            bool targetOccupied = IsSlotOccupied(toTab, toSlotIndex, out InventoryItem otherItem);
            if (targetOccupied && otherItem == item) return true;
            if (targetOccupied && otherItem != item)
            {
                int otherAnchor = GetAnchorSlot(toTab, otherItem);
                if (otherAnchor < 0) return false;
                if (!CanPlaceItemAt(fromTab, fromAnchorSlot, otherItem)) return false;
                RemoveItemFromStash(fromTab, fromAnchorSlot);
                RemoveItemFromStash(toTab, otherAnchor);
                PlaceItem(toTab, otherAnchor, item); // размещаем item по якорю освободившейся области
                PlaceItem(fromTab, fromAnchorSlot, otherItem);
                return true;
            }
            if (!CanPlaceItemAt(toTab, toSlotIndex, item)) return false;
            RemoveItemFromStash(fromTab, fromAnchorSlot);
            PlaceItem(toTab, toSlotIndex, item);
            return true;
        }

        /// <summary>Своп между слотом инвентаря и слотом склада (или просто перенос).</summary>
        public bool TrySwapInventoryStash(int invSlotIndex, int stashTab, int stashSlotIndex)
        {
            if (InventoryManager.Instance == null) return false;
            InventoryItem invItem = InventoryManager.Instance.GetItem(invSlotIndex);
            bool stashOccupied = IsSlotOccupied(stashTab, stashSlotIndex, out InventoryItem stashItem);

            if (invItem != null && !stashOccupied)
                return TryMoveFromInventory(invSlotIndex, stashTab, stashSlotIndex);
            if (invItem != null && stashOccupied)
            {
                if (!InventoryManager.Instance.CanPlaceItemAt(stashItem, invSlotIndex)) return false;
                RemoveItemFromInventory(invSlotIndex);
                RemoveItemFromStash(stashTab, GetAnchorSlot(stashTab, stashItem));
                PlaceItem(stashTab, stashSlotIndex, invItem);
                PlaceItemInInventory(stashItem, invSlotIndex);
                return true;
            }
            if (invItem == null && stashOccupied)
                return TryMoveToInventory(stashTab, stashSlotIndex, invSlotIndex);
            return false;
        }

        private int GetAnchorSlot(int tabIndex, InventoryItem item)
        {
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
                if (grid[i] == item) return i;
            return -1;
        }

        private bool PlaceItemInInventory(InventoryItem item, int toIndex)
        {
            if (toIndex == InventoryManager.CRAFT_SLOT_INDEX)
            {
                var inv = InventoryManager.Instance;
                if (inv.CraftingSlotItem != null) return false;
                inv.SetCraftingSlotItem(item);
                return true;
            }
            if (toIndex >= InventoryManager.EQUIP_OFFSET)
            {
                int localEquip = toIndex - InventoryManager.EQUIP_OFFSET;
                if ((int)item.Data.Slot != localEquip) return false;
                InventoryManager.Instance.EquipmentItems[localEquip] = item;
                InventoryManager.Instance.NotifyItemEquipped(item);
                InventoryManager.Instance.TriggerUIUpdate();
                return true;
            }
            bool ok = InventoryManager.Instance.CanPlaceItemAt(item, toIndex);
            if (ok)
            {
                InventoryManager.Instance.PlaceItemAt(toIndex, item);
                InventoryManager.Instance.TriggerUIUpdate();
            }
            return ok;
        }

        private void RemoveItemFromStash(int tabIndex, int anchorSlot)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return;
            var grid = _tabs[tabIndex];
            if (anchorSlot < 0 || anchorSlot >= grid.Length) return;
            InventoryItem item = grid[anchorSlot];
            if (item == null) return;
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    grid[anchorSlot + r * STASH_COLS + c] = null;
            OnStashChanged?.Invoke();
        }

        private void PlaceItem(int tabIndex, int slotIndex, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item?.Data == null) return;
            var grid = _tabs[tabIndex];
            int w = item.Data.Width;
            int h = item.Data.Height;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    grid[slotIndex + r * STASH_COLS + c] = item;
            OnStashChanged?.Invoke();
        }

        public StashSaveData GetSaveData()
        {
            var data = new StashSaveData();
            for (int t = 0; t < _tabs.Count; t++)
            {
                var tabData = new StashTabSaveData { TabName = $"Tab {t + 1}" };
                var grid = _tabs[t];
                for (int i = 0; i < grid.Length; i++)
                {
                    if (grid[i] == null || grid[i].Data == null) continue;
                    bool isAnchor = (i % STASH_COLS == 0 || grid[i - 1] != grid[i]) &&
                                  (i < STASH_COLS || grid[i - STASH_COLS] != grid[i]);
                    if (isAnchor)
                        tabData.Items.Add(grid[i].GetSaveData(i));
                }
                data.Tabs.Add(tabData);
            }
            return data;
        }

        public void LoadState(StashSaveData data, ItemDatabaseSO itemDB)
        {
            _tabs.Clear();
            if (data?.Tabs == null || data.Tabs.Count == 0)
            {
                AddTab();
                return;
            }
            foreach (var tabData in data.Tabs)
            {
                var grid = new InventoryItem[STASH_SLOTS_PER_TAB];
                if (tabData?.Items != null)
                {
                    foreach (var itemData in tabData.Items)
                    {
                        var item = InventoryItem.LoadFromSave(itemData, itemDB);
                        if (item != null && item.Data != null && itemData.SlotIndex >= 0 && itemData.SlotIndex < grid.Length)
                        {
                            int anchor = itemData.SlotIndex;
                            int w = item.Data.Width;
                            int h = item.Data.Height;
                            for (int r = 0; r < h; r++)
                                for (int c = 0; c < w; c++)
                                    grid[anchor + r * STASH_COLS + c] = item;
                        }
                    }
                }
                _tabs.Add(grid);
            }
            _currentTabIndex = Mathf.Clamp(_currentTabIndex, 0, _tabs.Count - 1);
            OnStashChanged?.Invoke();
        }

        private void RemoveItemFromInventory(int slotIndex)
        {
            if (InventoryManager.Instance == null) return;
            var inv = InventoryManager.Instance;
            if (slotIndex == InventoryManager.CRAFT_SLOT_INDEX)
            {
                inv.SetCraftingSlotItem(null);
                return;
            }
            if (slotIndex >= InventoryManager.EQUIP_OFFSET)
            {
                int localEquip = slotIndex - InventoryManager.EQUIP_OFFSET;
                var unequipped = inv.EquipmentItems[localEquip];
                inv.EquipmentItems[localEquip] = null;
                if (unequipped != null) inv.NotifyItemUnequipped(unequipped);
                inv.TriggerUIUpdate();
                return;
            }
            inv.RemoveItemAtSlot(slotIndex);
            inv.TriggerUIUpdate();
        }
    }
}
