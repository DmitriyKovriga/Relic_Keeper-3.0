using UnityEngine;

namespace Scripts.Combat
{
    /// <summary>
    /// Shared diminishing curve for armor mitigation and evasion chance.
    /// 10 000 rating reaches the 90% cap.
    /// </summary>
    public static class DefenseRatingCurve
    {
        public const float CapPercent = 90f;

        private static readonly Vector2[] RatingToPercent =
        {
            new Vector2(0f, 0f),
            new Vector2(500f, 50f),
            new Vector2(1000f, 60f),
            new Vector2(2000f, 70f),
            new Vector2(5000f, 80f),
            new Vector2(10000f, 90f)
        };

        public static float ToPercent(float rating)
        {
            if (rating <= 0f)
                return 0f;

            for (int i = 1; i < RatingToPercent.Length; i++)
            {
                Vector2 previous = RatingToPercent[i - 1];
                Vector2 current = RatingToPercent[i];
                if (rating <= current.x)
                {
                    float t = Mathf.InverseLerp(previous.x, current.x, rating);
                    return Mathf.Lerp(previous.y, current.y, t);
                }
            }

            return CapPercent;
        }
    }
}
