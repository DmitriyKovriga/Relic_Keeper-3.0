using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
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

        [MenuItem(MenuRoot + "Rebalance All Affix Values")]
        public static void RebalanceAllFromMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebalance affix values",
                    "Rewrite Min/Max (and damage ranges) on every ItemAffixSO using AffixValueBalance. Passive tree assets are not touched. Localization and pools stay as they are.",
                    "Rebalance",
                    "Cancel"))
                return;

            int updated = RebalanceAllAffixValues();
            EditorUtility.DisplayDialog("Affix content", $"Updated values on {updated} affix assets.", "OK");
        }

        public static int RebalanceAllAffixValues()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO");
            int updated = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                if (affix == null)
                    continue;

                if (RebalanceAffix(affix))
                {
                    EditorUtility.SetDirty(affix);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            return updated;
        }

        private static bool RebalanceAffix(ItemAffixSO affix)
        {
            bool changed = false;
            string strength = AffixValueBalance.ResolveStrength(!string.IsNullOrEmpty(affix.GroupID) ? affix.GroupID : affix.name);
            if (affix.Tiers != null)
            {
                foreach (var tierData in affix.Tiers)
                {
                    if (tierData?.Stats == null)
                        continue;
                    for (int i = 0; i < tierData.Stats.Length; i++)
                    {
                        var data = tierData.Stats[i];
                        var kind = StatPresentation.FromStatModType(data.Type);
                        var before = data;
                        AffixValueBalance.Apply(ref data, data.Stat, kind, strength, tierData.Tier);
                        if (data.MaxValue < 0f || before.MaxValue < 0f)
                            KeepNegativeSign(ref data, before);
                        if (!ValuesEqual(before, data))
                        {
                            tierData.Stats[i] = data;
                            changed = true;
                        }
                    }
                }
            }

            if (affix.Stats != null)
            {
                for (int i = 0; i < affix.Stats.Length; i++)
                {
                    var data = affix.Stats[i];
                    var kind = StatPresentation.FromStatModType(data.Type);
                    var before = data;
                    AffixValueBalance.Apply(ref data, data.Stat, kind, strength, Mathf.Clamp(affix.Tier, 1, 5));
                    if (data.MaxValue < 0f || before.MaxValue < 0f)
                        KeepNegativeSign(ref data, before);
                    if (!ValuesEqual(before, data))
                    {
                        affix.Stats[i] = data;
                        changed = true;
                    }
                }
            }

            return changed;
        }

        private static void KeepNegativeSign(ref ItemAffixSO.AffixStatData data, ItemAffixSO.AffixStatData before)
        {
            if (before.MaxValue >= 0f && before.MinValue >= 0f)
                return;

            float magMin = Mathf.Abs(data.MinValue);
            float magMax = Mathf.Abs(data.MaxValue);
            if (magMax < magMin)
                (magMin, magMax) = (magMax, magMin);
            data.MinValue = -magMax;
            data.MaxValue = -magMin;

            if (!data.UsesRangeRoll())
                return;

            float rangeMin = Mathf.Abs(data.RangeMinValue);
            float rangeMax = Mathf.Abs(data.RangeMaxValue);
            if (rangeMax < rangeMin)
                (rangeMin, rangeMax) = (rangeMax, rangeMin);
            data.RangeMinValue = -rangeMax;
            data.RangeMaxValue = -rangeMin;
        }

        private static bool ValuesEqual(ItemAffixSO.AffixStatData a, ItemAffixSO.AffixStatData b)
        {
            return Mathf.Approximately(a.MinValue, b.MinValue) &&
                   Mathf.Approximately(a.MaxValue, b.MaxValue) &&
                   Mathf.Approximately(a.RangeMinValue, b.RangeMinValue) &&
                   Mathf.Approximately(a.RangeMaxValue, b.RangeMaxValue);
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
