using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>
    /// Resolves cooldown duration without collapsing recovery modifiers into a single scalar.
    /// Slot recovery is applied first, then the all-skills recovery stage.
    /// </summary>
    public static class SkillCooldownRecovery
    {
        public const int MainSkillSlot = 0;
        public const int SpecialSkillSlot = 1;
        public const int HelmetSkillSlot = 2;
        public const int BodyArmorSkillSlot = 3;
        public const int GlovesSkillSlot = 4;
        public const int BootsSkillSlot = 5;

        public static float Resolve(float baseCooldown, IStatsProvider stats, int skillSlot)
        {
            float duration = Mathf.Max(0f, baseCooldown);
            if (duration <= 0f || stats == null)
                return duration;

            if (TryGetSlotRecoveryStat(skillSlot, out StatType slotStat))
                duration = ApplyStage(duration, stats, slotStat);

            return ApplyStage(duration, stats, StatType.SkillCooldownRecovery);
        }

        public static bool TryGetSlotRecoveryStat(int skillSlot, out StatType stat)
        {
            switch (skillSlot)
            {
                case SpecialSkillSlot:
                    stat = StatType.SpecialSkillCooldownRecovery;
                    return true;
                case HelmetSkillSlot:
                    stat = StatType.HelmetSkillCooldownRecovery;
                    return true;
                case BodyArmorSkillSlot:
                    stat = StatType.BodyArmorSkillCooldownRecovery;
                    return true;
                case GlovesSkillSlot:
                    stat = StatType.GlovesSkillCooldownRecovery;
                    return true;
                case BootsSkillSlot:
                    stat = StatType.BootsSkillCooldownRecovery;
                    return true;
                default:
                    stat = default;
                    return false;
            }
        }

        private static float ApplyStage(float cooldown, IStatsProvider stats, StatType statType)
        {
            if (!stats.TryGetStat(statType, out CharacterStat stat) || stat == null)
                return cooldown;

            float flatSeconds = stat.GetRawFlatValue();
            float result = Mathf.Max(0f, cooldown - flatSeconds);
            float additiveRecovery = stat.GetTotalPercentAdd();
            result *= Mathf.Max(0f, 1f - additiveRecovery / 100f);

            for (int i = 0; i < stat.Modifiers.Count; i++)
            {
                StatModifier modifier = stat.Modifiers[i];
                switch (modifier.Type)
                {
                    case StatModType.PercentMult:
                        result *= Mathf.Max(0f, 1f - modifier.Value / 100f);
                        break;
                    case StatModType.PercentLess:
                        result *= Mathf.Max(0f, 1f + modifier.Value / 100f);
                        break;
                }
            }

            return Mathf.Max(0f, result);
        }
    }
}
