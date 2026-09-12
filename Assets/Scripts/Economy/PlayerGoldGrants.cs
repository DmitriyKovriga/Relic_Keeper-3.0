using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;

namespace Scripts.Economy
{
    public static class PlayerGoldGrants
    {
        public const int StartingGold = 2000;
        public const string StarterWeaponItemId = "54e1b823";

        public static void GrantNewGameGold()
        {
            GoldWallet.Set(StartingGold);
        }

        /// <summary>
        /// After death, top gold up to 2000 if the player only has the starter dagger.
        /// Weapons in stash count — a well-geared stash does not get a handout.
        /// </summary>
        public static void TryGrantPovertyGold()
        {
            if (GoldWallet.Amount >= StartingGold)
                return;
            if (PlayerOwnsNonStarterWeapon())
                return;

            GoldWallet.Set(StartingGold);
        }

        public static bool IsStarterWeapon(InventoryItem item)
        {
            return item?.Data != null && item.Data.ID == StarterWeaponItemId;
        }

        public static bool IsWeapon(InventoryItem item)
        {
            if (item?.Data is WeaponItemSO weapon)
                return weapon.IsTwoHanded || weapon.Slot == EquipmentSlot.MainHand;
            return false;
        }

        public static bool PlayerOwnsNonStarterWeapon()
        {
            var inventory = InventoryManager.Instance;
            if (inventory != null)
            {
                int backpackSlots = inventory.Items != null ? inventory.Items.Length : 0;
                for (int i = 0; i < backpackSlots; i++)
                {
                    if (IsNonStarterWeapon(inventory.GetItem(i)))
                        return true;
                }

                var equipment = inventory.EquipmentItems;
                if (equipment != null)
                {
                    for (int i = 0; i < equipment.Length; i++)
                    {
                        if (IsNonStarterWeapon(equipment[i]))
                            return true;
                    }
                }

                if (IsNonStarterWeapon(inventory.CraftingSlotItem))
                    return true;
            }

            var stash = StashManager.Instance;
            if (stash == null)
                return false;

            for (int tab = 0; tab < stash.TabCount; tab++)
            {
                for (int slot = 0; slot < StashManager.STASH_SLOTS_PER_TAB; slot++)
                {
                    if (IsNonStarterWeapon(stash.GetItem(tab, slot)))
                        return true;
                }
            }

            return false;
        }

        private static bool IsNonStarterWeapon(InventoryItem item)
        {
            return IsWeapon(item) && !IsStarterWeapon(item);
        }
    }
}
