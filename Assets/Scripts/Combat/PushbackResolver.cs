using Scripts.Stats;
using UnityEngine;

namespace Scripts.Combat
{
    /// <summary>
    /// Converts Pushback rating into horizontal knockback.
    /// Rating is abstract: 200 is a small nudge, 1000 is clearly noticeable.
    /// Direction is left/right from the hit origin; a center overlap skips knockback.
    /// </summary>
    public static class PushbackResolver
    {
        public const float WorldUnitsPerRating = 0.002f;
        public const float DurationSeconds = 0.16f;
        public const float MinCenterDeadZone = 0.12f;
        public const float CenterDeadZoneWidthFraction = 0.12f;

        public static float RatingToDistance(float rating)
        {
            return Mathf.Max(0f, rating) * WorldUnitsPerRating;
        }

        public static float RatingToInitialVelocity(float rating)
        {
            float distance = RatingToDistance(rating);
            if (distance <= 0f)
                return 0f;

            // Linear decay: average velocity is half of the initial impulse.
            return (2f * distance) / DurationSeconds;
        }

        public static float ResolveCenterDeadZone(float targetWidth)
        {
            return Mathf.Max(MinCenterDeadZone, Mathf.Abs(targetWidth) * CenterDeadZoneWidthFraction);
        }

        public static int ResolveHorizontalSign(float hitOriginX, float targetCenterX, float targetWidth)
        {
            float dx = targetCenterX - hitOriginX;
            float deadZone = ResolveCenterDeadZone(targetWidth);
            if (Mathf.Abs(dx) <= deadZone)
                return 0;

            return dx > 0f ? 1 : -1;
        }

        public static float ResolveRating(bool skillEnabled, IStatsProvider scopedStats)
        {
            if (!skillEnabled)
                return 0f;

            return scopedStats != null ? Mathf.Max(0f, scopedStats.GetValue(StatType.Pushback)) : 0f;
        }

        public static void BindToSnapshot(
            DamageSnapshot snapshot,
            bool skillEnabled,
            IStatsProvider scopedStats,
            Vector2 hitOrigin)
        {
            if (snapshot == null)
                return;

            snapshot.HitOrigin = hitOrigin;
            snapshot.PushbackRating = ResolveRating(skillEnabled, scopedStats);
        }

        /// <summary>
        /// PushbackResist is percent reduction. 0 = full knockback, 50 = half, 100 = none.
        /// </summary>
        public static float ApplyResistance(float rating, float resistPercent)
        {
            if (rating <= 0f)
                return 0f;

            float multiplier = 1f - Mathf.Clamp(resistPercent, 0f, 100f) / 100f;
            return rating * multiplier;
        }
    }
}
