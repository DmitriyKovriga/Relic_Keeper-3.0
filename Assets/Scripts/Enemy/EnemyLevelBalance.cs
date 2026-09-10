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
    }
}
