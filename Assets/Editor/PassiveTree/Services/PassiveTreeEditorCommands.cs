using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Все мутации дерева пассивок: создание/удаление нод и кластеров, связи, конвертация размещения.
    /// Перед мутацией — Undo.RecordObject, после — SaveAssets.
    /// </summary>
    public class PassiveTreeEditorCommands
    {
        private PassiveSkillTreeSO _tree;

        public void SetTree(PassiveSkillTreeSO tree)
        {
            _tree = tree;
        }

        public PassiveSkillTreeSO Tree => _tree;

        private void RecordTree(string operationName)
        {
            if (_tree != null)
                Undo.RecordObject(_tree, operationName);
        }

        private Vector2 SnapPosition(Vector2 pos)
        {
            if (_tree == null || !_tree.SnapToGrid || _tree.GridSize <= 0)
                return pos;
            pos.x = Mathf.Round(pos.x / _tree.GridSize) * _tree.GridSize;
            pos.y = Mathf.Round(pos.y / _tree.GridSize) * _tree.GridSize;
            return pos;
        }

        public void CreateNodeAtPosition(Vector2 contentPos, PassiveNodeType type)
        {
            if (_tree == null) return;
            RecordTree("Create Node");
            contentPos = SnapPosition(contentPos);

            var newNodeData = new PassiveNodeDefinition
            {
                ID = Guid.NewGuid().ToString(),
                NodeType = type,
                PlacementMode = NodePlacementMode.Free,
                Position = contentPos,
                ConnectionIDs = new List<string>()
            };
            _tree.Nodes.Add(newNodeData);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void CreateClusterAtPosition(Vector2 contentPos)
        {
            if (_tree == null) return;
            RecordTree("Create Cluster");
            contentPos = SnapPosition(contentPos);

            var cluster = new PassiveClusterDefinition
            {
                ID = Guid.NewGuid().ToString(),
                Name = $"Cluster {_tree.Clusters.Count + 1}",
                Center = contentPos,
                Orbits = new List<PassiveOrbitDefinition>
                {
                    new PassiveOrbitDefinition { Radius = 80f }
                },
                EditorColor = new Color(
                    UnityEngine.Random.Range(0.3f, 0.8f),
                    UnityEngine.Random.Range(0.3f, 0.8f),
                    UnityEngine.Random.Range(0.5f, 1f),
                    0.4f
                ),
                RoadConnections = new List<string>()
            };
            _tree.Clusters.Add(cluster);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public PassiveClusterDefinition CreateClusterFromTemplateAtPosition(PassiveClusterTemplateSO template, Vector2 contentPos)
        {
            if (_tree == null || template == null)
                return null;

            RecordTree("Create Cluster From Template");
            contentPos = SnapPosition(contentPos);
            PassiveClusterDefinition cluster = template.ApplyToTree(_tree, contentPos);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
            return cluster;
        }

        public int GenerateBackboneFromStart()
        {
            if (_tree == null)
                return 0;

            PassiveNodeDefinition startNode = FindStartNode();
            if (startNode == null)
                return 0;

            if (_tree.Nodes == null)
                _tree.Nodes = new List<PassiveNodeDefinition>();

            RecordTree("Generate Passive Backbone");
            DisconnectNodeFromAll(startNode);

            Vector2 startPosition = startNode.GetWorldPosition(_tree);
            const int innerRingCount = 8;
            const int outerRingCount = 16;
            const int pathCount = 4;
            const float spokeInnerRadius = 180f;
            const float spokeOuterRadius = 320f;
            const float innerRingRadius = 500f;
            const float bridgeInnerRadius = 630f;
            const float bridgeOuterRadius = 770f;
            const float outerRingRadius = 900f;
            const float ringStartAngle = 0f;
            const float spokeStartAngle = 45f;
            const float bridgeStartAngle = 0f;

            List<PassiveNodeDefinition> spokeInnerNodes = CreateFreeNodes(BuildRingPoints(startPosition, spokeInnerRadius, pathCount, spokeStartAngle));
            List<PassiveNodeDefinition> spokeOuterNodes = CreateFreeNodes(BuildRingPoints(startPosition, spokeOuterRadius, pathCount, spokeStartAngle));
            List<PassiveNodeDefinition> innerRing = CreateFreeNodes(BuildRingPoints(startPosition, innerRingRadius, innerRingCount, ringStartAngle));
            List<PassiveNodeDefinition> bridgeInnerNodes = CreateFreeNodes(BuildRingPoints(startPosition, bridgeInnerRadius, pathCount, bridgeStartAngle));
            List<PassiveNodeDefinition> bridgeOuterNodes = CreateFreeNodes(BuildRingPoints(startPosition, bridgeOuterRadius, pathCount, bridgeStartAngle));
            List<PassiveNodeDefinition> outerRing = CreateFreeNodes(BuildRingPoints(startPosition, outerRingRadius, outerRingCount, ringStartAngle));

            ConnectSequentially(innerRing, true);
            ConnectSequentially(outerRing, true);

            int[] innerSpokeIndices = { 1, 3, 5, 7 };
            for (int i = 0; i < pathCount; i++)
            {
                AddConnectionBidirectional(startNode, spokeInnerNodes[i]);
                AddConnectionBidirectional(spokeInnerNodes[i], spokeOuterNodes[i]);
                AddConnectionBidirectional(spokeOuterNodes[i], innerRing[innerSpokeIndices[i]]);
            }

            int[] innerBridgeIndices = { 0, 2, 4, 6 };
            int[] outerBridgeIndices = { 0, 4, 8, 12 };
            for (int i = 0; i < pathCount; i++)
            {
                AddConnectionBidirectional(innerRing[innerBridgeIndices[i]], bridgeInnerNodes[i]);
                AddConnectionBidirectional(bridgeInnerNodes[i], bridgeOuterNodes[i]);
                AddConnectionBidirectional(bridgeOuterNodes[i], outerRing[outerBridgeIndices[i]]);
            }

            int createdNodes = spokeInnerNodes.Count + spokeOuterNodes.Count + innerRing.Count + bridgeInnerNodes.Count + bridgeOuterNodes.Count + outerRing.Count;

            _tree.InitLookup();
            PassiveTreeAssetPersistence.SaveAssets(_tree);
            return createdNodes;
        }

        public void AddOrbitToCluster(PassiveClusterDefinition cluster)
        {
            if (cluster == null || cluster.Orbits == null) return;
            RecordTree("Add Orbit");
            float newRadius = 80f;
            if (cluster.Orbits.Count > 0)
                newRadius = cluster.Orbits[cluster.Orbits.Count - 1].Radius + 40f;
            cluster.Orbits.Add(new PassiveOrbitDefinition { Radius = newRadius });
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void CreateNodeOnOrbit(PassiveClusterDefinition cluster, int orbitIndex, Vector2 contentPos)
        {
            if (_tree == null || cluster == null) return;
            if (orbitIndex < 0 || orbitIndex >= cluster.Orbits.Count) return;
            RecordTree("Create Node on Orbit");

            Vector2 toMouse = contentPos - cluster.Center;
            float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            var newNodeData = new PassiveNodeDefinition
            {
                ID = Guid.NewGuid().ToString(),
                NodeType = PassiveNodeType.Small,
                PlacementMode = NodePlacementMode.OnOrbit,
                ClusterID = cluster.ID,
                OrbitIndex = orbitIndex,
                OrbitAngle = angle,
                ConnectionIDs = new List<string>()
            };
            _tree.Nodes.Add(newNodeData);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void DeleteNode(PassiveNodeDefinition nodeData)
        {
            if (_tree == null || nodeData == null) return;
            RecordTree("Delete Node");
            foreach (var neighborID in new List<string>(nodeData.ConnectionIDs))
            {
                var neighbor = _tree.GetNode(neighborID);
                if (neighbor != null)
                    neighbor.ConnectionIDs.Remove(nodeData.ID);
            }
            _tree.Nodes.Remove(nodeData);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void DeleteCluster(PassiveClusterDefinition cluster)
        {
            if (_tree == null || cluster == null) return;
            RecordTree("Delete Cluster");
            foreach (var node in _tree.Nodes)
            {
                if (node.PlacementMode == NodePlacementMode.OnOrbit && node.ClusterID == cluster.ID)
                {
                    node.Position = node.GetWorldPosition(_tree);
                    node.PlacementMode = NodePlacementMode.Free;
                    node.ClusterID = null;
                }
            }
            _tree.Clusters.Remove(cluster);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void ConnectNodes(PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB)
        {
            if (_tree == null || nodeA == null || nodeB == null) return;
            if (nodeA.ConnectionIDs.Contains(nodeB.ID)) return;
            RecordTree("Connect Nodes");
            nodeA.ConnectionIDs.Add(nodeB.ID);
            nodeB.ConnectionIDs.Add(nodeA.ID);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void DisconnectNodes(PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB)
        {
            if (nodeA == null || nodeB == null) return;
            RecordTree("Disconnect Nodes");
            nodeA.ConnectionIDs.Remove(nodeB.ID);
            nodeB.ConnectionIDs.Remove(nodeA.ID);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void ConvertNodeToFree(PassiveNodeDefinition node)
        {
            if (_tree == null || node == null) return;
            RecordTree("Convert Node to Free");
            node.Position = node.GetWorldPosition(_tree);
            node.PlacementMode = NodePlacementMode.Free;
            node.ClusterID = null;
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void PlaceNodeOnClusterOrbit(PassiveNodeDefinition node, PassiveClusterDefinition cluster)
        {
            if (_tree == null || node == null || cluster == null || cluster.Orbits.Count == 0) return;
            RecordTree("Place Node on Orbit");
            Vector2 currentPos = node.GetWorldPosition(_tree);
            Vector2 toNode = currentPos - cluster.Center;
            float angle = Mathf.Atan2(toNode.y, toNode.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            node.PlacementMode = NodePlacementMode.OnOrbit;
            node.ClusterID = cluster.ID;
            node.OrbitIndex = 0;
            node.OrbitAngle = angle;
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        private PassiveNodeDefinition FindStartNode()
        {
            if (_tree?.Nodes == null)
                return null;

            return _tree.Nodes.FirstOrDefault(node => node != null && node.NodeType == PassiveNodeType.Start);
        }

        private void DisconnectNodeFromAll(PassiveNodeDefinition node)
        {
            if (_tree == null || node == null || node.ConnectionIDs == null)
                return;

            foreach (string connectionId in new List<string>(node.ConnectionIDs))
            {
                var neighbour = _tree.GetNode(connectionId);
                neighbour?.ConnectionIDs?.Remove(node.ID);
            }

            node.ConnectionIDs.Clear();
        }

        private List<PassiveNodeDefinition> CreateFreeNodes(IEnumerable<Vector2> positions)
        {
            var created = new List<PassiveNodeDefinition>();
            foreach (Vector2 position in positions)
            {
                var node = new PassiveNodeDefinition
                {
                    ID = Guid.NewGuid().ToString(),
                    NodeType = PassiveNodeType.Small,
                    PlacementMode = NodePlacementMode.Free,
                    Position = SnapPosition(position),
                    ConnectionIDs = new List<string>()
                };
                _tree.Nodes.Add(node);
                created.Add(node);
            }

            return created;
        }

        private int CreateBridgeChain(PassiveNodeDefinition from, PassiveNodeDefinition to, int internalNodeCount)
        {
            List<PassiveNodeDefinition> chain = CreateFreeNodes(BuildConnectorPoints(from.GetWorldPosition(_tree), to.GetWorldPosition(_tree), internalNodeCount));
            ConnectSequentially(chain, false, from, to);
            return chain.Count;
        }

        private int CreateApproachChain(PassiveNodeDefinition startNode, PassiveNodeDefinition targetNode, int internalNodeCount)
        {
            List<PassiveNodeDefinition> chain = CreateFreeNodes(BuildConnectorPoints(startNode.GetWorldPosition(_tree), targetNode.GetWorldPosition(_tree), internalNodeCount));
            ConnectSequentially(chain, false, startNode, targetNode);
            return chain.Count;
        }

        private static void ConnectSequentially(IReadOnlyList<PassiveNodeDefinition> nodes, bool closeLoop, PassiveNodeDefinition startAnchor = null, PassiveNodeDefinition endAnchor = null)
        {
            if (nodes == null || nodes.Count == 0)
            {
                if (startAnchor != null && endAnchor != null)
                    AddConnectionBidirectional(startAnchor, endAnchor);
                return;
            }

            if (startAnchor != null)
                AddConnectionBidirectional(startAnchor, nodes[0]);

            for (int i = 0; i < nodes.Count - 1; i++)
                AddConnectionBidirectional(nodes[i], nodes[i + 1]);

            if (endAnchor != null)
                AddConnectionBidirectional(nodes[nodes.Count - 1], endAnchor);

            if (closeLoop && nodes.Count > 2)
                AddConnectionBidirectional(nodes[nodes.Count - 1], nodes[0]);
        }

        private static void AddConnectionBidirectional(PassiveNodeDefinition a, PassiveNodeDefinition b)
        {
            if (a == null || b == null || a == b)
                return;

            a.ConnectionIDs ??= new List<string>();
            b.ConnectionIDs ??= new List<string>();

            if (!a.ConnectionIDs.Contains(b.ID))
                a.ConnectionIDs.Add(b.ID);
            if (!b.ConnectionIDs.Contains(a.ID))
                b.ConnectionIDs.Add(a.ID);
        }

        private static List<Vector2> BuildRingPoints(Vector2 center, float radius, int count, float startAngleDegrees)
        {
            var points = new List<Vector2>(count);
            float step = 360f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = (startAngleDegrees + step * i) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            return points;
        }

        private static List<Vector2> BuildConnectorPoints(Vector2 from, Vector2 to, int internalNodeCount)
        {
            var points = new List<Vector2>(internalNodeCount);
            for (int i = 1; i <= internalNodeCount; i++)
            {
                float t = i / (float)(internalNodeCount + 1);
                points.Add(Vector2.Lerp(from, to, t));
            }
            return points;
        }

    }
}
