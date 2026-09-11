using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Editor.PassiveTree;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
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
}
