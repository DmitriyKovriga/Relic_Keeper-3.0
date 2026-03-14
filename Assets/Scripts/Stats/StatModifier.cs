using System;

namespace Scripts.Stats
{
    public enum StatModType
    {
        Flat = 100,
        PercentAdd = 200,
        PercentSub = 210,
        PercentMult = 300,
        PercentLess = 310
    }

    public static class StatModTypeExtensions
    {
        public static bool IsAdditivePercent(this StatModType type)
        {
            return type == StatModType.PercentAdd || type == StatModType.PercentSub;
        }

        public static bool IsMultiplicativePercent(this StatModType type)
        {
            return type == StatModType.PercentMult || type == StatModType.PercentLess;
        }

        public static float ToSignedPercent(this StatModType type, float value)
        {
            switch (type)
            {
                case StatModType.PercentSub:
                case StatModType.PercentLess:
                    return -value;
                default:
                    return value;
            }
        }

        public static float ToMultiplierFactor(this StatModType type, float value)
        {
            if (!type.IsMultiplicativePercent())
                return 1f;

            float signedPercent = type.ToSignedPercent(value);
            return Math.Max(0f, 1f + (signedPercent / 100f));
        }

        public static string GetDisplayPrefix(this StatModType type, float value)
        {
            if (type == StatModType.Flat)
                return value >= 0f ? "+" : string.Empty;

            if (type == StatModType.PercentSub || type == StatModType.PercentLess)
                return "-";

            return "+";
        }
    }

    [Serializable]
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModType Type;
        public readonly int Order;
        public readonly object Source;

        public StatModifier(float value, StatModType type, int order, object source)
        {
            Value = value;
            Type = type;
            Order = order;
            Source = source;
        }

        public StatModifier(float value, StatModType type) : this(value, type, (int)type, null) { }
        public StatModifier(float value, StatModType type, int order) : this(value, type, order, null) { }
        public StatModifier(float value, StatModType type, object source) : this(value, type, (int)type, source) { }
    }
}
