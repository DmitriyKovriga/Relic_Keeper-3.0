namespace Scripts.Inventory
{
    /// <summary>
    /// Shared endpoint ids for quick-transfer and drag-drop routing between inventory-related UI windows.
    /// </summary>
    public static class ItemTransferEndpointIds
    {
        public const string InventoryBackpack = "inventory.backpack";
        public const string StashCurrentTab = "inventory.stash.current-tab";
        public const string CraftSlot = "inventory.craft.slot";
    }

    /// <summary>
    /// Shared endpoint priorities for quick-transfer and drag-drop routing.
    /// Higher value means higher routing priority.
    /// </summary>
    public static class ItemTransferEndpointPriorities
    {
        public const int InventoryBackpack = 100;
        public const int CraftSlot = 95;
        public const int StashCurrentTab = 90;

        // Recommended baseline for future companion windows (crafting, vendor, etc.).
        public const int CompanionDefault = 80;
    }

    /// <summary>
    /// Registers paired quick-transfer and drag-drop endpoints under one logical handle.
    /// Helps companion windows plug into transfer routing with minimal boilerplate.
    /// </summary>
    public sealed class ItemTransferEndpointRegistration : System.IDisposable
    {
        private readonly System.IDisposable _quickRegistration;
        private readonly System.IDisposable _dropRegistration;
        private bool _disposed;

        private ItemTransferEndpointRegistration(System.IDisposable quickRegistration, System.IDisposable dropRegistration)
        {
            _quickRegistration = quickRegistration;
            _dropRegistration = dropRegistration;
        }

        public static ItemTransferEndpointRegistration RegisterPair(
            string endpointId,
            int priority,
            System.Func<bool> isOpen,
            System.Func<ItemQuickTransferContext, bool> canAcceptQuick,
            System.Func<ItemQuickTransferContext, bool> tryAcceptQuick,
            System.Func<UnityEngine.Vector2, bool> isPointerOver,
            System.Func<ItemDragDropContext, bool> canAcceptDrop,
            System.Func<ItemDragDropContext, bool> tryAcceptDrop)
        {
            var quickRegistration = ItemQuickTransferService.Register(
                new DelegateItemQuickTransferEndpoint(
                    endpointId,
                    priority,
                    isOpen,
                    canAcceptQuick,
                    tryAcceptQuick));

            var dropRegistration = ItemDragDropService.Register(
                new DelegateItemDragDropEndpoint(
                    endpointId,
                    priority,
                    isOpen,
                    isPointerOver,
                    canAcceptDrop,
                    tryAcceptDrop));

            return new ItemTransferEndpointRegistration(quickRegistration, dropRegistration);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _quickRegistration?.Dispose();
            _dropRegistration?.Dispose();
        }
    }
}
