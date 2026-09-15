using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using UnityEngine;

/// <summary>
/// Shared item-tooltip copy for weapon handedness, Alt inspect (tier + generation range), and item level.
/// </summary>
public static class ItemTooltipInspect
{
    public const string OneHandedKey = "inventory.ui.weapon.oneHanded";
    public const string TwoHandedKey = "inventory.ui.weapon.twoHanded";
    public const string ItemLevelKey = "inventory.ui.itemLevel";
    public const string OneHandedFallback = "One-Handed";
    public const string TwoHandedFallback = "Two-Handed";
    public const string ItemLevelFallback = "ilvl {0}";

    public static bool TryGetWeaponHandedness(EquipmentItemSO data, out bool isTwoHanded)
    {
        isTwoHanded = false;
        if (data is not WeaponItemSO weapon || weapon.IsDefensiveOffHand)
            return false;

        isTwoHanded = weapon.IsTwoHanded;
        return true;
    }

    public static string GetWeaponHandKey(bool isTwoHanded)
    {
        return isTwoHanded ? TwoHandedKey : OneHandedKey;
    }

    public static string GetWeaponHandFallback(bool isTwoHanded)
    {
        return isTwoHanded ? TwoHandedFallback : OneHandedFallback;
    }

    public static string FormatItemLevel(int itemLevel, string localizedTemplate)
    {
        string template = string.IsNullOrEmpty(localizedTemplate) ? ItemLevelFallback : localizedTemplate;
        if (template.IndexOf("{0}") >= 0)
            return string.Format(template, itemLevel);
        return $"{template} {itemLevel}";
    }

    public static string FormatAffixInspect(AffixInstance affix)
    {
        if (affix == null)
            return string.Empty;

        string tierText = affix.Tier > 0 ? $"T{affix.Tier}" : string.Empty;
        string rangeText = FormatAffixGenerationRange(affix);
        if (string.IsNullOrEmpty(tierText))
            return rangeText;
        if (string.IsNullOrEmpty(rangeText))
            return tierText;
        return $"{tierText}  {rangeText}";
    }

    public static string FormatAffixGenerationRange(AffixInstance affix)
    {
        if (affix?.Data == null)
            return string.Empty;

        ItemAffixSO.AffixStatData[] stats = affix.Data.GetStatsForTier(affix.Tier);
        if (stats == null || stats.Length == 0)
            return string.Empty;

        ItemAffixSO.AffixStatData stat = stats[0];
        if (stat.UsesRangeRoll())
        {
            return $"{FormatNumber(stat.GetPrimaryRollMin())}-{FormatNumber(stat.GetPrimaryRollMax())} to " +
                   $"{FormatNumber(stat.GetSecondaryRollMin())}-{FormatNumber(stat.GetSecondaryRollMax())}";
        }

        float min = stat.GetPrimaryRollMin();
        float max = stat.GetPrimaryRollMax();
        if (Mathf.Approximately(min, max))
            return FormatNumber(min);
        return $"{FormatNumber(min)}-{FormatNumber(max)}";
    }

    private static string FormatNumber(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.RoundToInt(value).ToString();
        return value.ToString("0.##");
    }
}
