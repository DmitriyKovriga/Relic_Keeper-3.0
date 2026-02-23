using System;
using Scripts.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private ItemTransferEndpointRegistration _inventoryTransferRegistration;
    private ItemTransferEndpointRegistration _stashTransferRegistration;
    private ItemTransferEndpointRegistration _craftTransferRegistration;

    private void RegisterQuickTransferEndpoints()
    {
        UnregisterQuickTransferEndpoints();

        _inventoryTransferRegistration = ItemTransferEndpointRegistration.RegisterPair(
            endpointId: ItemTransferEndpointIds.InventoryBackpack,
            priority: ItemTransferEndpointPriorities.InventoryBackpack,
            isOpen: IsInventoryWindowVisible,
            canAcceptQuick: context => context.Item != null && context.Item.Data != null,
            tryAcceptQuick: context =>
                InventoryManager.Instance != null &&
                context.Item != null &&
                InventoryManager.Instance.AddItem(context.Item),
            isPointerOver: pointerWorld =>
                (_inventoryContainer != null && _inventoryContainer.worldBound.Contains(pointerWorld)) ||
                (_currentTab == 0 && IsAnyEquipmentSlotContains(pointerWorld)),
            canAcceptDrop: context => context.Item != null && context.Item.Data != null,
            tryAcceptDrop: TryAcceptInventoryDrop);

        _stashTransferRegistration = ItemTransferEndpointRegistration.RegisterPair(
            endpointId: ItemTransferEndpointIds.StashCurrentTab,
            priority: ItemTransferEndpointPriorities.StashCurrentTab,
            isOpen: () => IsInventoryWindowVisible() && IsStashVisible,
            canAcceptQuick: context => context.Item != null && context.Item.Data != null,
            tryAcceptQuick: context =>
                StashManager.Instance != null &&
                context.Item != null &&
                StashManager.Instance.TryAddItemPreferringTab(context.Item, StashManager.Instance.CurrentTabIndex),
            isPointerOver: pointerWorld => _stashPanel != null && _stashPanel.worldBound.Contains(pointerWorld),
            canAcceptDrop: context => context.Item != null && context.Item.Data != null,
            tryAcceptDrop: TryAcceptStashDrop);

        _craftTransferRegistration = ItemTransferEndpointRegistration.RegisterPair(
            endpointId: ItemTransferEndpointIds.CraftSlot,
            priority: ItemTransferEndpointPriorities.CraftSlot,
            isOpen: IsCraftEndpointOpen,
            canAcceptQuick: CanAcceptCraftEndpoint,
            tryAcceptQuick: TryAcceptCraftEndpoint,
            isPointerOver: pointerWorld => _craftSlot != null && _craftSlot.worldBound.Contains(pointerWorld),
            canAcceptDrop: CanAcceptCraftEndpoint,
            tryAcceptDrop: TryAcceptCraftEndpoint);
    }

    private void UnregisterQuickTransferEndpoints()
    {
        _inventoryTransferRegistration?.Dispose();
        _inventoryTransferRegistration = null;
        _stashTransferRegistration?.Dispose();
        _stashTransferRegistration = null;
        _craftTransferRegistration?.Dispose();
        _craftTransferRegistration = null;
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
