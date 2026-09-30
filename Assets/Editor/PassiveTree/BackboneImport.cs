using System;
using System.Collections.Generic;
using Scripts.Skills.PassiveTree;
using UnityEditor;
using UnityEngine;

namespace Scripts.Editor.PassiveTree
{
    public static class BackboneImport
    {
        [MenuItem("Tools/Passive Tree/Import Backbone JSON")]
        public static void ImportFromMenu()
        {
            PassiveSkillTreeSO tree = Selection.activeObject as PassiveSkillTreeSO;
            if (tree == null)
                tree = PassiveTreeEditorWindow.FindOpenTree();
            if (tree == null)
            {
                EditorUtility.DisplayDialog("Импорт каркаса", "Открой дерево в редакторе или выдели его ассет.", "OK");
                return;
            }
            ImportInto(tree);
        }

        public static void ImportInto(PassiveSkillTreeSO tree)
        {
            if (tree == null)
                return;
            string path = EditorUtility.OpenFilePanel("Импорт каркаса", "", "json");
            if (string.IsNullOrEmpty(path))
                return;

            BackboneFile file;
            try
            {
                file = JsonUtility.FromJson<BackboneFile>(System.IO.File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Импорт каркаса", "Не удалось прочитать файл: " + ex.Message, "OK");
                return;
            }
            if (file == null || file.nodes == null || file.nodes.Length == 0)
            {
                EditorUtility.DisplayDialog("Импорт каркаса", "В файле нет нодов.", "OK");
                return;
            }

            BackboneNode fileStart = null;
            for (int i = 0; i < file.nodes.Length; i++)
            {
                if (file.nodes[i] != null && file.nodes[i].type == "start")
                    fileStart = file.nodes[i];
            }
            if (fileStart == null)
            {
                EditorUtility.DisplayDialog("Импорт каркаса", "В файле нет стартового нода.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Импорт каркаса",
                    "Текущая геометрия дерева будет заменена. Шаблоны и статы нодов пропадут.",
                    "Заменить",
                    "Отмена"))
                return;

            Undo.RecordObject(tree, "Import Backbone");
            Vector2 origin = Vector2.zero;
            if (tree.Nodes != null)
            {
                for (int i = 0; i < tree.Nodes.Count; i++)
                {
                    if (tree.Nodes[i] != null && tree.Nodes[i].NodeType == PassiveNodeType.Start)
                    {
                        origin = tree.Nodes[i].GetWorldPosition(tree) - new Vector2(fileStart.x, fileStart.y);
                        break;
                    }
                }
            }

            tree.Nodes = new List<PassiveNodeDefinition>();
            tree.Clusters = new List<PassiveClusterDefinition>();
            tree.BezierConnections = new List<PassiveBezierConnection>();
            tree.BackboneEdgeA = new List<string>();
            tree.BackboneEdgeB = new List<string>();
            if (tree.ZoneLinks != null)
            {
                for (int i = 0; i < tree.ZoneLinks.Count; i++)
                {
                    if (tree.ZoneLinks[i] != null)
                        tree.ZoneLinks[i].Enabled = false;
                }
            }

            var created = new Dictionary<string, PassiveNodeDefinition>();
            for (int i = 0; i < file.nodes.Length; i++)
            {
                BackboneNode src = file.nodes[i];
                if (src == null || string.IsNullOrEmpty(src.id))
                    continue;
                var node = new PassiveNodeDefinition
                {
                    ID = src.id,
                    NodeType = ParseType(src.type),
                    PlacementMode = NodePlacementMode.Free,
                    Position = new Vector2(src.x, src.y) + origin,
                    ConnectionIDs = new List<string>(),
                    IsBackbone = file.version < 2 || src.backbone
                };
                if (!string.IsNullOrEmpty(src.cluster))
                {
                    node.PlacementMode = NodePlacementMode.OnOrbit;
                    node.ClusterID = src.cluster;
                    node.OrbitIndex = src.orbit;
                    node.OrbitAngle = src.angle;
                    node.IsBackbone = false;
                }
                tree.Nodes.Add(node);
                created[src.id] = node;
            }
            if (file.clusters != null)
            {
                for (int i = 0; i < file.clusters.Length; i++)
                {
                    BackboneCluster src = file.clusters[i];
                    if (src == null || string.IsNullOrEmpty(src.id))
                        continue;
                    var cluster = new PassiveClusterDefinition
                    {
                        ID = src.id,
                        Name = string.IsNullOrEmpty(src.name) ? "Cluster" : src.name,
                        Center = new Vector2(src.x, src.y) + origin,
                        Orbits = new List<PassiveOrbitDefinition>(),
                        RoadConnections = new List<string>(),
                        EditorColor = new Color(0.45f, 0.55f, 0.75f, 0.35f)
                    };
                    if (src.orbits != null && src.orbits.Length > 0)
                    {
                        for (int o = 0; o < src.orbits.Length; o++)
                            cluster.Orbits.Add(new PassiveOrbitDefinition { Radius = Mathf.Max(20f, src.orbits[o]) });
                    }
                    else
                        cluster.Orbits.Add(new PassiveOrbitDefinition { Radius = 80f });
                    tree.Clusters.Add(cluster);
                }
            }
            for (int i = 0; i < file.nodes.Length; i++)
            {
                BackboneNode src = file.nodes[i];
                if (src?.links == null || !created.TryGetValue(src.id, out PassiveNodeDefinition node))
                    continue;
                for (int l = 0; l < src.links.Length; l++)
                {
                    if (created.TryGetValue(src.links[l], out PassiveNodeDefinition other))
                        AddLink(node, other);
                }
            }
            if (file.curves != null)
            {
                for (int i = 0; i < file.curves.Length; i++)
                {
                    BackboneCurve src = file.curves[i];
                    if (src == null || !created.ContainsKey(src.a) || !created.ContainsKey(src.b))
                        continue;
                    AddLink(created[src.a], created[src.b]);
                    tree.BezierConnections.Add(new PassiveBezierConnection
                    {
                        NodeIdA = src.a,
                        NodeIdB = src.b,
                        AnchorPercent = src.anchor <= 0f ? 50f : src.anchor,
                        InHandleOffset = new Vector2(src.inX, src.inY),
                        OutHandleOffset = new Vector2(src.outX, src.outY),
                        MirrorHandles = src.mirror
                    });
                    tree.BezierConnections[tree.BezierConnections.Count - 1].NormalizeIds();
                }
            }

            tree.InitLookup();
            PassiveZoneOps.CaptureBackboneEdges(tree);
            EditorUtility.SetDirty(tree);
            PassiveTreeEditorWindow.ReloadOpenEditors();
            EditorUtility.DisplayDialog("Импорт каркаса", "Загружено нодов: " + tree.Nodes.Count, "OK");
        }

        static PassiveNodeType ParseType(string type)
        {
            return type switch
            {
                "start" => PassiveNodeType.Start,
                "notable" => PassiveNodeType.Notable,
                "keystone" => PassiveNodeType.Keystone,
                _ => PassiveNodeType.Small
            };
        }

        static void AddLink(PassiveNodeDefinition a, PassiveNodeDefinition b)
        {
            if (a == null || b == null || a == b)
                return;
            if (!a.ConnectionIDs.Contains(b.ID)) a.ConnectionIDs.Add(b.ID);
            if (!b.ConnectionIDs.Contains(a.ID)) b.ConnectionIDs.Add(a.ID);
        }

        [Serializable]
        class BackboneFile
        {
            public int version;
            public BackboneNode[] nodes;
        public BackboneCluster[] clusters;
        public BackboneCurve[] curves;
        }

        [Serializable]
        class BackboneNode
        {
            public string id;
            public string type;
            public float x;
            public float y;
            public string[] links;
            public string cluster;
            public int orbit;
            public float angle;
            public bool backbone;
        }

        [Serializable]
        class BackboneCluster
        {
            public string id;
            public string name;
            public float x;
            public float y;
            public float[] orbits;
        }

        [Serializable]
        class BackboneCurve
        {
            public string a;
            public string b;
            public float anchor;
            public float inX;
            public float inY;
            public float outX;
            public float outY;
            public bool mirror;
        }
    }
}
