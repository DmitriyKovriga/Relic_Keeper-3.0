using Scripts.Stats;
using UnityEngine;

namespace Scripts.Enemies
{
    /// <summary>
    /// Target combat scaling versus a level-30 Warrior.
    /// Level itself does not raise player combat stats; power comes from ~29 passive points
    /// (Flat/Increased only) plus gear. A realistic passive path is about 4–8× DPS and 4–8× EHP
    /// versus a fresh character; a dedicated life path can push EHP higher, a glass-cannon path lower.
    /// Damage at ~26%/level (~8.5× at 30) keeps packs threatening. Health at ~22%/level (~7.4×)
    /// avoids sponges against that DPS. Armor trails slightly so physical hits still matter.
    /// After the soft cap, remaining levels use half the listed rate, except damage which uses a quarter.
    /// Move and attack speed are a hidden Increased modifier: 1%/level to 30, then 0.2%/level.
    /// XPReward is the real level-one reward. Enemy level and dungeon modifiers
    /// are applied from a neutral multiplier of 1.
    /// </summary>
    public static class EnemyLevelBalance
    {
        public const int ReferenceLevel = 30;
        public const float HealthPercentPerLevel = 22f;
        public const float DamagePercentPerLevel = 26f;
        public const float DefensePercentPerLevel = 18f;
        public const float EnemyCountPercentPerLocationLevel = 2f;
        public const float PostSoftCapStatScale = 0.5f;
        public const float PostSoftCapDamageScale = 0.25f;
        public const float TempoPercentPerLevel = 1f;
        public const float PostSoftCapTempoPercentPerLevel = 0.2f;

        public static bool IsDamageStat(StatType type)
        {
            return type == StatType.DamagePhysical ||
                   type == StatType.DamageFire ||
                   type == StatType.DamageCold ||
                   type == StatType.DamageLightning;
        }

        public static float ScaledLevelSteps(int level, bool isDamage)
        {
            int clampedLevel = Mathf.Max(1, level);
            int beforeSoftCap = Mathf.Min(clampedLevel, ReferenceLevel) - 1;
            int afterSoftCap = Mathf.Max(0, clampedLevel - ReferenceLevel);
            float afterWeight = isDamage ? PostSoftCapDamageScale : PostSoftCapStatScale;
            return beforeSoftCap + afterSoftCap * afterWeight;
        }

        public static float PercentMultiplier(int level, float percentPerLevel)
        {
            return PercentMultiplier(level, percentPerLevel, isDamage: false);
        }

        public static float PercentMultiplier(int level, float percentPerLevel, bool isDamage)
        {
            return 1f + (percentPerLevel / 100f) * ScaledLevelSteps(level, isDamage);
        }

        public static float TempoPercent(int level)
        {
            int clampedLevel = Mathf.Max(1, level);
            int beforeSoftCap = Mathf.Min(clampedLevel, ReferenceLevel) - 1;
            int afterSoftCap = Mathf.Max(0, clampedLevel - ReferenceLevel);
            return TempoPercentPerLevel * beforeSoftCap + PostSoftCapTempoPercentPerLevel * afterSoftCap;
        }

        public static float TempoMultiplier(int level)
        {
            return 1f + TempoPercent(level) / 100f;
        }

        public static float ScaleDurationByActionSpeed(float duration, float actionSpeed)
        {
            return duration / Mathf.Max(0.01f, actionSpeed);
        }

        public static float ResolveExperienceReward(EnemyDataSO data, int level, float dungeonMultiplier, bool isTrainingDummy)
        {
            if (isTrainingDummy || data == null || data.XPReward <= 0f)
                return 0f;

            float growthPercent = data.LegacyGrowthPerLevelPercent;
            float dungeonScale = Mathf.Max(0f, dungeonMultiplier);
            return data.XPReward * PercentMultiplier(level, growthPercent) * dungeonScale;
        }

        public const int GoldCapPerKill = 1000;
        public const float KnightReferenceXp = 15f;

        public static float RecommendedBaseGold(float xpReward, float growthPercent = 25f)
        {
            float xpAtCap = KnightReferenceXp * PercentMultiplier(ReferenceLevel, growthPercent);
            if (xpAtCap <= 0f || xpReward <= 0f)
                return 0f;
            return xpReward * (GoldCapPerKill / xpAtCap);
        }

        public static int ResolveGoldReward(EnemyDataSO data, int level, float dungeonMultiplier, bool isTrainingDummy)
        {
            if (isTrainingDummy || data == null)
                return 0;

            float baseGold = data.GoldReward > 0f ? data.GoldReward : RecommendedBaseGold(data.XPReward, data.LegacyGrowthPerLevelPercent);
            if (baseGold <= 0f)
                return 0;

            float growthPercent = data.LegacyGrowthPerLevelPercent;
            float dungeonScale = Mathf.Max(0f, dungeonMultiplier);
            float amount = baseGold * PercentMultiplier(level, growthPercent) * dungeonScale;
            return Mathf.Clamp(Mathf.RoundToInt(amount), 0, GoldCapPerKill);
        }
    }
}
