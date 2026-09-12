using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    /// <summary>
    /// Reusable snapshot of an arbitrary connected selection of nodes.
    /// Nodes are stored as free positions relative to the selection centre.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Passive Tree/Node Group Template", fileName = "NewPassiveNodeGroupTemplate")]
    public class PassiveNodeGroupTemplateSO : ScriptableObject
    {
        public string DisplayName;
        public List<PassiveNodeDefinition> Nodes = new List<PassiveNodeDefinition>();
        public List<PassiveBezierConnection> BezierConnections = new List<PassiveBezierConnection>();

        public bool CaptureFrom(
            PassiveSkillTreeSO tree,
            IEnumerable<PassiveNodeDefinition> sourceNodes,
            bool requireConnected = true)
        {
            if (tree == null || sourceNodes == null)
                return false;

            List<PassiveNodeDefinition> selected = sourceNodes.Where(node => node != null).Distinct().ToList();
            if (selected.Count == 0 || (requireConnected && !IsConnected(selected)))
                return false;

            var ids = new HashSet<string>(selected.Select(node => node.ID));
            Vector2 min = selected[0].GetWorldPosition(tree);
            Vector2 max = min;
            foreach (var node in selected)
            {
                Vector2 position = node.GetWorldPosition(tree);
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position);
            }

            Vector2 centre = (min + max) * 0.5f;
            Nodes = new List<PassiveNodeDefinition>(selected.Count);
            foreach (var node in selected)
            {
                PassiveNodeDefinition clone = CloneNode(node);
                clone.PlacementMode = NodePlacementMode.Free;
                clone.Position = node.GetWorldPosition(tree) - centre;
                clone.ClusterID = string.Empty;
                clone.OrbitIndex = 0;
                clone.OrbitAngle = 0f;
                clone.ConnectionIDs = node.ConnectionIDs == null
                    ? new List<string>()
                    : node.ConnectionIDs.Where(ids.Contains).Distinct().ToList();
                Nodes.Add(clone);
            }

            BezierConnections = tree.BezierConnections == null
                ? new List<PassiveBezierConnection>()
                : tree.BezierConnections
                    .Where(connection => connection != null && ids.Contains(connection.NodeIdA) && ids.Contains(connection.NodeIdB))
                    .Select(CloneBezier)
                    .ToList();
            return true;
        }

        public List<PassiveNodeDefinition> ApplyToTree(PassiveSkillTreeSO tree, Vector2 centre)
        {
            var created = new List<PassiveNodeDefinition>();
            if (tree == null || Nodes == null || Nodes.Count == 0)
                return created;

            tree.Nodes ??= new List<PassiveNodeDefinition>();
            tree.BezierConnections ??= new List<PassiveBezierConnection>();
            var idMap = new Dictionary<string, string>();
            var createdByOldId = new Dictionary<string, PassiveNodeDefinition>();

            foreach (var stored in Nodes)
            {
                if (stored == null)
                    continue;

                PassiveNodeDefinition clone = CloneNode(stored);
                string oldId = stored.ID;
                clone.ID = Guid.NewGuid().ToString();
                clone.Position = centre + stored.Position;
                clone.PlacementMode = NodePlacementMode.Free;
                clone.ClusterID = string.Empty;
                clone.ConnectionIDs = new List<string>();
                idMap[oldId] = clone.ID;
                createdByOldId[oldId] = clone;
                tree.Nodes.Add(clone);
                created.Add(clone);
            }

            foreach (PassiveNodeDefinition stored in Nodes)
            {
                if (stored == null || stored.ConnectionIDs == null || !createdByOldId.TryGetValue(stored.ID, out var createdNode))
                    continue;

                foreach (string oldNeighbourId in stored.ConnectionIDs)
                    if (idMap.TryGetValue(oldNeighbourId, out string neighbourId) && !createdNode.ConnectionIDs.Contains(neighbourId))
                        createdNode.ConnectionIDs.Add(neighbourId);
            }

            if (BezierConnections != null)
            {
                foreach (var stored in BezierConnections)
                {
                    if (stored == null || !idMap.TryGetValue(stored.NodeIdA, out string idA) || !idMap.TryGetValue(stored.NodeIdB, out string idB))
                        continue;

                    PassiveBezierConnection clone = CloneBezier(stored);
                    clone.NodeIdA = idA;
                    clone.NodeIdB = idB;
                    clone.NormalizeIds();
                    tree.BezierConnections.Add(clone);
                }
            }

            tree.InitLookup();
            return created;
        }

        private static bool IsConnected(IReadOnlyList<PassiveNodeDefinition> nodes)
        {
            if (nodes.Count <= 1)
                return true;

            var byId = nodes.Where(node => !string.IsNullOrWhiteSpace(node.ID)).ToDictionary(node => node.ID);
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(nodes[0].ID);
            while (queue.Count > 0)
            {
                string id = queue.Dequeue();
                if (!visited.Add(id) || !byId.TryGetValue(id, out var node) || node.ConnectionIDs == null)
                    continue;
                foreach (string neighbour in node.ConnectionIDs)
                    if (byId.ContainsKey(neighbour) && !visited.Contains(neighbour))
                        queue.Enqueue(neighbour);
            }
            return visited.Count == byId.Count;
        }

        private static PassiveNodeDefinition CloneNode(PassiveNodeDefinition source)
        {
            return new PassiveNodeDefinition
            {
                ID = source.ID,
                NodeType = source.NodeType,
                PlacementMode = source.PlacementMode,
                Position = source.Position,
                ClusterID = source.ClusterID,
                OrbitIndex = source.OrbitIndex,
                OrbitAngle = source.OrbitAngle,
                Template = source.Template,
                UniqueModifiers = source.UniqueModifiers == null
                    ? new List<SerializableStatModifier>()
                    : source.UniqueModifiers.Select(modifier => new SerializableStatModifier
                    {
                        Stat = modifier.Stat,
                        Value = modifier.Value,
                        Type = modifier.Type
                    }).ToList(),
                ConnectionIDs = source.ConnectionIDs == null ? new List<string>() : new List<string>(source.ConnectionIDs)
            };
        }

        private static PassiveBezierConnection CloneBezier(PassiveBezierConnection source)
        {
            return new PassiveBezierConnection
            {
                NodeIdA = source.NodeIdA,
                NodeIdB = source.NodeIdB,
                AnchorPercent = source.AnchorPercent,
                InHandleOffset = source.InHandleOffset,
                OutHandleOffset = source.OutHandleOffset,
                MirrorHandles = source.MirrorHandles
            };
        }
    }
}
