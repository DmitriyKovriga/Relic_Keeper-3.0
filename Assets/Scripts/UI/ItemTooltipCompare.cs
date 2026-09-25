using Scripts.Inventory;
using Scripts.Items;

/// <summary>
/// Resolves the equipped item that should appear beside a hovered inventory item while comparing.
/// </summary>
public static class ItemTooltipCompare
{
    public static bool TryGetEquippedCounterpart(InventoryItem hovered, out InventoryItem equipped)
    {
        equipped = null;
        if (hovered?.Data == null)
            return false;

        InventoryManager inv = InventoryManager.Instance;
        if (inv?.EquipmentItems == null)
            return false;

        for (int i = 0; i < inv.EquipmentItems.Length; i++)
        {
            if (ReferenceEquals(inv.EquipmentItems[i], hovered))
                return false;
        }

        if (!TryResolveCompareSlot(hovered, out int localSlot))
            return false;

        if (localSlot < 0 || localSlot >= inv.EquipmentItems.Length)
            return false;

        equipped = inv.EquipmentItems[localSlot];
        return equipped != null && !ReferenceEquals(equipped, hovered);
    }

    public static bool TryResolveCompareSlot(InventoryItem hovered, out int localSlot)
    {
        localSlot = -1;
        if (hovered?.Data == null)
            return false;

        if (hovered.IsDefensiveOffHand)
        {
            localSlot = (int)EquipmentSlot.OffHand;
            return true;
        }

        localSlot = (int)hovered.Data.Slot;
        return localSlot >= 0;
    }
}
