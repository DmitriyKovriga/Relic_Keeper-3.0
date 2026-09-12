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
        internal const string BaseTemplateFolder = "Assets/Resources/PassiveTrees/Templates";
        private static StatsDatabaseSO _cachedStatsDatabase;

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

            return BuildModifierSummary(template.Modifiers, template.StatScalingRules, maxModifiers, template.Description);
        }

        internal static string GetNodeSummary(PassiveNodeDefinition node, int maxModifiers = 3)
        {
            if (node == null)
                return string.Empty;

            string summary = BuildModifierSummary(
                node.GetFinalModifiers(),
                node.GetFinalStatScalingRules(),
                maxModifiers,
                node.Template != null ? node.Template.Description : string.Empty);
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

        internal static PassiveNodeTemplateSO CreateNewTemplate(string preferredName = null, string category = "Misc")
        {
            EnsureTemplateFolders(category);

            string safeName = SanitizeAssetName(string.IsNullOrWhiteSpace(preferredName) ? "NewPassiveNode" : preferredName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "NewPassiveNode";

            string categoryFolder = GetCategoryFolder(category);
            string nodeFolder = AssetDatabase.GenerateUniqueAssetPath($"{categoryFolder}/{safeName}");
            string folderName = Path.GetFileName(nodeFolder);
            AssetDatabase.CreateFolder(categoryFolder, folderName);
            string path = $"{nodeFolder}/{safeName}.asset";

            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            template.Name = Path.GetFileNameWithoutExtension(path);
            template.Description = string.Empty;
            template.Modifiers = new List<SerializableStatModifier>();
            template.StatScalingRules = new List<PassiveStatScalingRule>();

            AssetDatabase.CreateAsset(template, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(template);
            return template;
        }

        internal static string OrganizeTemplate(PassiveNodeTemplateSO template)
        {
            if (template == null)
                return string.Empty;

            string assetPath = AssetDatabase.GetAssetPath(template).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            string category = GetStorageCategory(template);
            EnsureTemplateFolders(category);
            string categoryFolder = GetCategoryFolder(category);
            string currentFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(currentFolder))
                return assetPath;

            string currentParent = Path.GetDirectoryName(currentFolder)?.Replace('\\', '/');
            if (string.Equals(currentParent, categoryFolder, System.StringComparison.OrdinalIgnoreCase))
                return assetPath;

            bool ownsNodeFolder = currentFolder.StartsWith(BaseTemplateFolder + "/", System.StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(currentFolder, GetCategoryFolder(GetFolderCategory(currentFolder)), System.StringComparison.OrdinalIgnoreCase);

            if (ownsNodeFolder && AssetDatabase.IsValidFolder(currentFolder))
            {
                string destinationFolder = AssetDatabase.GenerateUniqueAssetPath($"{categoryFolder}/{Path.GetFileName(currentFolder)}");
                string error = AssetDatabase.MoveAsset(currentFolder, destinationFolder);
                if (string.IsNullOrEmpty(error))
                    return $"{destinationFolder}/{Path.GetFileName(assetPath)}";

                Debug.LogWarning($"[PassiveNodeLibrary] Could not move node folder: {error}");
                return assetPath;
            }

            string nodeFolderName = SanitizeAssetName(template.name);
            string newNodeFolder = AssetDatabase.GenerateUniqueAssetPath($"{categoryFolder}/{nodeFolderName}");
            AssetDatabase.CreateFolder(categoryFolder, Path.GetFileName(newNodeFolder));
            string destinationAsset = $"{newNodeFolder}/{Path.GetFileName(assetPath)}";
            string moveError = AssetDatabase.MoveAsset(assetPath, destinationAsset);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogWarning($"[PassiveNodeLibrary] Could not organize node asset: {moveError}");
                return assetPath;
            }

            return destinationAsset;
        }

        internal static Sprite ImportIconBesideTemplate(PassiveNodeTemplateSO template, string sourcePath)
        {
            if (template == null || string.IsNullOrWhiteSpace(sourcePath))
                return null;

            string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".tga" && extension != ".psd")
                return null;

            string assetPath = AssetDatabase.GetAssetPath(template).Replace('\\', '/');
            string destinationFolder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(destinationFolder))
                return null;

            string sourceAssetPath = sourcePath.Replace('\\', '/');
            if (Path.IsPathRooted(sourceAssetPath))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/').TrimEnd('/');
                string normalizedSource = Path.GetFullPath(sourceAssetPath).Replace('\\', '/');
                if (normalizedSource.StartsWith(projectRoot + "/", System.StringComparison.OrdinalIgnoreCase))
                    sourceAssetPath = normalizedSource.Substring(projectRoot.Length + 1);
            }

            string destinationPath = AssetDatabase.GenerateUniqueAssetPath($"{destinationFolder}/{Path.GetFileName(sourcePath)}");
            bool copied;
            if (sourceAssetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(sourceAssetPath, destinationPath, System.StringComparison.OrdinalIgnoreCase))
                    copied = true;
                else
                    copied = AssetDatabase.CopyAsset(sourceAssetPath, destinationPath);
            }
            else
            {
                try
                {
                    string absoluteDestination = Path.GetFullPath(Path.Combine(Application.dataPath, "..", destinationPath));
                    File.Copy(sourcePath, absoluteDestination, false);
                    copied = true;
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[PassiveNodeLibrary] Could not copy icon: {exception.Message}");
                    copied = false;
                }
            }

            if (!copied)
                return null;

            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(destinationPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(destinationPath);
        }

        internal static string GetStorageCategory(PassiveNodeTemplateSO template)
        {
            if (template == null)
                return "Misc";

            string stat;
            if (template.Modifiers != null && template.Modifiers.Count > 0)
                stat = template.Modifiers[0].Stat.ToString().ToLowerInvariant();
            else if (template.StatScalingRules != null && template.StatScalingRules.Count > 0 && template.StatScalingRules[0] != null)
                stat = template.StatScalingRules[0].TargetStat.ToString().ToLowerInvariant();
            else
                return "Misc";
            if (stat.Contains("health") || stat.Contains("life")) return "Life";
            if (stat.Contains("mana") || stat.Contains("mysticshield")) return "Mana";
            if (stat.Contains("bleed") || stat.Contains("poison") || stat.Contains("ignite") ||
                stat.Contains("freeze") || stat.Contains("shock") || stat.Contains("stun") || stat.Contains("ailment"))
                return "Ailments";
            if (stat.Contains("damage") || stat.Contains("attack") || stat.Contains("cast") || stat.Contains("crit") ||
                stat.Contains("accuracy") || stat.Contains("armor") || stat.Contains("evasion") || stat.Contains("block") ||
                stat.Contains("resist") || stat.Contains("penetration") || stat.Contains("projectile") ||
                stat.Contains("speed") || stat.Contains("area") || stat.Contains("duration") || stat.Contains("cooldown") ||
                stat.Contains("conversion") || stat.Contains("to") || stat.Contains("take"))
                return "Utility";

            return "Misc";
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

        private static string BuildModifierSummary(
            IReadOnlyList<SerializableStatModifier> modifiers,
            IReadOnlyList<PassiveStatScalingRule> scalingRules,
            int maxModifiers,
            string fallbackDescription)
        {
            int modifierCount = modifiers?.Count ?? 0;
            int scalingCount = scalingRules?.Count ?? 0;
            int totalCount = modifierCount + scalingCount;
            if (totalCount > 0)
            {
                var parts = new List<string>();
                int limit = Mathf.Clamp(maxModifiers, 1, totalCount);
                for (int i = 0; i < modifierCount && parts.Count < limit; i++)
                    parts.Add(FormatModifier(modifiers[i]));
                for (int i = 0; i < scalingCount && parts.Count < limit; i++)
                    parts.Add(FormatScalingRule(scalingRules[i]));

                if (totalCount > parts.Count)
                    parts.Add($"+{totalCount - parts.Count} more");

                return string.Join("\n", parts);
            }

            return string.IsNullOrWhiteSpace(fallbackDescription) ? string.Empty : fallbackDescription.Trim();
        }

        private static string FormatScalingRule(PassiveStatScalingRule rule)
        {
            if (rule == null)
                return "Invalid stat scaling rule";

            string target = FormatModifier(new SerializableStatModifier
            {
                Stat = rule.TargetStat,
                Value = rule.TargetValuePerStep,
                Type = rule.TargetModifierType
            });
            string source = ObjectNames.NicifyVariableName(rule.SourceStat.ToString());
            string step = rule.UseWholeSteps ? "per" : "scaled by";
            return $"{target} {step} {rule.SourceAmountPerStep:0.##} {source}";
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
            return StatPresentation.FormatModifierLine(
                GetStatsDatabase(),
                modifier.Stat,
                statName,
                modifier.Value,
                modifier.Type,
                StatPresentation.ModifierLineStyle.ValueThenStat);
        }

        private static StatsDatabaseSO GetStatsDatabase()
        {
            if (_cachedStatsDatabase != null)
                return _cachedStatsDatabase;

            _cachedStatsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            if (_cachedStatsDatabase == null)
                _cachedStatsDatabase = Resources.Load<StatsDatabaseSO>(EditorPaths.StatsDatabaseResources);

            return _cachedStatsDatabase;
        }

        internal static void EnsureTemplateFolders(string category)
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
            string normalized = NormalizeStorageCategory(category);
            return $"{BaseTemplateFolder}/{normalized}";
        }

        private static string GetFolderCategory(string folder)
        {
            string relative = folder.Substring(BaseTemplateFolder.Length).Trim('/');
            string[] segments = relative.Split('/');
            return segments.Length > 0 ? segments[0] : "Misc";
        }

        private static string NormalizeStorageCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return "Misc";
            switch (category.Trim().ToLowerInvariant())
            {
                case "life": return "Life";
                case "mana": return "Mana";
                case "ailments": return "Ailments";
                case "utility": return "Utility";
                case "misc": return "Misc";
                default: return "Misc";
            }
        }
    }
}
