using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Editor.PassiveTree;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveZoneGeneratorTests
    {
        [Test]
        public void SeedOne_OnMarkedRogueBackbone_PlacesSomething()
        {
            var tree = UnityEditor.AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(
                "Assets/Resources/PassiveTrees/" + "Rogue" + "PassiveSkillTree/" + "Rogue" + "PassiveSkillTree.asset");
            Assert.That(tree, Is.Not.Null);
            var copy = UnityEngine.Object.Instantiate(tree);
            var settings = new ZoneGenSettings { Pieces = 5, Nodes = 32, Seed = 1 };
            ZoneGenResult result = PassiveZoneGenerator.Generate(copy, 0, settings);
            Assert.That(result.PlacedPieces, Is.EqualTo(5), result.Message);
        }

        [Test]
        public void NotableGluedToBackbone_CostsZero()
        {
            PassiveSkillTreeSO tree = TreeWithBackbone();
            PassiveNodeDefinition start = PassiveZoneMath.FindStart(tree);
            var notable = new PassiveNodeDefinition
            {
                ID = "notable",
                NodeType = PassiveNodeType.Notable,
                Position = new Vector2(0f, -80f),
                ConnectionIDs = new List<string> { start.ID }
            };
            start.ConnectionIDs.Add(notable.ID);
            tree.Nodes.Add(notable);

            Assert.That(PassiveZoneMath.NotableCost(tree, notable), Is.EqualTo(0));
        }

        [Test]
        public void GeneratedZone_KeepsNotableAtLeastTwoNodesFromBackbone()
        {
            PassiveSkillTreeSO tree = TreeWithBackbone();
            var settings = new ZoneGenSettings
            {
                Advanced = true,
                Nodes = 48,
                Small = 1,
                Medium = 1,
                Large = 1,
                Complex = 1,
                DeadEnds = 2,
                Seed = 7
            };

            ZoneGenResult result = PassiveZoneGenerator.Generate(tree, 0, settings);
            Assert.That(result.PlacedPieces, Is.GreaterThan(0), result.Message);

            int notables = 0;
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                PassiveNodeDefinition node = tree.Nodes[i];
                if (node == null || node.IsBackbone || node.NodeType != PassiveNodeType.Notable)
                    continue;
                notables++;
                int cost = PassiveZoneMath.NotableCost(tree, node);
                int minimum = MinimumCost(tree, node);
                Assert.That(cost, Is.GreaterThanOrEqualTo(minimum), node.ID + " cost " + cost);
            }

            Assert.That(notables, Is.GreaterThan(0));
            Assert.That(result.PlacedNodes, Is.LessThanOrEqualTo(48));
        }

        [Test]
        public void FlipNorth_ReflectsAcrossVerticalAxis()
        {
            PassiveSkillTreeSO tree = TreeWithBackbone();
            var node = new PassiveNodeDefinition
            {
                ID = "side",
                NodeType = PassiveNodeType.Small,
                Position = new Vector2(80f, -260f),
                ConnectionIDs = new List<string>()
            };
            tree.Nodes.Add(node);
            PassiveZoneOps.FlipZone(tree, 0);
            Assert.That(node.Position.x, Is.EqualTo(-80f).Within(0.1f));
            Assert.That(node.Position.y, Is.EqualTo(-260f).Within(0.1f));
            Assert.That(PassiveZoneMath.ZoneOf(Vector2.zero, node.Position), Is.EqualTo(0));
        }

        [Test]
        public void CopyNorthToSouth_LandsInOppositeZone()
        {
            PassiveSkillTreeSO tree = TreeWithBackbone();
            var node = new PassiveNodeDefinition
            {
                ID = "side",
                NodeType = PassiveNodeType.Small,
                Position = new Vector2(80f, -260f),
                ConnectionIDs = new List<string>()
            };
            tree.Nodes.Add(node);
            PassiveZoneOps.CopyZone(tree, 0, 2);
            PassiveNodeDefinition copy = tree.GetNode(node.ZoneTwinId);
            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Position.x, Is.EqualTo(-80f).Within(0.1f));
            Assert.That(copy.Position.y, Is.EqualTo(260f).Within(0.1f));
            Assert.That(PassiveZoneMath.ZoneOf(Vector2.zero, copy.Position), Is.EqualTo(2));
        }

        static int MinimumCost(PassiveSkillTreeSO tree, PassiveNodeDefinition node)
        {
            if (node.PlacementMode != NodePlacementMode.OnOrbit || string.IsNullOrEmpty(node.ClusterID))
                return 2;
            PassiveClusterDefinition cluster = tree.GetCluster(node.ClusterID);
            if (cluster?.Orbits == null)
                return 2;
            // Geometric radius can vary independently of the requested piece tier.
            if (cluster.Name.Contains(" Large /") || cluster.Name.Contains(" Complex /"))
                return 4;
            if (cluster.Name.Contains(" Medium /"))
                return 3;
            return 2;
        }

        static PassiveSkillTreeSO TreeWithBackbone()
        {
            var tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            tree.Nodes = new List<PassiveNodeDefinition>
            {
                new PassiveNodeDefinition
                {
                    ID = "start",
                    NodeType = PassiveNodeType.Start,
                    Position = Vector2.zero,
                    IsBackbone = true,
                    ConnectionIDs = new List<string>()
                }
            };
            tree.SnapToGrid = false;
            var commands = new PassiveTreeEditorCommands();
            commands.SetTree(tree);
            Assert.That(commands.GenerateBackboneFromStart(), Is.GreaterThan(0));
            return tree;
        }
    }
}
