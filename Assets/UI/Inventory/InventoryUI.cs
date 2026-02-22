using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

public partial class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument _uiDoc;
    [Tooltip("Если задано, при закрытии окна инвентаря сбрасывается режим крафта орбой.")]
    [SerializeField] private WindowView _windowView;
    [Header("Crafting")]
    [SerializeField] private CraftingOrbSlotsConfigSO _orbSlotsConfig;
    
    private const int ROWS = 4;
    private const int COLUMNS = 10;
    [Tooltip("Размер одной ячейки рюкзака в px (родной размер). Иконки в инвентаре масштабируются под этот размер. Под 480×270: 24px влезает в экран.")]
    [SerializeField] [Min(16f)] private float _inventorySlotSize = 24f;
    [Tooltip("Размер одной ячейки на складе в px (уменьшенный, чтобы влезало больше). Иконки на складе и при перетаскивании со склада используют этот размер.")]
    [SerializeField] [Min(12f)] private float _stashSlotSize = 20f;
    private float InventorySlotSize => _inventorySlotSize;
    private float StashSlotSize => _stashSlotSize;
    /// <summary>Размер «клетки» для иконок экипировки/крафта (слот 2×2 = 48px → 24 на клетку).</summary>
    private const float EquipmentIconCellSize = 24f;

    /// <summary>Размеры слотов экипировки/крафта в пикселях (должны совпадать с USS: slot-2x2, slot-2x3, slot-2x4).</summary>
    private static readonly (float w, float h)[] EquipmentSlotSizes =
    {
        (48f, 48f),  // Helmet 2x2
        (48f, 72f),  // Body 2x3
        (48f, 96f),  // MainHand 2x4
        (48f, 96f),  // OffHand 2x4
        (48f, 48f),  // Gloves 2x2
        (48f, 48f),  // Boots 2x2
    };
    private const float CraftSlotWidth = 48f;
    private const float CraftSlotHeight = 96f;

    /// <summary>userData для слотов склада = STASH_SLOT_OFFSET + slotIndex (0..STASH_SLOTS_PER_TAB-1).</summary>
    private const int STASH_SLOT_OFFSET = 500000;

    private VisualElement _root;
    /// <summary>Корень UIDocument инвентаря — тултипы должны использовать его для координат (тот же panel).</summary>
    public VisualElement RootVisualElement => _root;
    /// <summary>Окно инвентаря/склада внутри полноэкранного корня. Позиционируем именно его.</summary>
    private VisualElement _windowRoot;
    private VisualElement _inventoryContainer;
    private VisualElement _itemsLayer;
    private VisualElement _ghostIcon;
    /// <summary>Подсветка зона дропа: зелёный — можно, жёлтый — своп, красный — нельзя.</summary>
    private VisualElement _ghostHighlight;

    private VisualElement _stashPanel;
    private VisualElement _stashTabsRow;
    private VisualElement _stashGridContainer;
    private VisualElement _stashItemsLayer;
    private VisualElement _mainRow;
    private List<VisualElement> _stashSlots = new List<VisualElement>();

    /// <summary>Склад открыт отдельно (бинт B). По умолчанию скрыт при открытии инвентаря по I.</summary>
    public bool IsStashVisible { get; private set; }

    private VisualElement _equipmentView;
    private VisualElement _craftView;
    private VisualElement _craftSlot;
    private VisualElement _orbSlotsRow;
    private int _currentTab; // 0 = Equipment, 1 = Craft
    
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    private List<(VisualElement slot, Label countLabel)> _orbSlots = new List<(VisualElement, Label)>();
    
    private bool _isDragging;
    /// <summary>Предмет «в руке» — не в контейнере, пока держим курсор (PoE-style).</summary>
    private InventoryItem _draggedItem;
    private int _draggedSourceAnchor = -1;
    private bool _draggedFromStash;
    private int _draggedStashTab = -1;
    private int _draggedStashAnchorSlot = -1;
    /// <summary>Смещение курсора от верх-левого угла иконки при захвате (grab offset), в локальных координатах root.</summary>
    private Vector2 _grabOffsetRootLocal;

    private bool _applyOrbMode;
    private CraftingOrbSO _applyOrbOrb;
    private VisualElement _applyOrbSlotHighlight;
    private int _capturedPointerId = -1;
    /// <summary>Блокировка повторного входа в OnPointerUp (частые дропы не должны дублировать предмет).</summary>
    private bool _dropInProgress;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        _windowRoot = _root.Q<VisualElement>("WindowRoot");
        if (_windowRoot == null) _windowRoot = _root;
        
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        if (_inventoryContainer == null) { Debug.LogError("InventoryGrid not found"); return; }
        _inventoryContainer.style.overflow = Overflow.Visible;

        _stashPanel = _root.Q<VisualElement>("StashPanel");
        _stashTabsRow = _root.Q<VisualElement>("StashTabsRow");
        _stashGridContainer = _root.Q<VisualElement>("StashGridContainer");
        _mainRow = _root.Q<VisualElement>("MainRow");
        SetStashPanelVisible(false);

        CreateGhostIcon();
        GenerateBackpackGrid();
        SetupEquipmentSlots();
        SetupStashPanel();
        LoadOrbSlotsConfig();
        SetupTabs();
        SetupCraftView();
        ApplyInventoryArtTheme();

        if (InventoryManager.Instance == null)
            _root.schedule.Execute(TrySubscribe).Every(100).Until(() => InventoryManager.Instance != null);
        else
            TrySubscribe();
        if (StashManager.Instance == null)
            _root.schedule.Execute(TrySubscribeStash).Every(100).Until(() => StashManager.Instance != null);
        else
            TrySubscribeStash();

        _root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.RegisterCallback<PointerOutEvent>(OnInventoryWindowPointerOut);
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        _root.RegisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);

        if (_windowView == null) _windowView = GetComponent<WindowView>();
        if (_windowView != null)
        {
            _windowView.OnClosed += OnInventoryWindowClosed;
            _windowView.OnOpened += OnInventoryWindowOpened;
        }
        _root.schedule.Execute(() => ApplyInventorySpacing(IsStashVisible)).ExecuteLater(3);
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (StashManager.Instance != null)
            StashManager.Instance.OnStashChanged -= RefreshStash;
        if (_windowView != null)
        {
            _windowView.OnClosed -= OnInventoryWindowClosed;
            _windowView.OnOpened -= OnInventoryWindowOpened;
        }
        ExitApplyOrbMode();

        _root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        _root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.UnregisterCallback<PointerOutEvent>(OnInventoryWindowPointerOut);
        _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        _root.UnregisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);
    }

    private void OnInventoryWindowClosed()
    {
        ExitApplyOrbMode();
        SetStashPanelVisible(false);
        if (_isDragging && _draggedItem != null)
        {
            InventoryItem held = _draggedItem;
            bool fromStash = _draggedFromStash;
            int stashTab = _draggedStashTab;
            int stashAnchor = _draggedStashAnchorSlot;
            int invAnchor = _draggedSourceAnchor;
            _draggedItem = null;
            _isDragging = false;
            _ghostIcon.style.display = DisplayStyle.None;
            if (_ghostHighlight != null) _ghostHighlight.style.display = DisplayStyle.None;
            _draggedSourceAnchor = -1;
            _draggedFromStash = false;
            _draggedStashTab = -1;
            _draggedStashAnchorSlot = -1;
            bool returned = false;
            if (fromStash && StashManager.Instance != null && stashTab >= 0 && stashAnchor >= 0)
                returned = StashManager.Instance.PlaceItemInStash(held, stashTab, stashAnchor, -1, -1, -1);
            else if (!fromStash && invAnchor >= 0 && InventoryManager.Instance != null)
                returned = InventoryManager.Instance.PlaceItemAt(held, invAnchor, -1);
            if (!returned && InventoryManager.Instance != null)
                InventoryManager.Instance.RecoverItemToInventory(held);
        }
    }

    private void OnInventoryWindowOpened()
    {
        ApplyInventorySpacing(IsStashVisible);
    }

    /// <summary>Переключает видимость склада. Раскладка задаётся только в USS: класс stash-open на WindowRoot и MainRow.</summary>
    public void SetStashPanelVisible(bool visible)
    {
        IsStashVisible = visible;
        if (_stashPanel != null)
        {
            if (visible) _stashPanel.AddToClassList("visible");
            else _stashPanel.RemoveFromClassList("visible");
        }
        if (_mainRow != null)
        {
            if (visible)
            {
                _mainRow.AddToClassList("stash-open");
                _mainRow.RemoveFromClassList("inventory-solo");
            }
            else
            {
                _mainRow.RemoveFromClassList("stash-open");
                _mainRow.AddToClassList("inventory-solo");
            }
        }
        VisualElement target = _windowRoot != null ? _windowRoot : _root;
        if (target != null)
        {
            if (visible)
            {
                target.AddToClassList("stash-open");
                target.RemoveFromClassList("inventory-solo");
            }
            else
            {
                target.RemoveFromClassList("stash-open");
                target.AddToClassList("inventory-solo");
            }
        }
        ApplyInventorySpacing(visible);
    }

    /// <summary>Внутренние отступы: соло — 8px слева и справа у окна; дуо — 8px справа у контента. Задаём из кода в пикселях.</summary>
    private void ApplyInventorySpacing(bool stashOpen)
    {
        Debug.Log($"[InventoryUI] ApplyInventorySpacing stashOpen={stashOpen} _windowRoot={(_windowRoot != null)} _mainRow={(_mainRow != null)}");
        const float paddingSoloPx = 8f;
        const float marginDuoRightPx = 8f;

        if (_windowRoot != null)
        {
            if (stashOpen)
            {
                _windowRoot.style.paddingLeft = new Length(paddingSoloPx, LengthUnit.Pixel);
                _windowRoot.style.paddingRight = new Length(paddingSoloPx, LengthUnit.Pixel);
            }
            else
            {
                _windowRoot.style.paddingLeft = new Length(paddingSoloPx, LengthUnit.Pixel);
                _windowRoot.style.paddingRight = new Length(paddingSoloPx, LengthUnit.Pixel);
            }
        }

        if (_mainRow != null)
        {
            if (stashOpen)
            {
                _mainRow.style.marginLeft = new Length(0f, LengthUnit.Pixel);
                _mainRow.style.marginRight = new Length(marginDuoRightPx, LengthUnit.Pixel);
            }
            else
            {
                _mainRow.style.marginLeft = new Length(0f, LengthUnit.Pixel);
                _mainRow.style.marginRight = new Length(0f, LengthUnit.Pixel);
            }
        }
    }

    private void OnRootPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode && evt.button == 0 && _capturedPointerId < 0)
        {
            _root.CapturePointer(evt.pointerId);
            _capturedPointerId = evt.pointerId;
        }
    }

    private void OnRootMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 1) return;
        VisualElement slot = evt.target as VisualElement;
        while (slot != null && !slot.ClassListContains("orb-slot")) slot = slot.parent;
        if (slot != null && TryEnterApplyOrbModeFromSlot(slot))
        {
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private void TrySubscribe()
    {
        if (InventoryManager.Instance == null) return;
        InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        RefreshInventory();
    }

    private void TrySubscribeStash()
    {
        if (StashManager.Instance == null) return;
        StashManager.Instance.OnStashChanged -= RefreshStash;
        StashManager.Instance.OnStashChanged += RefreshStash;
        RefreshStash();
    }

    private void SetupStashPanel()
    {
        if (_stashGridContainer == null) return;

        if (TryBindStashGridFromUxml())
            return;

        _stashGridContainer.Clear();
        _stashSlots.Clear();

        _stashGridContainer.style.width = StashManager.STASH_COLS * StashSlotSize;
        _stashGridContainer.style.height = StashManager.STASH_ROWS * StashSlotSize;

        _stashItemsLayer = new VisualElement { name = "StashItemsLayer" };
        _stashItemsLayer.style.position = Position.Absolute;
        _stashItemsLayer.StretchToParentSize();
        _stashItemsLayer.style.overflow = Overflow.Visible;

        for (int r = 0; r < StashManager.STASH_ROWS; r++)
        {
            var row = new VisualElement();
            row.AddToClassList("stash-row");
            row.style.width = StashManager.STASH_COLS * StashSlotSize;
            row.style.height = StashSlotSize;
            _stashGridContainer.Add(row);
            for (int c = 0; c < StashManager.STASH_COLS; c++)
            {
                int slotIndex = r * StashManager.STASH_COLS + c;
                var slot = new VisualElement();
                slot.AddToClassList("slot");
                slot.style.width = StashSlotSize;
                slot.style.height = StashSlotSize;
                slot.userData = STASH_SLOT_OFFSET + slotIndex;
                slot.RegisterCallback<PointerDownEvent>(OnStashSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                row.Add(slot);
                _stashSlots.Add(slot);
            }
        }
        _stashGridContainer.Add(_stashItemsLayer);
    }

    private void RefreshStash()
    {
        RefreshStashTabs();
        RefreshStashGrid();
    }

    private void RefreshStashTabs()
    {
        if (_stashTabsRow == null || StashManager.Instance == null) return;
        float savedScrollOffset = 0f;
        var existingScroll = _stashTabsRow.Q<ScrollView>();
        if (existingScroll != null && existingScroll.horizontalScroller != null)
            savedScrollOffset = existingScroll.horizontalScroller.value;

        _stashTabsRow.Clear();
        var scroll = new ScrollView(ScrollViewMode.Horizontal);
        scroll.AddToClassList("stash-tabs-scroll");
        scroll.style.height = 18;
        scroll.style.minHeight = 18;
        scroll.style.maxHeight = 18;
        if (scroll.verticalScroller != null)
        {
            scroll.verticalScroller.style.display = DisplayStyle.None;
            scroll.verticalScroller.style.width = 0;
            scroll.verticalScroller.style.minWidth = 0;
            scroll.verticalScroller.style.maxWidth = 0;
        }
        var content = new VisualElement();
        content.AddToClassList("stash-tabs-carousel");
        content.style.flexDirection = FlexDirection.Row;
        content.style.flexShrink = 0;

        int tabCount = StashManager.Instance.TabCount;
        int current = StashManager.Instance.CurrentTabIndex;
        for (int i = 0; i < tabCount; i++)
        {
            int t = i;
            var wrap = new VisualElement();
            wrap.AddToClassList("stash-tab-wrap");
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.alignItems = Align.Center;
            var tab = new Button(() => { if (StashManager.Instance != null) StashManager.Instance.SetCurrentTab(t); }) { text = (i + 1).ToString() };
            tab.AddToClassList("stash-tab");
            if (i == current) tab.AddToClassList("active");
            wrap.Add(tab);
            if (tabCount > 1 && i == current)
            {
                var del = new Button(() =>
                {
                    if (StashManager.Instance != null && StashManager.Instance.TryRemoveTab(t))
                    {
                        StashManager.Instance.SetCurrentTab(0);
                        RefreshStash();
                    }
                }) { text = "×", tooltip = "Закрыть вкладку (только пустую). Переход на первую." };
                del.AddToClassList("stash-tab-delete");
                wrap.Add(del);
            }
            content.Add(wrap);
        }
        var addTab = new Button(() => { if (StashManager.Instance != null) StashManager.Instance.AddTab(); }) { text = "+", tooltip = "Новая вкладка" };
        addTab.AddToClassList("stash-tab");
        addTab.AddToClassList("stash-tab-add");
        content.Add(addTab);
        scroll.Add(content);
        _stashTabsRow.Add(scroll);
        HideStashVerticalScrollerDelayed(scroll);
        HideStashHorizontalScrollerArrows(scroll);

        float offsetToRestore = savedScrollOffset;
        void RestoreScroll()
        {
            if (scroll == null || scroll.horizontalScroller == null) return;
            float high = scroll.horizontalScroller.highValue;
            scroll.horizontalScroller.value = Mathf.Clamp(offsetToRestore, 0, high);
        }
        scroll.schedule.Execute(RestoreScroll).ExecuteLater(1);
        scroll.schedule.Execute(RestoreScroll).ExecuteLater(5);
    }

    private void HideStashHorizontalScrollerArrows(ScrollView scroll)
    {
        if (scroll?.horizontalScroller == null) return;
        var h = scroll.horizontalScroller;
        if (h.lowButton != null) h.lowButton.style.display = DisplayStyle.None;
        if (h.highButton != null) h.highButton.style.display = DisplayStyle.None;
    }

    private void HideStashVerticalScrollerDelayed(ScrollView scroll)
    {
        void TryHide()
        {
            if (scroll == null || scroll.verticalScroller == null) return;
            var v = scroll.verticalScroller;
            v.style.display = DisplayStyle.None;
            v.style.width = 0;
            v.style.minWidth = 0;
            v.style.maxWidth = 0;
        }
        scroll.schedule.Execute(TryHide).ExecuteLater(2);
        scroll.schedule.Execute(TryHide).ExecuteLater(10);
    }

    private void RefreshStashGrid()
    {
        DrawStashIcons();
    }

    private void DrawStashIcons()
    {
        if (_stashItemsLayer == null || StashManager.Instance == null) return;
        _stashItemsLayer.Clear();
        int tab = StashManager.Instance.CurrentTabIndex;
        for (int i = 0; i < StashManager.STASH_SLOTS_PER_TAB; i++)
        {
            var item = StashManager.Instance.GetItem(tab, i);
            if (item != null && item.Data != null)
            {
                bool isAnchor = (i % StashManager.STASH_COLS == 0 || StashManager.Instance.GetItem(tab, i - 1) != item) &&
                               (i < StashManager.STASH_COLS || StashManager.Instance.GetItem(tab, i - StashManager.STASH_COLS) != item);
                if (!isAnchor) continue;
                StashManager.GetStashItemSize(item, out int sw, out int sh);
                var icon = CreateItemIcon(item, sw, sh, StashSlotSize, receivePointerEvents: true);
                icon.style.left = (i % StashManager.STASH_COLS) * StashSlotSize;
                icon.style.top = (i / StashManager.STASH_COLS) * StashSlotSize;
                icon.userData = i;
                icon.RegisterCallback<PointerOverEvent>(OnPointerOverStashIcon);
                icon.RegisterCallback<PointerOutEvent>(OnPointerOutStashIcon);
                icon.RegisterCallback<PointerDownEvent>(OnStashIconPointerDown);
                _stashItemsLayer.Add(icon);
            }
        }
        _stashItemsLayer.BringToFront();
    }
    
    private void GenerateBackpackGrid()
    {
        if (_inventoryContainer == null) return;

        if (TryBindBackpackGridFromUxml())
            return;

        _inventoryContainer.Clear();
        _backpackSlots.Clear();

        _inventoryContainer.style.width = COLUMNS * InventorySlotSize;
        _inventoryContainer.style.height = ROWS * InventorySlotSize;
        var inventoryWrap = _inventoryContainer.parent;
        if (inventoryWrap != null)
        {
            inventoryWrap.style.minWidth = COLUMNS * InventorySlotSize;
            inventoryWrap.style.width = COLUMNS * InventorySlotSize;
        }

        _itemsLayer = new VisualElement { name = "ItemsLayer" };
        _itemsLayer.style.position = Position.Absolute;
        _itemsLayer.StretchToParentSize();
        _itemsLayer.style.overflow = Overflow.Visible;

        int slotIndex = 0;
        for (int r = 0; r < ROWS; r++)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("inventory-row");
            row.style.width = COLUMNS * InventorySlotSize;
            row.style.height = InventorySlotSize;
            _inventoryContainer.Add(row);

            for (int c = 0; c < COLUMNS; c++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("slot");
                slot.style.width = InventorySlotSize;
                slot.style.height = InventorySlotSize;
                slot.userData = slotIndex;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                row.Add(slot);
                _backpackSlots.Add(slot);
                slotIndex++;
            }
        }
        _inventoryContainer.Add(_itemsLayer);
    }

    private void RefreshInventory()
    {
        if (_itemsLayer == null || InventoryManager.Instance == null) return;
        _itemsLayer.style.display = DisplayStyle.None;
        _itemsLayer.Clear();
        foreach (var slot in _equipmentSlots) 
        {
            var oldImg = slot.Q<Image>();
            if (oldImg != null) slot.Remove(oldImg);
        }
        var inv = InventoryManager.Instance;
        int slotCount = inv.BackpackSlotCount;
        for (int i = 0; i < slotCount; i++)
        {
            InventoryItem item = inv.GetItemAt(i, out int anchorIndex);
            if (item == null || item.Data == null) continue;
            if (i != anchorIndex) continue;
            inv.GetBackpackItemSize(item, out int w, out int h);
            var icon = CreateItemIcon(item, w, h, InventorySlotSize, receivePointerEvents: true);
            icon.style.left = (i % COLUMNS) * InventorySlotSize;
            icon.style.top = (i / COLUMNS) * InventorySlotSize;
            icon.userData = i;
            icon.RegisterCallback<PointerOverEvent>(OnPointerOverBackpackIcon);
            icon.RegisterCallback<PointerOutEvent>(OnPointerOutBackpackIcon);
            icon.RegisterCallback<PointerDownEvent>(OnBackpackIconPointerDown);
            _itemsLayer.Add(icon);
            icon.MarkDirtyRepaint();
        }
        if (_currentTab == 0)
            DrawEquipmentIcons();
        else
            DrawCraftSlotIcon();
        RefreshOrbSlots();
        _itemsLayer.style.display = DisplayStyle.Flex;
        _itemsLayer.BringToFront();
        _root.MarkDirtyRepaint();
    }

    private void DrawEquipmentIcons()
    {
        var equipItems = InventoryManager.Instance.EquipmentItems;
        
        for (int i = 0; i < equipItems.Length; i++)
        {
            var item = equipItems[i];
            
            int targetID = InventoryManager.EQUIP_OFFSET + i;
            VisualElement slot = _equipmentSlots.Find(s => (int)s.userData == targetID);

            if (slot != null && item != null && item.Data != null)
            {
                var icon = CreateItemIcon(item, null, null, EquipmentIconCellSize);
                float iconW = item.Data.Width * EquipmentIconCellSize;
                float iconH = item.Data.Height * EquipmentIconCellSize;
                float slotW = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].w : 48f;
                float slotH = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].h : 48f;
                icon.style.left = (slotW - iconW) * 0.5f;
                icon.style.top = (slotH - iconH) * 0.5f;
                icon.style.right = StyleKeyword.Null;
                icon.style.bottom = StyleKeyword.Null;
                slot.Add(icon);
            }
        }
    }

    private void DrawCraftSlotIcon()
    {
        if (_craftSlot == null) return;
        var oldImg = _craftSlot.Q<Image>();
        if (oldImg != null) _craftSlot.Remove(oldImg);
        var item = InventoryManager.Instance.CraftingSlotItem;
        if (item != null && item.Data != null)
        {
            var icon = CreateItemIcon(item, null, null, EquipmentIconCellSize);
            float iconW = item.Data.Width * EquipmentIconCellSize;
            float iconH = item.Data.Height * EquipmentIconCellSize;
            icon.style.left = (CraftSlotWidth - iconW) * 0.5f;
            icon.style.top = (CraftSlotHeight - iconH) * 0.5f;
            icon.style.right = StyleKeyword.Null;
            icon.style.bottom = StyleKeyword.Null;
            _craftSlot.Add(icon);
        }
    }

    private void RefreshOrbSlots()
    {
        if (InventoryManager.Instance == null) return;
        for (int i = 0; i < _orbSlots.Count; i++)
        {
            var (slot, countLabel) = _orbSlots[i];
            var orb = _orbSlotsConfig != null ? _orbSlotsConfig.GetOrbInSlot(i) : null;
            int count = orb != null ? InventoryManager.Instance.GetOrbCount(orb.ID) : 0;
            slot.style.backgroundImage = (orb != null && orb.Icon != null) ? new StyleBackground(orb.Icon) : default;
            countLabel.text = count.ToString();
            countLabel.style.visibility = Visibility.Visible;
        }
    }

    /// <param name="slotSizePx">Размер одной клетки в px (инвентарь, склад или экипировка).</param>
    /// <param name="receivePointerEvents">True для рюкзака/склада — иконка принимает hover/click, тултип только над иконкой.</param>
    private VisualElement CreateItemIcon(InventoryItem item, int? widthSlots, int? heightSlots, float slotSizePx, bool receivePointerEvents = false)
    {
        Image icon = new Image();
        icon.sprite = item.Data.Icon;
        int w = widthSlots ?? item.Data.Width;
        int h = heightSlots ?? item.Data.Height;
        w = Mathf.Clamp(w, 1, 10);
        h = Mathf.Clamp(h, 1, 10);
        icon.style.width = w * slotSizePx;
        icon.style.height = h * slotSizePx;
        icon.style.position = Position.Absolute;
        icon.pickingMode = receivePointerEvents ? PickingMode.Position : PickingMode.Ignore;
        if (item.Data.Icon != null) icon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        return icon;
    }

    private void CreateGhostIcon()
    {
        _ghostIcon = new VisualElement { name = "GhostIcon" };
        _ghostIcon.style.position = Position.Absolute;
        _ghostIcon.style.display = DisplayStyle.None;
        _ghostIcon.pickingMode = PickingMode.Ignore;
        _ghostIcon.style.opacity = 0.7f;
        _root.Add(_ghostIcon);

        _ghostHighlight = new VisualElement { name = "GhostHighlight" };
        _ghostHighlight.style.position = Position.Absolute;
        _ghostHighlight.style.display = DisplayStyle.None;
        _ghostHighlight.pickingMode = PickingMode.Ignore;
        _ghostHighlight.style.opacity = 0.4f;
        _ghostHighlight.style.borderTopWidth = _ghostHighlight.style.borderBottomWidth = 1f;
        _ghostHighlight.style.borderLeftWidth = _ghostHighlight.style.borderRightWidth = 1f;
        _root.Add(_ghostHighlight);
    }

    /// <summary>Текущая позиция мыши в локальных координатах _root. Screen -> Panel (RuntimePanelUtils) -> _root. Не зависит от target события.</summary>
    private Vector2 GetPointerRootLocalFromScreen()
    {
        if (_root == null || _root.panel == null) return Vector2.zero;
        Vector2 screen = Input.mousePosition;
        screen.y = Screen.height - screen.y;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, screen);
        return _root.WorldToLocal(panelPos);
    }

    /// <summary>Состояние подсветки под курсором: 0 = можно, 1 = своп, 2 = нельзя, -1 = не над сеткой.</summary>
    private void UpdateGhostHighlight(Vector2 rootLocalPos)
    {
        if (!_isDragging || _draggedItem?.Data == null)
        {
            _ghostHighlight.style.display = DisplayStyle.None;
            return;
        }
        int itemW = Mathf.Max(1, _draggedItem.Data.Width);
        int itemH = Mathf.Max(1, _draggedItem.Data.Height);
        Vector2 dropCenter = _root.LocalToWorld(rootLocalPos);

        // Склад: та же логика — зелёный только при пустой области и возможности размещения
        if (IsStashVisible && _stashPanel != null && _stashGridContainer != null && _stashPanel.worldBound.Contains(dropCenter))
        {
            int stashRoot = GetSmartStashTargetIndex(dropCenter, itemW, itemH);
            if (stashRoot >= 0 && StashManager.Instance != null)
            {
                var stash = StashManager.Instance;
                int tab = stash.CurrentTabIndex;
                var unique = stash.GetUniqueItemsInStashArea(tab, _draggedItem, stashRoot);
                int state;
                if (unique.Count > 1)
                    state = 2;
                else if (unique.Count == 1)
                {
                    InventoryItem single = null;
                    foreach (var u in unique) { single = u; break; }
                    state = (single == _draggedItem) ? 0 : 1;
                }
                else
                    state = stash.CanPlaceItemAt(tab, stashRoot, _draggedItem) ? 0 : 2;
                ShowHighlightAtStashRoot(stashRoot, itemW, itemH, state);
                return;
            }
        }

        // Рюкзак: 0=можно, 1=своп, 2=нельзя. Зелёный только если область пуста И CanPlace успешен (чтобы пустые ноды не светились жёлтым/красным из-за рассинхрона).
        if (_inventoryContainer != null && _itemsLayer != null && _inventoryContainer.worldBound.Contains(dropCenter))
        {
            int gridIndex = GetSmartTargetIndex(dropCenter, itemW, itemH);
            if (gridIndex >= 0 && InventoryManager.Instance != null)
            {
                var inv = InventoryManager.Instance;
                var unique = inv.GetUniqueItemsInBackpackArea(_draggedItem, gridIndex);
                int state;
                if (unique.Count > 1)
                    state = 2;
                else if (unique.Count == 1)
                {
                    InventoryItem single = null;
                    foreach (var u in unique) { single = u; break; }
                    state = (single == _draggedItem) ? 0 : 1;
                }
                else
                    state = inv.CanPlaceItemAt(_draggedItem, gridIndex) ? 0 : 2;
                ShowHighlightAtBackpackRoot(gridIndex, itemW, itemH, state);
                return;
            }
        }

        _ghostHighlight.style.display = DisplayStyle.None;
    }

    private void ShowHighlightAtBackpackRoot(int rootIndex, int itemW, int itemH, int state)
    {
        if (_itemsLayer == null) return;
        int row = rootIndex / COLUMNS;
        int col = rootIndex % COLUMNS;
        float x = col * InventorySlotSize;
        float y = row * InventorySlotSize;
        Rect localRect = new Rect(x, y, itemW * InventorySlotSize, itemH * InventorySlotSize);
        Vector2 minWorld = _itemsLayer.LocalToWorld(localRect.min);
        Vector2 maxWorld = _itemsLayer.LocalToWorld(localRect.max);
        Vector2 minRoot = _root.WorldToLocal(minWorld);
        Vector2 maxRoot = _root.WorldToLocal(maxWorld);
        _ghostHighlight.style.left = minRoot.x;
        _ghostHighlight.style.top = minRoot.y;
        _ghostHighlight.style.width = maxRoot.x - minRoot.x;
        _ghostHighlight.style.height = maxRoot.y - minRoot.y;
        SetHighlightColor(state);
        _ghostHighlight.style.display = DisplayStyle.Flex;
    }

    private void ShowHighlightAtStashRoot(int rootIndex, int itemW, int itemH, int state)
    {
        if (_stashItemsLayer == null) return;
        int row = rootIndex / StashManager.STASH_COLS;
        int col = rootIndex % StashManager.STASH_COLS;
        float x = col * StashSlotSize;
        float y = row * StashSlotSize;
        Rect localRect = new Rect(x, y, itemW * StashSlotSize, itemH * StashSlotSize);
        Vector2 minWorld = _stashItemsLayer.LocalToWorld(localRect.min);
        Vector2 maxWorld = _stashItemsLayer.LocalToWorld(localRect.max);
        Vector2 minRoot = _root.WorldToLocal(minWorld);
        Vector2 maxRoot = _root.WorldToLocal(maxWorld);
        _ghostHighlight.style.left = minRoot.x;
        _ghostHighlight.style.top = minRoot.y;
        _ghostHighlight.style.width = maxRoot.x - minRoot.x;
        _ghostHighlight.style.height = maxRoot.y - minRoot.y;
        SetHighlightColor(state);
        _ghostHighlight.style.display = DisplayStyle.Flex;
    }

    private void SetHighlightColor(int state)
    {
        Color c = state == 0 ? new Color(0.2f, 0.8f, 0.2f) : (state == 1 ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f));
        _ghostHighlight.style.backgroundColor = c;
        _ghostHighlight.style.borderTopColor = _ghostHighlight.style.borderBottomColor = _ghostHighlight.style.borderLeftColor = _ghostHighlight.style.borderRightColor = c;
    }

    private void SetupEquipmentSlots()
    {
        _equipmentSlots.Clear();
        for (int i = 0; i < EquipmentSlotUxmlNames.Count; i++)
        {
            string name = EquipmentSlotUxmlNames.GetName((EquipmentSlot)i);
            if (string.IsNullOrEmpty(name)) continue;
            var slot = _root.Q<VisualElement>(name);
            if (slot != null)
            {
                slot.userData = InventoryManager.EQUIP_OFFSET + i;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                _equipmentSlots.Add(slot);
            }
            else
            {
                Debug.LogError($"[InventoryUI] Слот '{name}' не найден в UXML!");
            }
        }
    }

    private void LoadOrbSlotsConfig()
    {
        if (_orbSlotsConfig == null)
            _orbSlotsConfig = Resources.Load<CraftingOrbSlotsConfigSO>(ProjectPaths.ResourcesCraftingOrbSlotsConfig);
    }

    private Button _toggleModeButton;

    private void SetupTabs()
    {
        _equipmentView = _root.Q<VisualElement>("EquipmentView");
        _craftView = _root.Q<VisualElement>("CraftView");
        _toggleModeButton = _root.Q<Button>("ToggleModeButton");
        if (_equipmentView != null) _equipmentView.AddToClassList("hidden");
        if (_craftView != null) _craftView.RemoveFromClassList("visible");
        _currentTab = 0;
        if (_equipmentView != null) _equipmentView.RemoveFromClassList("hidden");

        if (_toggleModeButton != null)
        {
            _toggleModeButton.clicked += OnToggleModeClicked;
            UpdateToggleButtonLabel();
        }
    }

    private void OnToggleModeClicked()
    {
        SwitchTab(_currentTab == 0 ? 1 : 0);
    }

    private void UpdateToggleButtonLabel()
    {
        if (_toggleModeButton == null) return;
        _toggleModeButton.text = _currentTab == 0 ? "K" : "E";
        _toggleModeButton.tooltip = _currentTab == 0 ? "Крафт" : "Экипировка";
    }

    private void SwitchTab(int tab)
    {
        if (_currentTab == tab) return;
        _currentTab = tab;
        if (_equipmentView != null)
        {
            if (tab == 0) _equipmentView.RemoveFromClassList("hidden");
            else _equipmentView.AddToClassList("hidden");
        }
        if (_craftView != null)
        {
            if (tab == 1) _craftView.AddToClassList("visible");
            else _craftView.RemoveFromClassList("visible");
        }
        UpdateToggleButtonLabel();
        RefreshInventory();
    }

    private void SetupCraftView()
    {
        _craftSlot = _root.Q<VisualElement>("CraftSlot");
        _orbSlotsRow = _root.Q<VisualElement>("OrbSlotsRow");
        if (_craftSlot != null)
        {
            _craftSlot.userData = InventoryManager.CRAFT_SLOT_INDEX;
            _craftSlot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
            _craftSlot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
            _craftSlot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
        }
        if (_orbSlotsRow != null)
        {
            _orbSlotsRow.Clear();
            _orbSlots.Clear();
            int count = _orbSlotsConfig != null ? _orbSlotsConfig.SlotCount : 0;
            if (count == 0)
            {
                var hint = new Label(_orbSlotsConfig == null
                    ? "Сферы: Tools → Crafting Orb Editor → Create config. Либо перетащите конфиг в поле Orb Slots Config у Inventory UI."
                    : "Сферы: в Crafting Orb Editor назначьте орбы в слоты и Save config.");
                hint.style.fontSize = 9;
                hint.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _orbSlotsRow.Add(hint);
            }
            for (int i = 0; i < count; i++)
            {
                var orb = _orbSlotsConfig.GetOrbInSlot(i);
                var slot = new VisualElement();
                slot.AddToClassList("orb-slot");
                slot.userData = i;
                var countLabel = new Label { name = "OrbCount" };
                countLabel.AddToClassList("orb-count");
                slot.Add(countLabel);
                slot.RegisterCallback<PointerDownEvent>(OnOrbSlotPointerDown);
                slot.RegisterCallback<MouseDownEvent>(OnOrbSlotMouseDown);
                slot.RegisterCallback<PointerOverEvent>(OnOrbSlotPointerOver);
                slot.RegisterCallback<PointerOutEvent>(OnOrbSlotPointerOut);
                _orbSlotsRow.Add(slot);
                _orbSlots.Add((slot, countLabel));
            }
        }
    }

    private void OnOrbSlotPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 1) return;
        TryEnterApplyOrbModeFromSlot(evt.currentTarget as VisualElement);
    }

    private void OnOrbSlotMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 1) return;
        if (TryEnterApplyOrbModeFromSlot(evt.currentTarget as VisualElement))
        {
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private bool TryEnterApplyOrbModeFromSlot(VisualElement slot)
    {
        if (_orbSlotsConfig == null || InventoryManager.Instance == null || slot?.userData == null) return false;
        int idx = (int)slot.userData;
        var orb = _orbSlotsConfig.GetOrbInSlot(idx);
        if (orb == null || string.IsNullOrEmpty(orb.ID)) return false;
        if (InventoryManager.Instance.GetOrbCount(orb.ID) <= 0) return false;
        EnterApplyOrbMode(orb, slot);
        return true;
    }

    private void OnOrbSlotPointerOver(PointerOverEvent evt)
    {
        if (_orbSlotsConfig == null) return;
        var slot = evt.currentTarget as VisualElement;
        if (slot?.userData == null) return;
        int idx = (int)slot.userData;
        var orb = _orbSlotsConfig.GetOrbInSlot(idx);
        if (orb != null && ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.ShowOrbTooltip(orb, slot);
    }

    private void OnOrbSlotPointerOut(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.HideTooltip();
    }

    private void EnterApplyOrbMode(CraftingOrbSO orb, VisualElement orbSlotElement)
    {
        _applyOrbMode = true;
        _applyOrbOrb = orb;
        _applyOrbSlotHighlight = orbSlotElement;
        orbSlotElement.AddToClassList("orb-slot-applying");
        _ghostIcon.style.backgroundImage = orb.Icon != null ? new StyleBackground(orb.Icon) : default;
        _ghostIcon.style.width = 32;
        _ghostIcon.style.height = 32;
        _ghostIcon.style.display = DisplayStyle.Flex;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
    }

    private void ExitApplyOrbMode()
    {
        if (_capturedPointerId >= 0 && _root != null)
        {
            _root.ReleasePointer(_capturedPointerId);
            _capturedPointerId = -1;
        }
        _applyOrbMode = false;
        if (_applyOrbSlotHighlight != null)
        {
            _applyOrbSlotHighlight.RemoveFromClassList("orb-slot-applying");
            _applyOrbSlotHighlight = null;
        }
        _applyOrbOrb = null;
        _ghostIcon.style.display = DisplayStyle.None;
    }

    private bool TryApplyOrbOnPointerUp(Vector2 pointerPosition)
    {
        if (_applyOrbOrb == null || _craftSlot == null || InventoryManager.Instance == null) return false;
        if (!_craftSlot.worldBound.Contains(pointerPosition)) return false;

        var craftItem = InventoryManager.Instance.CraftingSlotItem;
        if (craftItem == null || !ItemGenerator.IsRare(craftItem)) return false;
        if (_applyOrbOrb.EffectId != CraftingOrbEffectId.RerollRare) return false;
        if (ItemGenerator.Instance == null) return false;

        if (!InventoryManager.Instance.ConsumeOrb(_applyOrbOrb.ID)) return false;
        ItemGenerator.Instance.RerollRare(craftItem);
        ExitApplyOrbMode();
        InventoryManager.Instance.TriggerUIUpdate();
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.RefreshCurrentItemTooltip();
        return true;
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape && _applyOrbMode)
        {
            ExitApplyOrbMode();
            evt.StopPropagation();
        }
    }

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging) return;
        VisualElement hoveredSlot = evt.currentTarget as VisualElement;
        if (hoveredSlot == null || hoveredSlot.userData == null) return;

        int raw = (int)hoveredSlot.userData;
        // Рюкзак и склад: тултип показывается только при наведении на иконку (OnPointerOverBackpackIcon / OnPointerOverStashIcon). По слотам не показываем.
        if (raw >= STASH_SLOT_OFFSET)
        {
            if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
            return;
        }
        if (raw >= 0 && raw < _backpackSlots.Count)
        {
            if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
            return;
        }

        // Только экипировка и крафт-слот: тултип по слоту
        InventoryItem item = null;
        int anchorIndex = -1;
        if (InventoryManager.Instance == null) return;
        item = InventoryManager.Instance.GetItemAt(raw, out anchorIndex);
        if (ItemTooltipController.Instance == null) return;
        if (item != null && item.Data != null)
        {
            VisualElement anchorSlot = GetSlotVisual(anchorIndex);
            if (anchorSlot != null)
                ItemTooltipController.Instance.ShowTooltip(item, anchorSlot);
            else
                ItemTooltipController.Instance.HideTooltip();
        }
        else
            ItemTooltipController.Instance.HideTooltip();
    }

    private void OnPointerOverBackpackIcon(PointerOverEvent evt)
    {
        if (_isDragging || ItemTooltipController.Instance == null) return;
        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null || InventoryManager.Instance == null) return;
        int anchorIndex = (int)icon.userData;
        InventoryItem item = InventoryManager.Instance.GetItemAt(anchorIndex, out int _);
        if (item != null && item.Data != null)
            ItemTooltipController.Instance.ShowTooltip(item, icon);
    }

    private void OnPointerOutBackpackIcon(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
    }

    private void OnBackpackIconPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode || evt.button != 0 || InventoryManager.Instance == null) return;
        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null) return;
        int anchorIdx = (int)icon.userData;

        if (evt.ctrlKey && IsStashVisible && StashManager.Instance != null)
        {
            evt.StopPropagation();
            InventoryItem taken = InventoryManager.Instance.TakeItemFromSlot(anchorIdx);
            if (taken == null) return;
            int currentTab = StashManager.Instance.CurrentTabIndex;
            if (StashManager.Instance.TryAddItemPreferringTab(taken, currentTab))
            {
                RefreshInventory();
                RefreshStash();
                if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
                return;
            }
            InventoryManager.Instance.AddItem(taken);
            return;
        }

        evt.StopPropagation();
        InventoryItem takenDrag = InventoryManager.Instance.TakeItemFromSlot(anchorIdx);
        if (takenDrag == null) return;
        var iconEl = icon as VisualElement;
        Vector2 originRoot = iconEl != null ? _root.WorldToLocal(iconEl.worldBound.min) : GetPointerRootLocalFromScreen();
        _grabOffsetRootLocal = GetPointerRootLocalFromScreen() - originRoot;
        _isDragging = true;
        _draggedItem = takenDrag;
        _draggedSourceAnchor = anchorIdx;
        _draggedFromStash = false;
        _draggedStashTab = -1;
        _draggedStashAnchorSlot = -1;
        RefreshInventory();
        _ghostIcon.style.backgroundImage = new StyleBackground(takenDrag.Data.Icon);
        _ghostIcon.style.width = takenDrag.Data.Width * InventorySlotSize;
        _ghostIcon.style.height = takenDrag.Data.Height * InventorySlotSize;
        _ghostIcon.style.display = DisplayStyle.None;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
        _root.CapturePointer(evt.pointerId);
    }

    private void OnPointerOverStashIcon(PointerOverEvent evt)
    {
        if (_isDragging || ItemTooltipController.Instance == null || StashManager.Instance == null) return;
        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null) return;
        int anchorIndex = (int)icon.userData;
        int tab = StashManager.Instance.CurrentTabIndex;
        InventoryItem item = StashManager.Instance.GetItem(tab, anchorIndex);
        if (item != null && item.Data != null)
            ItemTooltipController.Instance.ShowTooltip(item, icon);
    }

    private void OnPointerOutStashIcon(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
    }

    private void OnStashIconPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode || evt.button != 0 || StashManager.Instance == null) return;
        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null) return;
        int anchorSlot = (int)icon.userData;
        int tab = StashManager.Instance.CurrentTabIndex;

        if (evt.ctrlKey && InventoryManager.Instance != null)
        {
            evt.StopPropagation();
            InventoryItem taken = StashManager.Instance.TakeItemFromStash(tab, anchorSlot);
            if (taken == null) return;
            if (InventoryManager.Instance.AddItem(taken))
            {
                RefreshInventory();
                RefreshStash();
                if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
                return;
            }
            StashManager.Instance.TryAddItemPreferringTab(taken, StashManager.Instance.CurrentTabIndex);
            return;
        }

        evt.StopPropagation();
        InventoryItem takenDrag = StashManager.Instance.TakeItemFromStash(tab, anchorSlot);
        if (takenDrag == null) return;
        var iconEl = icon as VisualElement;
        Vector2 originRoot = iconEl != null ? _root.WorldToLocal(iconEl.worldBound.min) : GetPointerRootLocalFromScreen();
        _grabOffsetRootLocal = GetPointerRootLocalFromScreen() - originRoot;
        _isDragging = true;
        _draggedItem = takenDrag;
        _draggedFromStash = true;
        _draggedStashTab = tab;
        _draggedStashAnchorSlot = anchorSlot;
        RefreshStash();
        _ghostIcon.style.backgroundImage = new StyleBackground(takenDrag.Data.Icon);
        _ghostIcon.style.width = takenDrag.Data.Width * StashSlotSize;
        _ghostIcon.style.height = takenDrag.Data.Height * StashSlotSize;
        _ghostIcon.style.display = DisplayStyle.None;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
        _root.CapturePointer(evt.pointerId);
    }

    private VisualElement GetSlotVisual(int index)
    {
        if (index == InventoryManager.CRAFT_SLOT_INDEX)
            return _craftSlot;
        if (index >= InventoryManager.EQUIP_OFFSET)
            return _equipmentSlots.Find(s => (int)s.userData == index);
        if (index >= 0 && index < _backpackSlots.Count)
            return _backpackSlots[index];
        return null;
    }

    /// <summary>Не скрываем тултип при уходе с слота — иначе мерцание (особенно на экипировке). Скрытие только при наведении на пустой слот (OnPointerOverSlot) или уходе с окна (OnInventoryWindowPointerOut).</summary>
    private void OnPointerOutSlot(PointerOutEvent evt) { }

    private void OnInventoryWindowPointerOut(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance == null) return;
        if (evt.target != _root) return;
        ItemTooltipController.Instance.HideTooltip();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isDragging)
        {
            Vector2 rootLocal = GetPointerRootLocalFromScreen();
            UpdateGhostPosition(rootLocal);
            UpdateGhostHighlight(rootLocal);
            _ghostIcon.style.display = DisplayStyle.Flex;
        }
        else
        {
            _ghostHighlight.style.display = DisplayStyle.None;
            if (_applyOrbMode)
                UpdateGhostPosition(GetPointerRootLocal(_root, evt));
        }
    }
    
    private void OnStashSlotPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode) return;
        if (evt.button != 0) return;
        if (StashManager.Instance == null) return;

        VisualElement slot = evt.currentTarget as VisualElement;
        if (slot.userData == null) return;
        int raw = (int)slot.userData;
        if (raw < STASH_SLOT_OFFSET) return;
        int slotIndex = raw - STASH_SLOT_OFFSET;
        int tab = StashManager.Instance.CurrentTabIndex;
        InventoryItem item = StashManager.Instance.GetItemAt(tab, slotIndex, out int anchorSlot);
        if (item == null) return;
        InventoryItem taken = StashManager.Instance.TakeItemFromStash(tab, anchorSlot);
        if (taken == null) return;

        if (_stashItemsLayer != null)
        {
            Vector2 iconTopLeftRoot = _stashItemsLayer.ChangeCoordinatesTo(_root, new Vector2((anchorSlot % StashManager.STASH_COLS) * StashSlotSize, (anchorSlot / StashManager.STASH_COLS) * StashSlotSize));
            _grabOffsetRootLocal = GetPointerRootLocalFromScreen() - iconTopLeftRoot;
        }
        else
            _grabOffsetRootLocal = Vector2.zero;
        _isDragging = true;
        _draggedItem = taken;
        _draggedFromStash = true;
        _draggedStashTab = tab;
        _draggedStashAnchorSlot = anchorSlot;
        RefreshStash();
        _ghostIcon.style.backgroundImage = new StyleBackground(taken.Data.Icon);
        _ghostIcon.style.width = taken.Data.Width * StashSlotSize;
        _ghostIcon.style.height = taken.Data.Height * StashSlotSize;
        _ghostIcon.style.display = DisplayStyle.None;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
        _root.CapturePointer(evt.pointerId);
    }

    private void OnSlotPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode) return;
        if (evt.button != 0) return;
        if (InventoryManager.Instance == null) return;

        VisualElement slot = evt.currentTarget as VisualElement;
        if (slot.userData != null && (int)slot.userData >= STASH_SLOT_OFFSET) return;
        int idx = (int)slot.userData;
        InventoryManager.Instance.GetItemAt(idx, out int anchorIdx);
        InventoryItem taken = InventoryManager.Instance.TakeItemFromSlot(anchorIdx);
        if (taken == null) return;

        Vector2 originRoot;
        if (idx >= InventoryManager.EQUIP_OFFSET && slot != null)
            originRoot = _root.WorldToLocal(slot.worldBound.min);
        else if (_itemsLayer != null)
            originRoot = _root.WorldToLocal(_itemsLayer.LocalToWorld(new Vector2((anchorIdx % COLUMNS) * InventorySlotSize, (anchorIdx / COLUMNS) * InventorySlotSize)));
        else
            originRoot = GetPointerRootLocalFromScreen();
        _grabOffsetRootLocal = GetPointerRootLocalFromScreen() - originRoot;
        _isDragging = true;
        _draggedItem = taken;
        _draggedSourceAnchor = anchorIdx;
        _draggedFromStash = false;
        _draggedStashTab = -1;
        _draggedStashAnchorSlot = -1;
        RefreshInventory();
        _ghostIcon.style.backgroundImage = new StyleBackground(taken.Data.Icon);
        _ghostIcon.style.width = taken.Data.Width * InventorySlotSize;
        _ghostIcon.style.height = taken.Data.Height * InventorySlotSize;
        _ghostIcon.style.display = DisplayStyle.None;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
        _root.CapturePointer(evt.pointerId);
    }

    /// <summary>Позиция в пространстве панели (для хита дропа).</summary>
    private static Vector2 GetDropCenterInPanel(VisualElement root, EventBase evt)
    {
        var ve = evt.target as VisualElement;
        if (ve == null || root == null) return Vector2.zero;
        Vector2 pos = ((IPointerEvent)evt).position;
        Vector2 localInRoot = ve.ChangeCoordinatesTo(root, pos);
        Rect r = root.worldBound;
        return new Vector2(r.xMin + localInRoot.x, r.yMin + localInRoot.y);
    }

    /// <summary>pos — координаты в локальном пространстве _root. При перетаскивании предмета размер берём только из _draggedItem (layout ещё не обновился на первом кадре — из-за этого призрак появлялся справа-снизу).</summary>
    private void UpdateGhostPosition(Vector2 rootLocalPos)
    {
        float w, h;
        if (_isDragging && _draggedItem?.Data != null)
        {
            float cell = _draggedFromStash ? StashSlotSize : InventorySlotSize;
            w = _draggedItem.Data.Width * cell;
            h = _draggedItem.Data.Height * cell;
            _ghostIcon.style.left = rootLocalPos.x - _grabOffsetRootLocal.x;
            _ghostIcon.style.top = rootLocalPos.y - _grabOffsetRootLocal.y;
        }
        else
        {
            w = _ghostIcon.resolvedStyle.width;
            h = _ghostIcon.resolvedStyle.height;
            _ghostIcon.style.left = rootLocalPos.x - (w * 0.5f);
            _ghostIcon.style.top = rootLocalPos.y - (h * 0.5f);
        }
    }

    /// <summary>Позиция указателя в локальном пространстве root. ChangeCoordinatesTo даёт согласованные координаты с призраком (оба в root).</summary>
    private static Vector2 GetPointerRootLocal(VisualElement root, EventBase evt)
    {
        var ve = evt.target as VisualElement;
        if (ve == null || root == null) return Vector2.zero;
        Vector2 pos = ((IPointerEvent)evt).position;
        return ve.ChangeCoordinatesTo(root, pos);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_applyOrbMode)
        {
            if (evt.button == 0)
            {
                bool applied = TryApplyOrbOnPointerUp(evt.position);
                if (!applied) ExitApplyOrbMode();
            }
            return;
        }

        if (!_isDragging || _draggedItem == null)
        {
            _isDragging = false;
            _ghostIcon.style.display = DisplayStyle.None;
            return;
        }
        if (_dropInProgress)
            return;
        _dropInProgress = true;

        // Сразу забираем ссылку и сбрасываем состояние, чтобы повторный PointerUp не привёл ко второму Place
        InventoryItem itemToPlace = _draggedItem;
        bool fromStash = _draggedFromStash;
        int stashTab = _draggedStashTab;
        int stashAnchor = _draggedStashAnchorSlot;
        int invSourceAnchor = _draggedSourceAnchor;
        _draggedItem = null;
        _isDragging = false;
        _draggedSourceAnchor = -1;
        _draggedFromStash = false;
        _draggedStashTab = -1;
        _draggedStashAnchorSlot = -1;
        _ghostIcon.style.display = DisplayStyle.None;
        if (_ghostHighlight != null) _ghostHighlight.style.display = DisplayStyle.None;
        _root.ReleasePointer(evt.pointerId);

        int itemW = itemToPlace.Data != null ? itemToPlace.Data.Width : 1;
        int itemH = itemToPlace.Data != null ? itemToPlace.Data.Height : 1;
        Vector2 dropCenter = GetDropCenterInPanel(_root, evt);

        // Склад — только если указатель над панелью склада
        int stashFoundSlotIndex = -1;
        int stashFoundTab = StashManager.Instance != null ? StashManager.Instance.CurrentTabIndex : -1;
        if (IsStashVisible && _stashPanel != null && _stashPanel.worldBound.Contains(dropCenter))
            stashFoundSlotIndex = GetSmartStashTargetIndex(dropCenter, itemW, itemH);

        int foundIndex = -1;
        if (stashFoundSlotIndex < 0)
        {
            if (_currentTab == 1 && _craftSlot != null && _craftSlot.worldBound.Contains(dropCenter))
                foundIndex = InventoryManager.CRAFT_SLOT_INDEX;
            else if (_currentTab == 0)
            {
                foreach (var slot in _equipmentSlots)
                {
                    if (slot.worldBound.Contains(dropCenter) && slot.userData != null)
                    {
                        foundIndex = (int)slot.userData;
                        break;
                    }
                }
            }
            if (foundIndex == -1 && _inventoryContainer != null && _inventoryContainer.worldBound.Contains(dropCenter))
            {
                int gridIndex = GetSmartTargetIndex(dropCenter, itemW, itemH);
                if (gridIndex >= 0)
                    foundIndex = gridIndex;
            }
        }

        bool placed = false;
        if (fromStash && StashManager.Instance != null)
        {
            if (stashFoundSlotIndex >= 0)
                placed = StashManager.Instance.PlaceItemInStash(itemToPlace, stashFoundTab, stashFoundSlotIndex, stashTab, stashAnchor, -1);
            else if (foundIndex >= 0)
                placed = StashManager.Instance.TryMoveItemToInventoryAtomic(itemToPlace, stashTab, stashAnchor, foundIndex);
        }
        else
        {
            if (stashFoundSlotIndex >= 0 && StashManager.Instance != null)
                placed = StashManager.Instance.PlaceItemInStash(itemToPlace, stashFoundTab, stashFoundSlotIndex, -1, -1, invSourceAnchor);
            else if (foundIndex >= 0 && InventoryManager.Instance != null)
                placed = InventoryManager.Instance.PlaceItemAt(itemToPlace, foundIndex, invSourceAnchor);
        }

        if (!placed)
        {
            bool returned = false;
            if (fromStash && StashManager.Instance != null)
                returned = StashManager.Instance.PlaceItemInStash(itemToPlace, stashTab, stashAnchor, -1, -1, -1);
            else if (invSourceAnchor >= 0 && InventoryManager.Instance != null)
                returned = InventoryManager.Instance.PlaceItemAt(itemToPlace, invSourceAnchor, -1);
            if (!returned && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.RecoverItemToInventory(itemToPlace);
                Debug.LogWarning("[InventoryUI] Дроп не удался: предмет возвращён в рюкзак/склад (ожидаемый слот был занят или недоступен).");
            }
        }

        try
        {
            RefreshInventory();
            RefreshStash();
            if (placed && _root != null)
                _root.schedule.Execute(() => { RefreshInventory(); RefreshStash(); }).ExecuteLater(2);
        }
        finally
        {
            _dropInProgress = false;
        }
    }

    /// <summary>
    /// Площадь пересечения двух прямоугольников (в одной системе координат).
    /// </summary>
    private static float OverlapArea(float aMinX, float aMinY, float aMaxX, float aMaxY, float bMinX, float bMinY, float bMaxX, float bMaxY)
    {
        float iMinX = Mathf.Max(aMinX, bMinX);
        float iMinY = Mathf.Max(aMinY, bMinY);
        float iMaxX = Mathf.Min(aMaxX, bMaxX);
        float iMaxY = Mathf.Min(aMaxY, bMaxY);
        float w = Mathf.Max(0f, iMaxX - iMinX);
        float h = Mathf.Max(0f, iMaxY - iMinY);
        return w * h;
    }

    /// <summary>
    /// Якорь по максимальному пересечению: центр дропа + размер предмета задают виртуальный прямоугольник,
    /// перебираем все допустимые позиции WxH и выбираем ту, с которой площадь пересечения максимальна.
    /// </summary>
    private int GetSmartStashTargetIndex(Vector2 dropCenterPanel, int itemWidth, int itemHeight)
    {
        if (_stashItemsLayer == null || itemWidth <= 0 || itemHeight <= 0) return -1;
        float w = itemWidth * StashSlotSize;
        float h = itemHeight * StashSlotSize;
        Rect ghostWorld = new Rect(dropCenterPanel.x - w * 0.5f, dropCenterPanel.y - h * 0.5f, w, h);
        Vector2 localMin = _stashItemsLayer.WorldToLocal(new Vector2(ghostWorld.xMin, ghostWorld.yMin));
        Vector2 localMax = _stashItemsLayer.WorldToLocal(new Vector2(ghostWorld.xMax, ghostWorld.yMax));
        float gMinX = Mathf.Min(localMin.x, localMax.x);
        float gMaxX = Mathf.Max(localMin.x, localMax.x);
        float gMinY = Mathf.Min(localMin.y, localMax.y);
        float gMaxY = Mathf.Max(localMin.y, localMax.y);

        int bestCol = 0, bestRow = 0;
        float bestArea = -1f;
        for (int row = 0; row <= StashManager.STASH_ROWS - itemHeight; row++)
        {
            for (int col = 0; col <= StashManager.STASH_COLS - itemWidth; col++)
            {
                float iMinX = col * StashSlotSize;
                float iMinY = row * StashSlotSize;
                float iMaxX = (col + itemWidth) * StashSlotSize;
                float iMaxY = (row + itemHeight) * StashSlotSize;
                float area = OverlapArea(gMinX, gMinY, gMaxX, gMaxY, iMinX, iMinY, iMaxX, iMaxY);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestCol = col;
                    bestRow = row;
                }
            }
        }
        return bestArea > 0 ? bestRow * StashManager.STASH_COLS + bestCol : -1;
    }

    /// <summary>
    /// Якорь в рюкзаке по максимальному пересечению виртуального прямоугольника (центр дропа + размер) с кандидатом.
    /// </summary>
    private int GetSmartTargetIndex(Vector2 dropCenterPanel, int itemWidth, int itemHeight)
    {
        if (_itemsLayer == null || itemWidth <= 0 || itemHeight <= 0) return -1;
        float w = itemWidth * InventorySlotSize;
        float h = itemHeight * InventorySlotSize;
        Rect ghostWorld = new Rect(dropCenterPanel.x - w * 0.5f, dropCenterPanel.y - h * 0.5f, w, h);
        Vector2 localMin = _itemsLayer.WorldToLocal(new Vector2(ghostWorld.xMin, ghostWorld.yMin));
        Vector2 localMax = _itemsLayer.WorldToLocal(new Vector2(ghostWorld.xMax, ghostWorld.yMax));
        float gMinX = Mathf.Min(localMin.x, localMax.x);
        float gMaxX = Mathf.Max(localMin.x, localMax.x);
        float gMinY = Mathf.Min(localMin.y, localMax.y);
        float gMaxY = Mathf.Max(localMin.y, localMax.y);

        int bestCol = 0, bestRow = 0;
        float bestArea = -1f;
        for (int row = 0; row <= ROWS - itemHeight; row++)
        {
            for (int col = 0; col <= COLUMNS - itemWidth; col++)
            {
                float iMinX = col * InventorySlotSize;
                float iMinY = row * InventorySlotSize;
                float iMaxX = (col + itemWidth) * InventorySlotSize;
                float iMaxY = (row + itemHeight) * InventorySlotSize;
                float area = OverlapArea(gMinX, gMinY, gMaxX, gMaxY, iMinX, iMinY, iMaxX, iMaxY);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestCol = col;
                    bestRow = row;
                }
            }
        }
        return bestArea > 0 ? bestRow * COLUMNS + bestCol : -1;
    }

    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot")) target = target.parent;
        return target;
    }
}
