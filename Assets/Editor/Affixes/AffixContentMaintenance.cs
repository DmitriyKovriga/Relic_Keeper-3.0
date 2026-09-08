using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace Scripts.Editor.Affixes
{
    /// <summary>
    /// Единственная точка пакетного обслуживания сгенерированного affix-контента.
    /// Обычные редакторы предметов и статов не должны содержать генераторы ассетов.
    /// </summary>
    public static class AffixContentMaintenance
    {
        private const string MenuRoot = "Tools/Items/Affix Content/";

        [MenuItem(MenuRoot + "Analyze Missing Stat Families")]
        public static void AnalyzeMissingFromMenu()
        {
            HashSet<StatType> missing = FindMissingStats();
            string report = BuildReport(missing);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Affix content", report, "OK");
        }

        [MenuItem(MenuRoot + "Generate Missing Stat Families")]
        public static void GenerateMissingFromMenu()
        {
            HashSet<StatType> missing = FindMissingStats();
            if (missing.Count == 0)
            {
                EditorUtility.DisplayDialog("Affix content", "Every stat already has an affix family.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Generate missing affix families",
                    BuildReport(missing) + "\n\nGenerate them in the embedded T1–T5 format?",
                    "Generate",
                    "Cancel"))
                return;

            int created = GenerateMissing();
            EditorUtility.DisplayDialog("Affix content", $"Created {created} affix assets.", "OK");
        }

        public static void GenerateMissingFromCommandLine()
        {
            int created = GenerateMissing();
            Debug.Log($"[Affix Content] Missing stat generation complete. Created: {created}.");
        }

        private static int GenerateMissing()
        {
            HashSet<StatType> missing = FindMissingStats();
            if (missing.Count == 0)
                return 0;

            StatsDatabaseSO statsDatabase = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            AffixTagDatabaseSO tagDatabase = AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase);
            StringTableCollection menuLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels);
            StringTableCollection affixLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

            if (statsDatabase == null)
                throw new InvalidOperationException($"Stats Database was not found at {EditorPaths.StatsDatabase}.");
            if (menuLabels == null || affixLabels == null)
                throw new InvalidOperationException("MenuLabels or AffixesLabels localization collection is missing.");

            int created = AffixSetGenerator.GenerateSetsForStats(
                missing,
                statsDatabase,
                tagDatabase,
                menuLabels,
                affixLabels,
                EditorPaths.AffixesBaseFolder);

            HashSet<StatType> remaining = FindMissingStats();
            if (remaining.Count > 0)
                throw new InvalidOperationException("Some stat families were not generated:\n" + string.Join(", ", remaining.OrderBy(value => value.ToString())));

            return created;
        }

        private static HashSet<StatType> FindMissingStats()
        {
            var affixes = AssetDatabase.FindAssets("t:ItemAffixSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ItemAffixSO>)
                .Where(affix => affix != null)
                .ToList();
            return AffixSetGenerator.GetStatsWithoutAffixSet(affixes);
        }

        private static string BuildReport(IEnumerable<StatType> stats)
        {
            List<StatType> ordered = stats.OrderBy(value => value.ToString()).ToList();
            return ordered.Count == 0
                ? "Every stat already has an affix family."
                : $"Stats without affixes: {ordered.Count}\n\n{string.Join("\n", ordered)}";
        }
    }
}
