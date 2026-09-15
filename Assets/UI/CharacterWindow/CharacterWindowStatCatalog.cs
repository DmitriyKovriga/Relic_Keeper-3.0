using System.Collections.Generic;
using Scripts.Stats;
using UnityEngine;

public static class CharacterWindowStatCatalog
{
    public static readonly StatType[] Resists =
    {
        StatType.FireResist,
        StatType.ColdResist,
        StatType.LightningResist,
        StatType.PhysicalResist
    };

    public static readonly StatType[] Damages =
    {
        StatType.DamagePhysical,
        StatType.DamageFire,
        StatType.DamageCold,
        StatType.DamageLightning
    };

    public static readonly StatType[] AilmentDamage =
    {
        StatType.BleedDamage,
        StatType.PoisonDamage,
        StatType.IgniteDamage
    };

    public static IReadOnlyList<StatType> GetOtherStats()
    {
        var result = new List<StatType>();
        foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
        {
            if (ShouldShowInOther(type))
                result.Add(type);
        }

        return result;
    }

    public static bool ShouldShowInOther(StatType type)
    {
        if (IsHeroOrSectionStat(type) || IsHidden(type))
            return false;

        return true;
    }

    public static bool IsHidden(StatType type)
    {
        if (StatsDatabaseSO.IsRetiredStat(type))
            return true;
        if (StatsDatabaseSO.IsCooldownRecoveryStat(type) &&
            type != StatType.SkillCooldownRecovery &&
            type != StatType.SpecialSkillCooldownRecovery)
            return true;
        if (type == StatType.HealthRegenPercent || type == StatType.ManaRegenPercent)
            return true;
        if (IsConversion(type))
            return true;
        if (type == StatType.MaxFireResist ||
            type == StatType.MaxColdResist ||
            type == StatType.MaxLightningResist ||
            type == StatType.MaxPhysicalResist ||
            type == StatType.MaxMysticShieldMitigationPercent ||
            type == StatType.MaxBlockChance)
            return true;

        return false;
    }

    public static bool IsHeroOrSectionStat(StatType type)
    {
        switch (type)
        {
            case StatType.Armor:
            case StatType.Evasion:
            case StatType.MaxMysticShield:
            case StatType.MysticShieldMitigationPercent:
            case StatType.MysticShieldRechargeDuration:
            case StatType.FireResist:
            case StatType.ColdResist:
            case StatType.LightningResist:
            case StatType.PhysicalResist:
            case StatType.DamagePhysical:
            case StatType.DamageFire:
            case StatType.DamageCold:
            case StatType.DamageLightning:
            case StatType.BleedDamage:
            case StatType.PoisonDamage:
            case StatType.IgniteDamage:
                return true;
            default:
                return false;
        }
    }

    public static bool IsConversion(StatType type)
    {
        string name = type.ToString();
        if (name.Contains("Avoid"))
            return false;
        return name.Contains("To") || (name.Contains("Take") && name.Contains("As"));
    }

    public static StatType? GetResistCap(StatType resist)
    {
        switch (resist)
        {
            case StatType.FireResist: return StatType.MaxFireResist;
            case StatType.ColdResist: return StatType.MaxColdResist;
            case StatType.LightningResist: return StatType.MaxLightningResist;
            case StatType.PhysicalResist: return StatType.MaxPhysicalResist;
            default: return null;
        }
    }

    public static string FormatCappedPercent(float value, float cap, float defaultCap, bool clampToCap)
    {
        if (cap <= 0f)
            cap = defaultCap;
        float shown = clampToCap ? Mathf.Clamp(value, 0f, cap) : value;
        return $"{Mathf.RoundToInt(shown)}/{Mathf.RoundToInt(cap)}%";
    }

    public static string FormatOvercap(float value, float cap, float defaultCap)
    {
        if (cap <= 0f)
            cap = defaultCap;
        int extra = Mathf.RoundToInt(value) - Mathf.RoundToInt(cap);
        if (extra <= 0)
            return string.Empty;
        return $"+{extra}%";
    }
}
