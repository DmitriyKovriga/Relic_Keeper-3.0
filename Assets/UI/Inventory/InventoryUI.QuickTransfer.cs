using System;
using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private IDisposable _inventoryQuickTransferRegistration;
    private IDisposable _stashQuickTransferRegistration;
    private IDisposable _craftQuickTransferRegistration;
    private IDisposable _inventoryDropRegistration;
    private IDisposable _stashDropRegistration;
    private IDisposable _craftDropRegistration;

    private void RegisterQuickTransferEndpoints()
    {
        UnregisterQuickTransferEndpoints();

        _inventoryQuickTransferRegistration = ItemQuickTransferService.Register(
            new DelegateItemQuickTransferEndpoint(
                ItemTransferEndpointIds.InventoryBackpack,
                priority: 100,
                isOpen: IsInventoryWindowVisible,
                canAccept: context => context.Item != null && context.Item.Data != null,
                tryAccept: context =>
                    InventoryManager.Instance != null &&
                    context.Item != null &&
                    InventoryManager.Instance.AddItem(context.Item)));

        _stashQuickTransferRegistration = ItemQuickTransferService.Register(
            new DelegateItemQuickTransferEndpoint(
                ItemTransferEndpointIds.StashCurrentTab,
                priority: 90,
                isOpen: () => IsInventoryWindowVisible() && IsStashVisible,
                canAccept: context => context.Item != null && context.Item.Data != null,
                tryAccept: context =>
                    StashManager.Instance != null &&
                    context.Item != null &&
                    StashManager.Instance.TryAddItemPreferringTab(context.Item, StashManager.Instance.CurrentTabIndex)));

        _craftQuickTransferRegistration = ItemQuickTransferService.Register(
            new DelegateItemQuickTransferEndpoint(
                ItemTransferEndpointIds.CraftSlot,
                priority: 95,
                isOpen: IsCraftEndpointOpen,
                canAccept: CanAcceptCraftEndpoint,
                tryAccept: TryAcceptCraftEndpoint));

        _inventoryDropRegistration = ItemDragDropService.Register(
            new DelegateItemDragDropEndpoint(
                ItemTransferEndpointIds.InventoryBackpack,
                priority: 100,
                isOpen: IsInventoryWindowVisible,
                isPointerOver: pointerWorld =>
                    (_inventoryContainer != null && _inventoryContainer.worldBound.Contains(pointerWorld)) ||
                    (_currentTab == 0 && IsAnyEquipmentSlotContains(pointerWorld)),
                canAccept: context => context.Item != null && context.Item.Data != null,
                tryAccept: TryAcceptInventoryDrop));

        _stashDropRegistration = ItemDragDropService.Register(
            new DelegateItemDragDropEndpoint(
                ItemTransferEndpointIds.StashCurrentTab,
                priority: 90,
                isOpen: () => IsInventoryWindowVisible() && IsStashVisible,
                isPointerOver: pointerWorld => _stashPanel != null && _stashPanel.worldBound.Contains(pointerWorld),
                canAccept: context => context.Item != null && context.Item.Data != null,
                tryAccept: TryAcceptStashDrop));

        _craftDropRegistration = ItemDragDropService.Register(
            new DelegateItemDragDropEndpoint(
                ItemTransferEndpointIds.CraftSlot,
                priority: 95,
                isOpen: IsCraftEndpointOpen,
                isPointerOver: pointerWorld => _craftSlot != null && _craftSlot.worldBound.Contains(pointerWorld),
                canAccept: CanAcceptCraftEndpoint,
                tryAccept: TryAcceptCraftEndpoint));
    }

    private void UnregisterQuickTransferEndpoints()
    {
        _inventoryQuickTransferRegistration?.Dispose();
        _inventoryQuickTransferRegistration = null;
        _stashQuickTransferRegistration?.Dispose();
        _stashQuickTransferRegistration = null;
        _craftQuickTransferRegistration?.Dispose();
        _craftQuickTransferRegistration = null;
        _inventoryDropRegistration?.Dispose();
        _inventoryDropRegistration = null;
        _stashDropRegistration?.Dispose();
        _stashDropRegistration = null;
        _craftDropRegistration?.Dispose();
        _craftDropRegistration = null;
    }

    private bool IsInventoryWindowVisible()
    {
        return _windowRoot != null && _windowRoot.resolvedStyle.display != DisplayStyle.None;
    }

    private bool IsAnyEquipmentSlotContains(Vector2 pointerWorld)
    {
        if (_equipmentSlots == null) return false;
        foreach (var slot in _equipmentSlots)
        {
            if (slot != null && slot.worldBound.Contains(pointerWorld))
                return true;
        }
        return false;
    }

    private bool TryAcceptInventoryDrop(ItemDragDropContext context)
    {
        if (context.Item == null || context.Item.Data == null || InventoryManager.Instance == null)
            return false;

        int itemW = Mathf.Max(1, context.Item.Data.Width);
        int itemH = Mathf.Max(1, context.Item.Data.Height);
        Vector2 dropCenter = context.PointerWorldPosition;

        int targetIndex = -1;
        if (_currentTab == 0)
        {
            foreach (var slot in _equipmentSlots)
            {
                if (slot != null && slot.worldBound.Contains(dropCenter) && slot.userData != null)
                {
                    targetIndex = (int)slot.userData;
                    break;
                }
            }
        }

        if (targetIndex < 0 && _inventoryContainer != null && _inventoryContainer.worldBound.Contains(dropCenter))
        {
            int gridIndex = GetSmartTargetIndex(dropCenter, itemW, itemH);
            if (gridIndex >= 0)
                targetIndex = gridIndex;
        }

        if (targetIndex < 0)
            return false;

        return InventoryManager.Instance.PlaceItemAt(context.Item, targetIndex, -1);
    }

    private bool TryAcceptStashDrop(ItemDragDropContext context)
    {
        if (!IsStashVisible || context.Item == null || context.Item.Data == null || StashManager.Instance == null)
            return false;
        if (_stashPanel == null || !_stashPanel.worldBound.Contains(context.PointerWorldPosition))
            return false;

        int itemW = Mathf.Max(1, context.Item.Data.Width);
        int itemH = Mathf.Max(1, context.Item.Data.Height);
        int stashTarget = GetSmartStashTargetIndex(context.PointerWorldPosition, itemW, itemH);
        if (stashTarget < 0)
            return false;

        int tab = StashManager.Instance.CurrentTabIndex;
        return StashManager.Instance.PlaceItemInStash(context.Item, tab, stashTarget, -1, -1, -1);
    }

    private bool IsCraftEndpointOpen()
    {
        return IsInventoryWindowVisible() && _currentTab == 1 && _craftSlot != null;
    }

    private bool CanAcceptCraftEndpoint(ItemQuickTransferContext context)
    {
        return context.Item != null &&
               context.Item.Data != null &&
               InventoryManager.Instance != null &&
               InventoryManager.Instance.CraftingSlotItem == null;
    }

    private bool CanAcceptCraftEndpoint(ItemDragDropContext context)
    {
        return context.Item != null &&
               context.Item.Data != null &&
               InventoryManager.Instance != null &&
               InventoryManager.Instance.CraftingSlotItem == null;
    }

    private bool TryAcceptCraftEndpoint(ItemQuickTransferContext context)
    {
        return InventoryManager.Instance != null &&
               InventoryManager.Instance.PlaceItemAt(context.Item, InventoryManager.CRAFT_SLOT_INDEX, -1);
    }

    private bool TryAcceptCraftEndpoint(ItemDragDropContext context)
    {
        return InventoryManager.Instance != null &&
               InventoryManager.Instance.PlaceItemAt(context.Item, InventoryManager.CRAFT_SLOT_INDEX, -1);
    }
}
