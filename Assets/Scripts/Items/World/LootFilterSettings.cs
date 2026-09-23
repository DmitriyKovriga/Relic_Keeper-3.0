using System;
using Scripts.Inventory;
using UnityEngine;

namespace Scripts.Items.World
{
    public enum LootFilterThreshold
    {
        None = 0,
        Common = 1,
        Magic = 2,
        Rare = 3
    }

    /// <summary>
    /// Global, reversible ground-loot visibility preference.
    /// The selected rarity and every lower rarity are hidden.
    /// </summary>
    public static class LootFilterSettings
    {
        public const string PlayerPrefsKey = "gameplay_loot_filter_threshold";

        public static event Action Changed;

        public static LootFilterThreshold Threshold => Normalize(PlayerPrefs.GetInt(PlayerPrefsKey, 0));

        public static void SetThreshold(LootFilterThreshold threshold)
        {
            LootFilterThreshold normalized = Normalize((int)threshold);
            if (Threshold == normalized)
                return;

            PlayerPrefs.SetInt(PlayerPrefsKey, (int)normalized);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool ShouldHide(InventoryItem item)
        {
            return ShouldHide(item, Threshold);
        }

        public static bool ShouldHide(InventoryItem item, LootFilterThreshold threshold)
        {
            if (item?.Data == null || threshold == LootFilterThreshold.None)
                return false;

            return (int)ItemRarity.GetTier(item) <= (int)Normalize((int)threshold);
        }

        private static LootFilterThreshold Normalize(int value)
        {
            return (LootFilterThreshold)Mathf.Clamp(
                value,
                (int)LootFilterThreshold.None,
                (int)LootFilterThreshold.Rare);
        }
    }
}
