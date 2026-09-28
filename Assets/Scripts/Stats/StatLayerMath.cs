using System;

namespace Scripts.Stats
{
    /// <summary>
    /// Reads flat, increased and more layers without copying <see cref="CharacterStat"/>.
    /// Hit modifiers implement this and sit on top of the attacker.
    /// </summary>
    public interface IStatLayerSource
    {
        bool TryGetLayers(StatType type, out float flat, out float additivePercent, out float multiplicativeFactor);
    }

    public static class StatLayerMath
    {
        public static void Read(CharacterStat stat, out float flat, out float additivePercent, out float multiplicativeFactor)
        {
            flat = stat != null ? stat.GetRawFlatValue() : 0f;
            additivePercent = stat != null ? stat.GetTotalPercentAdd() : 0f;
            multiplicativeFactor = stat != null ? stat.GetTotalMultiplier() : 1f;
        }

        public static void Apply(StatModType type, float value, ref float flat, ref float additivePercent, ref float multiplicativeFactor)
        {
            if (type == StatModType.Flat)
                flat += value;
            else if (type.IsAdditivePercent())
                additivePercent += type.ToSignedPercent(value);
            else if (type.IsMultiplicativePercent())
                multiplicativeFactor *= type.ToMultiplierFactor(value);
        }

        public static float Evaluate(float flat, float additivePercent, float multiplicativeFactor)
        {
            float additiveFactor = Math.Max(0f, 1f + (additivePercent / 100f));
            return (float)Math.Round(flat * additiveFactor * multiplicativeFactor, 4);
        }
    }
}
