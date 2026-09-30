using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Scripts.Editor.PassiveTree;
using Scripts.Skills.PassiveTree;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveZoneCompositionTests
    {
        static PassiveSkillTreeSO Backbone()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            tree.Nodes.Add(new PassiveNodeDefinition { ID = "start", NodeType = PassiveNodeType.Start,
                IsBackbone = true, ConnectionIDs = new List<string>() });
            var commands = new PassiveTreeEditorCommands(); commands.SetTree(tree);
            commands.GenerateBackboneFromStart();
            return tree;
        }

        [Test]
        public void Catalog_ContainsBroadConnectedGeometryWithoutDanglingEdges()
        {
            var motifs = PassiveZoneMotifLibrary.Motifs;
            Assert.That(motifs.Count, Is.GreaterThanOrEqualTo(80));
            Assert.That(motifs.Select(m => m.id).Distinct().Count(), Is.EqualTo(motifs.Count));
            foreach (ZoneMotif m in motifs)
            {
                Assert.That(m.nodes.Length, Is.InRange(3, 16), m.id);
                var connected = new HashSet<int> { 0 };
                for (int pass = 0; pass < m.nodes.Length; pass++)
                    foreach (ZoneMotifEdge e in m.edges)
                    {
                        Assert.That(e.a, Is.InRange(0, m.nodes.Length - 1));
                        Assert.That(e.b, Is.InRange(0, m.nodes.Length - 1));
                        Assert.That(e.a, Is.Not.EqualTo(e.b));
                        if (connected.Contains(e.a) || connected.Contains(e.b)) { connected.Add(e.a); connected.Add(e.b); }
                    }
                Assert.That(connected.Count, Is.EqualTo(m.nodes.Length), m.id);
                foreach (ZoneMotifNode n in m.nodes)
                    Assert.That(n.orbit, Is.InRange(0, m.radii.Length - 1));
            }
        }

        [TestCase(1, 0)]
        [TestCase(7, 0)]
        [TestCase(19, 0)]
        [TestCase(37, 0)]
        [TestCase(51, 0)]
        [TestCase(83, 0)]
        [TestCase(107, 0)]
        [TestCase(1, 1)]
        [TestCase(7, 2)]
        [TestCase(19, 3)]
        public void RequestedFivePieces_FitBudgetAndRenderedGeometry(int seed, int zone)
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                var before = PassiveZoneGeometry.Read(tree);
                var backboneIds = new HashSet<string>(tree.Nodes.Select(n => n.ID));
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, zone, new ZoneGenSettings { Pieces = 5, Nodes = 32, Seed = seed });
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.PlacedPieces, Is.EqualTo(5));
                Assert.That(result.PlacedNodes, Is.InRange(15, 32));
                List<PassiveNodeDefinition> added = tree.Nodes.Where(n => !backboneIds.Contains(n.ID)).ToList();
                Assert.That(added.Count, Is.EqualTo(result.PlacedNodes));
                Assert.That(CountPieces(tree, added), Is.EqualTo(5));
                foreach (PassiveNodeDefinition n in added)
                {
                    Assert.That(PassiveZoneMath.ZoneOf(Vector2.zero, n.GetWorldPosition(tree)), Is.EqualTo(zone));
                    Assert.That(n.Template, Is.Null);
                    foreach (string id in n.ConnectionIDs) Assert.That(tree.GetNode(id).ConnectionIDs, Does.Contain(n.ID));
                    if (n.NodeType == PassiveNodeType.Notable) Assert.That(PassiveZoneMath.NotableCost(tree, n), Is.GreaterThanOrEqualTo(2));
                }
                var geometry = PassiveZoneGeometry.Read(tree);
                var newGeometry = new PassiveZoneGeometry.Layout();
                newGeometry.Nodes.AddRange(geometry.Nodes.Where(n => !backboneIds.Contains(n.Id)));
                newGeometry.Edges.AddRange(geometry.Edges.Where(e => !backboneIds.Contains(e.A) || !backboneIds.Contains(e.B)));
                Assert.That(PassiveZoneGeometry.Fits(newGeometry, before, out string reason), Is.True, reason);
                Export(tree, result, seed, zone);
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void AdvancedCounts_AreExactAndAllRewardPathsRespectTheirTier()
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, new ZoneGenSettings {
                    Advanced = true, Small = 1, Medium = 1, Large = 1, Complex = 1, DeadEnds = 1, Nodes = 40, Seed = 27 });
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.PlacedPieces, Is.EqualTo(5));
                foreach (string tier in new[] { "Small", "Medium", "Large", "Complex" })
                    Assert.That(tree.Clusters.Count(c => c.Name.Contains(" " + tier + " /")), Is.EqualTo(1), tier);
                foreach (PassiveNodeDefinition node in tree.Nodes.Where(n => !n.IsBackbone && n.NodeType == PassiveNodeType.Notable))
                {
                    PassiveClusterDefinition cluster = string.IsNullOrEmpty(node.ClusterID) ? null : tree.GetCluster(node.ClusterID);
                    int min = cluster == null ? 2 : cluster.Name.Contains(" Medium /") ? 3 :
                        cluster.Name.Contains(" Large /") || cluster.Name.Contains(" Complex /") ? 4 : 2;
                    Assert.That(PassiveZoneMath.NotableCost(tree, node), Is.GreaterThanOrEqualTo(min));
                }
                Export(tree, result, 27, 0, "advanced");
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void SameSeed_ReproducesGeometryAndDifferentSeedsChangeStructure()
        {
            var signatures = new HashSet<string>();
            var motifs = new HashSet<string>();
            for (int seed = 1; seed <= 6; seed++)
            {
                PassiveSkillTreeSO a = Backbone();
                PassiveSkillTreeSO b = null;
                try
                {
                    var settings = new ZoneGenSettings { Pieces = 5, Nodes = 32, Seed = seed };
                    ZoneGenResult result = PassiveZoneGenerator.Generate(a, 0, settings);
                    Assert.That(result.Success, Is.True, "Seed " + seed + ": " + result.Message);
                    string signature = Signature(a);
                    signatures.Add(signature);
                    foreach (PassiveClusterDefinition c in a.Clusters) motifs.Add(c.Name.Split('/')[1].Trim());
                    if (seed == 1)
                    {
                        b = Backbone();
                        Assert.That(PassiveZoneGenerator.Generate(b, 0, settings).Success, Is.True);
                        Assert.That(Signature(b), Is.EqualTo(signature));
                    }
                    Export(a, result, seed, 0);
                }
                finally { Object.DestroyImmediate(a); if (b != null) Object.DestroyImmediate(b); }
            }
            Assert.That(signatures.Count, Is.EqualTo(6));
            Assert.That(motifs.Count, Is.GreaterThanOrEqualTo(8), "Variety must include different motif families, not only rotations.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImpossibleRequest_IsAtomicIncludingManualNodesAndConnections(bool cramped)
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                var manual = new PassiveNodeDefinition { ID = "manual", Position = new Vector2(0f, -250f),
                    ConnectionIDs = new List<string> { "start" } };
                tree.Nodes.Add(manual); tree.Nodes[0].ConnectionIDs.Add(manual.ID);
                if (cramped) foreach (PassiveNodeDefinition n in tree.Nodes) n.Position *= 0.12f;
                string before = EditorJsonUtility.ToJson(tree);
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, new ZoneGenSettings { Pieces = 5, Nodes = cramped ? 32 : 4, Seed = 1 });
                Assert.That(result.Success, Is.False);
                Assert.That(result.PlacedPieces, Is.Zero);
                Assert.That(EditorJsonUtility.ToJson(tree), Is.EqualTo(before));
                Assert.That(tree.Nodes, Does.Contain(manual));
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void Cost_CountsOnlySmallNodesAndUsesCheapestEntry()
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                PassiveNodeDefinition last = tree.Nodes[0];
                foreach (var type in new[] { PassiveNodeType.Small, PassiveNodeType.Notable, PassiveNodeType.Small, PassiveNodeType.Notable })
                {
                    var next = new PassiveNodeDefinition { ID = "test-" + tree.Nodes.Count, NodeType = type, ConnectionIDs = new List<string> { last.ID } };
                    last.ConnectionIDs.Add(next.ID); tree.Nodes.Add(next); last = next;
                }
                Assert.That(PassiveZoneMath.NotableCost(tree, last), Is.EqualTo(2));
                last.ConnectionIDs.Add(tree.Nodes[1].ID); tree.Nodes[1].ConnectionIDs.Add(last.ID);
                Assert.That(PassiveZoneMath.NotableCost(tree, last), Is.Zero);
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void TranslatedLargerBackbone_PreservesNeighbourAndExternalKeystone()
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                Vector2 origin = new Vector2(1800f, 1400f);
                foreach (PassiveNodeDefinition n in tree.Nodes) n.Position = n.Position * 1.3f + origin;
                var neighbour = new PassiveNodeDefinition { ID = "neighbour", Position = origin + new Vector2(80f, 300f), ConnectionIDs = new List<string>() };
                var key = new PassiveNodeDefinition { ID = "external-key", NodeType = PassiveNodeType.Keystone,
                    IsBackbone = true, Position = origin + new Vector2(0f, -740f), ConnectionIDs = new List<string>() };
                tree.Nodes.Add(neighbour); tree.Nodes.Add(key);
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, new ZoneGenSettings { Pieces = 5, Nodes = 32, Seed = 1 });
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(tree.GetNode(neighbour.ID), Is.SameAs(neighbour));
                Assert.That(neighbour.Position, Is.EqualTo(origin + new Vector2(80f, 300f)));
                Assert.That(key.ConnectionIDs, Is.Empty);
                Assert.That(PassiveZoneOps.ContentInZone(tree, origin, 0).Count, Is.EqualTo(result.PlacedNodes));
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void ZeroEntities_ClearsOnlyTheRequestedZone()
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                var north = new PassiveNodeDefinition { ID = "north", Position = new Vector2(0f, -260f), ConnectionIDs = new List<string>() };
                var south = new PassiveNodeDefinition { ID = "south", Position = new Vector2(0f, 260f), ConnectionIDs = new List<string>() };
                tree.Nodes.Add(north); tree.Nodes.Add(south);
                int count = tree.Nodes.Count;
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, new ZoneGenSettings { Pieces = 0, Nodes = 0, Seed = 1 });
                Assert.That(result.Success, Is.True, result.Message);
                Assert.That(result.PlacedNodes, Is.Zero);
                Assert.That(tree.Nodes.Count, Is.EqualTo(count - 1));
                Assert.That(tree.GetNode("south"), Is.SameAs(south));
                Assert.That(tree.GetNode("north"), Is.Null);
            }
            finally { Object.DestroyImmediate(tree); }
        }

        [Test]
        public void CopyGeneratedZone_RetainsCurvesAndValidCosts()
        {
            PassiveSkillTreeSO tree = Backbone();
            try
            {
                ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, new ZoneGenSettings { Pieces = 5, Nodes = 32, Seed = 19 });
                Assert.That(result.Success, Is.True, result.Message);
                int curves = tree.BezierConnections.Count;
                Assert.That(curves, Is.GreaterThan(0));
                PassiveZoneOps.CopyZone(tree, 0, 2);
                Assert.That(PassiveZoneOps.ContentInZone(tree, Vector2.zero, 2).Count, Is.EqualTo(result.PlacedNodes));
                Assert.That(tree.BezierConnections.Count, Is.EqualTo(curves * 2));
                foreach (PassiveNodeDefinition n in PassiveZoneOps.ContentInZone(tree, Vector2.zero, 0))
                {
                    PassiveNodeDefinition copy = tree.GetNode(n.ZoneTwinId);
                    Assert.That(Vector2.Distance(copy.GetWorldPosition(tree), -n.GetWorldPosition(tree)), Is.LessThan(0.01f));
                    if (copy.NodeType == PassiveNodeType.Notable)
                        Assert.That(PassiveZoneMath.NotableCost(tree, copy), Is.EqualTo(PassiveZoneMath.NotableCost(tree, n)));
                }
                var geometry = PassiveZoneGeometry.Read(tree);
                var south = new PassiveZoneGeometry.Layout();
                var fixedGeometry = new PassiveZoneGeometry.Layout();
                var ids = new HashSet<string>(PassiveZoneOps.ContentInZone(tree, Vector2.zero, 2).Select(n => n.ID));
                south.Nodes.AddRange(geometry.Nodes.Where(n => ids.Contains(n.Id)));
                south.Edges.AddRange(geometry.Edges.Where(e => ids.Contains(e.A) || ids.Contains(e.B)));
                fixedGeometry.Nodes.AddRange(geometry.Nodes.Where(n => !ids.Contains(n.Id)));
                fixedGeometry.Edges.AddRange(geometry.Edges.Where(e => !ids.Contains(e.A) && !ids.Contains(e.B)));
                Assert.That(PassiveZoneGeometry.Fits(south, fixedGeometry, out string reason), Is.True, reason);
            }
            finally { Object.DestroyImmediate(tree); }
        }

        static int CountPieces(PassiveSkillTreeSO tree, List<PassiveNodeDefinition> nodes)
        {
            var pending = new HashSet<string>(nodes.Select(n => n.ID));
            int pieces = 0;
            while (pending.Count > 0)
            {
                var queue = new Queue<string>(); queue.Enqueue(pending.First()); pieces++;
                while (queue.Count > 0)
                {
                    string id = queue.Dequeue();
                    if (!pending.Remove(id)) continue;
                    foreach (string other in tree.GetNode(id).ConnectionIDs) if (pending.Contains(other)) queue.Enqueue(other);
                }
            }
            return pieces;
        }

        static string Signature(PassiveSkillTreeSO tree)
        {
            string P(Vector2 p) => p.x.ToString("F3", CultureInfo.InvariantCulture) + "," + p.y.ToString("F3", CultureInfo.InvariantCulture);
            var geometry = PassiveZoneGeometry.Read(tree);
            return string.Join(";", geometry.Nodes.Select(n => P(n.Position) + ":" + n.Type).OrderBy(s => s)) +
                string.Join(";", geometry.Edges.Select(e =>
                {
                    string f = string.Join("/", e.Points.Select(P));
                    string r = string.Join("/", e.Points.Reverse().Select(P));
                    return string.CompareOrdinal(f, r) < 0 ? f : r;
                }).OrderBy(s => s));
        }

        [Serializable]
        sealed class Preview
        {
            public int seed, zone;
            public string message;
            public List<PreviewNode> nodes;
            public List<PreviewEdge> edges;
            public List<PassiveClusterDefinition> clusters;
        }

        [Serializable]
        sealed class PreviewNode { public Vector2 p; public int type; public bool backbone; }
        [Serializable]
        sealed class PreviewEdge { public Vector2[] points; }

        static void Export(PassiveSkillTreeSO tree, ZoneGenResult result, int seed, int zone, string prefix = "seed")
        {
            var geometry = PassiveZoneGeometry.Read(tree);
            var preview = new Preview { seed = seed, zone = zone, message = result.Message, clusters = tree.Clusters,
                nodes = tree.Nodes.Select(n => new PreviewNode { p = n.GetWorldPosition(tree), type = (int)n.NodeType, backbone = n.IsBackbone }).ToList(),
                edges = geometry.Edges.Select(e => new PreviewEdge { points = e.Points }).ToList() };
            const string path = "Library/PassiveZonePreviews";
            Directory.CreateDirectory(path);
            File.WriteAllText(path + "/" + prefix + "-" + seed + "-zone-" + zone + ".json", JsonUtility.ToJson(preview, true));
        }
    }
}
