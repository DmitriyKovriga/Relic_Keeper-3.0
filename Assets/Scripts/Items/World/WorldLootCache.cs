using System;
using System.Collections.Generic;
using Scripts.Dungeon;
using Scripts.Inventory;
using Scripts.UI;
using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Items.World
{
    [DisallowMultipleComponent]
    public sealed class WorldLootCache : MonoBehaviour, IInteractable, ITabbedItemGrid
    {
        private const int SpritePixels = 24;
        private const float PixelsPerUnit = 24f;

        private static readonly Color PurpleCore = new Color(0.20f, 0.055f, 0.31f, 1f);
        private static readonly Color PurpleMid = new Color(0.45f, 0.12f, 0.67f, 1f);
        private static readonly Color PurpleEdge = new Color(0.76f, 0.37f, 1f, 1f);
        private static readonly Color QuestionColor = new Color(1f, 0.86f, 0.30f, 1f);

        private static Sprite _cacheSprite;
        private static Sprite _glowSprite;

        private readonly List<InventoryItem> _items = new List<InventoryItem>();
        private readonly List<GridContainer> _tabs = new List<GridContainer>();
        private int _currentTabIndex;
        private SpriteRenderer _mainRenderer;
        private SpriteRenderer _glowRenderer;
        private CircleCollider2D _interactionCollider;
        private bool _destroying;

        public event Action Changed;
        public event Action OnChanged;

        public IReadOnlyList<InventoryItem> Items => _items;
        public int Count => _items.Count;
        public int TabCount => _tabs.Count;
        public int CurrentTabIndex => _currentTabIndex;
        public bool CanAddTab => false;
        public int VisibleCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (!LootFilterSettings.ShouldHide(_items[i]))
                        count++;
                }
                return count;
            }
        }
        public bool CanAcceptItems => !_destroying;

        public static WorldLootCache Create(Vector2 position, Transform parent)
        {
            var host = new GameObject("WorldLootCache");
            host.transform.SetParent(parent, true);
            host.transform.position = new Vector3(position.x, position.y, 0f);
            WorldLootCache cache = host.AddComponent<WorldLootCache>();
            cache.BuildVisual();
            cache.BuildCollider();
            cache.SubscribeToLootFilter();
            return cache;
        }

        private void Awake()
        {
            if (GetComponent<SpriteRenderer>() == null)
                BuildVisual();
            if (GetComponent<CircleCollider2D>() == null)
                BuildCollider();
            SubscribeToLootFilter();
        }

        private void OnEnable()
        {
            SubscribeToLootFilter();
            RefreshFilterState();
        }

        private void OnDisable()
        {
            LootFilterSettings.Changed -= OnLootFilterChanged;
        }

        private void Update()
        {
            if (_glowRenderer == null)
                return;

            float pulse = 1.18f + Mathf.Sin(Time.time * 3.5f) * 0.08f;
            _glowRenderer.transform.localScale = Vector3.one * pulse;
        }

        public bool Absorb(WorldDroppedItem droppedItem)
        {
            if (!CanAcceptItems || droppedItem == null)
                return false;

            Vector3 sourcePosition = droppedItem.transform.position;
            if (!droppedItem.TryExtractForLootCache(out InventoryItem item))
                return false;

            _items.Add(item);
            AddItemToGrid(item, _currentTabIndex);
            RefreshFilterState();
            NotifyChanged();

            if (Application.isPlaying)
            {
                WorldLootCacheFlightVisual.Spawn(sourcePosition, this);
                Destroy(droppedItem.gameObject);
            }
            else
            {
                DestroyImmediate(droppedItem.gameObject);
            }

            return true;
        }

        public bool TryTake(InventoryItem item)
        {
            if (item == null || !_items.Contains(item) || InventoryManager.Instance == null)
                return false;
            if (!InventoryManager.Instance.TryPickupItem(item))
            {
                PlayerNoticeBanner.ShowInventoryFull();
                return false;
            }

            RemoveItem(item);
            RefreshFilterState();
            NotifyChanged();
            if (_items.Count == 0)
            {
                _destroying = true;
                Destroy(gameObject);
            }
            return true;
        }

        public string GetPrompt()
        {
            return RuntimeLocalization.Resolve(
                "worldLootCache.open",
                "Open loot sphere",
                "Открыть сферу добычи");
        }

        public void Interact()
        {
            if (!CanInteract())
                return;

            StashPanelToggle toggle = FindFirstObjectByType<StashPanelToggle>(FindObjectsInactive.Include);
            if (toggle != null)
                toggle.OpenLootCache(this);
            else
                Debug.LogWarning("[WorldLootCache] StashPanelToggle was not found; loot cache cannot open.");
        }

        public bool CanInteract()
        {
            return !_destroying && VisibleCount > 0;
        }

        private void BuildVisual()
        {
            _mainRenderer = GetComponent<SpriteRenderer>();
            if (_mainRenderer == null)
                _mainRenderer = gameObject.AddComponent<SpriteRenderer>();
            _mainRenderer.sprite = GetCacheSprite();
            _mainRenderer.color = Color.white;
            _mainRenderer.sortingLayerName = WorldRenderSorting.LayerVfx;
            _mainRenderer.sortingOrder = WorldDroppedItem.TopVisualSortingOrder + 2;

            Transform glow = transform.Find("PurpleGlow");
            if (glow == null)
            {
                var glowObject = new GameObject("PurpleGlow");
                glow = glowObject.transform;
                glow.SetParent(transform, false);
            }

            _glowRenderer = glow.GetComponent<SpriteRenderer>();
            if (_glowRenderer == null)
                _glowRenderer = glow.gameObject.AddComponent<SpriteRenderer>();
            _glowRenderer.sprite = GetGlowSprite();
            _glowRenderer.color = new Color(0.62f, 0.18f, 1f, 0.32f);
            _glowRenderer.sortingLayerName = WorldRenderSorting.LayerVfx;
            _glowRenderer.sortingOrder = _mainRenderer.sortingOrder - 1;
            glow.localPosition = new Vector3(0f, 0f, 0.01f);
            RefreshFilterState();
        }

        private void BuildCollider()
        {
            _interactionCollider = GetComponent<CircleCollider2D>();
            if (_interactionCollider == null)
                _interactionCollider = gameObject.AddComponent<CircleCollider2D>();
            _interactionCollider.isTrigger = true;
            _interactionCollider.radius = 0.48f;
            RefreshFilterState();
        }

        private void OnLootFilterChanged()
        {
            RefreshFilterState();
            NotifyChanged();
        }

        private void SubscribeToLootFilter()
        {
            LootFilterSettings.Changed -= OnLootFilterChanged;
            LootFilterSettings.Changed += OnLootFilterChanged;
        }

        private void RefreshFilterState()
        {
            bool visible = !_destroying && VisibleCount > 0;
            if (_mainRenderer != null)
                _mainRenderer.enabled = visible;
            if (_glowRenderer != null)
                _glowRenderer.enabled = visible;
            if (_interactionCollider != null)
                _interactionCollider.enabled = visible;
        }

        public void SetCurrentTab(int index)
        {
            if (index < 0 || index >= _tabs.Count || index == _currentTabIndex)
                return;
            _currentTabIndex = index;
            NotifyChanged();
        }

        public void AddTab()
        {
            // Player-created tabs are intentionally disabled, but ITabbedItemGrid
            // requires this method. Pages are added automatically as the cache fills.
        }

        public bool CanRemoveTab(int index) => false;
        public bool TryRemoveTab(int index) => false;
        public string GetTabLabel(int index) => (index + 1).ToString();

        public InventoryItem GetItem(int tabIndex, int slotIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count)
                return null;
            InventoryItem item = _tabs[tabIndex].GetItemAt(slotIndex);
            return LootFilterSettings.ShouldHide(item) ? null : item;
        }

        public InventoryItem GetItemAt(int tabIndex, int slotIndex, out int anchorIndex)
        {
            anchorIndex = -1;
            if (tabIndex < 0 || tabIndex >= _tabs.Count)
                return null;
            _tabs[tabIndex].GetItemAt(slotIndex, out InventoryItem item, out anchorIndex);
            if (LootFilterSettings.ShouldHide(item))
            {
                anchorIndex = -1;
                return null;
            }
            return item;
        }

        public InventoryItem TakeItem(int tabIndex, int anchorSlot)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count)
                return null;
            InventoryItem item = _tabs[tabIndex].GetItemAt(anchorSlot);
            if (item == null || LootFilterSettings.ShouldHide(item))
                return null;
            item = _tabs[tabIndex].Take(anchorSlot);
            if (item != null)
            {
                _items.Remove(item);
                RefreshFilterState();
                NotifyChanged();
            }
            return item;
        }

        public bool TryAddItemPreferringTab(InventoryItem item, int preferredTab)
        {
            if (item?.Data == null)
                return false;
            if (_items.Contains(item))
                return true;
            if (!AddItemToGrid(item, preferredTab))
                return false;
            _items.Add(item);
            RefreshFilterState();
            NotifyChanged();
            return true;
        }

        public void CompleteItemTransfer()
        {
            if (_items.Count != 0 || _destroying)
                return;
            _destroying = true;
            Destroy(gameObject);
        }

        private bool AddItemToGrid(InventoryItem item, int preferredTab)
        {
            EnsureGridPage();
            if (preferredTab >= 0 && preferredTab < _tabs.Count && TryPlaceFirstFree(_tabs[preferredTab], item))
                return true;
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (i != preferredTab && TryPlaceFirstFree(_tabs[i], item))
                    return true;
            }
            var page = new GridContainer(StashManager.STASH_COLS, StashManager.STASH_ROWS);
            _tabs.Add(page);
            return TryPlaceFirstFree(page, item);
        }

        private void EnsureGridPage()
        {
            if (_tabs.Count == 0)
                _tabs.Add(new GridContainer(StashManager.STASH_COLS, StashManager.STASH_ROWS));
        }

        private static bool TryPlaceFirstFree(GridContainer grid, InventoryItem item)
        {
            int root = grid.FindFirstEmptyRoot(item);
            return root >= 0 && grid.Place(item, root);
        }

        private void RemoveItem(InventoryItem item)
        {
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Remove(item);
            _items.Remove(item);
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
            OnChanged?.Invoke();
        }

        private static Sprite GetCacheSprite()
        {
            if (_cacheSprite != null)
                return _cacheSprite;

            var texture = CreateTexture("RuntimeLootCacheSphere");
            Vector2 center = new Vector2((SpritePixels - 1) * 0.5f, (SpritePixels - 1) * 0.5f);
            for (int y = 0; y < SpritePixels; y++)
            {
                for (int x = 0; x < SpritePixels; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    Color color = Color.clear;
                    if (distance <= 10.8f)
                        color = distance > 9.1f ? PurpleEdge : distance > 7.8f ? PurpleMid : PurpleCore;
                    if (distance < 7.8f && x < 10 && y > 13)
                        color = Color.Lerp(color, PurpleEdge, 0.30f);
                    texture.SetPixel(x, y, color);
                }
            }

            DrawQuestionMark(texture);
            texture.Apply(false, true);
            _cacheSprite = CreateSprite(texture, "RuntimeLootCacheSphere");
            return _cacheSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (_glowSprite != null)
                return _glowSprite;

            var texture = CreateTexture("RuntimeLootCacheGlow");
            Vector2 center = new Vector2((SpritePixels - 1) * 0.5f, (SpritePixels - 1) * 0.5f);
            for (int y = 0; y < SpritePixels; y++)
            {
                for (int x = 0; x < SpritePixels; x++)
                {
                    float t = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center) / 11.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, t * t * 0.72f));
                }
            }
            texture.Apply(false, true);
            _glowSprite = CreateSprite(texture, "RuntimeLootCacheGlow");
            return _glowSprite;
        }

        private static void DrawQuestionMark(Texture2D texture)
        {
            SetBlock(texture, 9, 16, 6, 2, QuestionColor);
            SetBlock(texture, 8, 14, 2, 3, QuestionColor);
            SetBlock(texture, 14, 13, 2, 4, QuestionColor);
            SetBlock(texture, 12, 11, 3, 2, QuestionColor);
            SetBlock(texture, 10, 8, 3, 3, QuestionColor);
            SetBlock(texture, 10, 4, 3, 2, QuestionColor);
        }

        private static void SetBlock(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int iy = y; iy < y + height; iy++)
            for (int ix = x; ix < x + width; ix++)
                texture.SetPixel(ix, iy, color);
        }

        private static Texture2D CreateTexture(string textureName)
        {
            return new Texture2D(SpritePixels, SpritePixels, TextureFormat.RGBA32, false)
            {
                name = textureName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static Sprite CreateSprite(Texture2D texture, string spriteName)
        {
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, SpritePixels, SpritePixels),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void OnDestroy()
        {
            _destroying = true;
            LootFilterSettings.Changed -= OnLootFilterChanged;
        }
    }
}
