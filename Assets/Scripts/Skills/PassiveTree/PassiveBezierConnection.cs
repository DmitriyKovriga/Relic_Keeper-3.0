using System;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    /// <summary>
    /// Visual-only cubic Bezier between two connected nodes.
    /// Gameplay still uses <see cref="PassiveNodeDefinition.ConnectionIDs"/>.
    /// </summary>
    [Serializable]
    public class PassiveBezierConnection
    {
        public string NodeIdA;
        public string NodeIdB;

        [Range(0f, 100f)]
        [Tooltip("Anchor position along the straight segment from A to B.")]
        public float AnchorPercent = 50f;

        [Tooltip("Offset from the anchor to the incoming Bezier handle (toward A).")]
        public Vector2 InHandleOffset;

        [Tooltip("Offset from the anchor to the outgoing Bezier handle (toward B).")]
        public Vector2 OutHandleOffset;

        public bool Matches(string nodeIdA, string nodeIdB)
        {
            SortIds(ref nodeIdA, ref nodeIdB);
            return NodeIdA == nodeIdA && NodeIdB == nodeIdB;
        }

        public void NormalizeIds()
        {
            SortIds(ref NodeIdA, ref NodeIdB);
        }

        public Vector2 GetAnchor(Vector2 posA, Vector2 posB)
        {
            return Vector2.Lerp(posA, posB, Mathf.Clamp01(AnchorPercent / 100f));
        }

        public void GetCubicPoints(Vector2 posA, Vector2 posB, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3)
        {
            p0 = posA;
            p3 = posB;
            Vector2 anchor = GetAnchor(posA, posB);
            c1 = anchor + InHandleOffset;
            c2 = anchor + OutHandleOffset;
        }

        public static PassiveBezierConnection CreateDefault(string nodeIdA, string nodeIdB, Vector2 posA, Vector2 posB)
        {
            SortPair(ref nodeIdA, ref nodeIdB, ref posA, ref posB);
            Vector2 delta = posB - posA;
            float distance = Mathf.Max(delta.magnitude, 1f);
            Vector2 direction = delta / distance;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);

            return new PassiveBezierConnection
            {
                NodeIdA = nodeIdA,
                NodeIdB = nodeIdB,
                AnchorPercent = 50f,
                InHandleOffset = (-direction * 0.28f + perpendicular * 0.22f) * distance,
                OutHandleOffset = (direction * 0.28f + perpendicular * 0.22f) * distance
            };
        }

        public static void SortIds(ref string idA, ref string idB)
        {
            if (string.CompareOrdinal(idA ?? string.Empty, idB ?? string.Empty) <= 0)
                return;

            (idA, idB) = (idB, idA);
        }

        public static void SortPair(ref string idA, ref string idB, ref Vector2 posA, ref Vector2 posB)
        {
            if (string.CompareOrdinal(idA ?? string.Empty, idB ?? string.Empty) <= 0)
                return;

            (idA, idB) = (idB, idA);
            (posA, posB) = (posB, posA);
        }
    }

    public static class PassiveBezierMath
    {
        public static Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            float uu = u * u;
            float tt = t * t;
            return (uu * u * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (tt * t * p3);
        }

        public static float DistanceToCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 point, int samples = 32)
        {
            samples = Mathf.Max(4, samples);
            float min = float.MaxValue;
            Vector2 previous = p0;
            for (int i = 1; i <= samples; i++)
            {
                Vector2 current = EvaluateCubic(p0, p1, p2, p3, i / (float)samples);
                min = Mathf.Min(min, DistancePointToSegment(point, previous, current));
                previous = current;
            }

            return min;
        }

        public static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return Vector2.Distance(point, a);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
            return Vector2.Distance(point, a + (ab * t));
        }

        public static float PercentAlongSegment(Vector2 a, Vector2 b, Vector2 point)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq < 0.0001f)
                return 50f;

            return Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq) * 100f;
        }

        public static Rect Bounds(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float padding)
        {
            float minX = Mathf.Min(p0.x, Mathf.Min(p1.x, Mathf.Min(p2.x, p3.x))) - padding;
            float minY = Mathf.Min(p0.y, Mathf.Min(p1.y, Mathf.Min(p2.y, p3.y))) - padding;
            float maxX = Mathf.Max(p0.x, Mathf.Max(p1.x, Mathf.Max(p2.x, p3.x))) + padding;
            float maxY = Mathf.Max(p0.y, Mathf.Max(p1.y, Mathf.Max(p2.y, p3.y))) + padding;
            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }
    }
}
