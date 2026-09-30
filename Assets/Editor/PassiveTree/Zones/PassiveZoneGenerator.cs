using System;
using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    public enum ZonePieceKind
    {
        DeadEnd,
        Small,
        Medium,
        Large,
        Complex
    }

    public struct ZoneGenSettings
    {
        public bool Advanced;
        public int Pieces;
        public int Nodes;
        public int Small;
        public int Medium;
        public int Large;
        public int Complex;
        public int DeadEnds;
        public int Seed;
    }

    public struct ZoneGenResult
    {
        public bool Success;
        public int PlacedPieces;
        public int PlacedNodes;
        public int Skipped;
        public string Message;
    }

    public static class PassiveZoneGenerator
    {
        const int Compositions = 40;
        const int PlacementAttempts = 140;

        public static ZoneGenResult Generate(PassiveSkillTreeSO tree, int zone, ZoneGenSettings settings)
        {
            if (tree == null || zone < 0 || zone > 3) return Fail("Некорректное дерево или зона.");
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start == null) return Fail("Нет стартового нода.");
            if (PassiveZoneMath.CountBackbone(tree) < 3) return Fail("Сначала пометь каркас.");
            IReadOnlyList<ZoneMotif> catalog = PassiveZoneMotifLibrary.Motifs;
            if (catalog.Count == 0) return Fail("Не найден каталог форм " + PassiveZoneMotifLibrary.AssetPath);
            var rng = new System.Random(settings.Seed);
            List<ZonePieceKind> plan = BuildPlan(settings, rng);
            int budget = settings.Nodes <= 0 ? int.MaxValue : settings.Nodes;
            int minimum = 0;
            foreach (ZonePieceKind kind in plan) minimum += PassiveZoneDraft.MinimumNodes(kind);
            if (minimum > budget) return Fail("Для " + plan.Count + " сущностей нужно хотя бы " + minimum + " нодов; лимит " + budget + ". Зона сохранена.");
            if (plan.Count > 24) return Fail("Слишком много сущностей для одной зоны. Зона сохранена.");

            // All exploration and clearing happen on a deep Unity clone. A failed
            // request never deletes hand-authored content or leaves half a layout.
            PassiveSkillTreeSO scratch = UnityEngine.Object.Instantiate(tree);
            try
            {
                PassiveZoneOps.ClearZone(scratch, zone);
                scratch.InitLookup();
                Vector2 origin = start.GetWorldPosition(tree);
                var anchors = new List<PassiveNodeDefinition>();
                var backbonePoints = new List<Vector2>();
                foreach (PassiveNodeDefinition n in scratch.Nodes)
                {
                    if (n == null || !n.IsBackbone || n.NodeType == PassiveNodeType.Keystone) continue;
                    Vector2 p = n.GetWorldPosition(scratch);
                    backbonePoints.Add(p);
                    Vector2 local = Local(p - origin, zone);
                    if (n.NodeType != PassiveNodeType.Start && local.y < -40f && Mathf.Abs(local.x) <= -local.y + 5f)
                        anchors.Add(n);
                }
                if (anchors.Count == 0) return Fail("Нет точек подключения в выбранной зоне. Зона сохранена.");
                anchors.Sort((a, b) =>
                {
                    Vector2 pa = a.GetWorldPosition(scratch), pb = b.GetWorldPosition(scratch);
                    int x = pa.x.CompareTo(pb.x);
                    return x != 0 ? x : pa.y.CompareTo(pb.y);
                });
                List<Vector2> hull = PassiveZoneGeometry.Hull(backbonePoints);
                PassiveZoneGeometry.Layout fixedGeometry = PassiveZoneGeometry.Read(scratch);
                List<PassiveZoneDraft> best = null;
                float bestScore = float.NegativeInfinity;
                int bestPartial = 0;
                var rejections = new Dictionary<string, int>();
                float extent = 0f;
                foreach (Vector2 p in backbonePoints) extent = Mathf.Max(extent, Vector2.Distance(p, origin));

                for (int trial = 0; trial < Compositions; trial++)
                {
                    // In simple mode only the total is prescribed. Explore alternative
                    // size mixes as well as placements; advanced quotas never change.
                    if (!settings.Advanced && trial > 0 && trial % 8 == 0) plan = BuildPlan(settings, rng);
                    var order = new List<ZonePieceKind>(plan);
                    // Constrained shapes are packed first; ties and occasional order
                    // changes allow the entire composition to escape a greedy dead end.
                    Shuffle(order, rng);
                    if (trial % 4 != 3) order.Sort((a, b) => PassiveZoneDraft.MinimumNodes(b).CompareTo(PassiveZoneDraft.MinimumNodes(a)));
                    var occupied = new PassiveZoneGeometry.Layout(); occupied.Append(fixedGeometry);
                    var drafts = new List<PassiveZoneDraft>();
                    int nodes = 0;
                    for (int piece = 0; piece < order.Count; piece++)
                    {
                        int reserved = 0;
                        for (int i = piece + 1; i < order.Count; i++) reserved += PassiveZoneDraft.MinimumNodes(order[i]);
                        int available = budget == int.MaxValue ? 16 : budget - nodes - reserved;
                        PassiveZoneDraft chosen = null;
                        float chosenScore = float.NegativeInfinity;
                        int valid = 0;
                        for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                        {
                            PassiveZoneDraft draft = PassiveZoneDraft.Build(order[piece], rng, available, catalog);
                            if (draft == null) continue;
                            PassiveNodeDefinition anchor = anchors[rng.Next(anchors.Count)];
                            Vector2 anchorPos = anchor.GetWorldPosition(scratch);
                            // Sample a continuous interior target, then grow from the
                            // chosen anchor with a radial or tangent entry. No fixed slots.
                            Vector2 target = PassiveZoneMath.Place(origin,
                                PassiveZoneGeometry.Polar(Range(rng, 239f, 301f), Range(rng, extent * 0.33f, extent * 0.78f)), zone);
                            float direction = Mathf.Atan2(target.y - anchorPos.y, target.x - anchorPos.x) * Mathf.Rad2Deg;
                            direction = Mathf.Round(direction / 7.5f) * 7.5f;
                            draft.Place(anchorPos, anchor.ID, direction, Range(rng, 48f, 76f), piece);
                            string reason;
                            if (!Inside(draft, hull, origin, zone)) reason = "за границей зоны";
                            else if (PassiveZoneGeometry.Fits(draft.Geometry, occupied, out reason))
                            {
                                if (rng.Next(3) == 0)
                                    foreach (PassiveNodeDefinition otherAnchor in anchors)
                                        if (draft.TrySecondEntry(otherAnchor, otherAnchor.GetWorldPosition(scratch), piece, occupied)) break;
                                float score = Score(draft, drafts, origin, zone, extent, rng);
                                if (score > chosenScore) { chosen = draft; chosenScore = score; }
                                if (++valid >= 5) break;
                                continue;
                            }
                            rejections.TryGetValue(reason, out int count); rejections[reason] = count + 1;
                        }
                        if (chosen == null) break;
                        occupied.Append(chosen.Geometry); drafts.Add(chosen); nodes += chosen.Points.Count;
                    }
                    bestPartial = Mathf.Max(bestPartial, drafts.Count);
                    if (drafts.Count != plan.Count) continue;
                float compositionScore = CompositionScore(drafts, origin, zone, extent);
                    if (compositionScore > bestScore) { bestScore = compositionScore; best = drafts; }
                }
                if (best == null)
                {
                    string reason = "недостаточно места";
                    int most = 0;
                    foreach (KeyValuePair<string, int> pair in rejections)
                        if (pair.Value > most && pair.Key != "за границей зоны") { reason = pair.Key; most = pair.Value; }
                    return Fail("Не удалось разместить все " + plan.Count + " сущностей (лучший подбор " + bestPartial + "). " + reason + ". Зона сохранена.");
                }
                // Final audit uses the committed representation, including serialized
                // orbit geometry, instead of trusting only the search drafts.
                if (scratch.Clusters == null) scratch.Clusters = new List<PassiveClusterDefinition>();
                if (scratch.BezierConnections == null) scratch.BezierConnections = new List<PassiveBezierConnection>();
                for (int i = 0; i < best.Count; i++) best[i].Commit(scratch, zone, settings.Seed, i);
                var fixedIds = new HashSet<string>();
                foreach (PassiveZoneGeometry.Node n in fixedGeometry.Nodes) fixedIds.Add(n.Id);
                PassiveZoneGeometry.Layout rendered = PassiveZoneGeometry.Read(scratch);
                var added = new PassiveZoneGeometry.Layout();
                foreach (PassiveZoneGeometry.Node n in rendered.Nodes)
                    if (!fixedIds.Contains(n.Id)) added.Nodes.Add(n);
                foreach (PassiveZoneGeometry.Edge e in rendered.Edges)
                    if (!fixedIds.Contains(e.A) || !fixedIds.Contains(e.B)) added.Edges.Add(e);
                if (!PassiveZoneGeometry.Fits(added, fixedGeometry, out string auditReason))
                    return Fail("Проверка геометрии: " + auditReason + ". Зона сохранена.");
                foreach (PassiveNodeDefinition n in PassiveZoneOps.ContentInZone(scratch, origin, zone))
                    if (n.NodeType == PassiveNodeType.Notable && PassiveZoneMath.NotableCost(scratch, n) < 2)
                        return Fail("Проверка стоимости пути не пройдена. Зона сохранена.");

                // Only this zone is committed. Other nodes retain their object identity,
                // templates, manual edits and references held by the editor selection.
                PassiveZoneOps.ClearZone(tree, zone);
                if (tree.Clusters == null) tree.Clusters = new List<PassiveClusterDefinition>();
                if (tree.BezierConnections == null) tree.BezierConnections = new List<PassiveBezierConnection>();
                int placedNodes = 0;
                for (int i = 0; i < best.Count; i++) { best[i].Commit(tree, zone, settings.Seed, i); placedNodes += best[i].Points.Count; }
                tree.InitLookup();
                return new ZoneGenResult { Success = true, PlacedPieces = best.Count, PlacedNodes = placedNodes,
                    Message = "Зона " + PassiveZoneMath.ZoneNames[zone] + ": сущностей " + best.Count + "/" + plan.Count + ", нодов " + placedNodes +
                        (budget == int.MaxValue ? "" : "/" + budget) + ". Каталог: " + catalog.Count + " форм." };
            }
            finally { UnityEngine.Object.DestroyImmediate(scratch); }
        }

        static bool Inside(PassiveZoneDraft draft, List<Vector2> hull, Vector2 origin, int zone)
        {
            foreach (PassiveZoneGeometry.Node n in draft.Geometry.Nodes)
                if (!InZone(n.Position, n.Radius + 5f, hull, origin, zone)) return false;
            foreach (PassiveZoneGeometry.Edge e in draft.Geometry.Edges)
                for (int i = 1; i < e.Points.Length - 1; i++)
                    if (!InZone(e.Points[i], 3f, hull, origin, zone)) return false;
            if (draft.Radii != null && !InZone(draft.Center, 5f, hull, origin, zone)) return false;
            return true;
        }

        static bool InZone(Vector2 world, float margin, List<Vector2> hull, Vector2 origin, int zone)
        {
            Vector2 local = Local(world - origin, zone);
            return -local.y - Mathf.Abs(local.x) >= margin * 1.414214f &&
                   Vector2.Distance(world, origin) >= 70f + margin && PassiveZoneGeometry.Inside(hull, world, margin);
        }

        static Vector2 Local(Vector2 p, int zone) => PassiveZoneGeometry.Rotate(p, -90f * zone);
        static float Range(System.Random rng, float min, float max) => min + (max - min) * (float)rng.NextDouble();

        static float Score(PassiveZoneDraft draft, List<PassiveZoneDraft> others, Vector2 origin, int zone, float extent, System.Random rng)
        {
            float score = Range(rng, 0f, 2f);
            Vector2 center = CenterOf(draft);
            foreach (PassiveZoneDraft other in others)
            {
                if (draft.Motif == other.Motif) score -= 25f;
                if (draft.Anchor == other.Anchor) score -= 5f;
                float distance = Vector2.Distance(center, CenterOf(other));
                // Prefer readable separation without scattering every piece into a
                // different corner. Similar orbit sizes echo one another locally.
                score += Mathf.Clamp(distance / 50f, 0f, 4f);
                if (draft.Radii != null && other.Radii != null)
                {
                    float a = draft.Radii[draft.Radii.Length - 1], b = other.Radii[other.Radii.Length - 1];
                    score += 2f * (1f - Mathf.Clamp01(Mathf.Abs(a - b) / Mathf.Max(a, b)));
                }
                score += EchoScore(draft, other);
            }
            if (!string.IsNullOrEmpty(draft.SecondAnchor)) score += 3f;
            return score;
        }

        static float EchoScore(PassiveZoneDraft a, PassiveZoneDraft b)
        {
            float best = 0f;
            foreach (PassiveZoneGeometry.Edge first in a.Geometry.Edges)
            {
                if (first.Points.Length < 4) continue;
                int i = first.Points.Length / 2;
                Vector2 tangentA = (first.Points[i + 1] - first.Points[i - 1]).normalized;
                foreach (PassiveZoneGeometry.Edge second in b.Geometry.Edges)
                {
                    if (second.Points.Length < 4) continue;
                    int j = second.Points.Length / 2;
                    float separation = Vector2.Distance(first.Points[i], second.Points[j]);
                    if (separation < 30f || separation > 100f) continue;
                    Vector2 tangentB = (second.Points[j + 1] - second.Points[j - 1]).normalized;
                    float alignment = Mathf.Abs(Vector2.Dot(tangentA, tangentB));
                    best = Mathf.Max(best, Mathf.Max(0f, alignment - 0.85f) * 24f);
                }
            }
            return best;
        }

        static Vector2 CenterOf(PassiveZoneDraft draft)
        {
            Vector2 center = Vector2.zero;
            foreach (PassiveZoneDraft.Point p in draft.Points) center += p.Position;
            return center / draft.Points.Count;
        }

        static float CompositionScore(List<PassiveZoneDraft> drafts, Vector2 origin, int zone, float extent)
        {
            var motifs = new HashSet<string>();
            var anchors = new HashSet<string>();
            Vector2 center = Vector2.zero;
            float minX = float.MaxValue, maxX = float.MinValue;
            int nodes = 0;
            float echoes = 0f;
            for (int i = 0; i < drafts.Count; i++)
                for (int j = i + 1; j < drafts.Count; j++) echoes += EchoScore(drafts[i], drafts[j]);
            foreach (PassiveZoneDraft d in drafts)
            {
                motifs.Add(d.Motif); anchors.Add(d.Anchor);
                if (!string.IsNullOrEmpty(d.SecondAnchor)) anchors.Add(d.SecondAnchor);
                foreach (PassiveZoneDraft.Point p in d.Points)
                {
                    Vector2 local = Local(p.Position - origin, zone);
                    center += local; nodes++;
                    minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                }
            }
            if (nodes == 0) return 0f;
            center /= nodes;
            return motifs.Count * 12f + anchors.Count * 3f + echoes + (maxX - minX) / extent * 4f - Mathf.Abs(center.x) / extent * 4f;
        }

        public static List<ZonePieceKind> BuildPlan(ZoneGenSettings settings, System.Random rng)
        {
            var result = new List<ZonePieceKind>();
            void Add(ZonePieceKind kind, int count)
            {
                for (int i = 0; i < Mathf.Clamp(count, 0, 25); i++) result.Add(kind);
            }
            if (settings.Advanced)
            {
                Add(ZonePieceKind.Small, settings.Small); Add(ZonePieceKind.Medium, settings.Medium);
                Add(ZonePieceKind.Large, settings.Large); Add(ZonePieceKind.Complex, settings.Complex);
                Add(ZonePieceKind.DeadEnd, settings.DeadEnds);
            }
            else
            {
                int pieces = Mathf.Clamp(settings.Pieces, 0, 25);
                int spent = 0;
                for (int i = 0; i < pieces; i++)
                {
                    double roll = rng.NextDouble();
                    ZonePieceKind kind = roll < 0.25 ? ZonePieceKind.DeadEnd : roll < 0.5 ? ZonePieceKind.Small :
                        roll < 0.76 ? ZonePieceKind.Medium : roll < 0.89 ? ZonePieceKind.Large : ZonePieceKind.Complex;
                    int needed = spent + PassiveZoneDraft.MinimumNodes(kind) + (pieces - i - 1) * 3;
                    if (settings.Nodes > 0 && needed > settings.Nodes) kind = ZonePieceKind.Small;
                    result.Add(kind); spent += PassiveZoneDraft.MinimumNodes(kind);
                }
            }
            Shuffle(result, rng);
            return result;
        }

        static void Shuffle<T>(List<T> values, System.Random rng)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        static ZoneGenResult Fail(string reason) => new ZoneGenResult { Message = reason };
    }
}
