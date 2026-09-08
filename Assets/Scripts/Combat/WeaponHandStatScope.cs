using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;

namespace Scripts.Combat
{
    public static class WeaponHandStatScope
    {
        public const int MainHandSkillSlot = 0;
        public const int OffHandSkillSlot = 1;

        public static IStatsProvider ForSkill(IStatsProvider baseStats, int skillSlot)
        {
            if (baseStats == null)
                return null;

            if (!TryGetInactiveWeapon(skillSlot, out InventoryItem inactiveWeapon) || inactiveWeapon == null)
                return baseStats;

            return new WeaponHandStatsProvider(baseStats, inactiveWeapon);
        }

        public static InventoryItem GetActiveWeapon(int skillSlot)
        {
            if (!TryGetEquippedWeapons(out InventoryItem mainHand, out InventoryItem offHand))
                return null;

            if (IsCombatOneHandedWeapon(mainHand) && IsCombatOneHandedWeapon(offHand))
                return skillSlot == OffHandSkillSlot ? offHand : mainHand;

            if (IsCombatOneHandedWeapon(offHand) && !IsCombatWeapon(mainHand))
                return offHand;

            return mainHand;
        }

        public static bool TryGetInactiveWeapon(int skillSlot, out InventoryItem inactiveWeapon)
        {
            inactiveWeapon = null;
            if (!TryGetEquippedWeapons(out InventoryItem mainHand, out InventoryItem offHand))
                return false;

            if (!IsCombatOneHandedWeapon(mainHand) || !IsCombatOneHandedWeapon(offHand))
                return false;

            inactiveWeapon = skillSlot == OffHandSkillSlot ? mainHand : offHand;
            return inactiveWeapon != null;
        }

        private static bool TryGetEquippedWeapons(out InventoryItem mainHand, out InventoryItem offHand)
        {
            mainHand = null;
            offHand = null;
            if (InventoryManager.Instance == null)
                return false;

            var equipment = InventoryManager.Instance.EquipmentItems;
            if (equipment == null)
                return false;

            mainHand = GetSlot(equipment, EquipmentSlot.MainHand);
            offHand = GetSlot(equipment, EquipmentSlot.OffHand);
            return true;
        }

        private static InventoryItem GetSlot(InventoryItem[] equipment, EquipmentSlot slot)
        {
            int index = (int)slot;
            if (index < 0 || index >= equipment.Length)
                return null;

            InventoryItem item = equipment[index];
            return item != null && item.Data != null ? item : null;
        }

        private static bool IsCombatWeapon(InventoryItem item)
        {
            return item?.Data is WeaponItemSO weapon && !weapon.IsDefensiveOffHand;
        }

        private static bool IsCombatOneHandedWeapon(InventoryItem item)
        {
            return item?.Data is WeaponItemSO weapon && !weapon.IsTwoHanded && !weapon.IsDefensiveOffHand;
        }
    }
}
