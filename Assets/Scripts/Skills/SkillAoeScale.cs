using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>
    /// Area-of-effect scale is full on X and half as strong on Y, so spells grow sideways
    /// more than they stretch up and down.
    /// </summary>
    public static class SkillAoeScale
    {
        public const float VerticalContribution = 0.5f;

        public static float AxisMultiplierOrDefault(float value, float fallback = 1f)
        {
            return value <= 0.001f ? fallback : Mathf.Max(0.01f, value);
        }

        public static Vector2 FromAoe(float aoeScale, float horizontalMultiplier = 1f, float verticalMultiplier = 1f)
        {
            aoeScale = Mathf.Max(0.01f, aoeScale);
            horizontalMultiplier = Mathf.Max(0.01f, horizontalMultiplier);
            verticalMultiplier = Mathf.Max(0.01f, verticalMultiplier);

            float extra = aoeScale - 1f;
            float horizontal = (1f + extra) * horizontalMultiplier;
            float vertical = (1f + extra * VerticalContribution) * verticalMultiplier;
            return new Vector2(Mathf.Max(0.01f, horizontal), Mathf.Max(0.01f, vertical));
        }

        public static Vector2 ScaleSize(Vector2 baseSize, float aoeScale, float horizontalMultiplier = 1f, float verticalMultiplier = 1f)
        {
            Vector2 factors = FromAoe(aoeScale, horizontalMultiplier, verticalMultiplier);
            return new Vector2(baseSize.x * factors.x, baseSize.y * factors.y);
        }

        public static Vector3 ApplyToLocalScale(
            Vector3 localScale,
            float facingSign,
            float aoeScale,
            float horizontalMultiplier = 1f,
            float verticalMultiplier = 1f)
        {
            Vector2 factors = FromAoe(aoeScale, horizontalMultiplier, verticalMultiplier);
            return new Vector3(
                Mathf.Abs(localScale.x) * facingSign * factors.x,
                Mathf.Abs(localScale.y) * factors.y,
                localScale.z);
        }
    }
}
