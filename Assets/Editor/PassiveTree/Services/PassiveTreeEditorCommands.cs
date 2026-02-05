using System;
using System.Collections.Generic;
using UnityEngine;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>
    /// Все мутации дерева пассивок: создание/удаление нод и кластеров, связи, конвертация размещения.
    /// После каждой мутации вызывает SaveAssets. Не знает о UI и выборе — выбор передаётся снаружи для Connect/Disconnect.
    /// </summary>
    public class PassiveTreeEditorCommands
    {
        private PassiveSkillTreeSO _tree;

        public void SetTree(PassiveSkillTreeSO tree)
        {
            _tree = tree;
        }

        public PassiveSkillTreeSO Tree => _tree;

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
            contentPos = SnapPosition(contentPos);

            var cluster = new PassiveClusterDefinition
            {
                ID = Guid.NewGuid().ToString(),
                Name = $"Cluster {_tree.Clusters.Count + 1}",
                Center = contentPos,
                Orbits = new List<PassiveOrbitDefinition>
                {
                    new PassiveOrbitDefinition { Radius = 80f },
                    new PassiveOrbitDefinition { Radius = 120f }
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

        public void AddOrbitToCluster(PassiveClusterDefinition cluster)
        {
            if (cluster == null || cluster.Orbits == null) return;
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
            nodeA.ConnectionIDs.Add(nodeB.ID);
            nodeB.ConnectionIDs.Add(nodeA.ID);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void DisconnectNodes(PassiveNodeDefinition nodeA, PassiveNodeDefinition nodeB)
        {
            if (nodeA == null || nodeB == null) return;
            nodeA.ConnectionIDs.Remove(nodeB.ID);
            nodeB.ConnectionIDs.Remove(nodeA.ID);
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void ConvertNodeToFree(PassiveNodeDefinition node)
        {
            if (_tree == null || node == null) return;
            node.Position = node.GetWorldPosition(_tree);
            node.PlacementMode = NodePlacementMode.Free;
            node.ClusterID = null;
            PassiveTreeAssetPersistence.SaveAssets(_tree);
        }

        public void PlaceNodeOnClusterOrbit(PassiveNodeDefinition node, PassiveClusterDefinition cluster)
        {
            if (_tree == null || node == null || cluster == null || cluster.Orbits.Count == 0) return;
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
    }
}
