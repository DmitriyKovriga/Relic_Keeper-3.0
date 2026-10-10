using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills
{
    public static class MysticShieldSkillEffectiveness
    {
        public static float Resolve(IStatsProvider stats)
        {
            return stats != null && stats.TryGetStat(StatType.MysticShieldSkillEffectiveness, out CharacterStat stat)
                ? Mathf.Max(0f, stat.Value)
                : 1f;
        }
    }
}
