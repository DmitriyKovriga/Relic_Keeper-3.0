using System;
using Scripts.Inventory;

namespace Scripts.Inventory
{
    /// <summary>Shared tabbed grid used by stash and the market vendor panel.</summary>
    public interface ITabbedItemGrid
    {
        event Action OnChanged;
        int TabCount { get; }
        int CurrentTabIndex { get; }
        bool CanAddTab { get; }

        void SetCurrentTab(int index);
        void AddTab();
        bool CanRemoveTab(int index);
        bool TryRemoveTab(int index);
        string GetTabLabel(int index);

        InventoryItem GetItem(int tabIndex, int slotIndex);
        InventoryItem GetItemAt(int tabIndex, int slotIndex, out int anchorIndex);
        InventoryItem TakeItem(int tabIndex, int anchorSlot);
        bool TryAddItemPreferringTab(InventoryItem item, int preferredTab);
    }
}
