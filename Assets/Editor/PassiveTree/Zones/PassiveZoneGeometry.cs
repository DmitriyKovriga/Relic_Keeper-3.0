using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    // The solver and its independent final-tree audit use the same rendered geometry:
    // Bezier override, short orbit arc (<170 degrees), otherwise a straight edge.
    public static class PassiveZoneGeometry
    {
        public sealed class Node
        {
            public string Id;
            public Vector2 Position;
            public PassiveNodeType Type;
            public float Radius => Type == PassiveNodeType.Small ? 14f : Type == PassiveNodeType.Keystone ? 26f : 20f;
        }

        public sealed class Edge
        {
            public string A;
            public string B;
            public Vector2[] Points;
            public Rect Bounds;

            public Edge(string a, string b, Vector2[] points)
            {
                A = a;
                B = b;
                Points = points;
                Vector2 min = points[0], max = points[0];
                foreach (Vector2 p in points)
                {
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                Bounds = Rect.MinMaxRect(min.x - 6f, min.y - 6f, max.x + 6f, max.y + 6f);
            }

            public bool Contains(string id) => A == id || B == id;
        }

        public sealed class Layout
        {
            public readonly List<Node> Nodes = new List<Node>();
            public readonly List<Edge> Edges = new List<Edge>();

            public void Append(Layout other)
            {
                Nodes.AddRange(other.Nodes);
                Edges.AddRange(other.Edges);
            }
        }

        public static Vector2 Polar(float angle, float radius)
        {
            float a = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        public static Vector2 Rotate(Vector2 p, float angle)
        {
            float a = angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        public static Vector2[] Arc(Vector2 center, float radius, float start, float end)
        {
            float delta = Mathf.DeltaAngle(start, end);
            int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(delta) / 7.5f));
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
                points[i] = center + Polar(start + delta * i / steps, radius);
            return points;
        }

        public static Vector2[] Cubic(Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p3)
        {
            var points = new Vector2[17];
            for (int i = 0; i < points.Length; i++)
                points[i] = PassiveBezierMath.EvaluateCubic(p0, c1, c2, p3, i / 16f);
            return points;
        }

        public static Layout Read(PassiveSkillTreeSO tree)
        {
            tree.InitLookup();
            var result = new Layout();
            var seen = new HashSet<string>();
            var curves = new Dictionary<string, PassiveBezierConnection>();
            if (tree.BezierConnections != null)
                foreach (PassiveBezierConnection b in tree.BezierConnections)
                    if (b != null) curves[Key(b.NodeIdA, b.NodeIdB)] = b;
            foreach (PassiveNodeDefinition n in tree.Nodes)
            {
                if (n == null) continue;
                Vector2 a = n.GetWorldPosition(tree);
                result.Nodes.Add(new Node { Id = n.ID, Position = a, Type = n.NodeType });
                if (n.ConnectionIDs == null) continue;
                foreach (string id in n.ConnectionIDs)
                {
                    PassiveNodeDefinition other = tree.GetNode(id);
                    string key = Key(n.ID, id);
                    if (other == null || !seen.Add(key)) continue;
                    Vector2 b = other.GetWorldPosition(tree);
                    Vector2[] points = new[] { a, b };
                    if (curves.TryGetValue(key, out PassiveBezierConnection curve))
                    {
                        Vector2 ca = tree.GetNode(curve.NodeIdA).GetWorldPosition(tree);
                        Vector2 cb = tree.GetNode(curve.NodeIdB).GetWorldPosition(tree);
                        curve.GetCubicPoints(ca, cb, out _, out Vector2 c1, out Vector2 c2, out _);
                        result.Edges.Add(new Edge(curve.NodeIdA, curve.NodeIdB, Cubic(ca, c1, c2, cb)));
                        continue;
                    }
                    if (n.PlacementMode == NodePlacementMode.OnOrbit && other.PlacementMode == NodePlacementMode.OnOrbit &&
                        n.ClusterID == other.ClusterID && n.OrbitIndex == other.OrbitIndex &&
                        !PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(n.OrbitAngle, other.OrbitAngle))
                    {
                        PassiveClusterDefinition c = tree.GetCluster(n.ClusterID);
                        if (c != null && n.OrbitIndex >= 0 && n.OrbitIndex < c.Orbits.Count)
                            points = Arc(c.Center, c.Orbits[n.OrbitIndex].Radius, n.OrbitAngle, other.OrbitAngle);
                    }
                    result.Edges.Add(new Edge(n.ID, other.ID, points));
                }
            }
            return result;
        }

        static string Key(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;

        public static bool Fits(Layout added, Layout occupied, out string reason)
        {
            foreach (Node a in added.Nodes)
            {
                foreach (Node b in occupied.Nodes)
                    if (!Separated(a, b)) { reason = "ноды слишком близко"; return false; }
                foreach (Edge e in occupied.Edges)
                    if (HitsNode(e, a)) { reason = "нода на чужой линии"; return false; }
            }
            for (int i = 0; i < added.Nodes.Count; i++)
                for (int j = i + 1; j < added.Nodes.Count; j++)
                    if (!Separated(added.Nodes[i], added.Nodes[j])) { reason = "тесная форма"; return false; }
            foreach (Edge e in added.Edges)
            {
                foreach (Node n in occupied.Nodes)
                    if (HitsNode(e, n)) { reason = "линия через чужую ноду"; return false; }
                foreach (Node n in added.Nodes)
                    if (HitsNode(e, n)) { reason = "линия через ноду формы"; return false; }
                foreach (Edge other in occupied.Edges)
                    if (Crosses(e, other)) { reason = "пересечение линий"; return false; }
            }
            for (int i = 0; i < added.Edges.Count; i++)
                for (int j = i + 1; j < added.Edges.Count; j++)
                    if (Crosses(added.Edges[i], added.Edges[j])) { reason = "пересечение внутри формы"; return false; }
            reason = "ок";
            return true;
        }

        public static bool Separated(Node a, Node b)
        {
            float gap = a.Radius + b.Radius + 10f;
            if (a.Type == PassiveNodeType.Notable && b.Type == PassiveNodeType.Notable)
                gap = Mathf.Max(gap, 78f);
            return Vector2.SqrMagnitude(a.Position - b.Position) >= gap * gap;
        }

        public static bool HitsNode(Edge edge, Node node)
        {
            if (edge.Contains(node.Id)) return false;
            float radius = node.Radius + 6f;
            Rect bounds = edge.Bounds;
            bounds.xMin -= radius; bounds.yMin -= radius;
            bounds.xMax += radius; bounds.yMax += radius;
            if (!bounds.Contains(node.Position)) return false;
            for (int i = 1; i < edge.Points.Length; i++)
                if (PassiveBezierMath.DistancePointToSegment(node.Position, edge.Points[i - 1], edge.Points[i]) < radius)
                    return true;
            return false;
        }

        public static bool Crosses(Edge a, Edge b)
        {
            if (!a.Bounds.Overlaps(b.Bounds)) return false;
            bool shared = a.Contains(b.A) || a.Contains(b.B);
            Vector2 sharedPoint = a.Contains(b.A) ? b.Points[0] : b.Points[b.Points.Length - 1];
            for (int i = 1; i < a.Points.Length; i++)
                for (int j = 1; j < b.Points.Length; j++)
                {
                    Vector2 a0 = a.Points[i - 1], a1 = a.Points[i], b0 = b.Points[j - 1], b1 = b.Points[j];
                    // Only the immediate joint is exempt. Two edges sharing a node can
                    // still cross again or overlap further along the route.
                    if (shared)
                    {
                        if (Vector2.Distance(a0, sharedPoint) < 24f) a0 = Trim(a0, a1, sharedPoint);
                        if (Vector2.Distance(a1, sharedPoint) < 24f) a1 = Trim(a1, a0, sharedPoint);
                        if (Vector2.Distance(b0, sharedPoint) < 24f) b0 = Trim(b0, b1, sharedPoint);
                        if (Vector2.Distance(b1, sharedPoint) < 24f) b1 = Trim(b1, b0, sharedPoint);
                        if (Vector2.Distance(a0, sharedPoint) < 23f || Vector2.Distance(b0, sharedPoint) < 23f) continue;
                    }
                    if (SegmentsCross(a0, a1, b0, b1)) return true;
                    if (PassiveBezierMath.DistancePointToSegment(a0, b0, b1) < 4f ||
                        PassiveBezierMath.DistancePointToSegment(a1, b0, b1) < 4f ||
                        PassiveBezierMath.DistancePointToSegment(b0, a0, a1) < 4f ||
                        PassiveBezierMath.DistancePointToSegment(b1, a0, a1) < 4f) return true;
                }
            return false;
        }

        static Vector2 Trim(Vector2 a, Vector2 b, Vector2 joint)
        {
            float distance = Vector2.Distance(b, joint);
            return distance <= 24f ? b : Vector2.Lerp(a, b, Mathf.Clamp01((24f - Vector2.Distance(a, joint)) / Vector2.Distance(a, b)));
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return Cross(b - a, c - a) * Cross(b - a, d - a) < -0.001f &&
                   Cross(d - c, a - c) * Cross(d - c, b - c) < -0.001f;
        }

        public static List<Vector2> Hull(List<Vector2> points)
        {
            points.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
            var hull = new List<Vector2>();
            foreach (Vector2 p in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                Vector2 p = points[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        public static bool Inside(List<Vector2> hull, Vector2 point, float margin)
        {
            if (hull.Count < 3) return false;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
                if (Cross(b - a, point - a) < margin * Vector2.Distance(a, b)) return false;
            }
            return true;
        }
    }
}
