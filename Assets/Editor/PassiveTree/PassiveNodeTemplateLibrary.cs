using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;

namespace Scripts.Editor.PassiveTree
{
    internal static class PassiveNodeTemplateLibrary
    {
        private const string BaseTemplateFolder = "Assets/Resources/PassiveTrees/Templates";

        internal static IReadOnlyList<PassiveNodeTemplateSO> LoadAllTemplates()
        {
            var templates = new List<PassiveNodeTemplateSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:PassiveNodeTemplateSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (template != null)
                    templates.Add(template);
            }

            return templates
                .OrderBy(GetCategoryOrder)
                .ThenBy(GetCategory)
                .ThenBy(GetDisplayName)
                .ToList();
        }

        internal static string GetDisplayName(PassiveNodeTemplateSO template)
        {
            if (template == null)
                return "None";

            return string.IsNullOrWhiteSpace(template.Name) ? template.name : template.Name;
        }

        internal static string GetCategory(PassiveNodeTemplateSO template)
        {
            if (template == null)
                return "Other";

            string path = AssetDatabase.GetAssetPath(template).Replace('\\', '/');
            int baseIndex = path.IndexOf(BaseTemplateFolder, System.StringComparison.OrdinalIgnoreCase);
            if (baseIndex >= 0)
            {
                string relative = path.Substring(baseIndex + BaseTemplateFolder.Length).Trim('/');
                string[] segments = relative.Split('/');
                if (segments.Length >= 2)
                {
                    string mapped = NormalizeCategory(segments[0]);
                    if (!string.IsNullOrWhiteSpace(mapped))
                        return mapped;
                }
            }

            return InferCategoryFromModifiers(template.Modifiers);
        }

        internal static string GetSummary(PassiveNodeTemplateSO template, int maxModifiers = 2)
        {
            if (template == null)
                return "No template selected.";

            return BuildModifierSummary(template.Modifiers, maxModifiers, template.Description);
        }

        internal static string GetNodeSummary(PassiveNodeDefinition node, int maxModifiers = 3)
        {
            if (node == null)
                return string.Empty;

            string summary = BuildModifierSummary(node.GetFinalModifiers(), maxModifiers, node.Template != null ? node.Template.Description : string.Empty);
            return string.IsNullOrWhiteSpace(summary) ? "No modifiers." : summary;
        }

        internal static string GetNodeTooltipText(PassiveNodeDefinition node)
        {
            if (node == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine(node.GetDisplayName());
            string summary = GetNodeSummary(node, 5);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                builder.AppendLine();
                builder.Append(summary);
            }

            return builder.ToString().TrimEnd();
        }

        internal static PassiveNodeTemplateSO CreateNewTemplate(string preferredName = null, string category = "Utility")
        {
            EnsureTemplateFolders(category);

            string safeName = SanitizeAssetName(string.IsNullOrWhiteSpace(preferredName) ? "NewPassiveNode" : preferredName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "NewPassiveNode";

            string folder = GetCategoryFolder(category);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.asset");

            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            template.Name = Path.GetFileNameWithoutExtension(path);
            template.Description = string.Empty;
            template.Modifiers = new List<SerializableStatModifier>();

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(template);
            return template;
        }

        internal static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder();
            foreach (char character in value.Trim())
            {
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-' || character == ' ')
                    builder.Append(character);
            }

            return builder.ToString().Trim().Replace(' ', '_');
        }

        private static string BuildModifierSummary(IReadOnlyList<SerializableStatModifier> modifiers, int maxModifiers, string fallbackDescription)
        {
            if (modifiers != null && modifiers.Count > 0)
            {
                int takeCount = Mathf.Clamp(maxModifiers, 1, modifiers.Count);
                var parts = new List<string>();
                for (int i = 0; i < takeCount; i++)
                    parts.Add(FormatModifier(modifiers[i]));

                if (modifiers.Count > takeCount)
                    parts.Add($"+{modifiers.Count - takeCount} more");

                return string.Join("\n", parts);
            }

            return string.IsNullOrWhiteSpace(fallbackDescription) ? string.Empty : fallbackDescription.Trim();
        }

        private static string InferCategoryFromModifiers(IReadOnlyList<SerializableStatModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return "Utility";

            return NormalizeCategory(modifiers[0].Stat.ToString());
        }

        private static string NormalizeCategory(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Other";

            string value = raw.Trim().ToLowerInvariant();

            if (value.Contains("life") || value.Contains("health") || value.Contains("armor") || value.Contains("evasion") ||
                value.Contains("block") || value.Contains("resist") || value.Contains("shield") || value.Contains("defen"))
                return "Defense";

            if (value.Contains("mana") || value.Contains("resource"))
                return "Resources";

            if (value.Contains("move") || value.Contains("jump") || value.Contains("mobility"))
                return "Mobility";

            if (value.Contains("damage") || value.Contains("attack") || value.Contains("crit") || value.Contains("penetration") ||
                value.Contains("bleed") || value.Contains("poison") || value.Contains("ignite") || value.Contains("freeze") ||
                value.Contains("shock") || value.Contains("ailment"))
                return "Offense";

            if (value.Contains("projectile") || value.Contains("area") || value.Contains("cooldown") || value.Contains("duration") || value.Contains("misc"))
                return "Utility";

            return ObjectNames.NicifyVariableName(raw);
        }

        private static int GetCategoryOrder(PassiveNodeTemplateSO template)
        {
            return GetCategory(template) switch
            {
                "Defense" => 0,
                "Resources" => 1,
                "Offense" => 2,
                "Mobility" => 3,
                "Utility" => 4,
                _ => 10
            };
        }

        private static string FormatModifier(SerializableStatModifier modifier)
        {
            string statName = ObjectNames.NicifyVariableName(modifier.Stat.ToString());
            string prefix = modifier.Type.GetDisplayPrefix(modifier.Value);
            float magnitude = Mathf.Abs(modifier.Value);

            if (modifier.Type == StatModType.Flat)
                return $"{prefix}{magnitude:0.##} {statName}";

            if (modifier.Type == StatModType.PercentAdd || modifier.Type == StatModType.PercentSub)
                return $"{prefix}{magnitude:0.##}% {statName}";

            string suffix = modifier.Type == StatModType.PercentMult ? "more" : "less";
            return $"{magnitude:0.##}% {suffix} {statName}";
        }

        private static void EnsureTemplateFolders(string category)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/PassiveTrees"))
                AssetDatabase.CreateFolder("Assets/Resources", "PassiveTrees");
            if (!AssetDatabase.IsValidFolder(BaseTemplateFolder))
                AssetDatabase.CreateFolder("Assets/Resources/PassiveTrees", "Templates");

            string categoryFolder = GetCategoryFolder(category);
            string parent = BaseTemplateFolder;
            string folderName = Path.GetFileName(categoryFolder);
            if (!AssetDatabase.IsValidFolder(categoryFolder))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string GetCategoryFolder(string category)
        {
            string normalized = string.IsNullOrWhiteSpace(category) ? "Utility" : NormalizeCategory(category);
            return $"{BaseTemplateFolder}/{normalized}";
        }
    }
}
