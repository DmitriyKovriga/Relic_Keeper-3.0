using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    [CreateAssetMenu(menuName = "RPG/Passive Tree/Cluster Template", fileName = "NewPassiveClusterTemplate")]
    public class PassiveClusterTemplateSO : ScriptableObject
    {
        [Header("Editor Labels")]
        public string NameEN;
        public string NameRU;

        [Header("Cluster Snapshot")]
        public PassiveClusterDefinition Cluster = new PassiveClusterDefinition
        {
            Name = "Cluster",
            Orbits = new List<PassiveOrbitDefinition> { new PassiveOrbitDefinition { Radius = 80f } },
            RoadConnections = new List<string>()
        };

        [Header("Nodes Stored On This Cluster")]
        public List<PassiveNodeDefinition> Nodes = new List<PassiveNodeDefinition>();

        public string GetDisplayName()
        {
            if (!string.IsNullOrWhiteSpace(NameEN))
                return NameEN;
            if (!string.IsNullOrWhiteSpace(Cluster?.Name))
                return Cluster.Name;
            return name;
        }

        public void CaptureFrom(PassiveClusterDefinition cluster, IReadOnlyList<PassiveNodeDefinition> sourceNodes)
        {
            if (cluster == null)
                return;

            if (string.IsNullOrWhiteSpace(NameEN))
                NameEN = cluster.Name;

            Cluster = CloneCluster(cluster, keepIds: false);
            Cluster.Name = cluster.Name;
            Cluster.Center = Vector2.zero;
            Cluster.RoadConnections = new List<string>();

            Nodes = new List<PassiveNodeDefinition>();
            if (sourceNodes == null)
                return;

            List<PassiveNodeDefinition> clusterNodes = sourceNodes
                .Where(node => node != null && node.PlacementMode == NodePlacementMode.OnOrbit && node.ClusterID == cluster.ID)
                .ToList();

            var localIds = new HashSet<string>(clusterNodes.Where(node => !string.IsNullOrWhiteSpace(node.ID)).Select(node => node.ID));
            foreach (var node in clusterNodes)
            {
                PassiveNodeDefinition clone = CloneNode(node);
                clone.ConnectionIDs = node.ConnectionIDs == null
                    ? new List<string>()
                    : node.ConnectionIDs.Where(localIds.Contains).Distinct().ToList();
                clone.ClusterID = string.Empty;
                Nodes.Add(clone);
            }
        }

        public PassiveClusterDefinition ApplyToTree(PassiveSkillTreeSO tree, Vector2 center)
        {
            if (tree == null || Cluster == null)
                return null;

            if (tree.Clusters == null)
                tree.Clusters = new List<PassiveClusterDefinition>();
            if (tree.Nodes == null)
                tree.Nodes = new List<PassiveNodeDefinition>();

            PassiveClusterDefinition newCluster = CloneCluster(Cluster, keepIds: false);
            newCluster.ID = Guid.NewGuid().ToString();
            newCluster.Name = !string.IsNullOrWhiteSpace(NameEN) ? NameEN : Cluster.Name;
            newCluster.Center = center;
            newCluster.RoadConnections = new List<string>();
            tree.Clusters.Add(newCluster);

            var idMap = new Dictionary<string, string>();
            var createdNodes = new List<PassiveNodeDefinition>();

            foreach (var storedNode in Nodes)
            {
                if (storedNode == null)
                    continue;

                PassiveNodeDefinition newNode = CloneNode(storedNode);
                string oldId = newNode.ID;
                string newId = Guid.NewGuid().ToString();
                newNode.ID = newId;
                newNode.ClusterID = newCluster.ID;
                newNode.ConnectionIDs = new List<string>();
                idMap[oldId] = newId;
                createdNodes.Add(newNode);
                tree.Nodes.Add(newNode);
            }

            for (int i = 0; i < createdNodes.Count; i++)
            {
                PassiveNodeDefinition storedNode = Nodes[i];
                PassiveNodeDefinition createdNode = createdNodes[i];
                if (storedNode.ConnectionIDs == null)
                    continue;

                foreach (string oldConnectionId in storedNode.ConnectionIDs)
                {
                    if (idMap.TryGetValue(oldConnectionId, out string newConnectionId) &&
                        !createdNode.ConnectionIDs.Contains(newConnectionId))
                    {
                        createdNode.ConnectionIDs.Add(newConnectionId);
                    }
                }
            }

            tree.InitLookup();
            return newCluster;
        }

        public void ApplyStructureTo(PassiveClusterDefinition cluster)
        {
            if (cluster == null || Cluster == null)
                return;

            cluster.Name = !string.IsNullOrWhiteSpace(NameEN) ? NameEN : Cluster.Name;
            cluster.EditorColor = Cluster.EditorColor;
            cluster.Orbits = CloneOrbits(Cluster.Orbits);
            cluster.RoadConnections = new List<string>();
        }

        private static PassiveClusterDefinition CloneCluster(PassiveClusterDefinition source, bool keepIds)
        {
            if (source == null)
                return null;

            return new PassiveClusterDefinition
            {
                ID = keepIds ? source.ID : string.Empty,
                Name = source.Name,
                Center = source.Center,
                EditorColor = source.EditorColor,
                Orbits = CloneOrbits(source.Orbits),
                RoadConnections = source.RoadConnections == null ? new List<string>() : new List<string>(source.RoadConnections)
            };
        }

        private static PassiveNodeDefinition CloneNode(PassiveNodeDefinition source)
        {
            if (source == null)
                return null;

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
                UniqueModifiers = CloneModifiers(source.UniqueModifiers),
                ConnectionIDs = source.ConnectionIDs == null ? new List<string>() : new List<string>(source.ConnectionIDs)
            };
        }

        private static List<PassiveOrbitDefinition> CloneOrbits(List<PassiveOrbitDefinition> source)
        {
            var result = new List<PassiveOrbitDefinition>();
            if (source == null)
                return result;

            foreach (var orbit in source)
            {
                if (orbit == null)
                    continue;

                result.Add(new PassiveOrbitDefinition
                {
                    Radius = orbit.Radius,
                    IsPartialArc = orbit.IsPartialArc,
                    ArcStartAngle = orbit.ArcStartAngle,
                    ArcEndAngle = orbit.ArcEndAngle
                });
            }

            return result;
        }

        private static List<SerializableStatModifier> CloneModifiers(List<SerializableStatModifier> source)
        {
            var result = new List<SerializableStatModifier>();
            if (source == null)
                return result;

            foreach (var modifier in source)
            {
                result.Add(new SerializableStatModifier
                {
                    Stat = modifier.Stat,
                    Value = modifier.Value,
                    Type = modifier.Type
                });
            }

            return result;
        }
    }
}
