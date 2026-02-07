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

        public const int STASH_COLS = 8;
        public const int STASH_ROWS = 9;
        public const int STASH_SLOTS_PER_TAB = STASH_COLS * STASH_ROWS;

        public event Action OnStashChanged;

        private List<InventoryItem[]> _tabs = new List<InventoryItem[]>();
        private int _currentTabIndex;

        public int TabCount => _tabs.Count;
        public int CurrentTabIndex => _currentTabIndex;

        /// <summary>Размер предмета для сетки склада: только из Data, ограничен STASH_COLS x STASH_ROWS (защита от битых данных).</summary>
        public static void GetStashItemSize(InventoryItem item, out int w, out int h)
        {
            w = 1; h = 1;
            if (item?.Data == null) return;
            w = Mathf.Clamp(item.Data.Width, 1, STASH_COLS);
            h = Mathf.Clamp(item.Data.Height, 1, STASH_ROWS);
        }

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

        public bool IsTabEmpty(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return false;
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
                if (grid[i] != null) return false;
            return true;
        }

        /// <summary>Удалить вкладку, только если она пустая. Нельзя удалить последнюю (должна остаться минимум одна).</summary>
        public bool TryRemoveTab(int tabIndex)
        {
            if (_tabs.Count <= 1) return false;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return false;
            if (!IsTabEmpty(tabIndex)) return false;
            _tabs.RemoveAt(tabIndex);
            if (_currentTabIndex >= _tabs.Count)
                _currentTabIndex = _tabs.Count - 1;
            else if (_currentTabIndex > tabIndex)
                _currentTabIndex--;
            OnStashChanged?.Invoke();
            return true;
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
            GetStashItemSize(item, out int w, out int h);
            int startRow = anchorIndex / STASH_COLS;
            int startCol = anchorIndex % STASH_COLS;
            int targetRow = targetSlot / STASH_COLS;
            int targetCol = targetSlot % STASH_COLS;
            return targetRow >= startRow && targetRow < startRow + h && targetCol >= startCol && targetCol < startCol + w;
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
            GetStashItemSize(item, out int w, out int h);
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

        private static bool StashRectsOverlap(int anchor1, int w1, int h1, int anchor2, int w2, int h2)
        {
            int r1 = anchor1 / STASH_COLS, c1 = anchor1 % STASH_COLS;
            int r2 = anchor2 / STASH_COLS, c2 = anchor2 % STASH_COLS;
            return r1 < r2 + h2 && r2 < r1 + h1 && c1 < c2 + w2 && c2 < c1 + w1;
        }

        /// <summary>Найти слот для вытесненного: исключаем текущую позицию вытесненного (exclude1) и область, куда кладём перетаскиваемый (exclude2), чтобы не перезаписать один предмет другим.</summary>
        private bool FindStashSlotForDisplaced(InventoryItem item, int preferredTab, int preferredAnchor,
            int exclude1Tab, int exclude1Anchor, int exclude1W, int exclude1H,
            int exclude2Tab, int exclude2Anchor, int exclude2W, int exclude2H,
            out int outTab, out int outAnchor)
        {
            outTab = -1;
            outAnchor = -1;
            if (item?.Data == null) return false;
            GetStashItemSize(item, out int w, out int h);
            bool hasEx1 = exclude1Tab >= 0 && exclude1Anchor >= 0;
            bool hasEx2 = exclude2Tab >= 0 && exclude2Anchor >= 0;
            bool overlapsExclude(int t, int anchor) =>
                (hasEx1 && t == exclude1Tab && StashRectsOverlap(anchor, w, h, exclude1Anchor, exclude1W, exclude1H)) ||
                (hasEx2 && t == exclude2Tab && StashRectsOverlap(anchor, w, h, exclude2Anchor, exclude2W, exclude2H));
            if (preferredTab >= 0 && preferredAnchor >= 0 && CanPlaceItemAt(preferredTab, preferredAnchor, item))
            {
                if (!overlapsExclude(preferredTab, preferredAnchor))
                {
                    outTab = preferredTab;
                    outAnchor = preferredAnchor;
                    return true;
                }
            }
            for (int t = 0; t < _tabs.Count; t++)
            {
                for (int row = 0; row <= STASH_ROWS - h; row++)
                    for (int col = 0; col <= STASH_COLS - w; col++)
                    {
                        int anchor = row * STASH_COLS + col;
                        if (overlapsExclude(t, anchor)) continue;
                        if (CanPlaceItemAt(t, anchor, item))
                        {
                            outTab = t;
                            outAnchor = anchor;
                            return true;
                        }
                    }
            }
            return false;
        }

        /// <summary>Защита: положить предмет в первый свободный слот любой вкладки (если инвентарь полон).</summary>
        public bool TryAddItemToAnyTab(InventoryItem item)
        {
            return TryAddItemPreferringTab(item, -1);
        }

        /// <summary>Положить предмет в склад; сначала пробуем preferredTab, затем остальные вкладки.</summary>
        public bool TryAddItemPreferringTab(InventoryItem item, int preferredTab)
        {
            if (item?.Data == null) return false;
            GetStashItemSize(item, out int w, out int h);
            int n = _tabs.Count;
            if (n == 0) return false;
            if (preferredTab >= 0 && preferredTab < n)
            {
                for (int row = 0; row <= STASH_ROWS - h; row++)
                    for (int col = 0; col <= STASH_COLS - w; col++)
                    {
                        int anchor = row * STASH_COLS + col;
                        if (CanPlaceItemAt(preferredTab, anchor, item))
                        {
                            PlaceItem(preferredTab, anchor, item);
                            OnStashChanged?.Invoke();
                            return true;
                        }
                    }
            }
            for (int t = 0; t < n; t++)
            {
                if (t == preferredTab) continue;
                for (int row = 0; row <= STASH_ROWS - h; row++)
                    for (int col = 0; col <= STASH_COLS - w; col++)
                    {
                        int anchor = row * STASH_COLS + col;
                        if (CanPlaceItemAt(t, anchor, item))
                        {
                            PlaceItem(t, anchor, item);
                            OnStashChanged?.Invoke();
                            return true;
                        }
                    }
            }
            return false;
        }

        /// <summary>Забрать предмет из склада. Очищаем все ячейки с этим предметом по ссылке (нет дубликатов/фантомов), затем возвращаем ссылку.</summary>
        public InventoryItem TakeItemFromStash(int tabIndex, int anchorSlot)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            var grid = _tabs[tabIndex];
            if (anchorSlot < 0 || anchorSlot >= grid.Length) return null;
            InventoryItem item = grid[anchorSlot];
            if (item == null) return null;
            ClearItemFromTabByReference(tabIndex, item);
            OnStashChanged?.Invoke();
            return item;
        }

        /// <summary>Поставить уже взятый предмет в склад. fromTabForSwap/fromAnchorForSwap — слот склада для вытесненного; fromInvAnchorForSwap — слот инвентаря (если предмет из инвентаря, вытесненный идёт туда).</summary>
        public bool PlaceItemInStash(InventoryItem item, int toTab, int toSlotIndex, int fromTabForSwap = -1, int fromAnchorForSwap = -1, int fromInvAnchorForSwap = -1)
        {
            if (item == null || item.Data == null) return false;
            if (toTab < 0 || toTab >= _tabs.Count) return false;

            bool targetOccupied = IsSlotOccupied(toTab, toSlotIndex, out InventoryItem otherItem);
            if (targetOccupied && otherItem != null && otherItem != item)
            {
                int otherAnchor = GetAnchorSlot(toTab, otherItem);
                if (otherAnchor < 0) return false;
                if (fromInvAnchorForSwap >= 0 && InventoryManager.Instance != null && InventoryManager.Instance.CanPlaceItemAt(otherItem, fromInvAnchorForSwap))
                {
                    try
                    {
                        ClearItemFromTabByReference(toTab, item);
                        ClearItemFromTabByReference(toTab, otherItem);
                        PlaceItem(toTab, otherAnchor, item);
                        InventoryManager.Instance.PlaceItemAt(otherItem, fromInvAnchorForSwap, -1);
                        OnStashChanged?.Invoke();
                        return true;
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogException(ex);
                        if (InventoryManager.Instance != null)
                        {
                            InventoryManager.Instance.RecoverItemToInventory(item);
                            InventoryManager.Instance.RecoverItemToInventory(otherItem);
                        }
                        return false;
                    }
                }
                GetStashItemSize(otherItem, out int ow, out int oh);
                GetStashItemSize(item, out int iw, out int ih);
                if (!FindStashSlotForDisplaced(otherItem, fromTabForSwap, fromAnchorForSwap, toTab, otherAnchor, ow, oh, toTab, otherAnchor, iw, ih, out int destTab, out int destAnchor))
                    return false;
                try
                {
                    ClearItemFromTabByReference(toTab, item);
                    ClearItemFromTabByReference(toTab, otherItem);
                    if (destTab != toTab)
                        ClearItemFromTabByReference(destTab, otherItem);
                    PlaceItem(toTab, otherAnchor, item);
                    PlaceItem(destTab, destAnchor, otherItem);
                    OnStashChanged?.Invoke();
                    return true;
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    if (InventoryManager.Instance != null)
                    {
                        InventoryManager.Instance.RecoverItemToInventory(item);
                        InventoryManager.Instance.RecoverItemToInventory(otherItem);
                    }
                    return false;
                }
            }
            ClearItemFromTabByReference(toTab, item);
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
                GetStashItemSize(otherItem, out int ow, out int oh);
                GetStashItemSize(item, out int iw, out int ih);
                if (!FindStashSlotForDisplaced(otherItem, fromTab, fromAnchorSlot, toTab, otherAnchor, ow, oh, toTab, otherAnchor, iw, ih, out int destTab, out int destAnchor))
                    return false;
                RemoveItemFromStash(fromTab, fromAnchorSlot);
                RemoveItemFromStash(toTab, otherAnchor);
                PlaceItem(toTab, otherAnchor, item);
                PlaceItem(destTab, destAnchor, otherItem);
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
            GetStashItemSize(item, out int w, out int h);
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    grid[anchorSlot + r * STASH_COLS + c] = null;
            OnStashChanged?.Invoke();
        }

        /// <summary>Удалить предмет по ссылке из всех ячеек вкладки. Гарантирует отсутствие дубликатов и фантомов перед размещением.</summary>
        private void ClearItemFromTabByReference(int tabIndex, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item == null) return;
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
            {
                if (grid[i] == item)
                    grid[i] = null;
            }
        }

        private void PlaceItem(int tabIndex, int slotIndex, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item?.Data == null) return;
            var grid = _tabs[tabIndex];
            GetStashItemSize(item, out int w, out int h);
            // Сначала очищаем область, чтобы не оставалось «фантомных» ссылок от частичного состояния
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    grid[slotIndex + r * STASH_COLS + c] = null;
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
                    bool leftOk = (i % STASH_COLS == 0) || (i > 0 && grid[i - 1] != grid[i]);
                    bool topOk = (i < STASH_COLS) || (i >= STASH_COLS && grid[i - STASH_COLS] != grid[i]);
                    if (leftOk && topOk)
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
                var claimed = new HashSet<int>();
                if (tabData?.Items != null)
                {
                    foreach (var itemData in tabData.Items)
                    {
                        var item = InventoryItem.LoadFromSave(itemData, itemDB);
                        if (item == null || item.Data == null || itemData.SlotIndex < 0 || itemData.SlotIndex >= grid.Length)
                            continue;
                        int anchor = itemData.SlotIndex;
                        GetStashItemSize(item, out int w, out int h);
                        bool anyClaimed = false;
                        for (int r = 0; r < h && !anyClaimed; r++)
                            for (int c = 0; c < w; c++)
                                if (claimed.Contains(anchor + r * STASH_COLS + c)) { anyClaimed = true; break; }
                        if (anyClaimed) continue;
                        for (int r = 0; r < h; r++)
                            for (int c = 0; c < w; c++)
                                grid[anchor + r * STASH_COLS + c] = item;
                        for (int r = 0; r < h; r++)
                            for (int c = 0; c < w; c++)
                                claimed.Add(anchor + r * STASH_COLS + c);
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
