namespace Scripts.Inventory
{
    /// <summary>
    /// Source tag for a weapon's local combat stats (base damage/APS/crit after local affixes).
    /// Global affixes on the same item keep the InventoryItem itself as Source.
    /// </summary>
    public sealed class WeaponLocalStatSource
    {
        public InventoryItem Item { get; }

        public WeaponLocalStatSource(InventoryItem item)
        {
            Item = item;
        }
    }
}
