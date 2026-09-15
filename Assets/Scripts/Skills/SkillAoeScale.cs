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

    /// <summary>
    /// Front melee boxes are authored on the VFX, so the near edge often sits
    /// a gap in front of the caster. Pulling extends that edge toward the owner
    /// without changing far reach.
    /// </summary>
    public static class SkillHitboxFit
    {
        public const string PullTowardOwnerKey = "PullTowardOwner";
        public const float OwnerOverlap = 0.2f;

        public static void PullTowardOwner(Vector2 ownerPosition, float facing, ref Vector2 center, ref Vector2 size)
        {
            if (size.x <= 0.0001f)
                return;

            float sign = facing >= 0f ? 1f : -1f;
            float half = size.x * 0.5f;
            float centerLocal = (center.x - ownerPosition.x) * sign;
            float inner = centerLocal - half;
            float extend = inner + OwnerOverlap;
            if (extend <= 0.001f)
                return;

            size.x += extend;
            center.x -= sign * (extend * 0.5f);
        }
    }
}
