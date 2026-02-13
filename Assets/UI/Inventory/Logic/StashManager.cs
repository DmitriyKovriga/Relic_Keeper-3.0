using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Items;
using Scripts.Saving;

namespace Scripts.Inventory
{
    /// <summary>
    /// Склад с вкладками в стиле PoE. Каждая вкладка — GridContainer (STASH_COLS x STASH_ROWS).
    /// Swap-if-One при дропе: 0 предметов — место, 1 — своп, >1 — блок.
    /// </summary>
    public class StashManager : MonoBehaviour
    {
        public static StashManager Instance { get; private set; }

        public const int STASH_COLS = 9;
        public const int STASH_ROWS = 11;
        public const int STASH_SLOTS_PER_TAB = STASH_COLS * STASH_ROWS;

        public event Action OnStashChanged;

        private List<GridContainer> _tabs = new List<GridContainer>();
        private int _currentTabIndex;

        public int TabCount => _tabs.Count;
        public int CurrentTabIndex => _currentTabIndex;

        /// <summary>Размер предмета для сетки склада.</summary>
        public static void GetStashItemSize(InventoryItem item, out int w, out int h)
        {
            GridContainer.GetItemSize(item, STASH_COLS, STASH_ROWS, out w, out h);
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
            _tabs.Add(new GridContainer(STASH_COLS, STASH_ROWS));
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
            {
                grid.GetItemAt(i, out InventoryItem it, out _);
                if (it != null) return false;
            }
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
            return _tabs[tabIndex].GetItemAt(slotIndex);
        }

        public InventoryItem GetItemAt(int tabIndex, int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            _tabs[tabIndex].GetItemAt(slotIndex, out InventoryItem item, out anchorIndex);
            return item;
        }

        public bool IsSlotOccupied(int tabIndex, int slotIndex, out InventoryItem occupier)
        {
            occupier = null;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return false;
            _tabs[tabIndex].GetItemAt(slotIndex, out occupier, out _);
            return occupier != null;
        }

        public bool CanPlaceItemAt(int tabIndex, int targetSlot, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item?.Data == null) return false;
            return _tabs[tabIndex].CanPlace(item, targetSlot);
        }

        /// <summary>Уникальные предметы в области (для Swap-if-One). Если Count==1 — своп, >1 — блок.</summary>
        public HashSet<InventoryItem> GetUniqueItemsInStashArea(int tabIndex, InventoryItem item, int rootIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return new HashSet<InventoryItem>();
            return _tabs[tabIndex].GetUniqueItemsInAreaAtRoot(item, rootIndex);
        }

        private static bool StashRectsOverlap(int anchor1, int w1, int h1, int anchor2, int w2, int h2)
        {
            int r1 = anchor1 / STASH_COLS, c1 = anchor1 % STASH_COLS;
            int r2 = anchor2 / STASH_COLS, c2 = anchor2 % STASH_COLS;
            return r1 < r2 + h2 && r2 < r1 + h1 && c1 < c2 + w2 && c2 < c1 + w1;
        }

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
                var grid = _tabs[t];
                GetStashItemSize(item, out int ww, out int hh);
                for (int row = 0; row <= STASH_ROWS - hh; row++)
                    for (int col = 0; col <= STASH_COLS - ww; col++)
                    {
                        int anchor = row * STASH_COLS + col;
                        if (overlapsExclude(t, anchor)) continue;
                        if (grid.CanPlace(item, anchor)) { outTab = t; outAnchor = anchor; return true; }
                    }
            }
            return false;
        }

        /// <summary>Защита: положить предмет в первый свободный слот любой вкладки (если инвентарь полон).</summary>
        public bool TryAddItemToAnyTab(InventoryItem item)
        {
            return TryAddItemPreferringTab(item, -1);
        }

        /// <summary>Положить предмет в склад; сначала preferredTab, затем остальные вкладки.</summary>
        public bool TryAddItemPreferringTab(InventoryItem item, int preferredTab)
        {
            if (item?.Data == null) return false;
            int n = _tabs.Count;
            if (n == 0) return false;
            if (preferredTab >= 0 && preferredTab < n)
            {
                int root = _tabs[preferredTab].FindFirstEmptyRoot(item, -1);
                if (root >= 0 && _tabs[preferredTab].Place(item, root)) { OnStashChanged?.Invoke(); return true; }
            }
            for (int t = 0; t < n; t++)
            {
                if (t == preferredTab) continue;
                int root = _tabs[t].FindFirstEmptyRoot(item, -1);
                if (root >= 0 && _tabs[t].Place(item, root)) { OnStashChanged?.Invoke(); return true; }
            }
            return false;
        }

        /// <summary>Забрать предмет из склада по корневому слоту. Предмет «в руке» у вызывающего.</summary>
        public InventoryItem TakeItemFromStash(int tabIndex, int anchorSlot)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return null;
            var item = _tabs[tabIndex].Take(anchorSlot);
            if (item != null) OnStashChanged?.Invoke();
            return item;
        }

        /// <summary>Поставить предмет в склад. Swap-if-One: 0 — место, 1 — своп (вытесненный в fromTab/fromAnchor или fromInvAnchor), >1 — блок.</summary>
        public bool PlaceItemInStash(InventoryItem item, int toTab, int toSlotIndex, int fromTabForSwap = -1, int fromAnchorForSwap = -1, int fromInvAnchorForSwap = -1)
        {
            if (item == null || item.Data == null || toTab < 0 || toTab >= _tabs.Count) return false;
            var grid = _tabs[toTab];
            var unique = GetUniqueItemsInStashArea(toTab, item, toSlotIndex);
            if (unique.Count > 1) return false;
            if (unique.Count == 0)
            {
                if (!grid.CanPlace(item, toSlotIndex)) return false;
                if (!grid.Place(item, toSlotIndex)) return false;
                OnStashChanged?.Invoke();
                return true;
            }
            InventoryItem other = null;
            foreach (var u in unique) { other = u; break; }
            if (other == null || other == item)
            {
                if (!grid.CanPlace(item, toSlotIndex)) return false;
                if (!grid.Place(item, toSlotIndex)) return false;
                OnStashChanged?.Invoke();
                return true;
            }
            // Сначала полностью очищаем ячейки вытесняемого (Take), затем вписываем новый — иначе возможна потеря предмета при свопе
            grid.GetItemAt(toSlotIndex, out _, out int otherRoot);
            InventoryItem displaced = grid.Take(otherRoot);
            if (displaced == null) return false;
            if (!grid.Place(item, toSlotIndex))
            {
                grid.Place(displaced, otherRoot);
                OnStashChanged?.Invoke();
                return false;
            }
            bool ok = false;
            if (fromInvAnchorForSwap >= 0 && InventoryManager.Instance != null)
                ok = InventoryManager.Instance.PlaceItemAt(displaced, fromInvAnchorForSwap, -1);
            else if (fromTabForSwap >= 0 && fromAnchorForSwap >= 0 && fromTabForSwap < _tabs.Count && _tabs[fromTabForSwap].CanPlace(displaced, fromAnchorForSwap))
            {
                _tabs[fromTabForSwap].Place(displaced, fromAnchorForSwap);
                ok = true;
            }
            else
            {
                GetStashItemSize(item, out int iw, out int ih);
                GetStashItemSize(displaced, out int ow, out int oh);
                ok = FindStashSlotForDisplaced(displaced, fromTabForSwap, fromAnchorForSwap, toTab, otherRoot, ow, oh, toTab, toSlotIndex, iw, ih, out int dt, out int da) && _tabs[dt].Place(displaced, da);
            }
            if (!ok)
            {
                grid.Take(toSlotIndex);
                grid.Place(displaced, otherRoot);
                OnStashChanged?.Invoke();
                return false;
            }
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Переместить предмет из инвентаря в склад.</summary>
        public bool TryMoveFromInventory(int fromInventorySlotIndex, int toStashTab, int toStashSlot)
        {
            if (InventoryManager.Instance == null || toStashTab < 0 || toStashTab >= _tabs.Count) return false;
            InventoryItem item = InventoryManager.Instance.TakeItemFromSlot(fromInventorySlotIndex);
            if (item == null) return false;
            if (!_tabs[toStashTab].CanPlace(item, toStashSlot) || !_tabs[toStashTab].Place(item, toStashSlot))
            {
                InventoryManager.Instance.RecoverItemToInventory(item);
                return false;
            }
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Переместить предмет из склада в инвентарь (предмет уже взят из склада при драге).</summary>
        public bool TryMoveToInventory(int fromStashTab, int fromStashSlot, int toInventorySlotIndex)
        {
            if (InventoryManager.Instance == null || fromStashTab < 0 || fromStashTab >= _tabs.Count) return false;
            InventoryItem item = _tabs[fromStashTab].Take(fromStashSlot);
            if (item == null) return false;
            if (!InventoryManager.Instance.PlaceItemAt(item, toInventorySlotIndex, -1))
            {
                _tabs[fromStashTab].Place(item, fromStashSlot);
                OnStashChanged?.Invoke();
                return false;
            }
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Атомарный перенос предмета (уже в руке) из склада в инвентарь: вставка в инвентарь; при неудаче — откат в склад. Предмет уже взят из склада при старте драга.</summary>
        public bool TryMoveItemToInventoryAtomic(InventoryItem item, int fromStashTab, int fromStashAnchor, int toInventorySlotIndex)
        {
            if (item == null || item.Data == null || InventoryManager.Instance == null) return false;
            if (fromStashTab < 0 || fromStashTab >= _tabs.Count) return false;
            bool placed = InventoryManager.Instance.PlaceItemAt(item, toInventorySlotIndex, -1);
            if (!placed)
            {
                _tabs[fromStashTab].Place(item, fromStashAnchor);
                OnStashChanged?.Invoke();
                return false;
            }
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Переместить или свопнуть внутри склада. Swap-if-One.</summary>
        public bool TryMoveStashToStash(int fromTab, int fromAnchorSlot, int toTab, int toSlotIndex)
        {
            if (fromTab < 0 || fromTab >= _tabs.Count || toTab < 0 || toTab >= _tabs.Count) return false;
            InventoryItem itemA = _tabs[fromTab].Take(fromAnchorSlot);
            if (itemA == null) return false;
            if (fromTab == toTab && fromAnchorSlot == toSlotIndex) { _tabs[fromTab].Place(itemA, fromAnchorSlot); OnStashChanged?.Invoke(); return true; }
            var gridTo = _tabs[toTab];
            var unique = gridTo.GetUniqueItemsInAreaAtRoot(itemA, toSlotIndex);
            if (unique.Count > 1) { _tabs[fromTab].Place(itemA, fromAnchorSlot); OnStashChanged?.Invoke(); return false; }
            if (unique.Count == 0)
            {
                if (!gridTo.CanPlace(itemA, toSlotIndex)) { _tabs[fromTab].Place(itemA, fromAnchorSlot); OnStashChanged?.Invoke(); return false; }
                gridTo.Place(itemA, toSlotIndex);
                OnStashChanged?.Invoke();
                return true;
            }
            InventoryItem itemB = null;
            foreach (var u in unique) { itemB = u; break; }
            if (itemB == null || itemB == itemA) { gridTo.Place(itemA, toSlotIndex); OnStashChanged?.Invoke(); return true; }
            gridTo.GetItemAt(toSlotIndex, out _, out int otherRoot);
            InventoryItem displaced = gridTo.Take(otherRoot);
            if (displaced == null) { _tabs[fromTab].Place(itemA, fromAnchorSlot); OnStashChanged?.Invoke(); return false; }
            if (!gridTo.Place(itemA, toSlotIndex)) { gridTo.Place(displaced, otherRoot); _tabs[fromTab].Place(itemA, fromAnchorSlot); OnStashChanged?.Invoke(); return false; }
            if (!_tabs[fromTab].CanPlace(displaced, fromAnchorSlot) || !_tabs[fromTab].Place(displaced, fromAnchorSlot))
            {
                gridTo.Take(toSlotIndex);
                gridTo.Place(displaced, otherRoot);
                _tabs[fromTab].Place(itemA, fromAnchorSlot);
                OnStashChanged?.Invoke();
                return false;
            }
            OnStashChanged?.Invoke();
            return true;
        }

        /// <summary>Своп между слотом инвентаря и слотом склада (или перенос).</summary>
        public bool TrySwapInventoryStash(int invSlotIndex, int stashTab, int stashSlotIndex)
        {
            if (InventoryManager.Instance == null || stashTab < 0 || stashTab >= _tabs.Count) return false;
            InventoryItem invItem = InventoryManager.Instance.GetItem(invSlotIndex);
            InventoryItem stashItem = _tabs[stashTab].GetItemAt(stashSlotIndex);

            if (invItem != null && stashItem == null)
                return TryMoveFromInventory(invSlotIndex, stashTab, stashSlotIndex);
            if (invItem != null && stashItem != null)
            {
                if (!InventoryManager.Instance.CanPlaceItemAt(stashItem, invSlotIndex)) return false;
                _tabs[stashTab].GetItemAt(stashSlotIndex, out _, out int stashRoot);
                InventoryItem a = InventoryManager.Instance.TakeItemFromSlot(invSlotIndex);
                InventoryItem b = _tabs[stashTab].Take(stashRoot);
                if (a == null || b == null) { if (a != null) InventoryManager.Instance.RecoverItemToInventory(a); if (b != null) _tabs[stashTab].Place(b, stashRoot); OnStashChanged?.Invoke(); return false; }
                if (!_tabs[stashTab].Place(a, stashRoot)) { InventoryManager.Instance.RecoverItemToInventory(a); _tabs[stashTab].Place(b, stashRoot); OnStashChanged?.Invoke(); return false; }
                if (!InventoryManager.Instance.PlaceItemAt(b, invSlotIndex, -1)) { _tabs[stashTab].Take(stashRoot); _tabs[stashTab].Place(b, stashRoot); InventoryManager.Instance.RecoverItemToInventory(a); OnStashChanged?.Invoke(); return false; }
                OnStashChanged?.Invoke();
                return true;
            }
            if (invItem == null && stashItem != null)
                return TryMoveToInventory(stashTab, stashSlotIndex, invSlotIndex);
            return false;
        }

        private int GetAnchorSlot(int tabIndex, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count) return -1;
            var grid = _tabs[tabIndex];
            for (int i = 0; i < grid.Length; i++)
            {
                grid.GetItemAt(i, out InventoryItem it, out int root);
                if (it == item) return root;
            }
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

        private void PlaceItem(int tabIndex, int slotIndex, InventoryItem item)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count || item?.Data == null) return;
            _tabs[tabIndex].Place(item, slotIndex);
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
                    grid.GetItemAt(i, out InventoryItem item, out int root);
                    if (item != null && item.Data != null && root == i)
                        tabData.Items.Add(item.GetSaveData(i));
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
                EnsureAtLeastOneTab();
                OnStashChanged?.Invoke();
                return;
            }
            foreach (var tabData in data.Tabs ?? new List<StashTabSaveData>())
            {
                var grid = new GridContainer(STASH_COLS, STASH_ROWS);
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
                        grid.Place(item, anchor);
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
