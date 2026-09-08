using Scripts.Items;

/// <summary>
/// Ключи MenuLabels для подписей слотов инвентаря. Английские fallback совпадают с текстом в UXML.
/// </summary>
public static class InventorySlotLocKeys
{
    public const string Table = "MenuLabels";

    public const string Head = "inventory.slot.head";
    public const string Body = "inventory.slot.body";
    public const string Main = "inventory.slot.main";
    public const string Off = "inventory.slot.off";
    public const string Hand = "inventory.slot.hand";
    public const string Feet = "inventory.slot.feet";
    public const string Item = "inventory.slot.item";

    public const string HeadFallback = "Head";
    public const string BodyFallback = "Body";
    public const string MainFallback = "Main";
    public const string OffFallback = "Off";
    public const string HandFallback = "Hand";
    public const string FeetFallback = "Feet";
    public const string ItemFallback = "Item";

    public static bool TryGetEquipmentSlot(EquipmentSlot slot, out string key, out string fallback)
    {
        switch (slot)
        {
            case EquipmentSlot.Helmet:
                key = Head;
                fallback = HeadFallback;
                return true;
            case EquipmentSlot.BodyArmor:
                key = Body;
                fallback = BodyFallback;
                return true;
            case EquipmentSlot.MainHand:
                key = Main;
                fallback = MainFallback;
                return true;
            case EquipmentSlot.OffHand:
                key = Off;
                fallback = OffFallback;
                return true;
            case EquipmentSlot.Gloves:
                key = Hand;
                fallback = HandFallback;
                return true;
            case EquipmentSlot.Boots:
                key = Feet;
                fallback = FeetFallback;
                return true;
            default:
                key = null;
                fallback = null;
                return false;
        }
    }
}
