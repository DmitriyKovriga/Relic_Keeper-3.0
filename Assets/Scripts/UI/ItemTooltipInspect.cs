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

    public static string FormatTierLabel(AffixInstance affix)
    {
        return affix != null && affix.Tier > 0 ? $"T{affix.Tier}" : string.Empty;
    }

    public static string FormatPrimaryGenerationRange(AffixInstance affix)
    {
        if (!TryGetAffixStat(affix, out ItemAffixSO.AffixStatData stat))
            return string.Empty;
        return FormatRange(stat.GetPrimaryRollMin(), stat.GetPrimaryRollMax());
    }

    public static string FormatSecondaryGenerationRange(AffixInstance affix)
    {
        if (!TryGetAffixStat(affix, out ItemAffixSO.AffixStatData stat) || !stat.UsesRangeRoll())
            return string.Empty;
        return FormatRange(stat.GetSecondaryRollMin(), stat.GetSecondaryRollMax());
    }

    public static string ReplaceRolledValuesWithRanges(string localizedText, AffixInstance affix)
    {
        if (string.IsNullOrEmpty(localizedText) || affix?.Modifiers == null || affix.Modifiers.Count == 0)
            return localizedText;

        AffixModifierInstance modifier = affix.Modifiers[0];
        string result = localizedText;
        if (modifier.HasRange)
        {
            string secondary = FormatSecondaryGenerationRange(affix);
            if (!string.IsNullOrEmpty(secondary))
                result = ReplaceRolledNumber(result, modifier.SecondaryMod.Value, WrapRange(secondary));
        }

        string primary = FormatPrimaryGenerationRange(affix);
        if (!string.IsNullOrEmpty(primary))
            result = ReplaceRolledNumber(result, modifier.PrimaryMod.Value, WrapRange(primary));
        return result;
    }

    private static bool TryGetAffixStat(AffixInstance affix, out ItemAffixSO.AffixStatData stat)
    {
        stat = default;
        if (affix?.Data == null)
            return false;

        ItemAffixSO.AffixStatData[] stats = affix.Data.GetStatsForTier(affix.Tier);
        if (stats == null || stats.Length == 0)
            return false;

        stat = stats[0];
        return true;
    }

    private static string WrapRange(string range)
    {
        return $"({range})";
    }

    private static string FormatRange(float min, float max)
    {
        if (Mathf.Approximately(min, max))
            return FormatNumber(min);
        return $"{FormatNumber(min)}-{FormatNumber(max)}";
    }

    private static string ReplaceRolledNumber(string text, float value, string replacement)
    {
        string plain = FormatNumber(value);
        string[] needles = plain[0] == '+' || plain[0] == '-'
            ? new[] { plain }
            : new[] { "+" + plain, plain };

        for (int n = 0; n < needles.Length; n++)
        {
            int index = IndexOfStandalone(text, needles[n]);
            if (index >= 0)
                return text.Substring(0, index) + replacement + text.Substring(index + needles[n].Length);
        }

        return text;
    }

    private static int IndexOfStandalone(string text, string needle)
    {
        int start = 0;
        while (start < text.Length)
        {
            int index = text.IndexOf(needle, start, System.StringComparison.Ordinal);
            if (index < 0)
                return -1;

            bool leftOk = index == 0 || !IsNumberChar(text[index - 1]);
            int end = index + needle.Length;
            bool rightOk = end >= text.Length || !IsNumberChar(text[end]);
            if (leftOk && rightOk)
                return index;

            start = index + 1;
        }

        return -1;
    }

    private static bool IsNumberChar(char c)
    {
        return char.IsDigit(c) || c == '.' || c == ',';
    }

    private static string FormatNumber(float value)
    {
        if (Mathf.Approximately(value, Mathf.Round(value)))
            return Mathf.RoundToInt(value).ToString();
        return value.ToString("0.##");
    }
}
