using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using Scripts.Items.Affixes;
using Scripts.Items;
using Scripts.Editor.Affixes;

namespace Scripts.Editor.Stats
{
    /// <summary>
    /// Initialize stat, prepare for removal, remove from enum. Used by StatsEditorWindow.
    /// </summary>
    public static class StatsEditorStatLifecycle
    {
        private const string StatTypeScriptPath = "Assets/Scripts/Stats/StatType.cs";

        [Serializable]
        public sealed class StatsSystemUpgradeReport
        {
            public int MissingMetadataEntries;
            public int MetadataEntriesNormalized;
            public int LegacyCritMultiplierAffixes;
            public int MigratedCritMultiplierAffixes;
            public int RegeneratedAffixLocalizations;
            public int MigratedPassiveTemplates;
            public int MigratedPassiveTrees;
            public int InvalidContextModifierMetadata;
            public readonly List<string> Warnings = new List<string>();

            public string ToSummaryString()
            {
                var lines = new List<string>
                {
                    $"Missing metadata entries: {MissingMetadataEntries}",
                    $"Metadata entries normalized: {MetadataEntriesNormalized}",
                    $"Legacy CritMultiplier affixes: {LegacyCritMultiplierAffixes}",
                    $"Migrated CritMultiplier affixes: {MigratedCritMultiplierAffixes}",
                    $"Regenerated affix localizations: {RegeneratedAffixLocalizations}",
                    $"Migrated passive templates: {MigratedPassiveTemplates}",
                    $"Migrated passive trees: {MigratedPassiveTrees}",
                    $"Invalid context modifier metadata: {InvalidContextModifierMetadata}"
                };

                if (Warnings.Count > 0)
                {
                    lines.Add("Warnings:");
                    lines.AddRange(Warnings.Select(w => "• " + w));
                }

                return string.Join("\n", lines);
            }
        }

        public static AffixSetGenerator.AffixRebuildReport RebuildGeneratedAffixesForStat(
            StatType stat,
            StatsDatabaseSO statsDb,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels)
        {
            var tagDatabase = AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase);
            return AffixSetGenerator.RebuildGeneratedAffixesForStat(
                stat,
                statsDb,
                tagDatabase,
                menuLabels,
                affixesLabels,
                EditorPaths.AffixesBaseFolder,
                removeObsolete: true);
        }

        public static bool HasLocalizationKey(StringTableCollection menuLabels, StatType stat)
        {
            if (menuLabels == null) return false;
            string key = "stats." + stat;
            var enTable = menuLabels.GetTable("en") as StringTable
                ?? menuLabels.GetTable(new LocaleIdentifier("en")) as StringTable;
            if (enTable == null) return false;
            return enTable.GetEntry(key) != null;
        }

        /// <summary>
        /// Create stats.{id} in MenuLabels (en/ru) and metadata in StatsDatabase. Returns true on success.
        /// </summary>
        public static bool InitializeStat(StatType stat, StringTableCollection menuLabels, StatsDatabaseSO statsDb)
        {
            if (menuLabels == null)
            {
                Debug.LogWarning("Stats Editor: MenuLabels collection not assigned.");
                return false;
            }

            string id = stat.ToString();
            string fullKey = "stats." + id;
            var sharedData = menuLabels.SharedData;
            if (sharedData != null && !sharedData.Contains(fullKey))
            {
                sharedData.AddKey(fullKey);
                EditorUtility.SetDirty(sharedData);
            }

            var enTable = menuLabels.GetTable("en") as StringTable
                ?? menuLabels.GetTable(new LocaleIdentifier("en")) as StringTable;
            var ruTable = menuLabels.GetTable("ru") as StringTable
                ?? menuLabels.GetTable(new LocaleIdentifier("ru")) as StringTable;
            if (enTable == null || ruTable == null)
            {
                Debug.LogWarning("Stats Editor: en or ru table not found in MenuLabels.");
                return false;
            }

            string displayName = id; // or prettify
            SetOrAddEntry(enTable, fullKey, displayName);
            SetOrAddEntry(ruTable, fullKey, displayName);
            EditorUtility.SetDirty(enTable);
            EditorUtility.SetDirty(ruTable);

            if (statsDb != null)
            {
                statsDb.GetOrCreateEntry(stat);
                EditorUtility.SetDirty(statsDb);
            }

            AffixSetGenerator.EnsureValueUnitLocalizations(menuLabels);

            AssetDatabase.SaveAssets();
            Debug.Log($"Stats Editor: Initialized stat {id} (localization + metadata).");
            return true;
        }

        public static StatsSystemUpgradeReport AnalyzeProductionUpgrade(StatsDatabaseSO statsDb)
        {
            var report = new StatsSystemUpgradeReport();

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                var meta = statsDb != null ? statsDb.GetMetadata(stat) : null;
                if (meta == null)
                {
                    report.MissingMetadataEntries++;
                    continue;
                }

                if (NeedsMetadataNormalization(meta, stat))
                    report.MetadataEntriesNormalized++;

                if (meta.SemanticKind == StatSemanticKind.ContextModifier && meta.ContextTags == StatContextTagFlags.None)
                    report.InvalidContextModifierMetadata++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:ItemAffixSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (EnumerateAffixStats(affix).Any(stat => stat.Stat == StatType.CritMultiplier &&
                                            (stat.Type == StatModType.PercentAdd || stat.Type == StatModType.PercentSub)))
                {
                    report.LegacyCritMultiplierAffixes++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:PassiveNodeTemplateSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (template?.Modifiers == null)
                    continue;

                if (template.Modifiers.Any(IsLegacyCritMultiplierModifier))
                    report.MigratedPassiveTemplates++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:PassiveSkillTreeSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(path);
                if (tree?.Nodes == null)
                    continue;

                bool hasLegacyCrit = tree.Nodes.Any(node => node.UniqueModifiers != null && node.UniqueModifiers.Any(IsLegacyCritMultiplierModifier));
                if (hasLegacyCrit)
                    report.MigratedPassiveTrees++;
            }

            return report;
        }

        public static StatsSystemUpgradeReport ApplyProductionUpgrade(
            StatsDatabaseSO statsDb,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels)
        {
            var report = new StatsSystemUpgradeReport();

            if (statsDb == null)
            {
                report.Warnings.Add("StatsDatabase is not assigned.");
                return report;
            }

            if (menuLabels == null)
                report.Warnings.Add("MenuLabels table is not assigned. Value-unit localization was not refreshed.");

            if (affixesLabels == null)
                report.Warnings.Add("AffixesLabels table is not assigned. Affix localization regeneration was skipped.");

            statsDb.CreateDefaultsForAllStatTypes();

            foreach (StatType stat in Enum.GetValues(typeof(StatType)))
            {
                var entry = statsDb.GetOrCreateEntry(stat);
                if (entry == null)
                {
                    report.MissingMetadataEntries++;
                    continue;
                }

                if (ApplyMetadataNormalization(entry, stat))
                    report.MetadataEntriesNormalized++;

                if (entry.SemanticKind == StatSemanticKind.ContextModifier && entry.ContextTags == StatContextTagFlags.None)
                    report.InvalidContextModifierMetadata++;
            }

            if (menuLabels != null)
                AffixSetGenerator.EnsureValueUnitLocalizations(menuLabels);

            foreach (string guid in AssetDatabase.FindAssets("t:ItemAffixSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (affix == null || !EnumerateAffixStats(affix).Any())
                    continue;

                bool changed = false;
                bool migratedCritMultiplier = false;
                foreach (var stats in GetAffixStatArrays(affix))
                {
                    for (int i = 0; i < stats.Length; i++)
                    {
                        var statData = stats[i];
                        if (statData.Stat != StatType.CritMultiplier)
                            continue;

                        if (statData.Type == StatModType.PercentAdd)
                        {
                            statData.Type = StatModType.Flat;
                            stats[i] = statData;
                            changed = true;
                            migratedCritMultiplier = true;
                        }
                        else if (statData.Type == StatModType.PercentSub)
                        {
                            statData.Type = StatModType.Flat;
                            ConvertAffixStatRangeToNegative(ref statData);
                            stats[i] = statData;
                            changed = true;
                            migratedCritMultiplier = true;
                        }
                    }
                }

                if (changed)
                {
                    if (migratedCritMultiplier)
                    {
                        report.MigratedCritMultiplierAffixes++;
                        NormalizeAffixLocalizationKeys(affix);
                    }

                    EditorUtility.SetDirty(affix);
                }

                if (affixesLabels != null && !affix.LockAutoLocalization)
                {
                    AffixSetGenerator.RegenerateLocalizationFromStat(affix, menuLabels, affixesLabels);
                    report.RegeneratedAffixLocalizations++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:PassiveNodeTemplateSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (template?.Modifiers == null)
                    continue;

                if (!ConvertLegacyCritMultiplierModifiers(template.Modifiers))
                    continue;

                report.MigratedPassiveTemplates++;
                EditorUtility.SetDirty(template);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:PassiveSkillTreeSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(path);
                if (tree?.Nodes == null)
                    continue;

                bool changed = false;
                foreach (var node in tree.Nodes)
                {
                    if (node.UniqueModifiers == null)
                        continue;

                    if (ConvertLegacyCritMultiplierModifiers(node.UniqueModifiers))
                        changed = true;
                }

                if (!changed)
                    continue;

                report.MigratedPassiveTrees++;
                EditorUtility.SetDirty(tree);
            }

            EditorUtility.SetDirty(statsDb);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        [MenuItem("Tools/RPG/Stats/Analyze Production Upgrade")]
        public static void RunProductionUpgradeAnalysisMenu()
        {
            var statsDb = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase)
                         ?? Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            var report = AnalyzeProductionUpgrade(statsDb);
            Debug.Log("[Stats System Upgrade Analysis]\n" + report.ToSummaryString());
        }

        [MenuItem("Tools/RPG/Stats/Apply Production Upgrade")]
        public static void RunProductionUpgradeMenu()
        {
            var statsDb = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase)
                         ?? Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            var menuLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            var affixesLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

            if (!EditorUtility.DisplayDialog(
                    "Apply Production Upgrade",
                    "This will normalize stat metadata, migrate legacy Crit Multiplier modifiers, and regenerate auto-managed affix localization. Continue?",
                    "Apply",
                    "Cancel"))
            {
                return;
            }

            var report = ApplyProductionUpgrade(statsDb, menuLabels, affixesLabels);
            Debug.Log("[Stats System Upgrade]\n" + report.ToSummaryString());
        }

        public static void ExecuteProductionUpgrade()
        {
            var statsDb = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase)
                         ?? Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
            var menuLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            var affixesLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

            var report = ApplyProductionUpgrade(statsDb, menuLabels, affixesLabels);
            Debug.Log("[Stats System Upgrade]\n" + report.ToSummaryString());
        }

        private static void SetOrAddEntry(StringTable table, string key, string value)
        {
            var entry = table.GetEntry(key);
            if (entry != null)
                entry.Value = value;
            else
                table.AddEntry(key, value);
        }

        /// <summary>
        /// Remove stat from MenuLabels, StatsDatabase, affixes, templates, trees, CharacterDataSO.
        /// Returns a short report string.
        /// </summary>
        public static string PrepareStatForRemoval(StatType stat, StringTableCollection menuLabels, StatsDatabaseSO statsDb,
            out int affixesModified, out int templatesModified, out int treesModified, out int characterDataModified)
        {
            affixesModified = 0;
            templatesModified = 0;
            treesModified = 0;
            characterDataModified = 0;
            string id = stat.ToString();
            var report = new List<string>();

            // 1. MenuLabels
            if (menuLabels != null)
            {
                string fullKey = "stats." + id;
                var enTable = menuLabels.GetTable("en") as StringTable;
                var ruTable = menuLabels.GetTable("ru") as StringTable;
                if (enTable != null) { RemoveEntry(enTable, fullKey); EditorUtility.SetDirty(enTable); }
                if (ruTable != null) { RemoveEntry(ruTable, fullKey); EditorUtility.SetDirty(ruTable); }
                report.Add("Removed from MenuLabels (en/ru).");
            }

            // 2. StatsDatabase - remove entry from list (editor-only list, we need to remove from _entries)
            if (statsDb != null)
            {
                RemoveStatFromStatsDatabase(statsDb, stat);
                report.Add("Removed from Stats Database.");
            }

            // 3. Affixes - remove stat from Stats array; delete affix if empty
            var affixGuids = AssetDatabase.FindAssets("t:ItemAffixSO");
            foreach (string guid in affixGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (affix == null || !EnumerateAffixStats(affix).Any(s => s.Stat == stat)) continue;

                RemoveStatFromAffix(affix, stat);
                if (!EnumerateAffixStats(affix).Any())
                {
                    AssetDatabase.DeleteAsset(path);
                    affixesModified++;
                }
                else
                {
                    EditorUtility.SetDirty(affix);
                    affixesModified++;
                }
            }
            if (affixesModified > 0) report.Add($"Affixes: modified/deleted {affixesModified}.");

            // 4. PassiveNodeTemplateSO - remove modifier; delete template if empty
            var templateGuids = AssetDatabase.FindAssets("t:PassiveNodeTemplateSO");
            foreach (string guid in templateGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var template = AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
                if (template == null || template.Modifiers == null) continue;
                int removed = template.Modifiers.RemoveAll(m => m.Stat == stat);
                if (removed == 0) continue;
                templatesModified++;
                if (template.Modifiers.Count == 0)
                    AssetDatabase.DeleteAsset(path);
                else
                    EditorUtility.SetDirty(template);
            }
            if (templatesModified > 0) report.Add($"Passive templates: modified/deleted {templatesModified}.");

            // 5. PassiveSkillTreeSO - remove from UniqueModifiers in each node
            var treeGuids = AssetDatabase.FindAssets("t:PassiveSkillTreeSO");
            foreach (string guid in treeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tree = AssetDatabase.LoadAssetAtPath<PassiveSkillTreeSO>(path);
                if (tree == null || tree.Nodes == null) continue;
                bool changed = false;
                foreach (var node in tree.Nodes)
                {
                    if (node.UniqueModifiers == null) continue;
                    int before = node.UniqueModifiers.Count;
                    node.UniqueModifiers.RemoveAll(m => m.Stat == stat);
                    if (node.UniqueModifiers.Count != before) changed = true;
                }
                if (changed) { treesModified++; EditorUtility.SetDirty(tree); }
            }
            if (treesModified > 0) report.Add($"Passive trees: modified {treesModified}.");

            // 6. CharacterDataSO - remove from StartingStats
            var charGuids = AssetDatabase.FindAssets("t:CharacterDataSO");
            foreach (string guid in charGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
                if (data == null || data.StartingStats == null) continue;
                int before = data.StartingStats.Count;
                data.StartingStats.RemoveAll(c => c.Type == stat);
                if (data.StartingStats.Count != before)
                {
                    characterDataModified++;
                    EditorUtility.SetDirty(data);
                }
            }
            if (characterDataModified > 0) report.Add($"Character data: modified {characterDataModified}.");

            AssetDatabase.SaveAssets();
            return string.Join(" ", report);
        }

        private static void RemoveEntry(StringTable table, string key)
        {
            var entry = table.GetEntry(key) as StringTableEntry;
            if (entry != null)
                entry.RemoveFromTable();
        }

        private static void RemoveStatFromStatsDatabase(StatsDatabaseSO db, StatType stat)
        {
            var so = new SerializedObject(db);
            var entries = so.FindProperty("_entries");
            if (entries == null || !entries.isArray) return;
            string id = stat.ToString();
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var el = entries.GetArrayElementAtIndex(i);
                var idProp = el.FindPropertyRelative("StatTypeId");
                if (idProp != null && idProp.stringValue == id)
                {
                    entries.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>
        /// Edit StatType.cs to remove the enum value. Returns true if successful.
        /// </summary>
        public static bool RemoveFromEnum(StatType stat)
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "Stats", "StatType.cs");
            if (!File.Exists(path))
            {
                Debug.LogError($"Stats Editor: StatType.cs not found at {path}");
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            string[] lines = File.ReadAllLines(fullPath);
            string statName = stat.ToString();
            int removeIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Length >= statName.Length && trimmed.StartsWith(statName))
                {
                    char next = trimmed.Length > statName.Length ? trimmed[statName.Length] : '\0';
                    if (next == '\0' || next == ',' || next == ' ')
                    {
                        removeIndex = i;
                        break;
                    }
                }
            }

            if (removeIndex < 0)
            {
                Debug.LogWarning($"Stats Editor: Enum value {statName} not found in StatType.cs");
                return false;
            }

            var list = new List<string>(lines);
            list.RemoveAt(removeIndex);
            File.WriteAllLines(fullPath, list);
            AssetDatabase.Refresh();
            Debug.Log($"Stats Editor: Removed {statName} from StatType.cs. Recompile to apply.");
            return true;
        }

        /// <summary>
        /// Add a new enum value to StatType.cs (at the end of the enum). Returns true if successful.
        /// newStatName must be a valid C# identifier (PascalCase, no spaces).
        /// </summary>
        public static bool AddToEnum(string newStatName)
        {
            if (string.IsNullOrWhiteSpace(newStatName))
            {
                Debug.LogWarning("Stats Editor: Stat name cannot be empty.");
                return false;
            }

            string name = newStatName.Trim();
            if (name.Length == 0)
                return false;

            // Valid C# identifier: letter or _, then letters, digits, _
            if (!char.IsLetter(name[0]) && name[0] != '_')
            {
                Debug.LogWarning("Stats Editor: Stat name must start with a letter or underscore.");
                return false;
            }
            for (int i = 1; i < name.Length; i++)
            {
                if (!char.IsLetterOrDigit(name[i]) && name[i] != '_')
                {
                    Debug.LogWarning("Stats Editor: Stat name must contain only letters, digits, and underscores.");
                    return false;
                }
            }

            string path = Path.Combine(Application.dataPath, "Scripts", "Stats", "StatType.cs");
            if (!File.Exists(path))
            {
                Debug.LogError($"Stats Editor: StatType.cs not found at {path}");
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            string[] lines = File.ReadAllLines(fullPath);

            // Check if already exists (same as RemoveFromEnum match)
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length >= name.Length && trimmed.StartsWith(name))
                {
                    char next = trimmed.Length > name.Length ? trimmed[name.Length] : '\0';
                    if (next == '\0' || next == ',' || next == ' ')
                    {
                        Debug.LogWarning($"Stats Editor: Enum value \"{name}\" already exists in StatType.cs.");
                        return false;
                    }
                }
            }

            // Find enum closing brace: last "}" then the line before is "    }" (enum body end)
            int lastBraceIndex = -1;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                if (lines[i].Trim() == "}")
                {
                    lastBraceIndex = i;
                    break;
                }
            }
            if (lastBraceIndex < 1)
            {
                Debug.LogError("Stats Editor: Could not find enum closing brace in StatType.cs.");
                return false;
            }
            int enumCloseIndex = lastBraceIndex - 1; // line with "    }"
            int lastValueIndex = enumCloseIndex - 1;  // last enum value line

            var list = new List<string>(lines);
            string lastLine = list[lastValueIndex];
            if (!lastLine.TrimEnd().EndsWith(","))
                list[lastValueIndex] = lastLine.TrimEnd() + ",";
            list.Insert(enumCloseIndex, "        " + name);
            File.WriteAllLines(fullPath, list);
            AssetDatabase.Refresh();
            Debug.Log($"Stats Editor: Added {name} to StatType.cs. Recompile to apply. Then use \"Initialize stat\" for localization.");
            return true;
        }

        /// <summary>
        /// Create one sample ItemAffixSO for this stat with localization in Affixes table.
        /// </summary>
        public static ItemAffixSO CreateSampleAffix(StatType stat, StringTableCollection affixesCollection)
        {
            string category = StatsEditorWindow.GetStatCategory(stat);
            string statName = stat.ToString();
            string folder = $"{EditorPaths.AffixesBaseFolder}/ByStat/{category}/{statName}";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(EditorPaths.AffixesBaseFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Affixes");
            if (!AssetDatabase.IsValidFolder(EditorPaths.AffixesBaseFolder + "/ByStat")) AssetDatabase.CreateFolder(EditorPaths.AffixesBaseFolder, "ByStat");
            if (!AssetDatabase.IsValidFolder(EditorPaths.AffixesBaseFolder + "/ByStat/" + category)) AssetDatabase.CreateFolder(EditorPaths.AffixesBaseFolder + "/ByStat", category);
            if (!AssetDatabase.IsValidFolder(EditorPaths.AffixesBaseFolder + "/ByStat/" + category + "/" + statName)) AssetDatabase.CreateFolder(EditorPaths.AffixesBaseFolder + "/ByStat/" + category, statName);

            string path = $"{folder}/{statName}_Flat_Medium.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null)
            {
                Debug.LogWarning($"Stats Editor: Affix already exists at {path}");
                return AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
            }

            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            affix.GroupID = $"{statName}_Flat_Medium";
            affix.UniqueID = affix.GroupID;
            affix.NameKey = $"affix_name_{statName.ToLowerInvariant()}_flat_medium";
            affix.TranslationKey = $"affix_flat_{statName.ToLowerInvariant()}";
            affix.Tiers = new List<ItemAffixSO.AffixTierData>();
            for (int tier = 1; tier <= 5; tier++)
            {
                affix.Tiers.Add(new ItemAffixSO.AffixTierData
                {
                    Tier = tier,
                    Stats = new[]
                    {
                        new ItemAffixSO.AffixStatData
                        {
                            Stat = stat,
                            Type = StatModType.Flat,
                            Scope = StatScope.Global,
                            MinValue = 1f,
                            MaxValue = 10f
                        }
                    }
                });
            }

            AssetDatabase.CreateAsset(affix, path);
            EditorUtility.SetDirty(affix);

            if (affixesCollection != null)
            {
                var enTable = affixesCollection.GetTable("en") as StringTable;
                var ruTable = affixesCollection.GetTable("ru") as StringTable;
                if (enTable != null) SetOrAddEntry(enTable, affix.TranslationKey, $"Adds {{0}} to {statName}");
                if (ruTable != null) SetOrAddEntry(ruTable, affix.TranslationKey, $"Добавляет {{0}} к {statName}");
                if (enTable != null) EditorUtility.SetDirty(enTable);
                if (ruTable != null) EditorUtility.SetDirty(ruTable);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Stats Editor: Created sample affix at {path}");
            return affix;
        }

        /// <summary>
        /// Create one PassiveNodeTemplateSO with one modifier for this stat.
        /// </summary>
        public static PassiveNodeTemplateSO CreateSamplePassiveNode(StatType stat)
        {
            string category = StatsEditorWindow.GetStatCategory(stat);
            string statName = stat.ToString();
            string folder = $"{EditorPaths.PassiveTemplatesFolder}/Templates/{category}";
            EnsureFolderExists(EditorPaths.PassiveTemplatesFolder);
            EnsureFolderExists(EditorPaths.PassiveTemplatesFolder + "/Templates");
            EnsureFolderExists(EditorPaths.PassiveTemplatesFolder + "/Templates/" + category);

            string path = $"{folder}/{statName}_Sample.asset";
            if (AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path) != null)
            {
                Debug.LogWarning($"Stats Editor: Template already exists at {path}");
                return AssetDatabase.LoadAssetAtPath<PassiveNodeTemplateSO>(path);
            }

            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            template.Name = statName;
            template.Description = $"+{{0}} to {statName}";
            template.Modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = stat, Value = 5f, Type = StatModType.Flat }
            };

            AssetDatabase.CreateAsset(template, path);
            EditorUtility.SetDirty(template);
            AssetDatabase.SaveAssets();
            Debug.Log($"Stats Editor: Created sample passive node at {path}");
            return template;
        }

        private static void EnsureFolderExists(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static bool NeedsMetadataNormalization(StatMetadataEntry entry, StatType stat)
        {
            if (entry == null)
                return true;

            var defaultGenType = StatsDatabaseSO.DefaultAffixGenTypeFor(stat);
            var defaultAllowedKinds = StatsDatabaseSO.DefaultAllowedAffixKindsFor(stat, defaultGenType);

            return entry.SemanticKind != StatsDatabaseSO.DefaultSemanticKindFor(stat) ||
                   entry.Format != StatsDatabaseSO.DefaultFormatFor(stat) ||
                   entry.ValueUnit != StatsDatabaseSO.DefaultValueUnitFor(stat) ||
                   entry.AffixGenType != defaultGenType ||
                   entry.ShowInPrimaryStatsEditor != StatsDatabaseSO.DefaultShowInPrimaryStatsEditor(stat) ||
                   entry.DisplayAsPercentWhenFlat != StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(stat) ||
                   entry.AllowNegativeFlatGeneration != StatsDatabaseSO.DefaultAllowNegativeFlatGeneration(stat) ||
                   entry.ContextTags != StatsDatabaseSO.DefaultContextTagsFor(stat) ||
                   entry.DamageChannels != StatsDatabaseSO.DefaultDamageChannelsFor(stat) ||
                   entry.AllowedAffixKinds != defaultAllowedKinds;
        }

        private static bool ApplyMetadataNormalization(StatMetadataEntry entry, StatType stat)
        {
            bool changed = false;
            changed |= SetIfDifferent(ref entry.SemanticKind, StatsDatabaseSO.DefaultSemanticKindFor(stat));
            changed |= SetIfDifferent(ref entry.Format, StatsDatabaseSO.DefaultFormatFor(stat));
            changed |= SetIfDifferent(ref entry.ValueUnit, StatsDatabaseSO.DefaultValueUnitFor(stat));
            changed |= SetIfDifferent(ref entry.AffixGenType, StatsDatabaseSO.DefaultAffixGenTypeFor(stat));
            changed |= SetIfDifferent(ref entry.ShowInPrimaryStatsEditor, StatsDatabaseSO.DefaultShowInPrimaryStatsEditor(stat));
            changed |= SetIfDifferent(ref entry.DisplayAsPercentWhenFlat, StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(stat));
            changed |= SetIfDifferent(ref entry.AllowNegativeFlatGeneration, StatsDatabaseSO.DefaultAllowNegativeFlatGeneration(stat));
            changed |= SetIfDifferent(ref entry.ContextTags, StatsDatabaseSO.DefaultContextTagsFor(stat));
            changed |= SetIfDifferent(ref entry.DamageChannels, StatsDatabaseSO.DefaultDamageChannelsFor(stat));

            var normalizedKinds = StatsDatabaseSO.DefaultAllowedAffixKindsFor(stat, entry.AffixGenType);
            if (entry.AllowedAffixKinds != normalizedKinds)
            {
                entry.AllowedAffixKinds = normalizedKinds;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(entry.Category))
            {
                entry.Category = StatsDatabaseSO.DefaultCategoryFor(stat);
                changed = true;
            }

            return changed;
        }

        private static bool ConvertLegacyCritMultiplierModifiers(List<SerializableStatModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (!IsLegacyCritMultiplierModifier(modifier))
                    continue;

                modifier.Value = modifier.Type == StatModType.PercentSub ? -Mathf.Abs(modifier.Value) : Mathf.Abs(modifier.Value);
                modifier.Type = StatModType.Flat;
                modifiers[i] = modifier;
                changed = true;
            }

            return changed;
        }

        private static bool IsLegacyCritMultiplierModifier(SerializableStatModifier modifier)
        {
            return modifier.Stat == StatType.CritMultiplier &&
                   (modifier.Type == StatModType.PercentAdd || modifier.Type == StatModType.PercentSub);
        }

        private static void ConvertAffixStatRangeToNegative(ref ItemAffixSO.AffixStatData statData)
        {
            float originalMin = statData.MinValue;
            float originalMax = statData.MaxValue;
            statData.MinValue = -Mathf.Abs(Mathf.Max(originalMin, originalMax));
            statData.MaxValue = -Mathf.Abs(Mathf.Min(originalMin, originalMax));

            float originalSecondaryMin = statData.RangeMinValue;
            float originalSecondaryMax = statData.RangeMaxValue;
            if (!Mathf.Approximately(originalSecondaryMin, 0f) || !Mathf.Approximately(originalSecondaryMax, 0f))
            {
                statData.RangeMinValue = -Mathf.Abs(Mathf.Max(originalSecondaryMin, originalSecondaryMax));
                statData.RangeMaxValue = -Mathf.Abs(Mathf.Min(originalSecondaryMin, originalSecondaryMax));
            }
        }

        private static void NormalizeAffixLocalizationKeys(ItemAffixSO affix)
        {
            var stats = AffixSetGenerator.GetRepresentativeStats(affix);
            if (stats.Length == 0)
                return;

            var statData = stats[0];
            string strength = ParseStrengthFromGroupId(affix.GroupID);
            bool isNegativeFlat = statData.Type == StatModType.Flat && statData.MaxValue < 0f;
            string kindId = isNegativeFlat ? "flatnegative" : "flat";
            affix.NameKey = $"affix_name_{statData.Stat.ToString().ToLowerInvariant()}_{kindId}_{strength.ToLowerInvariant()}";
            affix.TranslationKey = AffixSetGenerator.GetValueKey(statData.Stat, StatAffixModifierKind.Flat, statData.GetEffectiveValueMode());
        }

        private static string ParseStrengthFromGroupId(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return "Medium";

            var parts = groupId.Split('_');
            if (parts.Length > 0)
            {
                string last = parts[parts.Length - 1];
                if (last == "Strong" || last == "Medium" || last == "Light")
                    return last;
            }

            return "Medium";
        }

        private static IEnumerable<ItemAffixSO.AffixStatData[]> GetAffixStatArrays(ItemAffixSO affix)
        {
            if (affix == null)
                yield break;

            if (affix.UsesEmbeddedTiers)
            {
                foreach (var tierData in affix.Tiers)
                    if (tierData?.Stats != null) yield return tierData.Stats;
                yield break;
            }

            if (affix.Stats != null)
                yield return affix.Stats;
        }

        private static IEnumerable<ItemAffixSO.AffixStatData> EnumerateAffixStats(ItemAffixSO affix)
        {
            return GetAffixStatArrays(affix).SelectMany(stats => stats);
        }

        private static void RemoveStatFromAffix(ItemAffixSO affix, StatType stat)
        {
            if (affix.UsesEmbeddedTiers)
            {
                foreach (var tierData in affix.Tiers)
                    if (tierData?.Stats != null)
                        tierData.Stats = tierData.Stats.Where(entry => entry.Stat != stat).ToArray();
            }
            else if (affix.Stats != null)
            {
                affix.Stats = affix.Stats.Where(entry => entry.Stat != stat).ToArray();
            }
        }

        private static bool SetIfDifferent<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            return true;
        }
    }
}
