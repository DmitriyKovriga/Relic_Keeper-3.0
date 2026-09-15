using UnityEngine;
using Scripts.Stats;

namespace Scripts.Combat
{
    public static class ArmorMitigation
    {
        private const float DefaultPhysicalResistCap = DefenseRatingCurve.CapPercent;

        public static float ArmorToPhysicalResist(float armor)
        {
            return DefenseRatingCurve.ToPercent(armor);
        }

        public static float ResolveTotalPhysicalResist(IStatsProvider statsProvider, out float armor, out float armorResist, out float statResist, out float cap)
        {
            armor = 0f;
            armorResist = 0f;
            statResist = 0f;
            cap = DefaultPhysicalResistCap;

            if (statsProvider == null)
                return 0f;

            armor = Mathf.Max(0f, statsProvider.GetValue(StatType.Armor));
            armorResist = ArmorToPhysicalResist(armor);
            statResist = statsProvider.GetValue(StatType.PhysicalResist);

            float configuredCap = statsProvider.GetValue(StatType.MaxPhysicalResist);
            if (configuredCap > 0f)
                cap = Mathf.Min(DefaultPhysicalResistCap, configuredCap);

            return Mathf.Clamp(armorResist + statResist, -200f, cap);
        }
    }
}
