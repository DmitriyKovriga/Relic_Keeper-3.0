using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scripts.Editor.PassiveTree;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveStatScalingRuleTests
    {
        [Test]
        public void CalculateTargetValue_UsesWholeSourceSteps()
        {
            var rule = new PassiveStatScalingRule
            {
                SourceStat = StatType.Armor,
                SourceAmountPerStep = 100f,
                UseWholeSteps = true,
                TargetStat = StatType.DamagePhysical,
                TargetValuePerStep = 10f,
                TargetModifierType = StatModType.Flat
            };

            Assert.That(rule.CalculateTargetValue(99f), Is.EqualTo(0f));
            Assert.That(rule.CalculateTargetValue(250f), Is.EqualTo(20f));
            Assert.That(rule.CalculateTargetValue(300f), Is.EqualTo(30f));
        }
    }

    public class PassiveBezierConnectionTests
    {
        [Test]
        public void Matches_IgnoresNodeIdOrder()
        {
            var connection = new PassiveBezierConnection { NodeIdA = "a", NodeIdB = "b" };

            Assert.That(connection.Matches("a", "b"), Is.True);
            Assert.That(connection.Matches("b", "a"), Is.True);
            Assert.That(connection.Matches("a", "c"), Is.False);
        }

        [Test]
        public void CreateDefault_SortsIdsAndOffsetsHandlesOffTheChord()
        {
            var connection = PassiveBezierConnection.CreateDefault(
                "b",
                "a",
                Vector2.right * 100f,
                Vector2.zero);

            Assert.That(connection.NodeIdA, Is.EqualTo("a"));
            Assert.That(connection.NodeIdB, Is.EqualTo("b"));
            Assert.That(connection.AnchorPercent, Is.EqualTo(50f));
            Assert.That(connection.InHandleOffset.sqrMagnitude, Is.GreaterThan(1f));
            Assert.That(connection.OutHandleOffset.sqrMagnitude, Is.GreaterThan(1f));

            connection.GetCubicPoints(Vector2.zero, Vector2.right * 100f, out Vector2 p0, out Vector2 c1, out Vector2 c2, out Vector2 p3);
            Assert.That(p0, Is.EqualTo(Vector2.zero));
            Assert.That(p3, Is.EqualTo(Vector2.right * 100f));
            Assert.That(c1.y, Is.Not.EqualTo(0f));
            Assert.That(c2.y, Is.Not.EqualTo(0f));
        }

        [Test]
        public void EvaluateCubic_HitsEndpoints()
        {
            Vector2 p0 = Vector2.zero;
            Vector2 p1 = new Vector2(10f, 20f);
            Vector2 p2 = new Vector2(30f, 20f);
            Vector2 p3 = new Vector2(40f, 0f);

            Assert.That(PassiveBezierMath.EvaluateCubic(p0, p1, p2, p3, 0f), Is.EqualTo(p0));
            Assert.That(PassiveBezierMath.EvaluateCubic(p0, p1, p2, p3, 1f), Is.EqualTo(p3));
        }

        [Test]
        public void DistanceToCubic_IsNearZeroOnTheCurve()
        {
            Vector2 p0 = Vector2.zero;
            Vector2 p1 = new Vector2(0f, 40f);
            Vector2 p2 = new Vector2(40f, 40f);
            Vector2 p3 = new Vector2(40f, 0f);
            Vector2 mid = PassiveBezierMath.EvaluateCubic(p0, p1, p2, p3, 0.5f);

            Assert.That(PassiveBezierMath.DistanceToCubic(p0, p1, p2, p3, mid), Is.LessThan(0.25f));
            Assert.That(PassiveBezierMath.DistanceToCubic(p0, p1, p2, p3, new Vector2(200f, 200f)), Is.GreaterThan(20f));
        }

        [Test]
        public void PercentAlongSegment_MapsProjectionToZeroHundred()
        {
            Assert.That(PassiveBezierMath.PercentAlongSegment(Vector2.zero, Vector2.right * 100f, new Vector2(25f, 8f)), Is.EqualTo(25f).Within(0.01f));
            Assert.That(PassiveBezierMath.PercentAlongSegment(Vector2.zero, Vector2.right * 100f, new Vector2(-10f, 0f)), Is.EqualTo(0f));
            Assert.That(PassiveBezierMath.PercentAlongSegment(Vector2.zero, Vector2.right * 100f, new Vector2(140f, 0f)), Is.EqualTo(100f));
        }

        [Test]
        public void HandleModifiers_SnapAndKeepOppositeAlignment()
        {
            Vector2 fortyFive = PassiveBezierMath.ConstrainTo45Degrees(new Vector2(10f, 9f));
            Assert.That(fortyFive.x, Is.EqualTo(fortyFive.y).Within(0.01f));
            Assert.That(PassiveBezierMath.SnapPercent(23f), Is.EqualTo(25f));
            Assert.That(PassiveBezierMath.AreSmoothOpposite(Vector2.left * 10f, Vector2.right * 4f), Is.True);
            Assert.That(PassiveBezierMath.AreSmoothOpposite(Vector2.left * 10f, Vector2.up * 4f), Is.False);

            Vector2 opposite = PassiveBezierMath.AlignOppositeHandle(Vector2.right * 8f, 4f);
            Assert.That(opposite, Is.EqualTo(Vector2.left * 4f));
            Assert.That(PassiveBezierMath.RotateOffset(Vector2.right, 90f).y, Is.EqualTo(1f).Within(0.01f));
            Assert.That(PassiveBezierMath.ReflectAcrossAxis(new Vector2(3f, 4f), Vector2.right), Is.EqualTo(new Vector2(3f, -4f)));
        }

        [Test]
        public void NormalizeIds_PreservesBezierGeometryWhenEndpointsSwap()
        {
            var connection = new PassiveBezierConnection
            {
                NodeIdA = "z",
                NodeIdB = "a",
                AnchorPercent = 30f,
                InHandleOffset = new Vector2(1f, 2f),
                OutHandleOffset = new Vector2(3f, 4f)
            };

            connection.NormalizeIds();

            Assert.That(connection.NodeIdA, Is.EqualTo("a"));
            Assert.That(connection.NodeIdB, Is.EqualTo("z"));
            Assert.That(connection.AnchorPercent, Is.EqualTo(70f));
            Assert.That(connection.InHandleOffset, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(connection.OutHandleOffset, Is.EqualTo(new Vector2(1f, 2f)));
        }

        [Test]
        public void TreeHelpers_FindAndRemoveBezierByEitherIdOrder()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                tree.BezierConnections = new List<PassiveBezierConnection>
                {
                    new PassiveBezierConnection { NodeIdA = "a", NodeIdB = "c" }
                };

                Assert.That(tree.FindBezierConnection("c", "a"), Is.Not.Null);
                tree.RemoveBezierConnection("c", "a");
                Assert.That(tree.HasBezierConnection("a", "c"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void ConnectFreeThenDirect_AddsThenRemovesBezierRecord()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                var nodeA = new PassiveNodeDefinition { ID = "a", Position = Vector2.zero, ConnectionIDs = new List<string>() };
                var nodeB = new PassiveNodeDefinition { ID = "b", Position = Vector2.right * 80f, ConnectionIDs = new List<string>() };
                tree.Nodes.Add(nodeA);
                tree.Nodes.Add(nodeB);
                tree.InitLookup();

                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.ConnectNodesFree(nodeA, nodeB);

                Assert.That(nodeA.ConnectionIDs, Does.Contain("b"));
                Assert.That(nodeB.ConnectionIDs, Does.Contain("a"));
                Assert.That(tree.HasBezierConnection("b", "a"), Is.True);

                commands.ConnectNodesDirect(nodeA, nodeB);
                Assert.That(nodeA.ConnectionIDs, Does.Contain("b"));
                Assert.That(tree.HasBezierConnection("a", "b"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void DeleteNode_RemovesBezierConnections()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                var nodeA = new PassiveNodeDefinition { ID = "a", Position = Vector2.zero, ConnectionIDs = new List<string>() };
                var nodeB = new PassiveNodeDefinition { ID = "b", Position = Vector2.right * 80f, ConnectionIDs = new List<string>() };
                tree.Nodes.Add(nodeA);
                tree.Nodes.Add(nodeB);
                tree.InitLookup();

                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.ConnectNodesFree(nodeA, nodeB);
                commands.DeleteNode(nodeA);

                Assert.That(tree.HasBezierConnection("a", "b"), Is.False);
                Assert.That(nodeB.ConnectionIDs, Does.Not.Contain("a"));
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }
    }

    public class PassiveNodeGroupTemplateTests
    {
        [Test]
        public void CaptureAndApply_PreservesInternalDirectAndFreeConnections()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            var template = ScriptableObject.CreateInstance<PassiveNodeGroupTemplateSO>();
            var targetTree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                var a = new PassiveNodeDefinition { ID = "a", Position = Vector2.zero, ConnectionIDs = new List<string> { "b" } };
                var b = new PassiveNodeDefinition { ID = "b", Position = new Vector2(100f, 0f), ConnectionIDs = new List<string> { "a", "c" } };
                var c = new PassiveNodeDefinition { ID = "c", Position = new Vector2(200f, 20f), ConnectionIDs = new List<string> { "b" } };
                tree.Nodes.AddRange(new[] { a, b, c });
                tree.BezierConnections.Add(PassiveBezierConnection.CreateDefault("b", "c", b.Position, c.Position));
                tree.InitLookup();

                Assert.That(template.CaptureFrom(tree, tree.Nodes), Is.True);
                List<PassiveNodeDefinition> created = template.ApplyToTree(targetTree, new Vector2(500f, 300f));

                Assert.That(created.Count, Is.EqualTo(3));
                Assert.That(created.Sum(node => node.ConnectionIDs.Count), Is.EqualTo(4));
                Assert.That(targetTree.BezierConnections.Count, Is.EqualTo(1));
                Assert.That(targetTree.HasBezierConnection(created[1].ID, created[2].ID), Is.True);
                Assert.That(created.TrueForAll(node => node.PlacementMode == NodePlacementMode.Free), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(targetTree);
            }
        }

        [Test]
        public void Capture_RejectsDisconnectedSelection()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            var template = ScriptableObject.CreateInstance<PassiveNodeGroupTemplateSO>();
            try
            {
                tree.Nodes.Add(new PassiveNodeDefinition { ID = "a", ConnectionIDs = new List<string>() });
                tree.Nodes.Add(new PassiveNodeDefinition { ID = "b", ConnectionIDs = new List<string>() });
                tree.InitLookup();
                Assert.That(template.CaptureFrom(tree, tree.Nodes), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(template);
            }
        }
    }

    public class PassiveClusterTemplatePlacementTests
    {
        [Test]
        public void ApplyToTree_ForcesStoredNodesOntoCreatedClusterOrbit()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            var template = ScriptableObject.CreateInstance<PassiveClusterTemplateSO>();
            try
            {
                template.Cluster.Orbits = new List<PassiveOrbitDefinition>
                {
                    new PassiveOrbitDefinition { Radius = 60f }
                };
                template.Nodes = new List<PassiveNodeDefinition>
                {
                    new PassiveNodeDefinition
                    {
                        ID = "stored",
                        PlacementMode = NodePlacementMode.Free,
                        Position = new Vector2(999f, 999f),
                        OrbitIndex = 12,
                        OrbitAngle = -45f,
                        ConnectionIDs = new List<string>()
                    }
                };

                PassiveClusterDefinition cluster = template.ApplyToTree(tree, new Vector2(200f, 300f));
                PassiveNodeDefinition created = tree.Nodes.Single();

                Assert.That(created.PlacementMode, Is.EqualTo(NodePlacementMode.OnOrbit));
                Assert.That(created.ClusterID, Is.EqualTo(cluster.ID));
                Assert.That(created.OrbitIndex, Is.EqualTo(0));
                Assert.That(created.OrbitAngle, Is.EqualTo(315f));
                Assert.That(Vector2.Distance(created.GetWorldPosition(tree), cluster.Center), Is.EqualTo(60f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void ClipboardCapture_AllowsDisconnectedSelectionAndCentresPasteAtTarget()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            var template = ScriptableObject.CreateInstance<PassiveNodeGroupTemplateSO>();
            var targetTree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                tree.Nodes.Add(new PassiveNodeDefinition { ID = "a", Position = new Vector2(100f, 50f), ConnectionIDs = new List<string>() });
                tree.Nodes.Add(new PassiveNodeDefinition { ID = "b", Position = new Vector2(300f, 150f), ConnectionIDs = new List<string>() });
                tree.InitLookup();

                Assert.That(template.CaptureFrom(tree, tree.Nodes, false), Is.True);
                List<PassiveNodeDefinition> created = template.ApplyToTree(targetTree, new Vector2(800f, 600f));

                Vector2 min = created.Select(node => node.Position).Aggregate(Vector2.Min);
                Vector2 max = created.Select(node => node.Position).Aggregate(Vector2.Max);
                Assert.That((min + max) * 0.5f, Is.EqualTo(new Vector2(800f, 600f)));
            }
            finally
            {
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(targetTree);
            }
        }
    }

    public class PassiveClusterMirrorTests
    {
        [Test]
        public void MirrorHorizontal_PreservesOrbitPlacementAndMirrorsArcAndBezierHandles()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                tree.SnapToGrid = false;
                var cluster = new PassiveClusterDefinition
                {
                    ID = "cluster",
                    Center = new Vector2(400f, 300f),
                    Orbits = new List<PassiveOrbitDefinition>
                    {
                        new PassiveOrbitDefinition
                        {
                            Radius = 100f,
                            IsPartialArc = true,
                            ArcStartAngle = 20f,
                            ArcEndAngle = 80f
                        }
                    }
                };
                var a = new PassiveNodeDefinition
                {
                    ID = "a",
                    PlacementMode = NodePlacementMode.OnOrbit,
                    ClusterID = cluster.ID,
                    OrbitIndex = 0,
                    OrbitAngle = 30f,
                    ConnectionIDs = new List<string> { "b" }
                };
                var b = new PassiveNodeDefinition
                {
                    ID = "b",
                    PlacementMode = NodePlacementMode.OnOrbit,
                    ClusterID = cluster.ID,
                    OrbitIndex = 0,
                    OrbitAngle = 60f,
                    ConnectionIDs = new List<string> { "a" }
                };
                tree.Clusters.Add(cluster);
                tree.Nodes.AddRange(new[] { a, b });
                tree.BezierConnections.Add(new PassiveBezierConnection
                {
                    NodeIdA = "a",
                    NodeIdB = "b",
                    InHandleOffset = new Vector2(10f, 3f),
                    OutHandleOffset = new Vector2(-15f, 4f)
                });
                tree.InitLookup();

                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.MirrorClusters(new[] { cluster }, true);

                Assert.That(cluster.Center, Is.EqualTo(new Vector2(400f, 300f)));
                Assert.That(a.PlacementMode, Is.EqualTo(NodePlacementMode.OnOrbit));
                Assert.That(a.ClusterID, Is.EqualTo("cluster"));
                Assert.That(a.OrbitAngle, Is.EqualTo(150f).Within(0.001f));
                Assert.That(b.OrbitAngle, Is.EqualTo(120f).Within(0.001f));
                Assert.That(cluster.Orbits[0].ArcStartAngle, Is.EqualTo(100f).Within(0.001f));
                Assert.That(cluster.Orbits[0].ArcEndAngle, Is.EqualTo(160f).Within(0.001f));
                Assert.That(tree.BezierConnections[0].InHandleOffset, Is.EqualTo(new Vector2(-10f, 3f)));
                Assert.That(tree.BezierConnections[0].OutHandleOffset, Is.EqualTo(new Vector2(15f, 4f)));
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }
    }

    public class PassiveSelectionRotationTests
    {
        [Test]
        public void RotateNodes_RotatesPositionsAndFreeBezierHandlesAroundSelectionCentre()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                tree.SnapToGrid = false;
                var a = new PassiveNodeDefinition
                {
                    ID = "a",
                    Position = new Vector2(-10f, 0f),
                    PlacementMode = NodePlacementMode.Free,
                    ConnectionIDs = new List<string> { "b" }
                };
                var b = new PassiveNodeDefinition
                {
                    ID = "b",
                    Position = new Vector2(10f, 0f),
                    PlacementMode = NodePlacementMode.Free,
                    ConnectionIDs = new List<string> { "a" }
                };
                tree.Nodes.AddRange(new[] { a, b });
                tree.BezierConnections.Add(new PassiveBezierConnection
                {
                    NodeIdA = "a",
                    NodeIdB = "b",
                    InHandleOffset = new Vector2(10f, 0f),
                    OutHandleOffset = new Vector2(-10f, 0f)
                });
                tree.InitLookup();

                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.RotateNodes(new[] { a, b }, 90f);

                Assert.That(a.Position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(a.Position.y, Is.EqualTo(-10f).Within(0.001f));
                Assert.That(b.Position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(b.Position.y, Is.EqualTo(10f).Within(0.001f));
                Assert.That(tree.BezierConnections[0].InHandleOffset.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(tree.BezierConnections[0].InHandleOffset.y, Is.EqualTo(10f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }

        [Test]
        public void RotateCluster_KeepsNodeOnOrbitAndRotatesPartialArc()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                var cluster = new PassiveClusterDefinition
                {
                    ID = "cluster",
                    Center = new Vector2(200f, 300f),
                    Orbits = new List<PassiveOrbitDefinition>
                    {
                        new PassiveOrbitDefinition
                        {
                            Radius = 80f,
                            IsPartialArc = true,
                            ArcStartAngle = 10f,
                            ArcEndAngle = 70f
                        }
                    }
                };
                var node = new PassiveNodeDefinition
                {
                    ID = "node",
                    PlacementMode = NodePlacementMode.OnOrbit,
                    ClusterID = "cluster",
                    OrbitIndex = 0,
                    OrbitAngle = 30f,
                    ConnectionIDs = new List<string>()
                };
                tree.Clusters.Add(cluster);
                tree.Nodes.Add(node);
                tree.InitLookup();

                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.RotateClusters(new[] { cluster }, 45f);

                Assert.That(cluster.Center, Is.EqualTo(new Vector2(200f, 300f)));
                Assert.That(node.PlacementMode, Is.EqualTo(NodePlacementMode.OnOrbit));
                Assert.That(node.OrbitAngle, Is.EqualTo(75f).Within(0.001f));
                Assert.That(cluster.Orbits[0].ArcStartAngle, Is.EqualTo(55f).Within(0.001f));
                Assert.That(cluster.Orbits[0].ArcEndAngle, Is.EqualTo(115f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }
    }

    public class PassiveOrbitArcDrawingTests
    {
        [Test]
        public void OppositeOrbitNodes_UseStraightChordInsteadOfBrokenArc()
        {
            Assert.That(PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(105.46f, 285f), Is.True);
            Assert.That(PassiveOrbitArcDrawing.ShouldDrawAsStraightChord(10f, 40f), Is.False);
        }

        [Test]
        public void ShortOrbitArc_StaysOneSegment()
        {
            Assert.That(PassiveOrbitArcDrawing.SegmentCount(10f, 40f), Is.EqualTo(1));
        }
    }

    public class PassiveNodeContentClipboardTests
    {
        [Test]
        public void Paste_ReplacesContentButKeepsIdPlacementAndConnections()
        {
            var source = new PassiveNodeDefinition
            {
                ID = "source",
                NodeType = PassiveNodeType.Notable,
                PlacementMode = NodePlacementMode.Free,
                Position = new Vector2(10f, 20f),
                Template = null,
                UniqueModifiers = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.MaxHealth, Value = 12f, Type = StatModType.Flat }
                },
                ConnectionIDs = new List<string> { "a" }
            };
            var target = new PassiveNodeDefinition
            {
                ID = "target",
                NodeType = PassiveNodeType.Small,
                PlacementMode = NodePlacementMode.OnOrbit,
                ClusterID = "cluster",
                OrbitIndex = 1,
                OrbitAngle = 45f,
                Position = new Vector2(80f, 90f),
                UniqueModifiers = new List<SerializableStatModifier>(),
                ConnectionIDs = new List<string> { "b", "c" }
            };

            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            try
            {
                tree.Nodes.Add(source);
                tree.Nodes.Add(target);
                var commands = new PassiveTreeEditorCommands();
                commands.SetTree(tree);
                commands.PasteNodeContent(target, PassiveNodeContentClipboard.From(source));

                Assert.That(target.ID, Is.EqualTo("target"));
                Assert.That(target.PlacementMode, Is.EqualTo(NodePlacementMode.OnOrbit));
                Assert.That(target.ClusterID, Is.EqualTo("cluster"));
                Assert.That(target.OrbitIndex, Is.EqualTo(1));
                Assert.That(target.OrbitAngle, Is.EqualTo(45f));
                Assert.That(target.Position, Is.EqualTo(new Vector2(80f, 90f)));
                Assert.That(target.ConnectionIDs, Is.EqualTo(new[] { "b", "c" }));
                Assert.That(target.NodeType, Is.EqualTo(PassiveNodeType.Notable));
                Assert.That(target.UniqueModifiers.Count, Is.EqualTo(1));
                Assert.That(target.UniqueModifiers[0].Value, Is.EqualTo(12f));
                Assert.That(source.ConnectionIDs, Is.EqualTo(new[] { "a" }));
            }
            finally
            {
                Object.DestroyImmediate(tree);
            }
        }
    }

    public class PassiveStartNodeDisplayTests
    {
        [Test]
        public void StartNode_UsesStartLabelInsteadOfUnknown()
        {
            var startNode = new PassiveNodeDefinition { NodeType = PassiveNodeType.Start };
            var unknownNode = new PassiveNodeDefinition { NodeType = PassiveNodeType.Small };

            Assert.That(startNode.GetDisplayName(), Is.EqualTo("Стартовый нод"));
            Assert.That(startNode.GetDisplayDescription(), Is.EqualTo("Начальная точка дерева пассивок."));
            Assert.That(unknownNode.GetDisplayName(), Is.EqualTo("Unknown Node"));
        }
    }
}
