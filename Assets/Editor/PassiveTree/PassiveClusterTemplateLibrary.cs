using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using Scripts.Skills.PassiveTree;

namespace Scripts.Editor.PassiveTree
{
    internal static class PassiveClusterTemplateLibrary
    {
        internal static IReadOnlyList<PassiveClusterTemplateSO> LoadAllTemplates()
        {
            var templates = new List<PassiveClusterTemplateSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:PassiveClusterTemplateSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveClusterTemplateSO>(path);
                if (template != null)
                    templates.Add(template);
            }

            return templates
                .OrderBy(GetDisplayName)
                .ThenBy(template => AssetDatabase.GetAssetPath(template))
                .ToList();
        }

        internal static string GetDisplayName(PassiveClusterTemplateSO template)
        {
            return template == null ? "None" : template.GetDisplayName();
        }

        internal static string GetLocalizedNameLine(PassiveClusterTemplateSO template)
        {
            if (template == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(template.NameRU) && template.NameRU != template.NameEN)
                return template.NameRU;

            return string.Empty;
        }

        internal static string GetSummary(PassiveClusterTemplateSO template, int maxNodes = 4)
        {
            if (template == null)
                return string.Empty;

            var builder = new StringBuilder();
            int orbitCount = template.Cluster?.Orbits?.Count ?? 0;
            int nodeCount = template.Nodes?.Count ?? 0;
            builder.Append($"{nodeCount} nodes • {orbitCount} orbits");

            if (template.Nodes != null && template.Nodes.Count > 0)
            {
                builder.AppendLine();
                int take = System.Math.Min(maxNodes, template.Nodes.Count);
                for (int i = 0; i < take; i++)
                {
                    if (i > 0)
                        builder.AppendLine();
                    builder.Append(template.Nodes[i].GetDisplayName());
                }

                if (template.Nodes.Count > take)
                {
                    builder.AppendLine();
                    builder.Append($"+{template.Nodes.Count - take} more");
                }
            }

            return builder.ToString();
        }
    }
}
