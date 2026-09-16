using Scripts.Items;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Items.Affixes
{
    /// <summary>
    /// Magnitude curves for item affixes. Tier 1 is weakest, tier 5 is strongest.
    /// Curves are Medium-strength; Light/Strong scale them. Local weapon % damage
    /// is calibrated so Strong T1 is 20–30% and Strong T5 is 100–120%.
    /// </summary>
    public static class AffixValueBalance
    {
        public const string StrengthStrong = "Strong";
        public const string StrengthMedium = "Medium";
        public const string StrengthLight = "Light";

        public readonly struct Roll
        {
            public readonly float Min;
            public readonly float Max;
            public readonly float RangeMin;
            public readonly float RangeMax;
            public readonly bool HasSecondary;

            public Roll(float min, float max, float rangeMin = 0f, float rangeMax = 0f, bool hasSecondary = false)
            {
                Min = min;
                Max = max;
                RangeMin = rangeMin;
                RangeMax = rangeMax;
                HasSecondary = hasSecondary;
            }
        }

        public static void Apply(ref ItemAffixSO.AffixStatData data, StatType stat, StatAffixModifierKind kind, string strength, int tier)
        {
            Roll roll = GetRoll(stat, kind, strength, tier, data.UsesRangeRoll(), data.Scope);
            data.MinValue = roll.Min;
            data.MaxValue = roll.Max;
            if (roll.HasSecondary)
            {
                data.RangeMinValue = roll.RangeMin;
                data.RangeMaxValue = roll.RangeMax;
            }
            else if (!data.UsesRangeRoll())
            {
                data.RangeMinValue = 0f;
                data.RangeMaxValue = 0f;
            }
        }

        public static Roll GetRoll(
            StatType stat,
            StatAffixModifierKind kind,
            string strength,
            int tier,
            bool includeSecondary = false,
            StatScope scope = StatScope.Global)
        {
            tier = Mathf.Clamp(tier, 1, 5);
            float strengthMul = StrengthMultiplier(strength);

            if (kind != StatAffixModifierKind.Flat)
                return FromCurve(GetPercentCurve(stat, kind, scope), strengthMul, tier, 0);

            if (IsIntegerToken(stat))
                return new Roll(1f, 1f);

            if (IsMaxResist(stat))
                return MaxResistRoll(strength, tier);

            if (stat == StatType.MaxMysticShield)
                return MaxShieldLayersRoll(strength, tier);

            if (includeSecondary)
            {
                Roll minRoll = FromCurve(GetFlatCurve(stat), strengthMul, tier, GetDecimals(stat));
                Roll maxRoll = FromCurve(GetAddedDamageMaxCurve(stat), strengthMul, tier, GetDecimals(stat));
                return new Roll(minRoll.Min, minRoll.Max, maxRoll.Min, maxRoll.Max, true);
            }

            return FromCurve(GetFlatCurve(stat), strengthMul, tier, GetDecimals(stat));
        }

        public static string ResolveStrength(string groupIdOrName)
        {
            if (string.IsNullOrEmpty(groupIdOrName))
                return StrengthMedium;
            if (groupIdOrName.IndexOf(StrengthStrong, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return StrengthStrong;
            if (groupIdOrName.IndexOf(StrengthLight, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return StrengthLight;
            return StrengthMedium;
        }

        public static float RollValue(float min, float max, StatType stat)
        {
            if (max < min)
                (min, max) = (max, min);

            float value = Mathf.Approximately(min, max)
                ? min
                : Random.Range(min, max);
            return QuantizeRolled(value, min, max, GetDecimals(stat));
        }

        public static float QuantizeRolled(float value, float min, float max, int decimals)
        {
            if (max < min)
                (min, max) = (max, min);

            float quantized = RoundSigned(value, decimals);
            float qMin = RoundSigned(min, decimals);
            float qMax = RoundSigned(max, decimals);
            if (qMax < qMin)
                qMax = qMin;
            return Mathf.Clamp(quantized, qMin, qMax);
        }

        private static float StrengthMultiplier(string strength)
        {
            if (string.Equals(strength, StrengthStrong, System.StringComparison.OrdinalIgnoreCase))
                return 1.25f;
            if (string.Equals(strength, StrengthLight, System.StringComparison.OrdinalIgnoreCase))
                return 0.75f;
            return 1f;
        }

        private static Roll FromCurve((float t1min, float t1max, float t5min, float t5max) curve, float strengthMul, int tier, int decimals)
        {
            float t = (tier - 1) / 4f;
            float min = RoundTo(Mathf.Lerp(curve.t1min, curve.t5min, t) * strengthMul, decimals);
            float max = RoundTo(Mathf.Lerp(curve.t1max, curve.t5max, t) * strengthMul, decimals);
            if (max < min)
                max = min;
            if (decimals == 0 && min < 1f && max < 1f)
            {
                min = 1f;
                max = 1f;
            }
            return new Roll(min, max);
        }

        private static float RoundTo(float value, int decimals)
        {
            return Mathf.Max(0f, RoundSigned(value, decimals));
        }

        private static float RoundSigned(float value, int decimals)
        {
            if (decimals <= 0)
                return Mathf.Round(value);
            float scale = Mathf.Pow(10f, decimals);
            return Mathf.Round(value * scale) / scale;
        }

        private static Roll MaxResistRoll(string strength, int tier)
        {
            bool high = tier == 5 && string.Equals(strength, StrengthStrong, System.StringComparison.OrdinalIgnoreCase);
            return high ? new Roll(1f, 2f) : new Roll(1f, 1f);
        }

        private static Roll MaxShieldLayersRoll(string strength, int tier)
        {
            if (tier >= 4 && string.Equals(strength, StrengthStrong, System.StringComparison.OrdinalIgnoreCase))
                return new Roll(1f, 2f);
            return new Roll(1f, 1f);
        }

        private static (float t1min, float t1max, float t5min, float t5max) GetPercentCurve(
            StatType stat,
            StatAffixModifierKind kind,
            StatScope scope)
        {
            bool more = kind == StatAffixModifierKind.More || kind == StatAffixModifierKind.Less;
            if (more)
                return GetMoreCurve(stat);

            if (IsWeaponDamageStat(stat))
                return scope == StatScope.Local
                    ? (16f, 24f, 80f, 96f)
                    : (8f, 12f, 32f, 44f);

            switch (stat)
            {
                case StatType.AttackSpeed:
                    return scope == StatScope.Local
                        ? (6f, 8f, 18f, 24f)
                        : (4f, 6f, 12f, 16f);
                case StatType.MeleeDamage:
                case StatType.SpellDamage:
                    return (8f, 12f, 32f, 44f);
                case StatType.Armor:
                case StatType.Evasion:
                    return scope == StatScope.Local
                        ? (12f, 18f, 60f, 85f)
                        : (6f, 10f, 22f, 32f);
                case StatType.MaxHealth:
                case StatType.MaxMana:
                    return (4f, 6f, 12f, 18f);
                case StatType.MoveSpeed:
                    return (4f, 6f, 16f, 24f);
                case StatType.CritChance:
                    return scope == StatScope.Local
                        ? (8f, 12f, 28f, 38f)
                        : (6f, 10f, 18f, 26f);
                case StatType.AreaOfEffect:
                    return (8f, 12f, 28f, 40f);
                case StatType.EffectDuration:
                case StatType.ProjectileSpeed:
                    return (8f, 12f, 24f, 36f);
                case StatType.CooldownReductionPercent:
                case StatType.DamageTaken:
                    return (4f, 6f, 14f, 20f);
                case StatType.BleedDamageMult:
                case StatType.PoisonDamageMult:
                case StatType.IgniteDamageMult:
                    return (8f, 12f, 28f, 42f);
                default:
                    if (IsConversion(stat))
                        return (8f, 12f, 24f, 36f);
                    return (6f, 10f, 22f, 34f);
            }
        }

        private static (float t1min, float t1max, float t5min, float t5max) GetMoreCurve(StatType stat)
        {
            if (IsWeaponDamageStat(stat) || stat == StatType.MeleeDamage || stat == StatType.SpellDamage)
                return (5f, 8f, 16f, 24f);
            if (stat == StatType.AttackSpeed)
                return (3f, 5f, 10f, 14f);
            return (3f, 5f, 10f, 16f);
        }

        private static (float t1min, float t1max, float t5min, float t5max) GetAddedDamageMaxCurve(StatType stat)
        {
            if (stat == StatType.BleedDamage || stat == StatType.PoisonDamage || stat == StatType.IgniteDamage)
                return (2f, 4f, 12f, 20f);
            if (IsAddedDamage(stat))
                return (2f, 4f, 16f, 24f);
            return GetFlatCurve(stat);
        }

        private static (float t1min, float t1max, float t5min, float t5max) GetFlatCurve(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth:
                    return (10f, 16f, 50f, 75f);
                case StatType.MaxMana:
                    return (8f, 12f, 36f, 54f);
                case StatType.HealthRegen:
                case StatType.ManaRegen:
                    return (0.4f, 0.7f, 2.4f, 4.0f);
                case StatType.HealthRegenPercent:
                case StatType.ManaRegenPercent:
                    return (0.2f, 0.4f, 1.0f, 1.8f);
                case StatType.HealthOnHit:
                case StatType.ManaOnHit:
                case StatType.HealthOnBlock:
                case StatType.ManaOnBlock:
                    return (1f, 2f, 6f, 10f);
                case StatType.Armor:
                case StatType.Evasion:
                    return (20f, 40f, 160f, 240f);
                case StatType.FireResist:
                case StatType.ColdResist:
                case StatType.LightningResist:
                case StatType.PhysicalResist:
                    return (8f, 12f, 36f, 48f);
                case StatType.CritChance:
                    return (1f, 2f, 8f, 12f);
                case StatType.CritMultiplier:
                    return (10f, 15f, 32f, 45f);
                case StatType.MoveSpeed:
                    return (0.4f, 0.6f, 1.4f, 2.0f);
                case StatType.JumpForce:
                    return (0.3f, 0.5f, 1.0f, 1.5f);
                case StatType.BlockChance:
                case StatType.MaxBlockChance:
                    return (2f, 3f, 8f, 14f);
                case StatType.PenetrationPhysical:
                case StatType.PenetrationFire:
                case StatType.PenetrationCold:
                case StatType.PenetrationLightning:
                    return (4f, 6f, 14f, 22f);
                case StatType.BleedChance:
                case StatType.PoisonChance:
                case StatType.IgniteChance:
                case StatType.FreezeChance:
                case StatType.ShockChance:
                case StatType.ChanseToAvoidBleed:
                case StatType.ChanseToAvoidPoison:
                case StatType.ChanseToAvoidIgnite:
                case StatType.ChanseToAvoidFreeze:
                case StatType.ChanseToAvoidShock:
                    return (6f, 10f, 24f, 36f);
                case StatType.BleedDamageMult:
                case StatType.PoisonDamageMult:
                case StatType.IgniteDamageMult:
                    return (8f, 12f, 28f, 42f);
                case StatType.BleedDuration:
                case StatType.PoisonDuration:
                case StatType.IgniteDuration:
                case StatType.FreezeDuration:
                case StatType.ShockDuration:
                case StatType.StunDuration:
                    return (0.3f, 0.5f, 1.4f, 2.2f);
                case StatType.MysticShieldRechargeDuration:
                    return (0.2f, 0.4f, 1.0f, 1.6f);
                case StatType.SkillCooldownRecovery:
                    return (0.05f, 0.1f, 0.4f, 0.7f);
                case StatType.SpecialSkillCooldownRecovery:
                case StatType.HelmetSkillCooldownRecovery:
                case StatType.BodyArmorSkillCooldownRecovery:
                case StatType.GlovesSkillCooldownRecovery:
                case StatType.BootsSkillCooldownRecovery:
                    return (0.1f, 0.2f, 0.6f, 1.0f);
                case StatType.BleedDamage:
                case StatType.PoisonDamage:
                case StatType.IgniteDamage:
                    return (1f, 2f, 6f, 10f);
                case StatType.DamagePhysical:
                case StatType.DamageFire:
                case StatType.DamageCold:
                case StatType.DamageLightning:
                    return (1f, 2f, 8f, 12f);
                case StatType.MysticShieldMitigationPercent:
                case StatType.MaxMysticShieldMitigationPercent:
                    return (1f, 2f, 4f, 7f);
                case StatType.AreaOfEffect:
                    return (6f, 10f, 24f, 36f);
                case StatType.EffectDuration:
                case StatType.ProjectileSpeed:
                    return (6f, 10f, 22f, 32f);
                case StatType.CooldownReductionPercent:
                case StatType.DamageTaken:
                    return (4f, 6f, 14f, 20f);
                case StatType.StunBuildUp:
                    return (5f, 8f, 18f, 28f);
                case StatType.Pushback:
                    return (20f, 35f, 70f, 120f);
                case StatType.PushbackResist:
                    return (4f, 6f, 14f, 22f);
                case StatType.StunThreshold:
                    return (8f, 12f, 28f, 42f);
                case StatType.MeleeDamage:
                case StatType.SpellDamage:
                    return (5f, 8f, 22f, 32f);
                default:
                    if (IsConversion(stat))
                        return (8f, 12f, 24f, 36f);
                    return (5f, 8f, 20f, 30f);
            }
        }

        private static int GetDecimals(StatType stat)
        {
            switch (stat)
            {
                case StatType.HealthRegen:
                case StatType.ManaRegen:
                case StatType.HealthRegenPercent:
                case StatType.ManaRegenPercent:
                case StatType.MoveSpeed:
                case StatType.JumpForce:
                case StatType.BleedDuration:
                case StatType.PoisonDuration:
                case StatType.IgniteDuration:
                case StatType.FreezeDuration:
                case StatType.ShockDuration:
                case StatType.StunDuration:
                case StatType.MysticShieldRechargeDuration:
                case StatType.SkillCooldownRecovery:
                case StatType.SpecialSkillCooldownRecovery:
                case StatType.HelmetSkillCooldownRecovery:
                case StatType.BodyArmorSkillCooldownRecovery:
                case StatType.GlovesSkillCooldownRecovery:
                case StatType.BootsSkillCooldownRecovery:
                    return 1;
                default:
                    return 0;
            }
        }

        private static bool IsIntegerToken(StatType stat)
        {
            switch (stat)
            {
                case StatType.ProjectilePierce:
                case StatType.ProjectileFork:
                case StatType.ProjectileChain:
                case StatType.ProjectileCount:
                case StatType.ExtraTargetsForMeleeHits:
                case StatType.MaxBleedStack:
                case StatType.MaxIgniteStacks:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMaxResist(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxFireResist:
                case StatType.MaxColdResist:
                case StatType.MaxLightningResist:
                case StatType.MaxPhysicalResist:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsAddedDamage(StatType stat)
        {
            switch (stat)
            {
                case StatType.DamagePhysical:
                case StatType.DamageFire:
                case StatType.DamageCold:
                case StatType.DamageLightning:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsWeaponDamageStat(StatType stat)
        {
            return IsAddedDamage(stat) ||
                   stat == StatType.BleedDamage ||
                   stat == StatType.PoisonDamage ||
                   stat == StatType.IgniteDamage;
        }

        private static bool IsConversion(StatType stat)
        {
            string name = stat.ToString();
            return name.Contains("To") || name.Contains("Take");
        }
    }
}
