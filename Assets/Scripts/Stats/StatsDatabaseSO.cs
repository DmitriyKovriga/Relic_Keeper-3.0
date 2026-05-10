using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Stats
{
    public enum StatValueUnit
    {
        None,
        HP,
        MP,
        Percent,
        Seconds,
        Stacks,
        Targets,
        Points,
        MysticShield
    }

    public enum StatSemanticKind
    {
        FinalScalar,
        CombatScalar,
        ContextModifier,
        Utility,
        Derived
    }

    [Flags]
    public enum StatContextTagFlags
    {
        None = 0,
        Attack = 1 << 0,
        Spell = 1 << 1,
        Melee = 1 << 2,
        Projectile = 1 << 3,
        Area = 1 << 4,
        DamageOverTime = 1 << 5,
        Ailment = 1 << 6
    }

    [Flags]
    public enum StatDamageChannelFlags
    {
        None = 0,
        Physical = 1 << 0,
        Fire = 1 << 1,
        Cold = 1 << 2,
        Lightning = 1 << 3,
        All = Physical | Fire | Cold | Lightning
    }

    [Flags]
    public enum StatAffixModifierKindFlags
    {
        None = 0,
        Flat = 1 << 0,
        Increase = 1 << 1,
        Decrease = 1 << 2,
        More = 1 << 3,
        Less = 1 << 4,
        Full = Flat | Increase | Decrease | More | Less
    }

    public enum StatAffixModifierKind
    {
        Flat,
        Increase,
        Decrease,
        More,
        Less
    }

    public static class StatPresentation
    {
        public enum ModifierLineStyle
        {
            StatThenValue,
            ValueThenStat,
            ValueToStat
        }

        private static readonly StatAffixModifierKind[] OrderedKinds =
        {
            StatAffixModifierKind.Flat,
            StatAffixModifierKind.Increase,
            StatAffixModifierKind.Decrease,
            StatAffixModifierKind.More,
            StatAffixModifierKind.Less
        };

        public static IEnumerable<StatAffixModifierKind> EnumerateKinds(StatAffixModifierKindFlags flags)
        {
            foreach (var kind in OrderedKinds)
            {
                if ((flags & ToFlag(kind)) != 0)
                    yield return kind;
            }
        }

        public static StatAffixModifierKindFlags ToFlag(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Flat:
                    return StatAffixModifierKindFlags.Flat;
                case StatAffixModifierKind.Increase:
                    return StatAffixModifierKindFlags.Increase;
                case StatAffixModifierKind.Decrease:
                    return StatAffixModifierKindFlags.Decrease;
                case StatAffixModifierKind.More:
                    return StatAffixModifierKindFlags.More;
                case StatAffixModifierKind.Less:
                    return StatAffixModifierKindFlags.Less;
                default:
                    return StatAffixModifierKindFlags.None;
            }
        }

        public static StatAffixModifierKind FromStatModType(StatModType type)
        {
            switch (type)
            {
                case StatModType.PercentAdd:
                    return StatAffixModifierKind.Increase;
                case StatModType.PercentSub:
                    return StatAffixModifierKind.Decrease;
                case StatModType.PercentMult:
                    return StatAffixModifierKind.More;
                case StatModType.PercentLess:
                    return StatAffixModifierKind.Less;
                default:
                    return StatAffixModifierKind.Flat;
            }
        }

        public static StatModType ToStatModType(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return StatModType.PercentAdd;
                case StatAffixModifierKind.Decrease:
                    return StatModType.PercentSub;
                case StatAffixModifierKind.More:
                    return StatModType.PercentMult;
                case StatAffixModifierKind.Less:
                    return StatModType.PercentLess;
                default:
                    return StatModType.Flat;
            }
        }

        public static string GetModifierKindId(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return "increase";
                case StatAffixModifierKind.Decrease:
                    return "decrease";
                case StatAffixModifierKind.More:
                    return "more";
                case StatAffixModifierKind.Less:
                    return "less";
                default:
                    return "flat";
            }
        }

        public static string GetModifierKindDisplayName(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return "Increase";
                case StatAffixModifierKind.Decrease:
                    return "Decrease";
                case StatAffixModifierKind.More:
                    return "More";
                case StatAffixModifierKind.Less:
                    return "Less";
                default:
                    return "Flat";
            }
        }

        public static string GetValueUnitLocalizationKey(StatValueUnit unit)
        {
            return "stats.unit." + unit.ToString().ToLowerInvariant();
        }

        public static string GetValueUnitFallback(StatValueUnit unit, string localeCode)
        {
            bool isRu = string.Equals(localeCode, "ru", StringComparison.OrdinalIgnoreCase);
            switch (unit)
            {
                case StatValueUnit.HP:
                    return "HP";
                case StatValueUnit.MP:
                    return "MP";
                case StatValueUnit.Percent:
                    return "%";
                case StatValueUnit.Seconds:
                    return isRu ? "с" : "s";
                case StatValueUnit.Stacks:
                    return isRu ? "стаков" : "stacks";
                case StatValueUnit.Targets:
                    return isRu ? "целей" : "targets";
                case StatValueUnit.MysticShield:
                    return isRu ? "МЩ" : "MS";
                case StatValueUnit.Points:
                    return isRu ? "ед." : "pts";
                default:
                    return string.Empty;
            }
        }

        public static bool IsSymbolUnit(StatValueUnit unit)
        {
            return unit == StatValueUnit.Percent;
        }

        public static bool ShouldDisplayPercentWhenFlat(StatsDatabaseSO db, StatType type)
        {
            if (db != null)
                return db.ShouldDisplayAsPercentWhenFlat(type);

            return StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(type);
        }

        public static string FormatModifierValue(StatsDatabaseSO db, StatType type, float value, StatModType modifierType, bool absoluteValue = false)
        {
            float displayValue = absoluteValue ? Mathf.Abs(value) : value;
            float magnitude = Mathf.Abs(displayValue);

            switch (modifierType)
            {
                case StatModType.PercentAdd:
                case StatModType.PercentSub:
                    return $"{modifierType.GetDisplayPrefix(displayValue)}{magnitude:0.##}%";

                case StatModType.PercentMult:
                    return $"{magnitude:0.##}% more";

                case StatModType.PercentLess:
                    return $"{magnitude:0.##}% less";

                default:
                    if (ShouldDisplayPercentWhenFlat(db, type))
                        return $"{displayValue:+0.##;-0.##;0}%";

                    return $"{displayValue:+0.##;-0.##;0}";
            }
        }

        public static string FormatModifierLine(
            StatsDatabaseSO db,
            StatType type,
            string statName,
            float value,
            StatModType modifierType,
            ModifierLineStyle lineStyle)
        {
            string valueText = FormatModifierValue(db, type, value, modifierType);
            switch (lineStyle)
            {
                case ModifierLineStyle.ValueThenStat:
                    return $"{valueText} {statName}";
                case ModifierLineStyle.ValueToStat:
                    return $"{valueText} to {statName}";
                default:
                    return $"{statName}: {valueText}";
            }
        }

        public static string FormatScalarValue(StatsDatabaseSO db, StatType type, float value)
        {
            StatDisplayFormat format = db != null ? db.GetFormat(type) ?? StatsDatabaseSO.DefaultFormatFor(type) : StatsDatabaseSO.DefaultFormatFor(type);
            switch (format)
            {
                case StatDisplayFormat.Time:
                    return $"{value:0.##}s";
                case StatDisplayFormat.Percent:
                    return $"{Mathf.Round(value)}%";
                default:
                    return $"{Mathf.Round(value)}";
            }
        }
    }

    public enum StatDisplayFormat
    {
        Number,
        Percent,
        Time,
        Damage
    }

    public enum StatAffixGenType
    {
        FullCalcStat,
        PercentStat,
        ContextModifierStat,
        NOCalcStat
    }

    [Serializable]
    public class StatMetadataEntry
    {
        public string StatTypeId;
        public string Category = "Misc";
        public StatSemanticKind SemanticKind = StatSemanticKind.FinalScalar;
        public StatDisplayFormat Format = StatDisplayFormat.Number;
        public bool ShowInCharacterWindow = true;
        public bool ShowInPrimaryStatsEditor = true;
        public StatAffixGenType AffixGenType = StatAffixGenType.FullCalcStat;
        public StatValueUnit ValueUnit = StatValueUnit.None;
        public bool DisplayAsPercentWhenFlat;
        public bool AllowNegativeFlatGeneration;
        public StatContextTagFlags ContextTags = StatContextTagFlags.None;
        public StatDamageChannelFlags DamageChannels = StatDamageChannelFlags.None;
        public StatAffixModifierKindFlags AllowedAffixKinds = StatAffixModifierKindFlags.Full;
    }

    [CreateAssetMenu(menuName = "RPG/Stats Database", fileName = "StatsDatabase")]
    public class StatsDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<StatMetadataEntry> _entries = new List<StatMetadataEntry>();
        private Dictionary<string, StatMetadataEntry> _lookup;

        public StatMetadataEntry GetMetadata(StatType type)
        {
            EnsureLookup();
            return _lookup.TryGetValue(type.ToString(), out var entry) ? entry : null;
        }

        public bool ShouldShowInCharacterWindow(StatType type)
        {
            var meta = GetMetadata(type);
            return meta == null || meta.ShowInCharacterWindow;
        }

        public StatDisplayFormat? GetFormat(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.Format : (StatDisplayFormat?)null;
        }

        public string GetCategory(StatType type)
        {
            var meta = GetMetadata(type);
            return meta?.Category ?? "Misc";
        }

        public StatSemanticKind GetSemanticKind(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.SemanticKind : DefaultSemanticKindFor(type);
        }

        public StatAffixGenType GetAffixGenType(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.AffixGenType : StatAffixGenType.FullCalcStat;
        }

        public StatValueUnit GetValueUnit(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.ValueUnit : DefaultValueUnitFor(type);
        }

        public bool ShouldDisplayAsPercentWhenFlat(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.DisplayAsPercentWhenFlat : DefaultDisplayAsPercentWhenFlat(type);
        }

        public bool ShouldShowInPrimaryStatsEditor(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.ShowInPrimaryStatsEditor : DefaultShowInPrimaryStatsEditor(type);
        }

        public bool AllowNegativeFlatGeneration(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.AllowNegativeFlatGeneration : DefaultAllowNegativeFlatGeneration(type);
        }

        public StatContextTagFlags GetContextTags(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.ContextTags : DefaultContextTagsFor(type);
        }

        public StatDamageChannelFlags GetDamageChannels(StatType type)
        {
            var meta = GetMetadata(type);
            return meta != null ? meta.DamageChannels : DefaultDamageChannelsFor(type);
        }

        public StatAffixModifierKindFlags GetAllowedAffixKinds(StatType type)
        {
            var meta = GetMetadata(type);
            if (meta != null)
                return NormalizeAllowedAffixKinds(meta.AllowedAffixKinds, meta.AffixGenType, type);
            return DefaultAllowedAffixKindsFor(type, DefaultAffixGenTypeFor(type));
        }

        public IEnumerable<StatType> GetRelatedContextModifiers(StatType targetType)
        {
            StatDamageChannelFlags targetChannels = GetDamageChannels(targetType);
            foreach (StatType candidate in Enum.GetValues(typeof(StatType)))
            {
                if (GetSemanticKind(candidate) != StatSemanticKind.ContextModifier)
                    continue;

                StatDamageChannelFlags candidateChannels = GetDamageChannels(candidate);
                if (candidateChannels == StatDamageChannelFlags.None ||
                    targetChannels == StatDamageChannelFlags.None ||
                    candidateChannels == StatDamageChannelFlags.All ||
                    (candidateChannels & targetChannels) != 0)
                {
                    yield return candidate;
                }
            }
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            _lookup = new Dictionary<string, StatMetadataEntry>(StringComparer.Ordinal);
            if (_entries == null)
                return;

            foreach (var entry in _entries)
            {
                if (string.IsNullOrEmpty(entry.StatTypeId))
                    continue;

                entry.AllowedAffixKinds = NormalizeAllowedAffixKinds(entry.AllowedAffixKinds, entry.AffixGenType, ParseStat(entry.StatTypeId));
                _lookup[entry.StatTypeId] = entry;
            }
        }

        private static StatType ParseStat(string id)
        {
            if (Enum.TryParse(id, out StatType parsed))
                return parsed;
            return StatType.MaxHealth;
        }

#if UNITY_EDITOR
        public StatMetadataEntry GetOrCreateEntry(StatType type)
        {
            EnsureLookup();
            string id = type.ToString();
            if (_lookup.TryGetValue(id, out var existing))
            {
                existing.AllowedAffixKinds = NormalizeAllowedAffixKinds(existing.AllowedAffixKinds, existing.AffixGenType, type);
                return existing;
            }

            var entry = CreateDefaultEntry(type);
            _entries.Add(entry);
            _lookup[id] = entry;
            return entry;
        }

        public void CreateDefaultsForAllStatTypes()
        {
            EnsureLookup();
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                string id = type.ToString();
                if (_lookup.ContainsKey(id))
                    continue;

                _entries.Add(CreateDefaultEntry(type));
            }

            _lookup = null;
            EnsureLookup();
        }

        private static StatMetadataEntry CreateDefaultEntry(StatType type)
        {
            var genType = DefaultAffixGenTypeFor(type);
            return new StatMetadataEntry
            {
                StatTypeId = type.ToString(),
                Category = DefaultCategoryFor(type),
                SemanticKind = DefaultSemanticKindFor(type),
                Format = DefaultFormatFor(type),
                ShowInCharacterWindow = DefaultShowInCharacterWindow(type),
                ShowInPrimaryStatsEditor = DefaultShowInPrimaryStatsEditor(type),
                AffixGenType = genType,
                ValueUnit = DefaultValueUnitFor(type),
                DisplayAsPercentWhenFlat = DefaultDisplayAsPercentWhenFlat(type),
                AllowNegativeFlatGeneration = DefaultAllowNegativeFlatGeneration(type),
                ContextTags = DefaultContextTagsFor(type),
                DamageChannels = DefaultDamageChannelsFor(type),
                AllowedAffixKinds = DefaultAllowedAffixKindsFor(type, genType)
            };
        }

        public static StatSemanticKind DefaultSemanticKindFor(StatType type)
        {
            switch (type)
            {
                case StatType.DamagePhysical:
                case StatType.DamageFire:
                case StatType.DamageCold:
                case StatType.DamageLightning:
                case StatType.PhysicalToFire:
                case StatType.PhysicalToCold:
                case StatType.PhysicalToLightning:
                case StatType.ElementalToPhysical:
                case StatType.TakePhysAsCold:
                case StatType.TakePhysAsFire:
                case StatType.TakePhysAsLightning:
                case StatType.TakeLightningAsPhys:
                case StatType.TakeLightningAsFire:
                case StatType.TakeLightningAsCold:
                case StatType.TakeColdAsPhys:
                case StatType.TakeColdAsFire:
                case StatType.TakeColdAsLightning:
                case StatType.TakeFireAsLightning:
                case StatType.TakeFireAsPhys:
                case StatType.TakeFireAsCold:
                case StatType.PenetrationPhysical:
                case StatType.PenetrationFire:
                case StatType.PenetrationCold:
                case StatType.PenetrationLightning:
                case StatType.BleedChance:
                case StatType.BleedDamage:
                case StatType.BleedDamageMult:
                case StatType.BleedDuration:
                case StatType.ChanseToAvoidBleed:
                case StatType.PoisonChance:
                case StatType.PoisonDamage:
                case StatType.PoisonDamageMult:
                case StatType.PoisonDuration:
                case StatType.ChanseToAvoidPoison:
                case StatType.IgniteChance:
                case StatType.IgniteDamage:
                case StatType.IgniteDamageMult:
                case StatType.IgniteDuration:
                case StatType.ChanseToAvoidIgnite:
                case StatType.FreezeChance:
                case StatType.FreezeDuration:
                case StatType.ChanseToAvoidFreeze:
                case StatType.ShockChance:
                case StatType.ShockDuration:
                case StatType.ChanseToAvoidShock:
                    return StatSemanticKind.CombatScalar;

                case StatType.MeleeDamage:
                    return StatSemanticKind.ContextModifier;

                case StatType.AreaOfEffect:
                case StatType.CooldownReductionPercent:
                case StatType.EffectDuration:
                case StatType.ProjectileSpeed:
                case StatType.ProjectileCount:
                case StatType.ProjectileFork:
                case StatType.ProjectileChain:
                case StatType.MaxBleedStack:
                case StatType.ExtraTargetsForMeleeHits:
                    return StatSemanticKind.Utility;

                default:
                    return StatSemanticKind.FinalScalar;
            }
        }

        public static string DefaultCategoryFor(StatType type)
        {
            string s = type.ToString();
            if (s.Contains("Bleed") || s.Contains("Poison") || s.Contains("Ignite") || s.Contains("Freeze") || s.Contains("Shock")) return "Ailments";
            if (s.Contains("Resist") || s.Contains("Penetration") || s.Contains("Mitigation") || s.Contains("ReduceDamage")) return "Resistances";
            if (s.Contains("Health") || s.Contains("Mana")) return "Vitals";
            if (s.Contains("Armor") || s.Contains("Evasion") || s.Contains("Block") || s.Contains("MysticShield")) return "Defense";
            if (s.Contains("Crit") || s.Contains("Accuracy")) return "Critical";
            if (s.Contains("Speed")) return "Speed";
            if (s.Contains("Damage") && !s.Contains("Mult") && !s.Contains("Taken")) return "Damage";
            if (s.Contains("To") || s.Contains("As")) return "Conversion";
            return "Misc";
        }

        public static StatDisplayFormat DefaultFormatFor(StatType type)
        {
            if (type == StatType.DamagePhysical || type == StatType.DamageFire || type == StatType.DamageCold || type == StatType.DamageLightning)
                return StatDisplayFormat.Damage;

            if (type == StatType.ShockDuration || type == StatType.FreezeDuration || type == StatType.BleedDuration ||
                type == StatType.PoisonDuration || type == StatType.IgniteDuration || type == StatType.MysticShieldRechargeDuration)
                return StatDisplayFormat.Time;

            string s = type.ToString();
            if (type == StatType.AreaOfEffect || type == StatType.ReduceDamageTaken || type == StatType.ProjectileSpeed || type == StatType.EffectDuration)
                return StatDisplayFormat.Percent;
            if (type == StatType.BleedDamageMult || type == StatType.PoisonDamageMult || type == StatType.IgniteDamageMult)
                return StatDisplayFormat.Percent;
            if (s.Contains("Percent") || s.Contains("Chance") || s.Contains("Multiplier") || s.Contains("Resist") || s.Contains("Reduction"))
                return StatDisplayFormat.Percent;

            return StatDisplayFormat.Number;
        }

        public static bool DefaultShowInCharacterWindow(StatType type)
        {
            return type != StatType.HealthRegenPercent && type != StatType.ManaRegenPercent;
        }

        public static bool DefaultShowInPrimaryStatsEditor(StatType type)
        {
            return DefaultSemanticKindFor(type) == StatSemanticKind.FinalScalar;
        }

        public static StatAffixGenType DefaultAffixGenTypeFor(StatType type)
        {
            string s = type.ToString();
            if (type == StatType.CritMultiplier)
                return StatAffixGenType.PercentStat;
            if (type == StatType.MeleeDamage)
                return StatAffixGenType.ContextModifierStat;
            if (type == StatType.AreaOfEffect || type == StatType.ProjectileSpeed || type == StatType.EffectDuration || type == StatType.ReduceDamageTaken)
                return StatAffixGenType.PercentStat;
            if (s.Contains("Stack") || s.Contains("ExtraTargets") || s.Contains("MaxBleed") || s.Contains("MaxPoison") || s.Contains("MaxIgnite"))
                return StatAffixGenType.NOCalcStat;
            return StatAffixGenType.FullCalcStat;
        }

        public static StatValueUnit DefaultValueUnitFor(StatType type)
        {
            string s = type.ToString();
            if (type == StatType.MaxHealth || type == StatType.HealthRegen || type == StatType.HealthOnHit || type == StatType.HealthOnBlock)
                return StatValueUnit.HP;
            if (type == StatType.MaxMana || type == StatType.ManaRegen || type == StatType.ManaOnHit || type == StatType.ManaOnBlock)
                return StatValueUnit.MP;
            if (type == StatType.MaxMysticShield)
                return StatValueUnit.MysticShield;
            if (type == StatType.ShockDuration || type == StatType.FreezeDuration || type == StatType.BleedDuration ||
                type == StatType.PoisonDuration || type == StatType.IgniteDuration || type == StatType.MysticShieldRechargeDuration)
                return StatValueUnit.Seconds;
            if (type == StatType.MaxBleedStack)
                return StatValueUnit.Stacks;
            if (type == StatType.ExtraTargetsForMeleeHits || type == StatType.ProjectileCount || type == StatType.ProjectileFork || type == StatType.ProjectileChain)
                return StatValueUnit.Targets;
            if (DefaultFormatFor(type) == StatDisplayFormat.Percent)
                return StatValueUnit.Percent;
            if (s.Contains("Percent") || s.Contains("Chance") || s.Contains("Multiplier") || s.Contains("Resist") || s.Contains("Reduction"))
                return StatValueUnit.Percent;
            return StatValueUnit.Points;
        }

        public static bool DefaultDisplayAsPercentWhenFlat(StatType type)
        {
            return DefaultValueUnitFor(type) == StatValueUnit.Percent || DefaultFormatFor(type) == StatDisplayFormat.Percent;
        }

        public static bool DefaultAllowNegativeFlatGeneration(StatType type)
        {
            return type == StatType.CritMultiplier;
        }

        public static StatContextTagFlags DefaultContextTagsFor(StatType type)
        {
            switch (type)
            {
                case StatType.MeleeDamage:
                    return StatContextTagFlags.Melee;
                case StatType.ProjectileSpeed:
                case StatType.ProjectileCount:
                case StatType.ProjectileFork:
                case StatType.ProjectileChain:
                    return StatContextTagFlags.Projectile;
                case StatType.AreaOfEffect:
                    return StatContextTagFlags.Area;
                case StatType.BleedChance:
                case StatType.BleedDamage:
                case StatType.BleedDamageMult:
                case StatType.BleedDuration:
                case StatType.PoisonChance:
                case StatType.PoisonDamage:
                case StatType.PoisonDamageMult:
                case StatType.PoisonDuration:
                case StatType.IgniteChance:
                case StatType.IgniteDamage:
                case StatType.IgniteDamageMult:
                case StatType.IgniteDuration:
                case StatType.FreezeChance:
                case StatType.FreezeDuration:
                case StatType.ShockChance:
                case StatType.ShockDuration:
                    return StatContextTagFlags.Ailment;
                default:
                    return StatContextTagFlags.None;
            }
        }

        public static StatDamageChannelFlags DefaultDamageChannelsFor(StatType type)
        {
            switch (type)
            {
                case StatType.DamagePhysical:
                case StatType.PhysicalResist:
                case StatType.MaxPhysicalResist:
                case StatType.PenetrationPhysical:
                case StatType.BleedChance:
                case StatType.BleedDamage:
                case StatType.BleedDamageMult:
                case StatType.BleedDuration:
                case StatType.ChanseToAvoidBleed:
                    return StatDamageChannelFlags.Physical;

                case StatType.DamageFire:
                case StatType.FireResist:
                case StatType.MaxFireResist:
                case StatType.PenetrationFire:
                case StatType.IgniteChance:
                case StatType.IgniteDamage:
                case StatType.IgniteDamageMult:
                case StatType.IgniteDuration:
                case StatType.ChanseToAvoidIgnite:
                    return StatDamageChannelFlags.Fire;

                case StatType.DamageCold:
                case StatType.ColdResist:
                case StatType.MaxColdResist:
                case StatType.PenetrationCold:
                case StatType.FreezeChance:
                case StatType.FreezeDuration:
                case StatType.ChanseToAvoidFreeze:
                    return StatDamageChannelFlags.Cold;

                case StatType.DamageLightning:
                case StatType.LightningResist:
                case StatType.MaxLightningResist:
                case StatType.PenetrationLightning:
                case StatType.ShockChance:
                case StatType.ShockDuration:
                case StatType.ChanseToAvoidShock:
                    return StatDamageChannelFlags.Lightning;

                case StatType.PhysicalToFire:
                case StatType.TakePhysAsFire:
                case StatType.TakeFireAsPhys:
                    return StatDamageChannelFlags.Physical | StatDamageChannelFlags.Fire;

                case StatType.PhysicalToCold:
                case StatType.TakePhysAsCold:
                case StatType.TakeColdAsPhys:
                    return StatDamageChannelFlags.Physical | StatDamageChannelFlags.Cold;

                case StatType.PhysicalToLightning:
                case StatType.TakePhysAsLightning:
                case StatType.TakeLightningAsPhys:
                    return StatDamageChannelFlags.Physical | StatDamageChannelFlags.Lightning;

                case StatType.TakeLightningAsFire:
                case StatType.TakeFireAsLightning:
                    return StatDamageChannelFlags.Lightning | StatDamageChannelFlags.Fire;

                case StatType.TakeLightningAsCold:
                case StatType.TakeColdAsLightning:
                    return StatDamageChannelFlags.Lightning | StatDamageChannelFlags.Cold;

                case StatType.TakeColdAsFire:
                case StatType.TakeFireAsCold:
                    return StatDamageChannelFlags.Cold | StatDamageChannelFlags.Fire;

                case StatType.ElementalToPhysical:
                    return StatDamageChannelFlags.All;

                case StatType.MeleeDamage:
                    return StatDamageChannelFlags.All;

                default:
                    return StatDamageChannelFlags.None;
            }
        }

        public static StatAffixModifierKindFlags DefaultAllowedAffixKindsFor(StatType type, StatAffixGenType genType)
        {
            if (type == StatType.CritMultiplier)
                return StatAffixModifierKindFlags.Flat;

            switch (genType)
            {
                case StatAffixGenType.NOCalcStat:
                    return StatAffixModifierKindFlags.Flat;
                case StatAffixGenType.PercentStat:
                    return StatAffixModifierKindFlags.Flat | StatAffixModifierKindFlags.Increase | StatAffixModifierKindFlags.Decrease;
                case StatAffixGenType.ContextModifierStat:
                    return StatAffixModifierKindFlags.Increase | StatAffixModifierKindFlags.Decrease | StatAffixModifierKindFlags.More | StatAffixModifierKindFlags.Less;
                default:
                    return StatAffixModifierKindFlags.Full;
            }
        }

        public static StatAffixModifierKindFlags NormalizeAllowedAffixKinds(StatAffixModifierKindFlags flags, StatAffixGenType genType, StatType type)
        {
            if (flags == StatAffixModifierKindFlags.None)
                flags = DefaultAllowedAffixKindsFor(type, genType);

            switch (genType)
            {
                case StatAffixGenType.NOCalcStat:
                    return StatAffixModifierKindFlags.Flat;
                case StatAffixGenType.PercentStat:
                    return flags & (StatAffixModifierKindFlags.Flat | StatAffixModifierKindFlags.Increase | StatAffixModifierKindFlags.Decrease);
                case StatAffixGenType.ContextModifierStat:
                    return flags & (StatAffixModifierKindFlags.Increase | StatAffixModifierKindFlags.Decrease | StatAffixModifierKindFlags.More | StatAffixModifierKindFlags.Less);
                default:
                    return flags;
            }
        }
#endif
    }
}
