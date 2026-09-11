using UnityEngine;
using UnityEngine.UIElements;

namespace Scripts.Skills.PassiveTree
{
    /// <summary>
    /// Painter2D arcs close to 180° can collapse to a diameter or vanish.
    /// Split them into shorter clockwise sweeps so editor and runtime match.
    /// </summary>
    public static class PassiveOrbitArcDrawing
    {
        public const float MaxContinuousSweepDegrees = 170f;

        public static void NormalizeShortClockwise(ref float startAngle, ref float endAngle)
        {
            float sweep = Mathf.Repeat(endAngle - startAngle, 360f);
            if (sweep > 180f)
                (startAngle, endAngle) = (endAngle, startAngle);
        }

        public static float ShortClockwiseSweep(float startAngle, float endAngle)
        {
            NormalizeShortClockwise(ref startAngle, ref endAngle);
            return Mathf.Repeat(endAngle - startAngle, 360f);
        }

        public static bool ShouldDrawAsStraightChord(float startAngle, float endAngle)
        {
            return ShortClockwiseSweep(startAngle, endAngle) >= MaxContinuousSweepDegrees;
        }

        public static int SegmentCount(float startAngle, float endAngle, float maxSweep = MaxContinuousSweepDegrees)
        {
            float sweep = ShortClockwiseSweep(startAngle, endAngle);
            if (sweep <= 0.01f)
                return 0;

            maxSweep = Mathf.Max(1f, maxSweep);
            return Mathf.Max(1, Mathf.CeilToInt(sweep / maxSweep));
        }

        public static void StrokeClockwiseArc(Painter2D painter, Vector2 center, float radius, float startAngle, float endAngle)
        {
            if (painter == null)
                return;

            NormalizeShortClockwise(ref startAngle, ref endAngle);
            float sweep = Mathf.Repeat(endAngle - startAngle, 360f);
            if (sweep <= 0.01f)
                return;

            int steps = Mathf.Max(1, Mathf.CeilToInt(sweep / MaxContinuousSweepDegrees));
            float step = sweep / steps;
            float current = startAngle;

            painter.BeginPath();
            for (int i = 0; i < steps; i++)
            {
                float next = current + step;
                painter.Arc(center, radius, Angle.Degrees(current), Angle.Degrees(next), ArcDirection.Clockwise);
                current = next;
            }

            painter.Stroke();
        }
    }
}
