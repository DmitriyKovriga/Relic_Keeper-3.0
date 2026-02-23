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
                }) { text = "x", tooltip = "Close empty tab and switch to first tab." };
                del.AddToClassList("stash-tab-delete");
                wrap.Add(del);
            }
            content.Add(wrap);
        }
        var addTab = new Button(() => { if (StashManager.Instance != null) StashManager.Instance.AddTab(); }) { text = "+", tooltip = "New tab" };
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

        if (evt.ctrlKey)
        {
            evt.StopPropagation();
            InventoryItem taken = StashManager.Instance.TakeItemFromStash(tab, anchorSlot);
            if (taken == null) return;
            if (ItemQuickTransferService.TryQuickTransfer(ItemTransferEndpointIds.StashCurrentTab, taken, isShortcut: true))
            {
                RefreshInventory();
                RefreshStash();
                if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
                return;
            }
            StashManager.Instance.TryAddItemPreferringTab(taken, tab);
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
        CaptureDragPointer(evt.pointerId);
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
        {
            _grabOffsetRootLocal = Vector2.zero;
        }

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
        CaptureDragPointer(evt.pointerId);
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
