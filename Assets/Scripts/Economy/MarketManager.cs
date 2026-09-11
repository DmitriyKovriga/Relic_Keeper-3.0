using System;
using System.Collections.Generic;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Saving;
using UnityEngine;

namespace Scripts.Economy
{
    /// <summary>
    /// Vendor stock + unlimited buyback tabs. Reuses the stash grid size so the stash window can host it.
    /// Shop stock refreshes when the player returns to the hub from a raid.
    /// </summary>
    public class MarketManager : MonoBehaviour, ITabbedItemGrid
    {
        public const int MinStockItems = 10;
        public const int MaxStockItems = 14;

        public static MarketManager Instance { get; private set; }

        public event Action OnChanged;

        private readonly List<GridContainer> _stockTabs = new List<GridContainer>();
        private readonly List<GridContainer> _buybackTabs = new List<GridContainer>();
        private int _currentTabIndex;
        private bool _wasInDungeon;
        private bool _subscribedToHub;

        public int StockTabCount => _stockTabs.Count;
        public int TabCount => _stockTabs.Count + _buybackTabs.Count;
        public int CurrentTabIndex => _currentTabIndex;
        public bool CanAddTab => false;

        public static MarketManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var existing = FindFirstObjectByType<MarketManager>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureHubSubscription();
                existing.EnsureStock();
                return existing;
            }

            var host = new GameObject("MarketManager");
            DontDestroyOnLoad(host);
            return host.AddComponent<MarketManager>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureHubSubscription();
            EnsureStock();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            DungeonController.HubActiveChanged -= HandleHubActiveChanged;
            _subscribedToHub = false;
        }

        private void EnsureHubSubscription()
        {
            if (_subscribedToHub)
                return;
            DungeonController.HubActiveChanged += HandleHubActiveChanged;
            _subscribedToHub = true;
            _wasInDungeon = !DungeonController.IsHubActive;
        }

        private void HandleHubActiveChanged(bool hubActive)
        {
            if (hubActive && _wasInDungeon)
                RefreshStock();
            _wasInDungeon = !hubActive;
        }

        public bool IsBuybackTab(int tabIndex)
        {
            return tabIndex >= _stockTabs.Count;
        }

        public bool CanRemoveTab(int index) => false;

        public string GetTabLabel(int index)
        {
            if (index < 0 || index >= TabCount)
                return string.Empty;
            if (index < _stockTabs.Count)
                return (index + 1).ToString();
            return $"B{index - _stockTabs.Count + 1}";
        }

        public void SetCurrentTab(int index)
        {
            if (index < 0 || index >= TabCount)
                return;
            _currentTabIndex = index;
            OnChanged?.Invoke();
        }

        public void AddTab()
        {
        }

        public bool TryRemoveTab(int index) => false;

        public InventoryItem GetItem(int tabIndex, int slotIndex)
        {
            var grid = GetGrid(tabIndex);
            return grid != null ? grid.GetItemAt(slotIndex) : null;
        }

        public InventoryItem GetItemAt(int tabIndex, int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            var grid = GetGrid(tabIndex);
            if (grid == null)
                return null;
            grid.GetItemAt(slotIndex, out InventoryItem item, out anchorIndex);
            return item;
        }

        public InventoryItem TakeItem(int tabIndex, int anchorSlot)
        {
            var grid = GetGrid(tabIndex);
            if (grid == null)
                return null;
            var item = grid.Take(anchorSlot);
            if (item != null)
                OnChanged?.Invoke();
            return item;
        }

        public bool TryAddItemPreferringTab(InventoryItem item, int preferredTab)
        {
            if (item?.Data == null)
                return false;

            if (TryPlaceInGrid(GetGrid(preferredTab), item))
            {
                OnChanged?.Invoke();
                return true;
            }

            if (IsBuybackTab(preferredTab))
                return AcceptSoldItem(item);

            return TryPlaceInStock(item);
        }

        public bool PlaceItemBack(InventoryItem item, int tabIndex, int anchorSlot)
        {
            var grid = GetGrid(tabIndex);
            if (grid == null || item?.Data == null)
                return AcceptSoldItem(item);

            if (grid.CanPlace(item, anchorSlot) && grid.Place(item, anchorSlot))
            {
                OnChanged?.Invoke();
                return true;
            }

            if (TryPlaceInGrid(grid, item))
            {
                OnChanged?.Invoke();
                return true;
            }

            return IsBuybackTab(tabIndex) ? AcceptSoldItem(item) : TryPlaceInStock(item);
        }

        public bool TryBuy(InventoryItem item, bool isBuyback)
        {
            if (item?.Data == null || InventoryManager.Instance == null)
                return false;

            int price = isBuyback ? ItemPriceCalculator.GetSellPrice(item) : ItemPriceCalculator.GetVendorPrice(item);
            if (!GoldWallet.Has(price))
            {
                PlayerNoticeBanner.ShowNotEnoughGold();
                return false;
            }

            if (!InventoryManager.Instance.CanAddItem(item))
            {
                PlayerNoticeBanner.ShowInventoryFull();
                return false;
            }

            if (!GoldWallet.TrySpend(price))
            {
                PlayerNoticeBanner.ShowNotEnoughGold();
                return false;
            }

            if (!InventoryManager.Instance.AddItem(item))
            {
                GoldWallet.Add(price);
                PlayerNoticeBanner.ShowInventoryFull();
                return false;
            }

            return true;
        }

        public bool TrySell(InventoryItem item)
        {
            if (item?.Data == null)
                return false;

            int price = ItemPriceCalculator.GetSellPrice(item);
            if (!AcceptSoldItem(item))
                return false;

            GoldWallet.Add(price);
            return true;
        }

        public bool AcceptSoldItem(InventoryItem item)
        {
            if (item?.Data == null)
                return false;

            for (int i = 0; i < _buybackTabs.Count; i++)
            {
                if (TryPlaceInGrid(_buybackTabs[i], item))
                {
                    OnChanged?.Invoke();
                    return true;
                }
            }

            var tab = CreateGrid();
            _buybackTabs.Add(tab);
            if (!TryPlaceInGrid(tab, item))
            {
                _buybackTabs.Remove(tab);
                return false;
            }

            OnChanged?.Invoke();
            return true;
        }

        public void RefreshStock()
        {
            _stockTabs.Clear();
            GenerateStock();
            if (_currentTabIndex >= TabCount)
                _currentTabIndex = 0;
            OnChanged?.Invoke();
        }

        public MarketSaveData GetSaveData()
        {
            return new MarketSaveData
            {
                CurrentTabIndex = _currentTabIndex,
                StockTabs = SerializeTabs(_stockTabs),
                BuybackTabs = SerializeTabs(_buybackTabs)
            };
        }

        public void LoadState(MarketSaveData data, ItemDatabaseSO itemDB)
        {
            _stockTabs.Clear();
            _buybackTabs.Clear();
            LoadTabs(data?.StockTabs, _stockTabs, itemDB);
            LoadTabs(data?.BuybackTabs, _buybackTabs, itemDB);
            EnsureStock();
            _currentTabIndex = data != null
                ? Mathf.Clamp(data.CurrentTabIndex, 0, Mathf.Max(0, TabCount - 1))
                : 0;
            OnChanged?.Invoke();
        }

        private void EnsureStock()
        {
            if (_stockTabs.Count == 0)
                GenerateStock();
        }

        private void GenerateStock()
        {
            var tab = CreateGrid();
            _stockTabs.Add(tab);

            int itemLevel = 1;
            var player = FindFirstObjectByType<PlayerStats>();
            if (player != null && player.Leveling != null)
                itemLevel = Mathf.Max(1, player.Leveling.Level);

            int count = UnityEngine.Random.Range(MinStockItems, MaxStockItems + 1);
            for (int i = 0; i < count; i++)
            {
                InventoryItem item = EnemyLootDropService.CreateGuaranteedItem(itemLevel, UnityEngine.Random.value);
                if (item == null)
                    continue;
                if (TryPlaceInGrid(tab, item))
                    continue;

                tab = CreateGrid();
                _stockTabs.Add(tab);
                TryPlaceInGrid(tab, item);
            }
        }

        private bool TryPlaceInStock(InventoryItem item)
        {
            for (int i = 0; i < _stockTabs.Count; i++)
            {
                if (TryPlaceInGrid(_stockTabs[i], item))
                {
                    OnChanged?.Invoke();
                    return true;
                }
            }

            var tab = CreateGrid();
            _stockTabs.Add(tab);
            bool placed = TryPlaceInGrid(tab, item);
            if (placed)
                OnChanged?.Invoke();
            return placed;
        }

        private GridContainer GetGrid(int tabIndex)
        {
            if (tabIndex < 0)
                return null;
            if (tabIndex < _stockTabs.Count)
                return _stockTabs[tabIndex];
            int buybackIndex = tabIndex - _stockTabs.Count;
            if (buybackIndex < 0 || buybackIndex >= _buybackTabs.Count)
                return null;
            return _buybackTabs[buybackIndex];
        }

        private static bool TryPlaceInGrid(GridContainer grid, InventoryItem item)
        {
            if (grid == null || item?.Data == null)
                return false;
            int root = grid.FindFirstEmptyRoot(item, -1);
            return root >= 0 && grid.Place(item, root);
        }

        private static GridContainer CreateGrid()
        {
            return new GridContainer(StashManager.STASH_COLS, StashManager.STASH_ROWS);
        }

        private static List<StashTabSaveData> SerializeTabs(List<GridContainer> tabs)
        {
            var result = new List<StashTabSaveData>();
            for (int t = 0; t < tabs.Count; t++)
            {
                var tabData = new StashTabSaveData { TabName = $"Tab {t + 1}" };
                var grid = tabs[t];
                for (int i = 0; i < grid.Length; i++)
                {
                    grid.GetItemAt(i, out InventoryItem item, out int root);
                    if (item != null && item.Data != null && root == i)
                        tabData.Items.Add(item.GetSaveData(i));
                }
                result.Add(tabData);
            }

            return result;
        }

        private static void LoadTabs(List<StashTabSaveData> source, List<GridContainer> target, ItemDatabaseSO itemDB)
        {
            if (source == null)
                return;

            foreach (var tabData in source)
            {
                var grid = CreateGrid();
                var claimed = new HashSet<int>();
                if (tabData?.Items != null)
                {
                    foreach (var itemData in tabData.Items)
                    {
                        var item = InventoryItem.LoadFromSave(itemData, itemDB);
                        if (item == null || item.Data == null || itemData.SlotIndex < 0 || itemData.SlotIndex >= grid.Length)
                            continue;

                        int anchor = itemData.SlotIndex;
                        StashManager.GetStashItemSize(item, out int w, out int h);
                        bool anyClaimed = false;
                        for (int r = 0; r < h && !anyClaimed; r++)
                            for (int c = 0; c < w; c++)
                                if (claimed.Contains(anchor + r * StashManager.STASH_COLS + c))
                                    anyClaimed = true;

                        if (anyClaimed)
                            continue;

                        grid.Place(item, anchor);
                        for (int r = 0; r < h; r++)
                            for (int c = 0; c < w; c++)
                                claimed.Add(anchor + r * StashManager.STASH_COLS + c);
                    }
                }

                target.Add(grid);
            }
        }
    }
}
