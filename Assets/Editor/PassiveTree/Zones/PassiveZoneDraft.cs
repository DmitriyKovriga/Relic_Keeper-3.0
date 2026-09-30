using System;
using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    internal sealed class PassiveZoneDraft
    {
        internal sealed class Point
        {
            public Vector2 Position;
            public PassiveNodeType Type;
            public int Orbit = -1;
            public float Angle;
        }

        internal sealed class Link
        {
            public int A, B;
            public bool Curved;
            public Vector2 C1, C2;
        }

        public ZonePieceKind Kind;
        public string Motif;
        public readonly List<Point> Points = new List<Point>();
        public readonly List<Link> Links = new List<Link>();
        public float[] Radii;
        public int Entry;
        public Vector2 Center;
        public Vector2 PortDirection;
        public string Anchor;
        public string SecondAnchor;
        public int SecondEntry;
        public PassiveZoneGeometry.Layout Geometry;

        public static int MinimumCost(ZonePieceKind kind) => kind == ZonePieceKind.Small || kind == ZonePieceKind.DeadEnd ? 2 : kind == ZonePieceKind.Medium ? 3 : 4;
        public static int MinimumNodes(ZonePieceKind kind) => kind == ZonePieceKind.Large ? 7 : kind == ZonePieceKind.Complex ? 5 : MinimumCost(kind) + 1;
        static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

        public static PassiveZoneDraft Build(ZonePieceKind kind, System.Random rng, int budget, IReadOnlyList<ZoneMotif> catalog)
        {
            if (kind == ZonePieceKind.DeadEnd) return Wave(rng, budget);
            var eligible = new List<ZoneMotif>();
            foreach (ZoneMotif motif in catalog)
                if (motif.nodes.Length <= budget && PassiveZoneMotifLibrary.Matches(motif, kind)) eligible.Add(motif);
            if (eligible.Count == 0) return null;
            ZoneMotif source = eligible[rng.Next(eligible.Count)];
            var d = new PassiveZoneDraft { Kind = kind, Motif = source.id, Radii = new float[source.radii.Length] };
            float phase = Range(rng, 0f, 360f);
            float amplitude = Range(rng, 3f, 7f) * (rng.Next(2) == 0 ? -1f : 1f);
            float mirror = rng.Next(2) == 0 ? -1f : 1f;
            for (int i = 0; i < d.Radii.Length; i++)
                d.Radii[i] = source.radii[i] * Range(rng, 0.93f, 1.07f);
            foreach (ZoneMotifNode n in source.nodes)
            {
                // Coherent angular warp preserves orbit order and circular arcs. No
                // independent Cartesian jitter, so rings never become wobbly polygons.
                float angle = mirror * (n.angle + amplitude * Mathf.Sin((n.angle * 2f + phase) * Mathf.Deg2Rad));
                d.Points.Add(new Point { Position = PassiveZoneGeometry.Polar(angle, d.Radii[n.orbit]), Orbit = n.orbit, Angle = angle });
            }
            foreach (ZoneMotifEdge e in source.edges) d.Links.Add(new Link { A = e.a, B = e.b });

            var entries = new List<int>();
            for (int i = 0; i < source.nodes.Length; i++)
                if (source.nodes[i].port || d.Points[i].Position.magnitude >= 0.8f) entries.Add(i);
            if (entries.Count == 0) return null;
            d.Entry = entries[rng.Next(entries.Count)];
            if (d.Points[d.Entry].Position.sqrMagnitude < 0.01f) return null;
            int[] distance = d.Distances(d.Entry);
            int furthest = 0;
            for (int i = 0; i < distance.Length; i++) furthest = Mathf.Max(furthest, distance[i]);
            int stem = Mathf.Max(0, MinimumCost(kind) - furthest);
            if (stem > 2 || d.Points.Count + stem > budget) return null;

            var rewards = new List<int>();
            for (int i = 0; i < distance.Length; i++)
                if (distance[i] + stem >= MinimumCost(kind) && i != d.Entry) rewards.Add(i);
            if (rewards.Count == 0) return null;
            rewards.Sort((a, b) => (distance[b] + (source.nodes[b].notable ? 2 : 0)).CompareTo(distance[a] + (source.nodes[a].notable ? 2 : 0)));
            d.Points[rewards[0]].Type = PassiveNodeType.Notable;
            if (kind == ZonePieceKind.Large || kind == ZonePieceKind.Complex || (kind == ZonePieceKind.Medium && rng.Next(3) == 0))
                for (int i = 1; i < rewards.Count; i++)
                {
                    int next = rewards[i];
                    if (!source.nodes[next].notable || Vector2.Distance(d.Points[next].Position, d.Points[rewards[0]].Position) < 0.9f) continue;
                    d.Points[next].Type = PassiveNodeType.Notable;
                    if (!d.CostsValid(stem)) d.Points[next].Type = PassiveNodeType.Small;
                    else break;
                }

            float scale = 0f;
            for (int i = 0; i < d.Points.Count; i++)
                for (int j = i + 1; j < d.Points.Count; j++)
                {
                    float distanceBetween = Vector2.Distance(d.Points[i].Position, d.Points[j].Position);
                    if (distanceBetween < 0.01f) return null;
                    float required = 46f + (d.Points[i].Type == PassiveNodeType.Notable ? 6f : 0f) + (d.Points[j].Type == PassiveNodeType.Notable ? 6f : 0f);
                    if (d.Points[i].Type == PassiveNodeType.Notable && d.Points[j].Type == PassiveNodeType.Notable) required = 82f;
                    scale = Mathf.Max(scale, required / distanceBetween);
                }
            scale *= Range(rng, 1.01f, 1.13f);
            for (int i = 0; i < d.Radii.Length; i++) d.Radii[i] *= scale;
            foreach (Point p in d.Points) p.Position *= scale;

            Vector2 outward = d.Points[d.Entry].Position.normalized;
            for (int i = 0; i < stem; i++)
            {
                int next = d.Points.Count;
                d.Points.Add(new Point { Position = d.Points[d.Entry].Position + outward * 48f });
                d.Links.Add(new Link { A = next, B = d.Entry });
                d.Entry = next;
            }
            d.PortDirection = -outward;
            return d;
        }

        int[] Distances(int start)
        {
            var distance = new int[Points.Count];
            for (int i = 0; i < distance.Length; i++) distance[i] = -1;
            distance[start] = 0;
            var queue = new Queue<int>(); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                foreach (Link e in Links)
                {
                    int other = e.A == n ? e.B : e.B == n ? e.A : -1;
                    if (other < 0 || distance[other] >= 0) continue;
                    distance[other] = distance[n] + 1; queue.Enqueue(other);
                }
            }
            return distance;
        }

        bool CostsValid(int stem, int entry = -1)
        {
            if (entry < 0) entry = Entry;
            var costs = new int[Points.Count];
            for (int i = 0; i < costs.Length; i++) costs[i] = 10000;
            costs[entry] = stem;
            for (int pass = 0; pass < Points.Count; pass++)
                foreach (Link edge in Links)
                {
                    costs[edge.B] = Mathf.Min(costs[edge.B], costs[edge.A] + (Points[edge.A].Type == PassiveNodeType.Small ? 1 : 0));
                    costs[edge.A] = Mathf.Min(costs[edge.A], costs[edge.B] + (Points[edge.B].Type == PassiveNodeType.Small ? 1 : 0));
                }
            for (int i = 0; i < Points.Count; i++)
                if (Points[i].Type == PassiveNodeType.Notable && costs[i] < MinimumCost(Kind)) return false;
            return true;
        }

        public bool TrySecondEntry(PassiveNodeDefinition anchor, Vector2 position, int piece, PassiveZoneGeometry.Layout occupied)
        {
            if (Radii == null || anchor.ID == Anchor) return false;
            for (int i = 0; i < Points.Count; i++)
            {
                Point point = Points[i];
                if (i == Entry || point.Type != PassiveNodeType.Small || point.Orbit < 0) continue;
                Vector2 delta = position - point.Position;
                if (delta.magnitude < 48f || delta.magnitude > 115f) continue;
                Vector2 radial = (point.Position - Center).normalized;
                if (Vector2.Dot(radial, delta.normalized) < 0.9f || !CostsValid(0, i)) continue;
                var edge = new PassiveZoneGeometry.Edge(anchor.ID, "candidate-" + piece + "-" + i, new[] { position, point.Position });
                Geometry.Edges.Add(edge);
                if (PassiveZoneGeometry.Fits(Geometry, occupied, out _))
                {
                    SecondAnchor = anchor.ID;
                    SecondEntry = i;
                    return true;
                }
                Geometry.Edges.RemoveAt(Geometry.Edges.Count - 1);
            }
            return false;
        }

        static PassiveZoneDraft Wave(System.Random rng, int budget)
        {
            if (budget < 3) return null;
            int count = rng.Next(3, Mathf.Min(5, budget) + 1);
            int variant = rng.Next(6);
            float length = (count - 1) * Range(rng, 48f, 58f);
            float bow = Range(rng, 0.22f, 0.55f) * length;
            if ((variant & 1) != 0) bow = -bow;
            Vector2 p0 = Vector2.zero, p3 = new Vector2(length, 0f);
            Vector2 c1 = new Vector2(length * 0.3f, bow);
            Vector2 c2 = new Vector2(length * 0.7f, variant < 2 ? bow : -bow);
            if (variant >= 4)
            {
                p3 = new Vector2(length * 0.5f, bow * 1.5f);
                c1 = new Vector2(length * 0.65f, 0f);
                c2 = new Vector2(length, bow * 1.5f);
            }
            var d = new PassiveZoneDraft { Kind = ZonePieceKind.DeadEnd, Motif = "wave-" + variant, PortDirection = (c1 - p0).normalized };
            // Arc-length sampling gives an even rhythm. Each segment is an exact
            // restriction of the same cubic (endpoint derivatives preserve C1).
            var lengths = new float[129];
            Vector2 previous = p0;
            for (int i = 1; i < lengths.Length; i++)
            {
                Vector2 p = PassiveBezierMath.EvaluateCubic(p0, c1, c2, p3, i / 128f);
                lengths[i] = lengths[i - 1] + Vector2.Distance(previous, p); previous = p;
            }
            var ts = new float[count];
            for (int i = 0; i < count; i++)
            {
                float target = lengths[128] * i / (count - 1);
                int sample = 1;
                while (sample < 128 && lengths[sample] < target) sample++;
                ts[i] = ((sample - 1) + Mathf.InverseLerp(lengths[sample - 1], lengths[sample], target)) / 128f;
                d.Points.Add(new Point { Position = PassiveBezierMath.EvaluateCubic(p0, c1, c2, p3, ts[i]), Type = i == count - 1 ? PassiveNodeType.Notable : PassiveNodeType.Small });
            }
            for (int i = 1; i < count; i++)
            {
                float dt = ts[i] - ts[i - 1];
                d.Links.Add(new Link { A = i - 1, B = i, Curved = true,
                    C1 = d.Points[i - 1].Position + Derivative(p0, c1, c2, p3, ts[i - 1]) * dt / 3f,
                    C2 = d.Points[i].Position - Derivative(p0, c1, c2, p3, ts[i]) * dt / 3f });
            }
            return d;
        }

        static Vector2 Derivative(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float u = 1f - t;
            return 3f * u * u * (b - a) + 6f * u * t * (c - b) + 3f * t * t * (d - c);
        }

        public void Place(Vector2 anchor, string anchorId, float direction, float distance, int piece)
        {
            float angle = direction - Mathf.Atan2(PortDirection.y, PortDirection.x) * Mathf.Rad2Deg;
            Vector2 entry = anchor + PassiveZoneGeometry.Polar(direction, distance);
            Vector2 offset = entry - PassiveZoneGeometry.Rotate(Points[Entry].Position, angle);
            Center = offset;
            Anchor = anchorId;
            foreach (Point p in Points)
            {
                p.Position = PassiveZoneGeometry.Rotate(p.Position, angle) + offset;
                p.Angle = Mathf.Repeat(p.Angle + angle, 360f);
            }
            foreach (Link e in Links)
                if (e.Curved)
                {
                    e.C1 = PassiveZoneGeometry.Rotate(e.C1, angle) + offset;
                    e.C2 = PassiveZoneGeometry.Rotate(e.C2, angle) + offset;
                }
            Geometry = new PassiveZoneGeometry.Layout();
            string Prefix(int i) => "candidate-" + piece + "-" + i;
            for (int i = 0; i < Points.Count; i++)
                Geometry.Nodes.Add(new PassiveZoneGeometry.Node { Id = Prefix(i), Position = Points[i].Position, Type = Points[i].Type });
            Geometry.Edges.Add(new PassiveZoneGeometry.Edge(anchorId, Prefix(Entry), new[] { anchor, Points[Entry].Position }));
            foreach (Link e in Links)
            {
                Point a = Points[e.A], b = Points[e.B];
                Vector2[] points = new[] { a.Position, b.Position };
                if (e.Curved) points = PassiveZoneGeometry.Cubic(a.Position, e.C1, e.C2, b.Position);
                else if (Radii != null && a.Orbit >= 0 && a.Orbit == b.Orbit && !PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(a.Angle, b.Angle))
                    points = PassiveZoneGeometry.Arc(Center, Radii[a.Orbit], a.Angle, b.Angle);
                Geometry.Edges.Add(new PassiveZoneGeometry.Edge(Prefix(e.A), Prefix(e.B), points));
            }
        }

        public void Commit(PassiveSkillTreeSO tree, int zone, int seed, int piece)
        {
            string Id(string role)
            {
                string basis = "zone-layout-v2/" + zone + "/" + seed + "/" + piece + "/" + role;
                int salt = 0;
                string id;
                do { id = Hash128.Compute(basis + "/" + salt++).ToString(); }
                while (tree.Nodes.Exists(n => n != null && n.ID == id) || tree.Clusters.Exists(c => c != null && c.ID == id));
                return id;
            }
            PassiveClusterDefinition cluster = null;
            if (Radii != null)
            {
                cluster = new PassiveClusterDefinition { ID = Id("cluster"), Name = "Z" + zone + " " + Kind + " / " + Motif,
                    Center = Center, EditorColor = new Color(0.44f, 0.51f, 0.68f, 0.3f) };
                foreach (float radius in Radii) cluster.Orbits.Add(new PassiveOrbitDefinition { Radius = radius });
                tree.Clusters.Add(cluster);
            }
            var created = new List<PassiveNodeDefinition>();
            foreach (Point point in Points)
            {
                var node = new PassiveNodeDefinition { ID = Id("node-" + created.Count), Position = point.Position, NodeType = point.Type,
                    ConnectionIDs = new List<string>(), IsBackbone = false };
                if (cluster != null && point.Orbit >= 0)
                {
                    node.ClusterID = cluster.ID;
                    node.PlacementMode = NodePlacementMode.OnOrbit;
                    node.OrbitIndex = point.Orbit;
                    node.OrbitAngle = point.Angle;
                }
                tree.Nodes.Add(node); created.Add(node);
            }
            foreach (Link e in Links)
            {
                Connect(created[e.A], created[e.B]);
                if (!e.Curved) continue;
                Vector2 middle = (created[e.A].Position + created[e.B].Position) * 0.5f;
                var curve = new PassiveBezierConnection { NodeIdA = created[e.A].ID, NodeIdB = created[e.B].ID,
                    AnchorPercent = 50f, InHandleOffset = e.C1 - middle, OutHandleOffset = e.C2 - middle };
                curve.NormalizeIds(); tree.BezierConnections.Add(curve);
            }
            tree.InitLookup();
            Connect(tree.GetNode(Anchor), created[Entry]);
            if (!string.IsNullOrEmpty(SecondAnchor)) Connect(tree.GetNode(SecondAnchor), created[SecondEntry]);
        }

        static void Connect(PassiveNodeDefinition a, PassiveNodeDefinition b)
        {
            if (a.ConnectionIDs == null) a.ConnectionIDs = new List<string>();
            if (b.ConnectionIDs == null) b.ConnectionIDs = new List<string>();
            if (!a.ConnectionIDs.Contains(b.ID)) a.ConnectionIDs.Add(b.ID);
            if (!b.ConnectionIDs.Contains(a.ID)) b.ConnectionIDs.Add(a.ID);
        }
    }
}
