using System;
using System.Collections.Generic;
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

        [Tooltip("When enabled, dragging either handle keeps the other handle exactly mirrored around the anchor.")]
        public bool MirrorHandles;

        [Tooltip("When enabled, the curve is a chain of cubics through Knots instead of the single anchor curve.")]
        public bool UseSpanPath;

        [Tooltip("Outgoing handle offset from node A. Used when UseSpanPath is set.")]
        public Vector2 StartOutOffset;

        [Tooltip("Incoming handle offset from node B. Used when UseSpanPath is set.")]
        public Vector2 EndInOffset;

        public List<PassiveBezierKnot> Knots = new List<PassiveBezierKnot>();

        public bool HasKnots => Knots != null && Knots.Count > 0;

        public int SpanCount => UseSpanPath ? (Knots?.Count ?? 0) + 1 : 1;

        public bool Matches(string nodeIdA, string nodeIdB)
        {
            SortIds(ref nodeIdA, ref nodeIdB);
            return NodeIdA == nodeIdA && NodeIdB == nodeIdB;
        }

        public void NormalizeIds()
        {
            if (string.CompareOrdinal(NodeIdA ?? string.Empty, NodeIdB ?? string.Empty) <= 0)
                return;

            (NodeIdA, NodeIdB) = (NodeIdB, NodeIdA);
            (InHandleOffset, OutHandleOffset) = (OutHandleOffset, InHandleOffset);
            AnchorPercent = 100f - AnchorPercent;
            if (!UseSpanPath)
                return;

            (StartOutOffset, EndInOffset) = (EndInOffset, StartOutOffset);
            if (Knots == null)
                return;

            Knots.Reverse();
            for (int i = 0; i < Knots.Count; i++)
            {
                PassiveBezierKnot knot = Knots[i];
                if (knot == null)
                    continue;
                (knot.InHandleOffset, knot.OutHandleOffset) = (knot.OutHandleOffset, knot.InHandleOffset);
            }
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
                OutHandleOffset = (direction * 0.28f + perpendicular * 0.22f) * distance,
                MirrorHandles = false
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

        public void CopySpans(Vector2 posA, Vector2 posB, List<Vector2> points)
        {
            points?.Clear();
            if (points == null)
                return;

            int count = SpanCount;
            for (int i = 0; i < count; i++)
            {
                GetSpan(posA, posB, i, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);
                points.Add(p0);
                points.Add(c1);
                points.Add(c2);
                points.Add(p3);
            }
        }

        public void GetSpan(Vector2 posA, Vector2 posB, int index, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3)
        {
            if (!UseSpanPath)
            {
                GetCubicPoints(posA, posB, out p0, out c1, out c2, out p3);
                return;
            }

            int knots = Knots?.Count ?? 0;
            index = Mathf.Clamp(index, 0, knots);
            Vector2 start = index == 0 ? posA : Knots[index - 1].Position;
            Vector2 end = index >= knots ? posB : Knots[index].Position;
            Vector2 outOffset = index == 0 ? StartOutOffset : Knots[index - 1].OutHandleOffset;
            Vector2 inOffset = index >= knots ? EndInOffset : Knots[index].InHandleOffset;
            p0 = start;
            p3 = end;
            c1 = start + outOffset;
            c2 = end + inOffset;
        }

        public float DistanceToPoint(Vector2 posA, Vector2 posB, Vector2 point)
        {
            float best = float.MaxValue;
            int count = SpanCount;
            for (int i = 0; i < count; i++)
            {
                GetSpan(posA, posB, i, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);
                best = Mathf.Min(best, PassiveBezierMath.DistanceToCubic(p0, c1, c2, p3, point));
            }

            return best;
        }

        public bool TryInsertKnot(Vector2 posA, Vector2 posB, Vector2 click, float maxDistance, out int knotIndex)
        {
            knotIndex = -1;
            int count = SpanCount;
            int bestSpan = -1;
            float bestT = 0f;
            float bestDistance = maxDistance;
            for (int i = 0; i < count; i++)
            {
                GetSpan(posA, posB, i, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);
                if (!PassiveBezierMath.TryClosestOnCubic(p0, c1, c2, p3, click, out float t, out _, out float distance))
                    continue;
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                bestSpan = i;
                bestT = t;
            }

            if (bestSpan < 0 || bestT < 0.06f || bestT > 0.94f)
                return false;

            GetSpan(posA, posB, bestSpan, out Vector2 span0, out Vector2 span1, out Vector2 span2, out Vector2 span3);
            PassiveBezierMath.SplitCubic(span0, span1, span2, span3, bestT,
                out Vector2 left0, out Vector2 left1, out Vector2 left2, out Vector2 left3,
                out _, out Vector2 right1, out Vector2 right2, out Vector2 right3);

            Knots ??= new List<PassiveBezierKnot>();
            var knot = new PassiveBezierKnot
            {
                Position = left3,
                InHandleOffset = left2 - left3,
                OutHandleOffset = right1 - left3,
                MirrorHandles = true
            };

            if (!UseSpanPath)
            {
                StartOutOffset = left1 - left0;
                EndInOffset = right2 - right3;
                Knots.Clear();
                Knots.Add(knot);
                UseSpanPath = true;
                knotIndex = 0;
                return true;
            }

            if (bestSpan == 0)
                StartOutOffset = left1 - left0;
            else if (Knots[bestSpan - 1] != null)
                Knots[bestSpan - 1].OutHandleOffset = left1 - left0;

            if (bestSpan >= Knots.Count)
                EndInOffset = right2 - right3;
            else if (Knots[bestSpan] != null)
                Knots[bestSpan].InHandleOffset = right2 - right3;

            Knots.Insert(bestSpan, knot);
            knotIndex = bestSpan;
            return true;
        }

        public bool RemoveKnot(int index)
        {
            if (!UseSpanPath || Knots == null || index < 0 || index >= Knots.Count)
                return false;

            Knots.RemoveAt(index);
            return true;
        }

        public void ReflectAcrossChord(Vector2 posA, Vector2 posB)
        {
            Vector2 axis = posB - posA;
            InHandleOffset = PassiveBezierMath.ReflectAcrossAxis(InHandleOffset, axis);
            OutHandleOffset = PassiveBezierMath.ReflectAcrossAxis(OutHandleOffset, axis);
            if (!UseSpanPath)
                return;

            StartOutOffset = PassiveBezierMath.ReflectAcrossAxis(StartOutOffset, axis);
            EndInOffset = PassiveBezierMath.ReflectAcrossAxis(EndInOffset, axis);
            if (Knots == null)
                return;

            for (int i = 0; i < Knots.Count; i++)
            {
                PassiveBezierKnot knot = Knots[i];
                if (knot == null)
                    continue;
                knot.Position = posA + PassiveBezierMath.ReflectAcrossAxis(knot.Position - posA, axis);
                knot.InHandleOffset = PassiveBezierMath.ReflectAcrossAxis(knot.InHandleOffset, axis);
                knot.OutHandleOffset = PassiveBezierMath.ReflectAcrossAxis(knot.OutHandleOffset, axis);
            }
        }

        public PassiveBezierConnection Clone()
        {
            var copy = new PassiveBezierConnection
            {
                NodeIdA = NodeIdA,
                NodeIdB = NodeIdB,
                AnchorPercent = AnchorPercent,
                InHandleOffset = InHandleOffset,
                OutHandleOffset = OutHandleOffset,
                MirrorHandles = MirrorHandles,
                UseSpanPath = UseSpanPath,
                StartOutOffset = StartOutOffset,
                EndInOffset = EndInOffset,
                Knots = new List<PassiveBezierKnot>()
            };
            if (Knots == null)
                return copy;

            for (int i = 0; i < Knots.Count; i++)
            {
                PassiveBezierKnot knot = Knots[i];
                if (knot == null)
                    continue;
                copy.Knots.Add(knot.Clone());
            }

            return copy;
        }

        public void MapAffine(Func<Vector2, Vector2> mapPoint)
        {
            if (mapPoint == null)
                return;

            Vector2 MapVector(Vector2 value) => mapPoint(value) - mapPoint(Vector2.zero);
            InHandleOffset = MapVector(InHandleOffset);
            OutHandleOffset = MapVector(OutHandleOffset);
            StartOutOffset = MapVector(StartOutOffset);
            EndInOffset = MapVector(EndInOffset);
            if (Knots == null)
                return;

            for (int i = 0; i < Knots.Count; i++)
            {
                PassiveBezierKnot knot = Knots[i];
                if (knot == null)
                    continue;
                knot.Position = mapPoint(knot.Position);
                knot.InHandleOffset = MapVector(knot.InHandleOffset);
                knot.OutHandleOffset = MapVector(knot.OutHandleOffset);
            }
        }
    }

    [Serializable]
    public class PassiveBezierKnot
    {
        public Vector2 Position;
        public Vector2 InHandleOffset;
        public Vector2 OutHandleOffset;
        public bool MirrorHandles = true;

        public PassiveBezierKnot Clone()
        {
            return new PassiveBezierKnot
            {
                Position = Position,
                InHandleOffset = InHandleOffset,
                OutHandleOffset = OutHandleOffset,
                MirrorHandles = MirrorHandles
            };
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

        public static void SplitCubic(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t,
            out Vector2 left0, out Vector2 left1, out Vector2 left2, out Vector2 left3,
            out Vector2 right0, out Vector2 right1, out Vector2 right2, out Vector2 right3)
        {
            t = Mathf.Clamp(t, 0.001f, 0.999f);
            Vector2 q0 = Vector2.Lerp(p0, p1, t);
            Vector2 q1 = Vector2.Lerp(p1, p2, t);
            Vector2 q2 = Vector2.Lerp(p2, p3, t);
            Vector2 r0 = Vector2.Lerp(q0, q1, t);
            Vector2 r1 = Vector2.Lerp(q1, q2, t);
            Vector2 point = Vector2.Lerp(r0, r1, t);
            left0 = p0;
            left1 = q0;
            left2 = r0;
            left3 = point;
            right0 = point;
            right1 = r1;
            right2 = q2;
            right3 = p3;
        }

        public static bool TryClosestOnCubic(
            Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Vector2 point,
            out float t, out Vector2 closest, out float distance, int samples = 32)
        {
            samples = Mathf.Max(8, samples);
            t = 0f;
            closest = p0;
            distance = Vector2.Distance(point, p0);
            Vector2 previous = p0;
            float previousT = 0f;
            for (int i = 1; i <= samples; i++)
            {
                float currentT = i / (float)samples;
                Vector2 current = EvaluateCubic(p0, p1, p2, p3, currentT);
                Vector2 ab = current - previous;
                float lengthSq = ab.sqrMagnitude;
                float segmentT = lengthSq < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - previous, ab) / lengthSq);
                Vector2 projected = previous + ab * segmentT;
                float candidate = Vector2.Distance(point, projected);
                if (candidate < distance)
                {
                    distance = candidate;
                    closest = projected;
                    t = Mathf.Lerp(previousT, currentT, segmentT);
                }

                previous = current;
                previousT = currentT;
            }

            return true;
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

        public static float SnapPercent(float percent, float step = 5f)
        {
            step = Mathf.Max(0.01f, step);
            return Mathf.Clamp(Mathf.Round(percent / step) * step, 0f, 100f);
        }

        public static Vector2 ConstrainTo45Degrees(Vector2 offset)
        {
            if (offset.sqrMagnitude < 0.0001f)
                return offset;

            float angle = Mathf.Atan2(offset.y, offset.x);
            float snapped = Mathf.Round(angle / (Mathf.PI * 0.25f)) * (Mathf.PI * 0.25f);
            return new Vector2(Mathf.Cos(snapped), Mathf.Sin(snapped)) * offset.magnitude;
        }

        public static Vector2 RotateOffset(Vector2 offset, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2((offset.x * cos) - (offset.y * sin), (offset.x * sin) + (offset.y * cos));
        }

        public static bool AreSmoothOpposite(Vector2 inHandle, Vector2 outHandle, float minDot = -0.92f)
        {
            if (inHandle.sqrMagnitude < 0.01f || outHandle.sqrMagnitude < 0.01f)
                return false;

            return Vector2.Dot(inHandle.normalized, outHandle.normalized) <= minDot;
        }

        public static Vector2 AlignOppositeHandle(Vector2 draggedHandle, float oppositeLength)
        {
            if (draggedHandle.sqrMagnitude < 0.0001f)
                return Vector2.zero;

            return -draggedHandle.normalized * Mathf.Max(0f, oppositeLength);
        }

        public static Vector2 MirrorHandle(Vector2 draggedHandle)
        {
            return -draggedHandle;
        }

        public static Vector2 ReflectAcrossAxis(Vector2 offset, Vector2 axis)
        {
            if (axis.sqrMagnitude < 0.0001f)
                return offset;

            Vector2 direction = axis.normalized;
            return (2f * Vector2.Dot(offset, direction) * direction) - offset;
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
