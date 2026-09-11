using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>Read-only breakdown of every node stored in a cluster template.</summary>
    internal sealed class PassiveClusterNodesWindow : EditorWindow
    {
        private PassiveClusterTemplateSO _clusterTemplate;
        private Vector2 _scroll;
        private string _search = string.Empty;

        internal static void Open(PassiveClusterTemplateSO clusterTemplate)
        {
            if (clusterTemplate == null)
                return;

            var window = CreateInstance<PassiveClusterNodesWindow>();
            window._clusterTemplate = clusterTemplate;
            window.titleContent = new GUIContent("Cluster Nodes");
            window.minSize = new Vector2(440f, 360f);
            window.ShowAuxWindow();
        }

        private void OnGUI()
        {
            if (_clusterTemplate == null)
            {
                EditorGUILayout.HelpBox("Cluster template is no longer available.", MessageType.Warning);
                return;
            }

            DrawHeader();
            DrawSearch();

            var nodes = (_clusterTemplate.Nodes ?? new System.Collections.Generic.List<PassiveNodeDefinition>())
                .Where(node => node != null && MatchesSearch(node))
                .ToList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (nodes.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(_search) ? "This cluster contains no nodes." : "No nodes match the search.",
                    MessageType.Info);
            }
            else
            {
                for (int i = 0; i < nodes.Count; i++)
                    DrawNodeRow(nodes[i], i + 1);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(PassiveClusterTemplateLibrary.GetDisplayName(_clusterTemplate), EditorStyles.boldLabel);
                string localizedName = PassiveClusterTemplateLibrary.GetLocalizedNameLine(_clusterTemplate);
                if (!string.IsNullOrWhiteSpace(localizedName))
                    EditorGUILayout.LabelField(localizedName, EditorStyles.miniLabel);

                int nodeCount = _clusterTemplate.Nodes?.Count ?? 0;
                int orbitCount = _clusterTemplate.Cluster?.Orbits?.Count ?? 0;
                EditorGUILayout.LabelField($"{nodeCount} nodes  •  {orbitCount} orbits", EditorStyles.miniLabel);
            }
        }

        private void DrawSearch()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(44f));
                _search = GUILayout.TextField(_search ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                    _search = string.Empty;
            }
        }

        private static void DrawNodeRow(PassiveNodeDefinition node, int displayIndex)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawNodeIcon(node);

                using (new EditorGUILayout.VerticalScope())
                {
                    string nodeName = node.GetDisplayName();
                    EditorGUILayout.LabelField($"{displayIndex}. {nodeName}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        $"{node.NodeType}  •  Orbit {node.OrbitIndex + 1}  •  {node.OrbitAngle:0.#}°",
                        EditorStyles.miniLabel);

                    string stats = PassiveNodeTemplateLibrary.GetNodeSummary(node, 99);
                    EditorGUILayout.LabelField(stats, EditorStyles.wordWrappedMiniLabel);
                }

                using (new EditorGUI.DisabledScope(node.Template == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(52f), GUILayout.Height(24f)))
                    {
                        Selection.activeObject = node.Template;
                        EditorGUIUtility.PingObject(node.Template);
                    }
                }
            }
        }

        private static void DrawNodeIcon(PassiveNodeDefinition node)
        {
            Rect iconRect = GUILayoutUtility.GetRect(38f, 38f, GUILayout.Width(38f), GUILayout.Height(38f));
            EditorGUI.DrawRect(iconRect, new Color(0.10f, 0.10f, 0.11f));
            Sprite icon = node.GetIcon();
            Texture preview = icon != null ? AssetPreview.GetAssetPreview(icon) ?? AssetPreview.GetMiniThumbnail(icon) : null;
            if (preview != null)
                GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(iconRect, "?", EditorStyles.centeredGreyMiniLabel);
        }

        private bool MatchesSearch(PassiveNodeDefinition node)
        {
            string search = (_search ?? string.Empty).Trim();
            if (search.Length == 0)
                return true;

            string haystack = $"{node.GetDisplayName()} {node.NodeType} {PassiveNodeTemplateLibrary.GetNodeSummary(node, 99)}";
            return haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
