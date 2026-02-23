using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private void CaptureDragPointer(int pointerId)
    {
        if (_root == null) return;
        if (_dragPointerId >= 0 && _dragPointerId != pointerId)
            _root.ReleasePointer(_dragPointerId);
        _root.CapturePointer(pointerId);
        _dragPointerId = pointerId;
    }

    private void ReleaseDragPointer()
    {
        if (_root != null && _dragPointerId >= 0)
            _root.ReleasePointer(_dragPointerId);
        _dragPointerId = -1;
    }

    private void CancelDragSession(bool restoreHeldItem)
    {
        InventoryItem held = _draggedItem;
        bool fromStash = _draggedFromStash;
        int stashTab = _draggedStashTab;
        int stashAnchor = _draggedStashAnchorSlot;
        int invAnchor = _draggedSourceAnchor;

        _draggedItem = null;
        _isDragging = false;
        _dropInProgress = false;
        _draggedSourceAnchor = -1;
        _draggedFromStash = false;
        _draggedStashTab = -1;
        _draggedStashAnchorSlot = -1;

        if (_ghostIcon != null)
            _ghostIcon.style.display = DisplayStyle.None;
        if (_ghostHighlight != null)
            _ghostHighlight.style.display = DisplayStyle.None;

        ReleaseDragPointer();

        if (!restoreHeldItem || held == null)
            return;

        bool returned = false;
        if (fromStash && StashManager.Instance != null && stashTab >= 0 && stashAnchor >= 0)
            returned = StashManager.Instance.PlaceItemInStash(held, stashTab, stashAnchor, -1, -1, -1);
        else if (!fromStash && invAnchor >= 0 && InventoryManager.Instance != null)
            returned = InventoryManager.Instance.PlaceItemAt(held, invAnchor, -1);

        if (!returned && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RecoverItemToInventory(held);
            returned = true;
        }

        if (!returned && fromStash && StashManager.Instance != null)
            returned = StashManager.Instance.TryAddItemPreferringTab(held, stashTab >= 0 ? stashTab : StashManager.Instance.CurrentTabIndex);

        if (!returned)
            Debug.LogWarning("[InventoryUI] Could not restore held item while canceling drag session.");
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

    private Vector2 GetPointerRootLocalFromScreen()
    {
        if (_root == null || _root.panel == null) return Vector2.zero;
        Vector2 screen = Input.mousePosition;
        screen.y = Screen.height - screen.y;
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_root.panel, screen);
        return _root.WorldToLocal(panelPos);
    }

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
                {
                    state = 2;
                }
                else if (unique.Count == 1)
                {
                    InventoryItem single = null;
                    foreach (var u in unique) { single = u; break; }
                    state = (single == _draggedItem) ? 0 : 1;
                }
                else
                {
                    state = stash.CanPlaceItemAt(tab, stashRoot, _draggedItem) ? 0 : 2;
                }

                ShowHighlightAtStashRoot(stashRoot, itemW, itemH, state);
                return;
            }
        }

        if (_inventoryContainer != null && _itemsLayer != null && _inventoryContainer.worldBound.Contains(dropCenter))
        {
            int gridIndex = GetSmartTargetIndex(dropCenter, itemW, itemH);
            if (gridIndex >= 0 && InventoryManager.Instance != null)
            {
                var inv = InventoryManager.Instance;
                var unique = inv.GetUniqueItemsInBackpackArea(_draggedItem, gridIndex);
                int state;
                if (unique.Count > 1)
                {
                    state = 2;
                }
                else if (unique.Count == 1)
                {
                    InventoryItem single = null;
                    foreach (var u in unique) { single = u; break; }
                    state = (single == _draggedItem) ? 0 : 1;
                }
                else
                {
                    state = inv.CanPlaceItemAt(_draggedItem, gridIndex) ? 0 : 2;
                }

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

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging) return;
        VisualElement hoveredSlot = evt.currentTarget as VisualElement;
        if (hoveredSlot == null || hoveredSlot.userData == null) return;

        int raw = (int)hoveredSlot.userData;
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

        if (InventoryManager.Instance == null || ItemTooltipController.Instance == null) return;
        InventoryItem item = InventoryManager.Instance.GetItemAt(raw, out int anchorIndex);
        if (item != null && item.Data != null)
        {
            VisualElement anchorSlot = GetSlotVisual(anchorIndex);
            if (anchorSlot != null)
                ItemTooltipController.Instance.ShowTooltip(item, anchorSlot);
            else
                ItemTooltipController.Instance.HideTooltip();
        }
        else
        {
            ItemTooltipController.Instance.HideTooltip();
        }
    }

    private void OnPointerOverBackpackIcon(PointerOverEvent evt)
    {
        if (_isDragging || ItemTooltipController.Instance == null || InventoryManager.Instance == null) return;
        var icon = evt.currentTarget as VisualElement;
        if (icon?.userData == null) return;
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

        if (evt.ctrlKey)
        {
            evt.StopPropagation();
            InventoryItem taken = InventoryManager.Instance.TakeItemFromSlot(anchorIdx);
            if (taken == null) return;
            if (ItemQuickTransferService.TryQuickTransfer(ItemTransferEndpointIds.InventoryBackpack, taken, isShortcut: true))
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
        CaptureDragPointer(evt.pointerId);
    }

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
                UpdateGhostPosition(GetPointerRootLocalFromScreen());
        }
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

        if (evt.ctrlKey)
        {
            evt.StopPropagation();
            InventoryItem takenCtrl = InventoryManager.Instance.TakeItemFromSlot(anchorIdx);
            if (takenCtrl == null) return;

            string sourceEndpointId = anchorIdx == InventoryManager.CRAFT_SLOT_INDEX
                ? ItemTransferEndpointIds.CraftSlot
                : ItemTransferEndpointIds.InventoryBackpack;

            if (ItemQuickTransferService.TryQuickTransfer(sourceEndpointId, takenCtrl, isShortcut: true))
            {
                RefreshInventory();
                RefreshStash();
                if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
                return;
            }

            InventoryManager.Instance.PlaceItemAt(takenCtrl, anchorIdx, -1);
            return;
        }

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
        CaptureDragPointer(evt.pointerId);
    }

    private static Vector2 GetDropCenterInPanel(VisualElement root, EventBase evt)
    {
        var ve = evt.target as VisualElement;
        if (ve == null || root == null) return Vector2.zero;
        Vector2 pos = ((IPointerEvent)evt).position;
        Vector2 localInRoot = ve.ChangeCoordinatesTo(root, pos);
        Rect r = root.worldBound;
        return new Vector2(r.xMin + localInRoot.x, r.yMin + localInRoot.y);
    }

    private void UpdateGhostPosition(Vector2 rootLocalPos)
    {
        float w;
        float h;
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
            if (_suppressNextApplyOrbPointerUp)
            {
                _suppressNextApplyOrbPointerUp = false;
                return;
            }

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
            ReleaseDragPointer();
            return;
        }
        if (_dropInProgress)
            return;
        _dropInProgress = true;

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
        ReleaseDragPointer();

        int itemW = itemToPlace.Data != null ? itemToPlace.Data.Width : 1;
        int itemH = itemToPlace.Data != null ? itemToPlace.Data.Height : 1;
        Vector2 dropCenter = GetDropCenterInPanel(_root, evt);

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
            string sourceEndpointId = fromStash
                ? ItemTransferEndpointIds.StashCurrentTab
                : (invSourceAnchor == InventoryManager.CRAFT_SLOT_INDEX
                    ? ItemTransferEndpointIds.CraftSlot
                    : ItemTransferEndpointIds.InventoryBackpack);
            placed = ItemDragDropService.TryDrop(sourceEndpointId, itemToPlace, dropCenter);
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
                Debug.LogWarning("[InventoryUI] Drop failed. Item was returned to inventory/stash fallback.");
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

        int bestCol = 0;
        int bestRow = 0;
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
