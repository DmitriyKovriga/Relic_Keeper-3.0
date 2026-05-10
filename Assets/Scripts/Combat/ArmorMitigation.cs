using UnityEngine;
using Scripts.Stats;

namespace Scripts.Combat
{
    public static class ArmorMitigation
    {
        private const float DefaultPhysicalResistCap = 90f;

        private static readonly Vector2[] ArmorToResistCurve =
        {
            new Vector2(0f, 0f),
            new Vector2(500f, 50f),
            new Vector2(1000f, 60f),
            new Vector2(2000f, 70f),
            new Vector2(5000f, 80f),
            new Vector2(10000f, 90f)
        };

        public static float ArmorToPhysicalResist(float armor)
        {
            if (armor <= 0f)
                return 0f;

            for (int i = 1; i < ArmorToResistCurve.Length; i++)
            {
                Vector2 previous = ArmorToResistCurve[i - 1];
                Vector2 current = ArmorToResistCurve[i];
                if (armor <= current.x)
                {
                    float t = Mathf.InverseLerp(previous.x, current.x, armor);
                    return Mathf.Lerp(previous.y, current.y, t);
                }
            }

            return DefaultPhysicalResistCap;
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
