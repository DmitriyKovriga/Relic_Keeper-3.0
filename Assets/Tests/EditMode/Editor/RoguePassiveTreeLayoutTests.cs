using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Skills.PassiveTree;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class RoguePassiveTreeLayoutTests
    {
        [Test]
        public void RogueTree_HasFullConnectedSymmetricGraph()
        {
            var tree = Resources.Load<PassiveSkillTreeSO>("PassiveTrees/RoguePassiveSkillTree/RoguePassiveSkillTree");
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.Nodes, Has.Count.GreaterThanOrEqualTo(100));

            tree.InitLookup();
            var visited = new HashSet<string>();
            var pending = new Queue<string>();
            PassiveNodeDefinition start = tree.Nodes.Find(node => node.NodeType == PassiveNodeType.Start);
            Assert.That(start, Is.Not.Null);
            pending.Enqueue(start.ID);

            while (pending.Count > 0)
            {
                string id = pending.Dequeue();
                if (!visited.Add(id))
                    continue;

                PassiveNodeDefinition node = tree.GetNode(id);
                Assert.That(node, Is.Not.Null, $"Missing node '{id}'.");
                foreach (string connectionId in node.ConnectionIDs)
                {
                    PassiveNodeDefinition neighbour = tree.GetNode(connectionId);
                    Assert.That(neighbour, Is.Not.Null, $"'{id}' points to missing '{connectionId}'.");
                    Assert.That(neighbour.ConnectionIDs, Does.Contain(id), $"Connection {id} ↔ {connectionId} must be symmetric.");
                    pending.Enqueue(connectionId);
                }
            }

            Assert.That(visited, Has.Count.EqualTo(tree.Nodes.Count), "Every Rogue node must be reachable from the start node.");
        }
    }
}
