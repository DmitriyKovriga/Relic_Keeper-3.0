using Scripts.Stats;
using Scripts.Skills.Steps;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Skills
{
    public static class MysticShieldSkillEffectiveness
    {
        public static bool UsesShield(SkillDataSO skill)
        {
            if (skill == null) return false;
            if (skill.StatLinks != null)
                foreach (var link in skill.StatLinks)
                    if (link.SourceStat == StatType.MaxMysticShield) return true;
            return UsesShield(skill.Recipe?.Steps);
        }

        private static bool UsesShield(IReadOnlyList<StepEntry> steps)
        {
            if (steps == null) return false;
            foreach (var step in steps)
            {
                if (step == null) continue;
                string id = step.StepDefinition != null ? step.StepDefinition.Id : null;
                if ((!string.IsNullOrEmpty(id) && id.Contains("MysticShield")) || id == "FrostWall") return true;
                if (UsesShield(step.SubSteps)) return true;
            }
            return false;
        }

        public static float Resolve(IStatsProvider stats)
        {
            return stats != null && stats.TryGetStat(StatType.MysticShieldSkillEffectiveness, out CharacterStat stat)
                ? Mathf.Max(0f, stat.Value)
                : 1f;
        }
    }
}
