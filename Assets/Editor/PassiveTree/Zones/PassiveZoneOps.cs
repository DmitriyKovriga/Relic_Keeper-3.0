using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    public static class PassiveZoneOps
    {
        static bool _busy;

        public static void EnsureLinks(PassiveSkillTreeSO tree)
        {
            if (tree.ZoneLinks == null)
                tree.ZoneLinks = new List<PassiveZoneLink>();
            EnsurePair(tree, 0, 2);
            EnsurePair(tree, 1, 3);
        }

        public static void MarkAllAsBackbone(PassiveSkillTreeSO tree)
        {
            if (tree?.Nodes == null)
                return;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                if (tree.Nodes[i] != null && tree.Nodes[i].NodeType != PassiveNodeType.Start)
                    tree.Nodes[i].IsBackbone = true;
            }
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start != null)
                start.IsBackbone = true;
            CaptureBackboneEdges(tree);
        }

        public static void MarkNodes(PassiveSkillTreeSO tree, IList<PassiveNodeDefinition> nodes, bool backbone)
        {
            if (nodes == null)
                return;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    nodes[i].IsBackbone = backbone || nodes[i].NodeType == PassiveNodeType.Start;
            }
            CaptureBackboneEdges(tree);
        }

        public static void ClearBackbone(PassiveSkillTreeSO tree)
        {
            if (tree?.Nodes == null)
                return;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                if (tree.Nodes[i] != null)
                    tree.Nodes[i].IsBackbone = false;
            }
            tree.BackboneEdgeA = new List<string>();
            tree.BackboneEdgeB = new List<string>();
        }

        public static void CaptureBackboneEdges(PassiveSkillTreeSO tree)
        {
            tree.BackboneEdgeA = new List<string>();
            tree.BackboneEdgeB = new List<string>();
            if (tree.Nodes == null)
                return;
            tree.InitLookup();
            var seen = new HashSet<string>();
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node == null || !node.IsBackbone || node.ConnectionIDs == null)
                    continue;
                for (int c = 0; c < node.ConnectionIDs.Count; c++)
                {
                    PassiveNodeDefinition other = tree.GetNode(node.ConnectionIDs[c]);
                    if (other == null || !other.IsBackbone)
                        continue;
                    string key = node.ID.CompareTo(other.ID) < 0 ? node.ID + "|" + other.ID : other.ID + "|" + node.ID;
                    if (!seen.Add(key))
                        continue;
                    tree.BackboneEdgeA.Add(node.ID);
                    tree.BackboneEdgeB.Add(other.ID);
                }
            }
        }

        public static void RestoreBackboneEdges(PassiveSkillTreeSO tree)
        {
            if (tree == null || !tree.ZoneToolsEnabled || !tree.LockBackbone)
                return;
            if (tree.BackboneEdgeA == null || tree.BackboneEdgeB == null)
                return;
            tree.InitLookup();
            int count = Mathf.Min(tree.BackboneEdgeA.Count, tree.BackboneEdgeB.Count);
            for (int i = 0; i < count; i++)
            {
                PassiveNodeDefinition a = tree.GetNode(tree.BackboneEdgeA[i]);
                PassiveNodeDefinition b = tree.GetNode(tree.BackboneEdgeB[i]);
                if (a == null || b == null)
                    continue;
                AddLink(a, b);
            }
        }

        public static void FlipZone(PassiveSkillTreeSO tree, int zone)
        {
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start == null)
                return;
            Vector2 origin = start.GetWorldPosition(tree);
            bool horizontal = PassiveZoneMath.FlipIsHorizontal(zone);
            List<PassiveNodeDefinition> nodes = ContentInZone(tree, origin, zone);
            var clusters = ClustersOf(tree, nodes);
            var world = new Vector2[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                world[i] = nodes[i].GetWorldPosition(tree);
            var clusterWorld = new Vector2[clusters.Count];
            for (int i = 0; i < clusters.Count; i++)
                clusterWorld[i] = clusters[i].Center;
            for (int i = 0; i < clusters.Count; i++)
                clusters[i].Center = PassiveZoneMath.Flip(origin, clusterWorld[i], horizontal);
            for (int i = 0; i < nodes.Count; i++)
            {
                PassiveNodeDefinition node = nodes[i];
                node.Position = PassiveZoneMath.Flip(origin, world[i], horizontal);
                if (node.PlacementMode == NodePlacementMode.OnOrbit)
                    node.OrbitAngle = PassiveZoneMath.FlipAngle(node.OrbitAngle, horizontal);
            }
            FlipBeziers(tree, nodes, origin, horizontal, world);
        }

        public static void SyncStructure(PassiveSkillTreeSO tree)
        {
            if (_busy || tree == null || !tree.ZoneToolsEnabled)
                return;
            _busy = true;
            try
            {
                RestoreBackboneEdges(tree);
                EnsureLinks(tree);
                for (int i = 0; i < tree.ZoneLinks.Count; i++)
                {
                    PassiveZoneLink link = tree.ZoneLinks[i];
                    if (link != null && link.Enabled)
                        CopyZone(tree, link.SourceZone, link.TargetZone);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        public static void SyncPositions(PassiveSkillTreeSO tree)
        {
            if (_busy || tree == null || !tree.ZoneToolsEnabled || tree.ZoneLinks == null)
                return;
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start == null)
                return;
            _busy = true;
            try
            {
                tree.InitLookup();
                Vector2 origin = start.GetWorldPosition(tree);
                for (int i = 0; i < tree.ZoneLinks.Count; i++)
                {
                    PassiveZoneLink link = tree.ZoneLinks[i];
                    if (link == null || !link.Enabled)
                        continue;
                    ApplyLiveMirror(tree, origin, link.SourceZone);
                }
            }
            finally
            {
                _busy = false;
            }
        }

        public static void MakeDriver(PassiveSkillTreeSO tree, int zone)
        {
            EnsureLinks(tree);
            int opposite = PassiveZoneMath.Opposite(zone);
            for (int i = 0; i < tree.ZoneLinks.Count; i++)
            {
                PassiveZoneLink link = tree.ZoneLinks[i];
                if (link == null)
                    continue;
                bool pair = (link.SourceZone == zone && link.TargetZone == opposite)
                    || (link.SourceZone == opposite && link.TargetZone == zone)
                    || (link.SourceZone == zone && link.TargetZone == zone);
                if ((link.SourceZone == zone || link.SourceZone == opposite) &&
                    (link.TargetZone == zone || link.TargetZone == opposite))
                {
                    link.SourceZone = zone;
                    link.TargetZone = opposite;
                }
            }
        }

        public static PassiveZoneLink FindPair(PassiveSkillTreeSO tree, int zone)
        {
            EnsureLinks(tree);
            int opposite = PassiveZoneMath.Opposite(zone);
            for (int i = 0; i < tree.ZoneLinks.Count; i++)
            {
                PassiveZoneLink link = tree.ZoneLinks[i];
                if (link == null)
                    continue;
                if ((link.SourceZone == zone || link.SourceZone == opposite) &&
                    (link.TargetZone == zone || link.TargetZone == opposite))
                    return link;
            }
            return null;
        }

        public static void CopyZone(PassiveSkillTreeSO tree, int sourceZone, int targetZone)
        {
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start == null || sourceZone == targetZone)
                return;
            Vector2 origin = start.GetWorldPosition(tree);
            ClearZone(tree, targetZone);
            tree.InitLookup();
            List<PassiveNodeDefinition> source = ContentInZone(tree, origin, sourceZone);
            if (source.Count == 0)
                return;

            var clusterMap = new Dictionary<string, PassiveClusterDefinition>();
            foreach (PassiveClusterDefinition cluster in ClustersOf(tree, source))
            {
                var copy = new PassiveClusterDefinition
                {
                    ID = System.Guid.NewGuid().ToString(),
                    Name = cluster.Name,
                    Center = PassiveZoneMath.Rotate(origin, cluster.Center, 2),
                    EditorColor = cluster.EditorColor,
                    Orbits = new List<PassiveOrbitDefinition>(),
                    RoadConnections = new List<string>()
                };
                if (cluster.Orbits != null)
                {
                    for (int i = 0; i < cluster.Orbits.Count; i++)
                    {
                        PassiveOrbitDefinition orbit = cluster.Orbits[i];
                        copy.Orbits.Add(new PassiveOrbitDefinition
                        {
                            Radius = orbit.Radius,
                            IsPartialArc = orbit.IsPartialArc,
                            ArcStartAngle = orbit.ArcStartAngle,
                            ArcEndAngle = orbit.ArcEndAngle
                        });
                    }
                }
                tree.Clusters.Add(copy);
                clusterMap[cluster.ID] = copy;
            }

            var idMap = new Dictionary<string, string>();
            var copies = new List<PassiveNodeDefinition>();
            for (int i = 0; i < source.Count; i++)
            {
                PassiveNodeDefinition node = source[i];
                var copy = new PassiveNodeDefinition
                {
                    ID = System.Guid.NewGuid().ToString(),
                    NodeType = node.NodeType,
                    PlacementMode = node.PlacementMode,
                    Position = PassiveZoneMath.Rotate(origin, node.GetWorldPosition(tree), 2),
                    OrbitIndex = node.OrbitIndex,
                    OrbitAngle = PassiveZoneMath.RotateAngle(node.OrbitAngle, 2),
                    Template = node.Template,
                    UniqueModifiers = node.UniqueModifiers != null ? new List<Scripts.Stats.SerializableStatModifier>(node.UniqueModifiers) : null,
                    UniqueStatScalingRules = node.UniqueStatScalingRules != null ? new List<PassiveStatScalingRule>(node.UniqueStatScalingRules) : null,
                    ConnectionIDs = new List<string>(),
                    IsBackbone = false
                };
                if (node.PlacementMode == NodePlacementMode.OnOrbit && clusterMap.TryGetValue(node.ClusterID ?? "", out PassiveClusterDefinition mapped))
                    copy.ClusterID = mapped.ID;
                tree.Nodes.Add(copy);
                idMap[node.ID] = copy.ID;
                copies.Add(copy);
                node.ZoneTwinId = copy.ID;
                copy.ZoneTwinId = node.ID;
            }

            tree.InitLookup();
            for (int i = 0; i < source.Count; i++)
            {
                PassiveNodeDefinition node = source[i];
                PassiveNodeDefinition copy = tree.GetNode(idMap[node.ID]);
                if (node.ConnectionIDs == null)
                    continue;
                for (int c = 0; c < node.ConnectionIDs.Count; c++)
                {
                    string neighborId = node.ConnectionIDs[c];
                    if (idMap.TryGetValue(neighborId, out string mapped))
                    {
                        AddLink(copy, tree.GetNode(mapped));
                        continue;
                    }
                    PassiveNodeDefinition neighbor = tree.GetNode(neighborId);
                    if (neighbor != null && neighbor.IsBackbone)
                        AddLink(copy, OppositeBackbone(tree, origin, neighbor));
                }
            }

            CopyBeziers(tree, source, idMap, origin);
        }

        static void CopyBeziers(PassiveSkillTreeSO tree, List<PassiveNodeDefinition> source, Dictionary<string, string> idMap, Vector2 origin)
        {
            if (tree.BezierConnections == null)
                return;
            var sourceIds = new HashSet<string>();
            for (int i = 0; i < source.Count; i++)
                sourceIds.Add(source[i].ID);
            int count = tree.BezierConnections.Count;
            for (int i = 0; i < count; i++)
            {
                PassiveBezierConnection bezier = tree.BezierConnections[i];
                if (bezier == null || !sourceIds.Contains(bezier.NodeIdA) || !sourceIds.Contains(bezier.NodeIdB))
                    continue;
                if (!idMap.ContainsKey(bezier.NodeIdA) || !idMap.ContainsKey(bezier.NodeIdB))
                    continue;
                PassiveNodeDefinition srcA = tree.GetNode(bezier.NodeIdA);
                PassiveNodeDefinition srcB = tree.GetNode(bezier.NodeIdB);
                PassiveNodeDefinition dstA = tree.GetNode(idMap[bezier.NodeIdA]);
                PassiveNodeDefinition dstB = tree.GetNode(idMap[bezier.NodeIdB]);
                if (srcA == null || srcB == null || dstA == null || dstB == null)
                    continue;
                bezier.GetCubicPoints(srcA.GetWorldPosition(tree), srcB.GetWorldPosition(tree), out _, out Vector2 c1, out Vector2 c2, out _);
                var clone = new PassiveBezierConnection
                {
                    NodeIdA = dstA.ID,
                    NodeIdB = dstB.ID,
                    AnchorPercent = 50f,
                    MirrorHandles = false
                };
                WriteHandles(
                    clone,
                    dstA.GetWorldPosition(tree),
                    dstB.GetWorldPosition(tree),
                    PassiveZoneMath.Rotate(origin, c1, 2),
                    PassiveZoneMath.Rotate(origin, c2, 2));
                tree.BezierConnections.Add(clone);
            }
        }

        public static void ClearZone(PassiveSkillTreeSO tree, int zone)
        {
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            if (start == null)
                return;
            Vector2 origin = start.GetWorldPosition(tree);
            List<PassiveNodeDefinition> nodes = ContentInZone(tree, origin, zone);
            RemoveNodes(tree, nodes);
        }

        static void ApplyLiveMirror(PassiveSkillTreeSO tree, Vector2 origin, int sourceZone)
        {
            List<PassiveNodeDefinition> source = ContentInZone(tree, origin, sourceZone);
            var movedClusters = new HashSet<string>();
            for (int i = 0; i < source.Count; i++)
            {
                PassiveNodeDefinition node = source[i];
                PassiveNodeDefinition twin = tree.GetNode(node.ZoneTwinId);
                if (twin == null || twin.IsBackbone)
                    continue;
                twin.Position = PassiveZoneMath.Rotate(origin, node.GetWorldPosition(tree), 2);
                twin.NodeType = node.NodeType;
                if (node.PlacementMode == NodePlacementMode.OnOrbit)
                {
                    twin.OrbitAngle = PassiveZoneMath.RotateAngle(node.OrbitAngle, 2);
                    if (!string.IsNullOrEmpty(node.ClusterID) && movedClusters.Add(node.ClusterID))
                    {
                        PassiveClusterDefinition cluster = tree.GetCluster(node.ClusterID);
                        PassiveClusterDefinition twinCluster = tree.GetCluster(twin.ClusterID);
                        if (cluster != null && twinCluster != null)
                            twinCluster.Center = PassiveZoneMath.Rotate(origin, cluster.Center, 2);
                    }
                }
            }
            MirrorLiveBeziers(tree, origin, source);
        }

        static void MirrorLiveBeziers(PassiveSkillTreeSO tree, Vector2 origin, List<PassiveNodeDefinition> source)
        {
            if (tree.BezierConnections == null)
                return;
            var sourceIds = new HashSet<string>();
            for (int i = 0; i < source.Count; i++)
                sourceIds.Add(source[i].ID);
            for (int i = 0; i < tree.BezierConnections.Count; i++)
            {
                PassiveBezierConnection bezier = tree.BezierConnections[i];
                if (bezier == null || !sourceIds.Contains(bezier.NodeIdA) || !sourceIds.Contains(bezier.NodeIdB))
                    continue;
                PassiveNodeDefinition srcA = tree.GetNode(bezier.NodeIdA);
                PassiveNodeDefinition srcB = tree.GetNode(bezier.NodeIdB);
                PassiveNodeDefinition dstA = srcA != null ? tree.GetNode(srcA.ZoneTwinId) : null;
                PassiveNodeDefinition dstB = srcB != null ? tree.GetNode(srcB.ZoneTwinId) : null;
                PassiveBezierConnection twin = dstA != null && dstB != null ? tree.FindBezierConnection(dstA.ID, dstB.ID) : null;
                if (twin == null)
                    continue;
                bezier.GetCubicPoints(srcA.GetWorldPosition(tree), srcB.GetWorldPosition(tree), out _, out Vector2 c1, out Vector2 c2, out _);
                Vector2 rc1 = PassiveZoneMath.Rotate(origin, c1, 2);
                Vector2 rc2 = PassiveZoneMath.Rotate(origin, c2, 2);
                bool swapped = twin.NodeIdA != dstA.ID;
                if (swapped)
                    (rc1, rc2) = (rc2, rc1);
                Vector2 anchor = Vector2.Lerp(
                    tree.GetNode(twin.NodeIdA).GetWorldPosition(tree),
                    tree.GetNode(twin.NodeIdB).GetWorldPosition(tree),
                    Mathf.Clamp01(twin.AnchorPercent / 100f));
                twin.InHandleOffset = rc1 - anchor;
                twin.OutHandleOffset = rc2 - anchor;
            }
        }

        static void FlipBeziers(PassiveSkillTreeSO tree, List<PassiveNodeDefinition> nodes, Vector2 origin, bool horizontal, Vector2[] previousWorld)
        {
            if (tree.BezierConnections == null)
                return;
            var index = new Dictionary<string, int>();
            for (int i = 0; i < nodes.Count; i++)
                index[nodes[i].ID] = i;
            for (int i = 0; i < tree.BezierConnections.Count; i++)
            {
                PassiveBezierConnection bezier = tree.BezierConnections[i];
                if (bezier == null || !index.ContainsKey(bezier.NodeIdA) || !index.ContainsKey(bezier.NodeIdB))
                    continue;
                PassiveNodeDefinition a = nodes[index[bezier.NodeIdA]];
                PassiveNodeDefinition b = nodes[index[bezier.NodeIdB]];
                Vector2 preA = previousWorld[index[bezier.NodeIdA]];
                Vector2 preB = previousWorld[index[bezier.NodeIdB]];
                bezier.GetCubicPoints(preA, preB, out _, out Vector2 c1, out Vector2 c2, out _);
                WriteHandles(
                    bezier,
                    a.GetWorldPosition(tree),
                    b.GetWorldPosition(tree),
                    PassiveZoneMath.Flip(origin, c1, horizontal),
                    PassiveZoneMath.Flip(origin, c2, horizontal));
            }
        }

        static void WriteHandles(PassiveBezierConnection bezier, Vector2 a, Vector2 b, Vector2 c1, Vector2 c2)
        {
            Vector2 anchor = Vector2.Lerp(a, b, Mathf.Clamp01(bezier.AnchorPercent / 100f));
            bezier.InHandleOffset = c1 - anchor;
            bezier.OutHandleOffset = c2 - anchor;
            bezier.NormalizeIds();
        }

        static PassiveNodeDefinition OppositeBackbone(PassiveSkillTreeSO tree, Vector2 origin, PassiveNodeDefinition backbone)
        {
            if (backbone.NodeType == PassiveNodeType.Start)
                return backbone;
            Vector2 wanted = PassiveZoneMath.Rotate(origin, backbone.GetWorldPosition(tree), 2);
            PassiveNodeDefinition best = null;
            float bestDist = 48f;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node == null || !node.IsBackbone)
                    continue;
                float dist = Vector2.Distance(node.GetWorldPosition(tree), wanted);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = node;
                }
            }
            return best;
        }

        public static List<PassiveNodeDefinition> ContentInZone(PassiveSkillTreeSO tree, Vector2 origin, int zone)
        {
            var list = new List<PassiveNodeDefinition>();
            if (tree.Nodes == null)
                return list;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node == null || node.IsBackbone || node.NodeType == PassiveNodeType.Start)
                    continue;
                if (PassiveZoneMath.ZoneOf(origin, node.GetWorldPosition(tree)) == zone)
                    list.Add(node);
            }
            return list;
        }

        static List<PassiveClusterDefinition> ClustersOf(PassiveSkillTreeSO tree, List<PassiveNodeDefinition> nodes)
        {
            var list = new List<PassiveClusterDefinition>();
            var seen = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].PlacementMode != NodePlacementMode.OnOrbit || string.IsNullOrEmpty(nodes[i].ClusterID))
                    continue;
                if (!seen.Add(nodes[i].ClusterID))
                    continue;
                PassiveClusterDefinition cluster = tree.GetCluster(nodes[i].ClusterID);
                if (cluster != null)
                    list.Add(cluster);
            }
            return list;
        }

        static void RemoveNodes(PassiveSkillTreeSO tree, List<PassiveNodeDefinition> nodes)
        {
            if (nodes.Count == 0)
                return;
            var ids = new HashSet<string>();
            var clusterIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                ids.Add(nodes[i].ID);
                if (!string.IsNullOrEmpty(nodes[i].ClusterID))
                    clusterIds.Add(nodes[i].ClusterID);
            }
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node?.ConnectionIDs == null)
                    continue;
                if (node.ZoneTwinId != null && ids.Contains(node.ZoneTwinId))
                    node.ZoneTwinId = null;
                node.ConnectionIDs.RemoveAll(ids.Contains);
            }
            tree.Nodes.RemoveAll(node => node != null && ids.Contains(node.ID));
            if (tree.BezierConnections != null)
            {
                tree.BezierConnections.RemoveAll(bezier => bezier != null &&
                    (ids.Contains(bezier.NodeIdA) || ids.Contains(bezier.NodeIdB)));
            }
            if (tree.Clusters != null)
            {
                tree.Clusters.RemoveAll(cluster =>
                {
                    if (cluster == null || !clusterIds.Contains(cluster.ID))
                        return false;
                    for (int i = 0; i < tree.Nodes.Count; i++)
                    {
                        if (tree.Nodes[i] != null && tree.Nodes[i].ClusterID == cluster.ID)
                            return false;
                    }
                    return true;
                });
            }
            tree.InitLookup();
        }

        static void AddLink(PassiveNodeDefinition a, PassiveNodeDefinition b)
        {
            if (a == null || b == null || a == b)
                return;
            if (a.ConnectionIDs == null) a.ConnectionIDs = new List<string>();
            if (b.ConnectionIDs == null) b.ConnectionIDs = new List<string>();
            if (!a.ConnectionIDs.Contains(b.ID)) a.ConnectionIDs.Add(b.ID);
            if (!b.ConnectionIDs.Contains(a.ID)) b.ConnectionIDs.Add(a.ID);
        }

        static void EnsurePair(PassiveSkillTreeSO tree, int a, int b)
        {
            for (int i = 0; i < tree.ZoneLinks.Count; i++)
            {
                PassiveZoneLink link = tree.ZoneLinks[i];
                if (link == null)
                    continue;
                if ((link.SourceZone == a && link.TargetZone == b) || (link.SourceZone == b && link.TargetZone == a))
                    return;
            }
            tree.ZoneLinks.Add(new PassiveZoneLink { SourceZone = a, TargetZone = b, Enabled = false });
        }
    }
}
