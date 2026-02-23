using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private void TrySubscribeStash()
    {
        if (StashManager.Instance == null) return;
        StashManager.Instance.OnStashChanged -= RefreshStash;
        StashManager.Instance.OnStashChanged += RefreshStash;
        RefreshStash();
    }

    private void SetupStashPanel()
    {
        EnsureStashPresentersInitialized();
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

    private void EnsureStashPresentersInitialized()
    {
        _stashWindowController ??= new StashWindowController(
            getStash: () => StashManager.Instance,
            refreshStash: RefreshStash,
            createItemIcon: CreateItemIcon,
            onPointerOver: OnPointerOverStashIcon,
            onPointerOut: OnPointerOutStashIcon,
            onPointerDown: OnStashIconPointerDown);
    }

    private void RefreshStash()
    {
        EnsureStashPresentersInitialized();
        RefreshStashTabs();
        RefreshStashGrid();
    }

    private void RefreshStashTabs()
    {
        _stashWindowController?.RefreshTabs(_stashTabsRow);
    }

    private void RefreshStashGrid()
    {
        DrawStashIcons();
    }

    private void DrawStashIcons()
    {
        _stashWindowController?.DrawIcons(_stashItemsLayer, StashSlotSize);
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.ValidateCurrentTarget();
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
        if (_applyOrbMode || _stashWindowController == null) return;
        var action = _stashWindowController.ResolveIconPointerDown(evt);
        if (action.Kind == StashPointerActionKind.QuickTransferSuccess)
        {
            RefreshInventory();
            RefreshStash();
            if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
            return;
        }
        if (action.Kind != StashPointerActionKind.StartDrag || action.Item == null) return;

        Vector2 originRoot = action.SourceElement != null
            ? _root.WorldToLocal(action.SourceElement.worldBound.min)
            : GetPointerRootLocalFromScreen();
        BeginStashDrag(action.Item, action.Tab, action.AnchorSlot, originRoot, action.PointerId);
    }

    private void OnStashSlotPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode || _stashWindowController == null) return;
        var action = _stashWindowController.ResolveSlotPointerDown(evt, STASH_SLOT_OFFSET);
        if (action.Kind != StashPointerActionKind.StartDrag || action.Item == null) return;

        Vector2 originRoot = GetPointerRootLocalFromScreen();
        if (_stashItemsLayer != null)
        {
            originRoot = _stashItemsLayer.ChangeCoordinatesTo(
                _root,
                new Vector2(
                    (action.AnchorSlot % StashManager.STASH_COLS) * StashSlotSize,
                    (action.AnchorSlot / StashManager.STASH_COLS) * StashSlotSize));
        }
        BeginStashDrag(action.Item, action.Tab, action.AnchorSlot, originRoot, action.PointerId);
    }

    private void BeginStashDrag(InventoryItem item, int tab, int anchorSlot, Vector2 originRoot, int pointerId)
    {
        _grabOffsetRootLocal = GetPointerRootLocalFromScreen() - originRoot;
        _isDragging = true;
        _draggedItem = item;
        _draggedFromStash = true;
        _draggedStashTab = tab;
        _draggedStashAnchorSlot = anchorSlot;
        RefreshStash();
        _ghostIcon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        _ghostIcon.style.width = item.Data.Width * StashSlotSize;
        _ghostIcon.style.height = item.Data.Height * StashSlotSize;
        _ghostIcon.style.display = DisplayStyle.None;
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
        CaptureDragPointer(pointerId);
    }

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

        int bestCol = 0;
        int bestRow = 0;
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

}

internal sealed class StashWindowController
{
    private readonly StashTabsPresenter _tabsPresenter;
    private readonly StashGridPresenter _gridPresenter;

    public StashWindowController(
        System.Func<StashManager> getStash,
        System.Action refreshStash,
        System.Func<InventoryItem, int?, int?, float, bool, VisualElement> createItemIcon,
        EventCallback<PointerOverEvent> onPointerOver,
        EventCallback<PointerOutEvent> onPointerOut,
        EventCallback<PointerDownEvent> onPointerDown)
    {
        _tabsPresenter = new StashTabsPresenter(getStash, refreshStash);
        _gridPresenter = new StashGridPresenter(getStash, createItemIcon, onPointerOver, onPointerOut, onPointerDown);
    }

    public void RefreshTabs(VisualElement stashTabsRow)
    {
        _tabsPresenter.Render(stashTabsRow);
    }

    public void DrawIcons(VisualElement stashItemsLayer, float stashSlotSize)
    {
        _gridPresenter.RenderIcons(stashItemsLayer, stashSlotSize);
    }

    public StashPointerAction ResolveIconPointerDown(PointerDownEvent evt)
    {
        var stash = _tabsPresenter.GetStash();
        if (evt == null || evt.button != 0 || stash == null) return StashPointerAction.None;

        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null) return StashPointerAction.None;

        int anchorSlot = (int)icon.userData;
        int tab = stash.CurrentTabIndex;

        if (evt.ctrlKey)
        {
            evt.StopPropagation();
            InventoryItem taken = stash.TakeItemFromStash(tab, anchorSlot);
            if (taken == null) return StashPointerAction.None;

            if (ItemQuickTransferService.TryQuickTransfer(ItemTransferEndpointIds.StashCurrentTab, taken, isShortcut: true))
                return StashPointerAction.QuickTransferSuccess;

            stash.TryAddItemPreferringTab(taken, tab);
            return StashPointerAction.ConsumedNoAction;
        }

        evt.StopPropagation();
        InventoryItem dragItem = stash.TakeItemFromStash(tab, anchorSlot);
        if (dragItem == null) return StashPointerAction.None;
        return StashPointerAction.CreateDrag(dragItem, tab, anchorSlot, evt.pointerId, icon);
    }

    public StashPointerAction ResolveSlotPointerDown(PointerDownEvent evt, int stashSlotOffset)
    {
        var stash = _tabsPresenter.GetStash();
        if (evt == null || evt.button != 0 || stash == null) return StashPointerAction.None;

        var slot = evt.currentTarget as VisualElement;
        if (slot?.userData == null) return StashPointerAction.None;
        int raw = (int)slot.userData;
        if (raw < stashSlotOffset) return StashPointerAction.None;

        int slotIndex = raw - stashSlotOffset;
        int tab = stash.CurrentTabIndex;
        InventoryItem item = stash.GetItemAt(tab, slotIndex, out int anchorSlot);
        if (item == null) return StashPointerAction.None;

        InventoryItem taken = stash.TakeItemFromStash(tab, anchorSlot);
        if (taken == null) return StashPointerAction.None;
        return StashPointerAction.CreateDrag(taken, tab, anchorSlot, evt.pointerId, null);
    }
}

internal sealed class StashTabsPresenter
{
    private readonly System.Func<StashManager> _getStash;
    private readonly System.Action _refreshStash;

    public StashTabsPresenter(System.Func<StashManager> getStash, System.Action refreshStash)
    {
        _getStash = getStash;
        _refreshStash = refreshStash;
    }

    public StashManager GetStash() => _getStash?.Invoke();

    public void Render(VisualElement stashTabsRow)
    {
        var stash = _getStash?.Invoke();
        if (stashTabsRow == null || stash == null) return;

        float savedScrollOffset = 0f;
        var existingScroll = stashTabsRow.Q<ScrollView>();
        if (existingScroll != null && existingScroll.horizontalScroller != null)
            savedScrollOffset = existingScroll.horizontalScroller.value;

        stashTabsRow.Clear();
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

        int tabCount = stash.TabCount;
        int current = stash.CurrentTabIndex;
        for (int i = 0; i < tabCount; i++)
        {
            int tabIndex = i;
            var wrap = new VisualElement();
            wrap.AddToClassList("stash-tab-wrap");
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.alignItems = Align.Center;

            var tab = new Button(() =>
            {
                var currentStash = _getStash?.Invoke();
                if (currentStash != null) currentStash.SetCurrentTab(tabIndex);
            }) { text = (i + 1).ToString() };
            tab.AddToClassList("stash-tab");
            if (i == current) tab.AddToClassList("active");
            wrap.Add(tab);

            if (tabCount > 1 && i == current)
            {
                var del = new Button(() =>
                {
                    var currentStash = _getStash?.Invoke();
                    if (currentStash != null && currentStash.TryRemoveTab(tabIndex))
                    {
                        currentStash.SetCurrentTab(0);
                        _refreshStash?.Invoke();
                    }
                }) { text = "x", tooltip = "Close empty tab and switch to first tab." };
                del.AddToClassList("stash-tab-delete");
                wrap.Add(del);
            }

            content.Add(wrap);
        }

        var addTab = new Button(() =>
        {
            var currentStash = _getStash?.Invoke();
            if (currentStash != null) currentStash.AddTab();
        }) { text = "+", tooltip = "New tab" };
        addTab.AddToClassList("stash-tab");
        addTab.AddToClassList("stash-tab-add");
        content.Add(addTab);

        scroll.Add(content);
        stashTabsRow.Add(scroll);
        HideVerticalScrollerDelayed(scroll);
        HideHorizontalScrollerArrows(scroll);

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

    private static void HideHorizontalScrollerArrows(ScrollView scroll)
    {
        if (scroll?.horizontalScroller == null) return;
        var h = scroll.horizontalScroller;
        if (h.lowButton != null) h.lowButton.style.display = DisplayStyle.None;
        if (h.highButton != null) h.highButton.style.display = DisplayStyle.None;
    }

    private static void HideVerticalScrollerDelayed(ScrollView scroll)
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
}

internal enum StashPointerActionKind
{
    None = 0,
    ConsumedNoAction = 1,
    QuickTransferSuccess = 2,
    StartDrag = 3
}

internal readonly struct StashPointerAction
{
    public static readonly StashPointerAction None = new StashPointerAction(StashPointerActionKind.None, null, -1, -1, -1, null);
    public static readonly StashPointerAction ConsumedNoAction = new StashPointerAction(StashPointerActionKind.ConsumedNoAction, null, -1, -1, -1, null);
    public static readonly StashPointerAction QuickTransferSuccess = new StashPointerAction(StashPointerActionKind.QuickTransferSuccess, null, -1, -1, -1, null);

    public StashPointerActionKind Kind { get; }
    public InventoryItem Item { get; }
    public int Tab { get; }
    public int AnchorSlot { get; }
    public int PointerId { get; }
    public VisualElement SourceElement { get; }

    private StashPointerAction(
        StashPointerActionKind kind,
        InventoryItem item,
        int tab,
        int anchorSlot,
        int pointerId,
        VisualElement sourceElement)
    {
        Kind = kind;
        Item = item;
        Tab = tab;
        AnchorSlot = anchorSlot;
        PointerId = pointerId;
        SourceElement = sourceElement;
    }

    public static StashPointerAction CreateDrag(
        InventoryItem item,
        int tab,
        int anchorSlot,
        int pointerId,
        VisualElement sourceElement)
    {
        return new StashPointerAction(StashPointerActionKind.StartDrag, item, tab, anchorSlot, pointerId, sourceElement);
    }
}

internal sealed class StashGridPresenter
{
    private readonly System.Func<StashManager> _getStash;
    private readonly System.Func<InventoryItem, int?, int?, float, bool, VisualElement> _createItemIcon;
    private readonly EventCallback<PointerOverEvent> _onPointerOver;
    private readonly EventCallback<PointerOutEvent> _onPointerOut;
    private readonly EventCallback<PointerDownEvent> _onPointerDown;

    public StashGridPresenter(
        System.Func<StashManager> getStash,
        System.Func<InventoryItem, int?, int?, float, bool, VisualElement> createItemIcon,
        EventCallback<PointerOverEvent> onPointerOver,
        EventCallback<PointerOutEvent> onPointerOut,
        EventCallback<PointerDownEvent> onPointerDown)
    {
        _getStash = getStash;
        _createItemIcon = createItemIcon;
        _onPointerOver = onPointerOver;
        _onPointerOut = onPointerOut;
        _onPointerDown = onPointerDown;
    }

    public void RenderIcons(VisualElement stashItemsLayer, float stashSlotSize)
    {
        var stash = _getStash?.Invoke();
        if (stashItemsLayer == null || stash == null || _createItemIcon == null) return;

        stashItemsLayer.Clear();
        int tab = stash.CurrentTabIndex;
        for (int i = 0; i < StashManager.STASH_SLOTS_PER_TAB; i++)
        {
            var item = stash.GetItem(tab, i);
            if (item == null || item.Data == null) continue;

            bool isAnchor =
                (i % StashManager.STASH_COLS == 0 || stash.GetItem(tab, i - 1) != item) &&
                (i < StashManager.STASH_COLS || stash.GetItem(tab, i - StashManager.STASH_COLS) != item);
            if (!isAnchor) continue;

            StashManager.GetStashItemSize(item, out int sw, out int sh);
            var icon = _createItemIcon(item, sw, sh, stashSlotSize, true);
            icon.style.left = (i % StashManager.STASH_COLS) * stashSlotSize;
            icon.style.top = (i / StashManager.STASH_COLS) * stashSlotSize;
            icon.userData = i;
            icon.RegisterCallback(_onPointerOver);
            icon.RegisterCallback(_onPointerOut);
            icon.RegisterCallback(_onPointerDown);
            stashItemsLayer.Add(icon);
        }

        stashItemsLayer.BringToFront();
    }
}
