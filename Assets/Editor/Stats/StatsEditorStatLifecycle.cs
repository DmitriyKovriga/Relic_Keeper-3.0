using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using Scripts.Stats;
using Scripts.Skills.PassiveTree;
using Scripts.Items.Affixes;
using Scripts.Items;

namespace Scripts.Editor.Stats
{
    /// <summary>
    /// Initialize stat, prepare for removal, remove from enum. Used by StatsEditorWindow.
    /// </summary>
    public static class StatsEditorStatLifecycle
    {
        private const string StatTypeScriptPath = "Assets/Scripts/Stats/StatType.cs";
        private const string MenuLabelsPath = "Assets/Localization/LocalizationTables/MenuLabels.asset";
        private const string AffixesTablePath = "Assets/Localization/LocalizationTables/Affixes.asset";
        private const string AffixesBasePath = "Assets/Resources/Affixes";
        private const string PassiveTemplatesPath = "Assets/Resources/PassiveTrees";

        public static bool HasLocalizationKey(StringTableCollection menuLabels, StatType stat)
        {
            if (menuLabels == null) return false;
            string key = "stats." + stat;
            var enTable = menuLabels.GetTable("en") as StringTable;
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
            var enTable = menuLabels.GetTable("en") as StringTable;
            var ruTable = menuLabels.GetTable("ru") as StringTable;
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

            AssetDatabase.SaveAssets();
            Debug.Log($"Stats Editor: Initialized stat {id} (localization + metadata).");
            return true;
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
                if (affix == null || affix.Stats == null) continue;
                int idx = -1;
                for (int i = 0; i < affix.Stats.Length; i++)
                    if (affix.Stats[i].Stat == stat) { idx = i; break; }
                if (idx < 0) continue;

                var list = affix.Stats.ToList();
                list.RemoveAt(idx);
                if (list.Count == 0)
                {
                    AssetDatabase.DeleteAsset(path);
                    affixesModified++;
                }
                else
                {
                    affix.Stats = list.ToArray();
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
            string folder = $"{AffixesBasePath}/ByStat/{category}/{statName}";
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Affixes")) AssetDatabase.CreateFolder("Assets/Resources", "Affixes");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Affixes/ByStat")) AssetDatabase.CreateFolder("Assets/Resources/Affixes", "ByStat");
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Affixes/ByStat/" + category)) AssetDatabase.CreateFolder("Assets/Resources/Affixes/ByStat", category);
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Affixes/ByStat/" + category + "/" + statName)) AssetDatabase.CreateFolder("Assets/Resources/Affixes/ByStat/" + category, statName);

            string path = $"{folder}/{statName}_Flat_T1.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null)
            {
                Debug.LogWarning($"Stats Editor: Affix already exists at {path}");
                return AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
            }

            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            affix.GroupID = $"{statName}_Flat";
            affix.Tier = 1;
            affix.RequiredLevel = 5;
            affix.TranslationKey = $"affix_flat_{statName.ToLowerInvariant()}_t1";
            affix.Stats = new ItemAffixSO.AffixStatData[1];
            affix.Stats[0].Stat = stat;
            affix.Stats[0].Type = StatModType.Flat;
            affix.Stats[0].Scope = StatScope.Global;
            affix.Stats[0].MinValue = 1f;
            affix.Stats[0].MaxValue = 10f;

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
            string folder = $"{PassiveTemplatesPath}/Templates/{category}";
            EnsureFolderExists("Assets/Resources/PassiveTrees");
            EnsureFolderExists("Assets/Resources/PassiveTrees/Templates");
            EnsureFolderExists("Assets/Resources/PassiveTrees/Templates/" + category);

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
    }
}
