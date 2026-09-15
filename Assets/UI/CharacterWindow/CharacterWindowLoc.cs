using System;
using Scripts.Stats;
using UnityEngine.Localization.Settings;

public static class CharacterWindowLoc
{
    public const string Table = "MenuLabels";

    public const string WindowTitle = "stats.window.title";
    public const string SectionResists = "stats.section.resists";
    public const string SectionDamage = "stats.section.damage";
    public const string SectionAilments = "stats.section.ailments";
    public const string SectionOther = "stats.section.other";
    public const string ArmorMitigation = "stats.armor.mitigation";
    public const string EvasionChance = "stats.evasion.chance";
    public const string MysticLayers = "stats.mystic.layers";
    public const string MysticAbsorb = "stats.mystic.absorb";
    public const string MysticRecharge = "stats.mystic.recharge";

    public static bool IsRussian()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
    }

    public static string StatName(StatType type)
    {
        return Resolve($"stats.{type}", Humanize(type.ToString()), Humanize(type.ToString()));
    }

    public static string Resolve(string key, string english, string russian)
    {
        string localized = TryGet(key);
        if (IsMissing(localized, key))
            return IsRussian() ? russian : english;
        return localized;
    }

    public static string Title() => Resolve(WindowTitle, "Character", "Персонаж");
    public static string ResistsHeader() => Resolve(SectionResists, "Resists", "Сопротивления");
    public static string DamageHeader() => Resolve(SectionDamage, "Damage", "Урон");
    public static string AilmentsHeader() => Resolve(SectionAilments, "Ailments", "Доты");
    public static string OtherHeader() => Resolve(SectionOther, "Other", "Прочее");
    public static string ArmorMitigationLabel() => Resolve(ArmorMitigation, "Phys. reduction", "Снижение физ. урона");
    public static string EvasionChanceLabel() => Resolve(EvasionChance, "Evade chance", "Шанс уклонения");
    public static string MysticLayersLabel() => Resolve(MysticLayers, "Mystic shields", "Мистические щиты");
    public static string MysticAbsorbLabel() => Resolve(MysticAbsorb, "Absorb", "Поглощение");
    public static string MysticRechargeLabel() => Resolve(MysticRecharge, "Recharge", "Перезарядка");

    private static string TryGet(string key)
    {
        try
        {
            if (LocalizationSettings.StringDatabase == null)
                return null;
            return LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsMissing(string text, string key)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (string.Equals(text, key, StringComparison.Ordinal))
            return true;
        if (text.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var chars = new char[value.Length * 2];
        int n = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                chars[n++] = ' ';
            chars[n++] = c;
        }

        return new string(chars, 0, n);
    }
}
