using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Scripts.Editor.Affixes
{
    public static class AffixSetGenerator
    {
        public sealed class AffixRebuildReport
        {
            public StatType Stat;
            public int Created;
            public int Updated;
            public int DeletedObsolete;
            public int PoolReferencesReplaced;
            public int PoolReferencesRemoved;
            public int LocalizationsRegenerated;

            public string ToSummaryString()
            {
                return string.Join(
                    "\n",
                    new[]
                    {
                        $"Stat: {Stat}",
                        $"Created: {Created}",
                        $"Updated: {Updated}",
                        $"Deleted obsolete: {DeletedObsolete}",
                        $"Pool references replaced: {PoolReferencesReplaced}",
                        $"Pool references removed: {PoolReferencesRemoved}",
                        $"Localizations regenerated: {LocalizationsRegenerated}"
                    });
            }
        }

        public sealed class AffixKindGenerationReport
        {
            public StatType Stat;
            public StatAffixModifierKind Kind;
            public bool NegativeFlat;
            public int Created;
            public int Existing;
            public int Skipped;
            public int LocalizationsRegenerated;

            public string ToSummaryString()
            {
                string kindName = GetGeneratedKindDisplayName(Kind, NegativeFlat);
                return string.Join(
                    "\n",
                    new[]
                    {
                        $"Stat: {Stat}",
                        $"Kind: {kindName}",
                        $"Created: {Created}",
                        $"Already existed: {Existing}",
                        $"Skipped: {Skipped}",
                        $"Localizations regenerated: {LocalizationsRegenerated}"
                    });
            }
        }

        private readonly struct GeneratedAffixDefinition
        {
            public readonly StatType Stat;
            public readonly StatModType ModType;
            public readonly StatAffixModifierKind Kind;
            public readonly string Strength;
            public readonly StatAffixGenType GenType;
            public readonly bool NegativeFlat;
            public readonly string AssetPath;

            public GeneratedAffixDefinition(
                StatType stat,
                StatModType modType,
                StatAffixModifierKind kind,
                string strength,
                StatAffixGenType genType,
                bool negativeFlat,
                string assetPath)
            {
                Stat = stat;
                ModType = modType;
                Kind = kind;
                Strength = strength;
                GenType = genType;
                NegativeFlat = negativeFlat;
                AssetPath = assetPath;
            }
        }

        private const string StrengthStrong = "Strong";
        private const string StrengthMedium = "Medium";
        private const string StrengthLight = "Light";
        private static readonly string[] Strengths = { StrengthStrong, StrengthMedium, StrengthLight };

        public static int DeleteAllAffixes(List<AffixPoolSO> pools)
        {
            foreach (var pool in pools)
            {
                if (pool.Affixes == null || pool.Affixes.Count == 0)
                    continue;

                pool.Affixes.Clear();
                EditorUtility.SetDirty(pool);
            }

            string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO");
            int removed = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.DeleteAsset(path);
                removed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return removed;
        }

        public static HashSet<StatType> GetStatsWithoutAffixSet(List<ItemAffixSO> allAffixes)
        {
            var withSet = new HashSet<StatType>();
            foreach (var affix in allAffixes)
            {
                var stats = GetRepresentativeStats(affix);
                if (stats != null && stats.Length > 0)
                    withSet.Add(stats[0].Stat);
            }

            var result = new HashSet<StatType>();
            foreach (StatType type in Enum.GetValues(typeof(StatType)))
            {
                if (!withSet.Contains(type))
                    result.Add(type);
            }

            return result;
        }

        public static int GenerateSetsForStats(
            HashSet<StatType> statsToGenerate,
            StatsDatabaseSO statsDb,
            AffixTagDatabaseSO tagDatabase,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels,
            string affixesBaseFolder)
        {
            if (statsToGenerate == null || statsToGenerate.Count == 0 || statsDb == null)
                return 0;

            int created = 0;
            EnsureValueUnitLocalizations(menuLabels);

            foreach (StatType stat in statsToGenerate)
            {
                StatAffixGenType genType = statsDb.GetAffixGenType(stat);
                string category = statsDb.GetCategory(stat);
                string statName = stat.ToString();
                string folder = $"{affixesBaseFolder}/ByStat/{category}/{statName}";

                EnsureFolder($"{affixesBaseFolder}/ByStat");
                EnsureFolder($"{affixesBaseFolder}/ByStat/{category}");
                EnsureFolder(folder);

                foreach (var kind in StatPresentation.EnumerateKinds(statsDb.GetAllowedAffixKinds(stat)))
                {
                    if (!IsKindAllowedForGenType(kind, genType))
                        continue;

                    bool generateNegativeFlatVariant = kind == StatAffixModifierKind.Flat && statsDb.AllowNegativeFlatGeneration(stat);
                    int variantCount = generateNegativeFlatVariant ? 2 : 1;

                    for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
                    {
                        bool negativeFlat = generateNegativeFlatVariant && variantIndex == 1;
                        StatModType modType = StatPresentation.ToStatModType(kind);
                        string kindDisplayName = GetGeneratedKindDisplayName(kind, negativeFlat);

                        foreach (string strength in Strengths)
                        {
                            string fileName = $"{statName}_{kindDisplayName}_{strength}.asset";
                            string path = Path.Combine(folder, fileName).Replace('\\', '/');
                            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null)
                                continue;

                            var affix = CreateTieredAffix(stat, modType, kind, strength, genType, negativeFlat);
                            AssetDatabase.CreateAsset(affix, path);
                            affix.UniqueID = affix.GroupID;
                            WriteLocalization(affix, stat, kind, strength, menuLabels, affixesLabels, statsDb);
                            SyncTagFromCategory(affix, statsDb, stat, tagDatabase);
                            EditorUtility.SetDirty(affix);
                            created++;
                        }
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return created;
        }

        public static AffixRebuildReport RebuildGeneratedAffixesForStat(
            StatType stat,
            StatsDatabaseSO statsDb,
            AffixTagDatabaseSO tagDatabase,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels,
            string affixesBaseFolder,
            bool removeObsolete = true)
        {
            var report = new AffixRebuildReport { Stat = stat };
            if (statsDb == null)
                return report;

            EnsureValueUnitLocalizations(menuLabels);

            string folder = GetGeneratedFolderPath(statsDb, stat, affixesBaseFolder);
            EnsureFolder($"{affixesBaseFolder}/ByStat");
            EnsureFolder($"{affixesBaseFolder}/ByStat/{statsDb.GetCategory(stat)}");
            EnsureFolder(folder);

            List<GeneratedAffixDefinition> desiredDefinitions = BuildDefinitionsForStat(stat, statsDb, folder);
            var desiredByPath = desiredDefinitions.ToDictionary(definition => definition.AssetPath, definition => definition, StringComparer.OrdinalIgnoreCase);
            var createdOrUpdatedAssets = new Dictionary<string, ItemAffixSO>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in desiredDefinitions)
            {
                var blueprint = CreateTieredAffix(
                    definition.Stat,
                    definition.ModType,
                    definition.Kind,
                    definition.Strength,
                    definition.GenType,
                    definition.NegativeFlat);

                var existing = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(definition.AssetPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(blueprint, definition.AssetPath);
                    blueprint.UniqueID = blueprint.GroupID;
                    WriteLocalization(blueprint, definition.Stat, definition.Kind, definition.Strength, menuLabels, affixesLabels, statsDb);
                    SyncTagFromCategory(blueprint, statsDb, definition.Stat, tagDatabase);
                    EditorUtility.SetDirty(blueprint);
                    createdOrUpdatedAssets[definition.AssetPath] = blueprint;
                    report.Created++;
                    report.LocalizationsRegenerated++;
                    continue;
                }

                CopyGeneratedAffixData(blueprint, existing);
                existing.UniqueID = existing.GroupID;
                SyncTagFromCategory(existing, statsDb, definition.Stat, tagDatabase);

                if (!existing.LockAutoLocalization)
                {
                    RegenerateLocalizationFromStat(existing, menuLabels, affixesLabels);
                    report.LocalizationsRegenerated++;
                }

                EditorUtility.SetDirty(existing);
                createdOrUpdatedAssets[definition.AssetPath] = existing;
                report.Updated++;
            }

            if (removeObsolete)
            {
                var pools = LoadAllPools();
                string[] existingGuids = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { folder });
                foreach (string guid in existingGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (desiredByPath.ContainsKey(path))
                        continue;

                    var obsolete = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
                    if (obsolete == null || !LooksLikeManagedGeneratedAffix(stat, obsolete))
                        continue;

                    ItemAffixSO replacement = ResolveReplacementAssetForObsolete(stat, obsolete, desiredByPath, createdOrUpdatedAssets);
                    foreach (var pool in pools)
                    {
                        if (pool?.Affixes == null)
                            continue;

                        bool poolChanged = false;
                        for (int index = pool.Affixes.Count - 1; index >= 0; index--)
                        {
                            if (pool.Affixes[index] != obsolete)
                                continue;

                            if (replacement != null)
                            {
                                if (!pool.Affixes.Contains(replacement))
                                {
                                    pool.Affixes[index] = replacement;
                                    report.PoolReferencesReplaced++;
                                }
                                else
                                {
                                    pool.Affixes.RemoveAt(index);
                                    report.PoolReferencesRemoved++;
                                }
                            }
                            else
                            {
                                pool.Affixes.RemoveAt(index);
                                report.PoolReferencesRemoved++;
                            }

                            poolChanged = true;
                        }

                        if (poolChanged)
                            EditorUtility.SetDirty(pool);
                    }

                    AssetDatabase.DeleteAsset(path);
                    report.DeletedObsolete++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        public static AffixKindGenerationReport GenerateKindForStat(
            StatType stat,
            StatAffixModifierKind kind,
            bool negativeFlat,
            StatsDatabaseSO statsDb,
            AffixTagDatabaseSO tagDatabase,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels,
            string affixesBaseFolder)
        {
            var report = new AffixKindGenerationReport
            {
                Stat = stat,
                Kind = kind,
                NegativeFlat = negativeFlat
            };

            if (statsDb == null)
                return report;

            StatAffixGenType genType = statsDb.GetAffixGenType(stat);
            StatAffixModifierKindFlags allowedKinds = statsDb.GetAllowedAffixKinds(stat);
            if ((allowedKinds & StatPresentation.ToFlag(kind)) == 0 || !IsKindAllowedForGenType(kind, genType))
            {
                report.Skipped++;
                return report;
            }

            if (negativeFlat && (kind != StatAffixModifierKind.Flat || !statsDb.AllowNegativeFlatGeneration(stat)))
            {
                report.Skipped++;
                return report;
            }

            EnsureValueUnitLocalizations(menuLabels);

            string category = statsDb.GetCategory(stat);
            string statName = stat.ToString();
            string folder = $"{affixesBaseFolder}/ByStat/{category}/{statName}";
            EnsureFolder($"{affixesBaseFolder}/ByStat");
            EnsureFolder($"{affixesBaseFolder}/ByStat/{category}");
            EnsureFolder(folder);

            StatModType modType = StatPresentation.ToStatModType(kind);
            string kindDisplayName = GetGeneratedKindDisplayName(kind, negativeFlat);

            foreach (string strength in Strengths)
            {
                string fileName = $"{statName}_{kindDisplayName}_{strength}.asset";
                string path = Path.Combine(folder, fileName).Replace('\\', '/');
                if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null)
                {
                    report.Existing++;
                    continue;
                }

                var affix = CreateTieredAffix(stat, modType, kind, strength, genType, negativeFlat);
                AssetDatabase.CreateAsset(affix, path);
                affix.UniqueID = affix.GroupID;
                WriteLocalization(affix, stat, kind, strength, menuLabels, affixesLabels, statsDb);
                SyncTagFromCategory(affix, statsDb, stat, tagDatabase);
                EditorUtility.SetDirty(affix);
                report.Created++;
                report.LocalizationsRegenerated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        public static void EnsureValueUnitLocalizations(StringTableCollection menuLabels)
        {
            if (menuLabels == null)
                return;

            foreach (StatValueUnit unit in Enum.GetValues(typeof(StatValueUnit)))
            {
                if (unit == StatValueUnit.None)
                    continue;

                string key = StatPresentation.GetValueUnitLocalizationKey(unit);
                SetOrAddEntry(menuLabels, "en", key, StatPresentation.GetValueUnitFallback(unit, "en"));
                SetOrAddEntry(menuLabels, "ru", key, StatPresentation.GetValueUnitFallback(unit, "ru"));
            }
        }

        public static void FillMissingLocalization(ItemAffixSO affix, StringTableCollection menuLabels, StringTableCollection affixesLabels)
        {
            if (affix == null || affixesLabels == null || affix.LockAutoLocalization)
                return;

            var stats = GetRepresentativeStats(affix);
            if (stats == null || stats.Length == 0)
                return;

            EnsureValueUnitLocalizations(menuLabels);
            var stat = stats[0].Stat;
            var kind = StatPresentation.FromStatModType(stats[0].Type);
            string strength = ParseStrengthFromGroupId(affix.GroupID);

            string nameKey = string.IsNullOrEmpty(affix.NameKey) ? "affix_name_" + SanitizeKey(affix.name) : affix.NameKey;
            string valueKey = ResolveValueKey(affix, stat, kind);

            if (IsMissingLocalizationValue(GetLocalizedString(affixesLabels, "en", nameKey)))
            {
                WriteNameLocalization(affix, stat, kind, strength, menuLabels, affixesLabels);
            }

            if (IsMissingLocalizationValue(GetLocalizedString(affixesLabels, "en", valueKey)))
            {
                WriteValueLocalization(affix, stat, kind, menuLabels, affixesLabels, Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase));
            }
        }

        public static void RegenerateLocalizationFromStat(ItemAffixSO affix, StringTableCollection menuLabels, StringTableCollection affixesLabels)
        {
            var stats = GetRepresentativeStats(affix);
            if (affix == null || affixesLabels == null || stats == null || stats.Length == 0)
                return;

            EnsureValueUnitLocalizations(menuLabels);
            var stat = stats[0].Stat;
            var kind = StatPresentation.FromStatModType(stats[0].Type);
            string strength = ParseStrengthFromGroupId(affix.GroupID);

            WriteNameLocalization(affix, stat, kind, strength, menuLabels, affixesLabels);
            WriteValueLocalization(affix, stat, kind, menuLabels, affixesLabels, Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase));
            EditorUtility.SetDirty(affix);
        }

        public static string GetValueKey(StatType stat, StatAffixModifierKind kind, AffixValueMode valueMode = AffixValueMode.Single)
        {
            return BuildValueKey(stat, kind, valueMode);
        }

        public static string GetValueKeyForAffix(ItemAffixSO affix)
        {
            var stats = GetRepresentativeStats(affix);
            if (stats == null || stats.Length == 0)
                return null;

            var statData = stats[0];
            var kind = StatPresentation.FromStatModType(statData.Type);
            return ResolveValueKey(affix, statData.Stat, kind);
        }

        public static string GetTypeDisplayName(StatModType type)
        {
            return StatPresentation.GetModifierKindDisplayName(StatPresentation.FromStatModType(type));
        }

        public static ItemAffixSO.AffixStatData[] GetRepresentativeStats(ItemAffixSO affix)
        {
            if (affix == null)
                return Array.Empty<ItemAffixSO.AffixStatData>();

            return affix.GetStatsForTier(affix.GetDefaultTier()) ?? Array.Empty<ItemAffixSO.AffixStatData>();
        }

        public static string GetGeneratedFolderPath(StatsDatabaseSO statsDb, StatType stat, string affixesBaseFolder)
        {
            string category = statsDb != null ? statsDb.GetCategory(stat) : StatsDatabaseSO.DefaultCategoryFor(stat);
            string statName = stat.ToString();
            return $"{affixesBaseFolder}/ByStat/{category}/{statName}";
        }

        public static bool IsKindAllowedForGenType(StatAffixModifierKind kind, StatAffixGenType genType)
        {
            switch (genType)
            {
                case StatAffixGenType.NOCalcStat:
                    return kind == StatAffixModifierKind.Flat;
                case StatAffixGenType.PercentStat:
                    return kind == StatAffixModifierKind.Flat || kind == StatAffixModifierKind.Increase || kind == StatAffixModifierKind.Decrease;
                case StatAffixGenType.ContextModifierStat:
                    return kind != StatAffixModifierKind.Flat;
                default:
                    return true;
            }
        }

        private static List<GeneratedAffixDefinition> BuildDefinitionsForStat(StatType stat, StatsDatabaseSO statsDb, string folder)
        {
            var definitions = new List<GeneratedAffixDefinition>();
            StatAffixGenType genType = statsDb.GetAffixGenType(stat);

            foreach (var kind in StatPresentation.EnumerateKinds(statsDb.GetAllowedAffixKinds(stat)))
            {
                if (!IsKindAllowedForGenType(kind, genType))
                    continue;

                bool generateNegativeFlatVariant = kind == StatAffixModifierKind.Flat && statsDb.AllowNegativeFlatGeneration(stat);
                int variantCount = generateNegativeFlatVariant ? 2 : 1;

                for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
                {
                    bool negativeFlat = generateNegativeFlatVariant && variantIndex == 1;
                    StatModType modType = StatPresentation.ToStatModType(kind);
                    string kindDisplayName = GetGeneratedKindDisplayName(kind, negativeFlat);

                    foreach (string strength in Strengths)
                    {
                        string fileName = $"{stat}_{kindDisplayName}_{strength}.asset";
                        string assetPath = Path.Combine(folder, fileName).Replace('\\', '/');
                        definitions.Add(new GeneratedAffixDefinition(stat, modType, kind, strength, genType, negativeFlat, assetPath));
                    }
                }
            }

            return definitions;
        }

        private static ItemAffixSO CreateAffix(StatType stat, StatModType modType, StatAffixModifierKind kind, string strength, int tier, StatAffixGenType genType, bool negativeFlat = false)
        {
            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            string groupKindId = GetGeneratedKindDisplayName(kind, negativeFlat);
            string nameKeyKindId = GetGeneratedKindLocalizationId(kind, negativeFlat);
            affix.GroupID = $"{stat}_{groupKindId}_{strength}";
            affix.Tier = tier;
            affix.TranslationKey = BuildValueKey(stat, kind, AffixValueMode.Single);
            affix.NameKey = $"affix_name_{stat.ToString().ToLowerInvariant()}_{nameKeyKindId}_{strength.ToLowerInvariant()}_t{tier}";
            affix.Stats = new ItemAffixSO.AffixStatData[1];
            affix.Stats[0].Stat = stat;
            affix.Stats[0].Type = modType;
            affix.Stats[0].Scope = StatScope.Global;
            affix.Stats[0].ValueMode = AffixValueMode.Single;

            if (genType == StatAffixGenType.FullCalcStat)
                SetValuesFullCalc(ref affix.Stats[0], stat, kind, tier, strength);
            else
                SetValuesSmallFlat(ref affix.Stats[0], tier, strength);

            if (negativeFlat)
                ConvertStatDataToNegativeFlat(ref affix.Stats[0]);

            if (affix.TagIds == null)
                affix.TagIds = new List<string>();

            return affix;
        }

        private static ItemAffixSO CreateTieredAffix(
            StatType stat,
            StatModType modType,
            StatAffixModifierKind kind,
            string strength,
            StatAffixGenType genType,
            bool negativeFlat = false)
        {
            var result = ScriptableObject.CreateInstance<ItemAffixSO>();
            string groupKindId = GetGeneratedKindDisplayName(kind, negativeFlat);
            string nameKeyKindId = GetGeneratedKindLocalizationId(kind, negativeFlat);
            result.GroupID = $"{stat}_{groupKindId}_{strength}";
            result.UniqueID = result.GroupID;
            result.TranslationKey = BuildValueKey(stat, kind, AffixValueMode.Single);
            result.NameKey = $"affix_name_{stat.ToString().ToLowerInvariant()}_{nameKeyKindId}_{strength.ToLowerInvariant()}";
            result.Tiers = new List<ItemAffixSO.AffixTierData>();

            for (int tier = 1; tier <= 5; tier++)
            {
                var legacy = CreateAffix(stat, modType, kind, strength, tier, genType, negativeFlat);
                result.Tiers.Add(new ItemAffixSO.AffixTierData
                {
                    Tier = tier,
                    Stats = legacy.Stats
                });
                UnityEngine.Object.DestroyImmediate(legacy);
            }

            result.Tier = 0;
            result.Stats = Array.Empty<ItemAffixSO.AffixStatData>();
            result.LegacyTierIds = new List<ItemAffixSO.LegacyTierId>();
            result.TagIds = new List<string>();
            return result;
        }

        private static void SetValuesFullCalc(ref ItemAffixSO.AffixStatData data, StatType stat, StatAffixModifierKind kind, int tier, string strength)
        {
            int stepIndex = 5 - tier;
            float hpManaMultiplier = (stat.ToString().Contains("Health") || stat.ToString().Contains("Mana")) ? 5f : 1f;

            float baseMin;
            float baseMax;
            float stepMin;
            float stepMax;

            if (kind == StatAffixModifierKind.Flat)
            {
                if (strength == StrengthStrong) { baseMin = 5f; baseMax = 10f; stepMin = 5f; stepMax = 5f; }
                else if (strength == StrengthMedium) { baseMin = 4f; baseMax = 8f; stepMin = 4f; stepMax = 4f; }
                else { baseMin = 3f; baseMax = 7f; stepMin = 3f; stepMax = 3f; }

                data.MinValue = (baseMin + (stepIndex * stepMin)) * hpManaMultiplier;
                data.MaxValue = (baseMax + (stepIndex * stepMax)) * hpManaMultiplier;
                return;
            }

            if (kind == StatAffixModifierKind.Increase || kind == StatAffixModifierKind.Decrease)
            {
                if (strength == StrengthStrong) { baseMin = 5f; baseMax = 10f; stepMin = 5f; stepMax = 5f; }
                else if (strength == StrengthMedium) { baseMin = 4f; baseMax = 8f; stepMin = 4f; stepMax = 4f; }
                else { baseMin = 3f; baseMax = 7f; stepMin = 3f; stepMax = 3f; }

                data.MinValue = baseMin + (stepIndex * stepMin);
                data.MaxValue = baseMax + (stepIndex * stepMax);
                return;
            }

            if (strength == StrengthStrong) { baseMin = 2f; baseMax = 5f; stepMin = 2f; stepMax = 2f; }
            else if (strength == StrengthMedium) { baseMin = 1.5f; baseMax = 4f; stepMin = 1.5f; stepMax = 1.5f; }
            else { baseMin = 1f; baseMax = 3f; stepMin = 1f; stepMax = 1f; }

            data.MinValue = baseMin + (stepIndex * stepMin);
            data.MaxValue = baseMax + (stepIndex * stepMax);
        }

        private static void SetValuesSmallFlat(ref ItemAffixSO.AffixStatData data, int tier, string strength)
        {
            if (strength == StrengthStrong)
            {
                data.MinValue = Mathf.Clamp(6 - tier, 1, 5);
                data.MaxValue = Mathf.Clamp(8 - tier, 2, 7);
            }
            else if (strength == StrengthMedium)
            {
                data.MinValue = Mathf.Clamp(4 - tier, 1, 3);
                data.MaxValue = Mathf.Clamp(6 - tier, 2, 5);
            }
            else
            {
                data.MinValue = 1f;
                data.MaxValue = Mathf.Clamp(4 - tier, 1, 3);
            }
        }

        private static void WriteLocalization(
            ItemAffixSO affix,
            StatType stat,
            StatAffixModifierKind kind,
            string strength,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels,
            StatsDatabaseSO statsDb)
        {
            WriteNameLocalization(affix, stat, kind, strength, menuLabels, affixesLabels);
            WriteValueLocalization(affix, stat, kind, menuLabels, affixesLabels, statsDb);
        }

        private static void CopyGeneratedAffixData(ItemAffixSO source, ItemAffixSO target)
        {
            target.GroupID = source.GroupID;
            target.Tier = 0;
            target.NameKey = source.NameKey;
            target.TranslationKey = source.TranslationKey;
            target.Stats = Array.Empty<ItemAffixSO.AffixStatData>();
            target.Tiers = source.Tiers;
            if (target.TagIds == null)
                target.TagIds = new List<string>();
        }

        private static void WriteNameLocalization(
            ItemAffixSO affix,
            StatType stat,
            StatAffixModifierKind kind,
            string strength,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels)
        {
            string statNameEn = ResolveStatName(menuLabels, stat, "en");
            string statNameRu = ResolveStatName(menuLabels, stat, "ru");
            string strengthRu = strength == StrengthStrong ? "Сильный" : strength == StrengthMedium ? "Средний" : "Лёгкий";
            string nameKey = string.IsNullOrEmpty(affix.NameKey) ? "affix_name_" + SanitizeKey(affix.name) : affix.NameKey;

            if (kind == StatAffixModifierKind.Flat)
            {
                SetOrAddEntry(affixesLabels, "en", nameKey, $"{strength} {statNameEn}");
                SetOrAddEntry(affixesLabels, "ru", nameKey, $"{strengthRu} {statNameRu}");
            }
            else
            {
                string kindEn = StatPresentation.GetModifierKindDisplayName(kind).ToLowerInvariant();
                string kindRu = GetModifierKindRu(kind);
                SetOrAddEntry(affixesLabels, "en", nameKey, $"{strength} {statNameEn} {kindEn}");
                SetOrAddEntry(affixesLabels, "ru", nameKey, $"{strengthRu} {statNameRu} {kindRu}");
            }

            affix.NameKey = nameKey;
        }

        private static void WriteValueLocalization(
            ItemAffixSO affix,
            StatType stat,
            StatAffixModifierKind kind,
            StringTableCollection menuLabels,
            StringTableCollection affixesLabels,
            StatsDatabaseSO statsDb)
        {
            string statNameEn = ResolveStatName(menuLabels, stat, "en");
            string statNameRu = ResolveStatName(menuLabels, stat, "ru");
            StatValueUnit unit = statsDb != null ? statsDb.GetValueUnit(stat) : StatsDatabaseSO.DefaultValueUnitFor(stat);
            string unitEn = ResolveUnit(menuLabels, unit, "en");
            string unitRu = ResolveUnit(menuLabels, unit, "ru");
            bool isRangeValue = IsRangeValueMode(affix, kind);
            string valueKey = ResolveValueKey(affix, stat, kind);

            SetOrAddEntry(affixesLabels, "en", valueKey, GenerateValueTemplateEn(kind, statNameEn, unit, unitEn, isRangeValue));
            SetOrAddEntry(affixesLabels, "ru", valueKey, GenerateValueTemplateRu(kind, statNameRu, unit, unitRu, isRangeValue));
            affix.TranslationKey = valueKey;
        }

        private static void SyncTagFromCategory(ItemAffixSO affix, StatsDatabaseSO db, StatType stat, AffixTagDatabaseSO tagDb)
        {
            if (affix.TagIds == null)
                affix.TagIds = new List<string>();

            string category = db != null ? db.GetCategory(stat) : "Misc";
            if (string.IsNullOrEmpty(category))
                return;

            if (!affix.TagIds.Contains(category))
                affix.TagIds.Add(category);

            if (tagDb != null && !tagDb.HasTag(category))
            {
                tagDb.AddTag(category, "tag_" + category.ToLowerInvariant());
                EditorUtility.SetDirty(tagDb);
            }
        }

        private static string BuildValueKey(StatType stat, StatAffixModifierKind kind, AffixValueMode valueMode)
        {
            string key = $"affix_{StatPresentation.GetModifierKindId(kind)}_{stat.ToString().ToLowerInvariant()}";
            if (kind == StatAffixModifierKind.Flat && valueMode == AffixValueMode.Range)
                key += "_range";
            return key;
        }

        private static string ResolveValueKey(ItemAffixSO affix, StatType stat, StatAffixModifierKind kind)
        {
            string preferredKey = BuildValueKey(stat, kind, GetValueMode(affix));
            if (affix == null || string.IsNullOrEmpty(affix.TranslationKey))
                return preferredKey;

            if (IsAutoValueKey(affix.TranslationKey, stat, kind))
                return preferredKey;

            return affix.TranslationKey;
        }

        private static bool IsAutoValueKey(string key, StatType stat, StatAffixModifierKind kind)
        {
            return key == BuildValueKey(stat, kind, AffixValueMode.Single) ||
                   key == BuildValueKey(stat, kind, AffixValueMode.Range);
        }

        private static AffixValueMode GetValueMode(ItemAffixSO affix)
        {
            var stats = GetRepresentativeStats(affix);
            if (stats == null || stats.Length == 0)
                return AffixValueMode.Single;

            return stats[0].GetEffectiveValueMode();
        }

        private static bool IsRangeValueMode(ItemAffixSO affix, StatAffixModifierKind kind)
        {
            return kind == StatAffixModifierKind.Flat && GetValueMode(affix) == AffixValueMode.Range;
        }

        private static string ResolveStatName(StringTableCollection menuLabels, StatType stat, string locale)
        {
            string key = "stats." + stat;
            string localized = GetLocalizedString(menuLabels, locale, key);
            return string.IsNullOrWhiteSpace(localized) ? stat.ToString() : localized;
        }

        private static string ResolveUnit(StringTableCollection menuLabels, StatValueUnit unit, string locale)
        {
            if (unit == StatValueUnit.None)
                return string.Empty;

            string key = StatPresentation.GetValueUnitLocalizationKey(unit);
            string localized = GetLocalizedString(menuLabels, locale, key);
            return string.IsNullOrWhiteSpace(localized) ? StatPresentation.GetValueUnitFallback(unit, locale) : localized;
        }

        private static string GenerateValueTemplateEn(StatAffixModifierKind kind, string statName, StatValueUnit unit, string localizedUnit, bool isRangeValue)
        {
            const string SignedValue = "{0:+0.##;-0.##;0}";
            const string SignedRangeMin = "{0:+0.##;-0.##;0}";
            const string SignedRangeMax = "{1:+0.##;-0.##;0}";

            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return $"{{0}}% increased {statName}";
                case StatAffixModifierKind.Decrease:
                    return $"{{0}}% reduced {statName}";
                case StatAffixModifierKind.More:
                    return $"{{0}}% more {statName}";
                case StatAffixModifierKind.Less:
                    return $"{{0}}% less {statName}";
                default:
                    if (isRangeValue)
                    {
                        if (unit == StatValueUnit.Percent)
                            return $"{SignedRangeMin}-{SignedRangeMax}% to {statName}";

                        if (string.IsNullOrEmpty(localizedUnit))
                            return $"{SignedRangeMin}-{SignedRangeMax} to {statName}";

                        return StatPresentation.IsSymbolUnit(unit)
                            ? $"{SignedRangeMin}-{SignedRangeMax}{localizedUnit} to {statName}"
                            : $"{SignedRangeMin}-{SignedRangeMax} {localizedUnit} to {statName}";
                    }

                    if (unit == StatValueUnit.Percent)
                        return $"{SignedValue}% to {statName}";

                    if (string.IsNullOrEmpty(localizedUnit))
                        return $"{SignedValue} to {statName}";

                    return StatPresentation.IsSymbolUnit(unit)
                        ? $"{SignedValue}{localizedUnit} to {statName}"
                        : $"{SignedValue} {localizedUnit} to {statName}";
            }
        }

        private static string GenerateValueTemplateRu(StatAffixModifierKind kind, string statName, StatValueUnit unit, string localizedUnit, bool isRangeValue)
        {
            const string SignedValue = "{0:+0.##;-0.##;0}";
            const string SignedRangeMin = "{0:+0.##;-0.##;0}";
            const string SignedRangeMax = "{1:+0.##;-0.##;0}";

            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return $"{{0}}% \u0443\u0432\u0435\u043b\u0438\u0447\u0435\u043d\u0438\u0435 {statName}";
                case StatAffixModifierKind.Decrease:
                    return $"{{0}}% \u0443\u043c\u0435\u043d\u044c\u0448\u0435\u043d\u0438\u0435 {statName}";
                case StatAffixModifierKind.More:
                    return $"{{0}}% \u0431\u043e\u043b\u044c\u0448\u0435 {statName}";
                case StatAffixModifierKind.Less:
                    return $"{{0}}% \u043c\u0435\u043d\u044c\u0448\u0435 {statName}";
                default:
                    if (isRangeValue)
                    {
                        if (unit == StatValueUnit.Percent)
                            return $"{SignedRangeMin}-{SignedRangeMax}% \u043a {statName}";

                        if (string.IsNullOrEmpty(localizedUnit))
                            return $"{SignedRangeMin}-{SignedRangeMax} \u043a {statName}";

                        return StatPresentation.IsSymbolUnit(unit)
                            ? $"{SignedRangeMin}-{SignedRangeMax}{localizedUnit} \u043a {statName}"
                            : $"{SignedRangeMin}-{SignedRangeMax} {localizedUnit} \u043a {statName}";
                    }

                    if (unit == StatValueUnit.Percent)
                        return $"{SignedValue}% \u043a {statName}";

                    if (string.IsNullOrEmpty(localizedUnit))
                        return $"{SignedValue} \u043a {statName}";

                    return StatPresentation.IsSymbolUnit(unit)
                        ? $"{SignedValue}{localizedUnit} \u043a {statName}"
                        : $"{SignedValue} {localizedUnit} \u043a {statName}";
            }
        }

        private static void ConvertStatDataToNegativeFlat(ref ItemAffixSO.AffixStatData data)
        {
            float originalMin = data.MinValue;
            float originalMax = data.MaxValue;
            data.MinValue = -Mathf.Abs(Mathf.Max(originalMin, originalMax));
            data.MaxValue = -Mathf.Abs(Mathf.Min(originalMin, originalMax));

            float originalSecondaryMin = data.RangeMinValue;
            float originalSecondaryMax = data.RangeMaxValue;
            if (!Mathf.Approximately(originalSecondaryMin, 0f) || !Mathf.Approximately(originalSecondaryMax, 0f))
            {
                data.RangeMinValue = -Mathf.Abs(Mathf.Max(originalSecondaryMin, originalSecondaryMax));
                data.RangeMaxValue = -Mathf.Abs(Mathf.Min(originalSecondaryMin, originalSecondaryMax));
            }
        }

        private static string GetGeneratedKindDisplayName(StatAffixModifierKind kind, bool negativeFlat)
        {
            if (negativeFlat && kind == StatAffixModifierKind.Flat)
                return "FlatNegative";

            return StatPresentation.GetModifierKindDisplayName(kind);
        }

        private static string GetGeneratedKindLocalizationId(StatAffixModifierKind kind, bool negativeFlat)
        {
            if (negativeFlat && kind == StatAffixModifierKind.Flat)
                return "flatnegative";

            return StatPresentation.GetModifierKindId(kind);
        }

        private static string GetModifierKindRu(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return "\u0443\u0432\u0435\u043b\u0438\u0447\u0435\u043d\u0438\u0435";
                case StatAffixModifierKind.Decrease:
                    return "\u0443\u043c\u0435\u043d\u044c\u0448\u0435\u043d\u0438\u0435";
                case StatAffixModifierKind.More:
                    return "\u0431\u043e\u043b\u044c\u0448\u0435";
                case StatAffixModifierKind.Less:
                    return "\u043c\u0435\u043d\u044c\u0448\u0435";
                default:
                    return string.Empty;
            }
        }

        private static string GetLocalizedString(StringTableCollection collection, string locale, string key)
        {
            if (collection == null || string.IsNullOrEmpty(key))
                return string.Empty;

            var table = collection.GetTable(locale) as StringTable;
            if (table == null)
                table = collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
            if (table == null)
                return string.Empty;

            var entry = table.GetEntry(key);
            return entry?.Value ?? string.Empty;
        }

        private static void SetOrAddEntry(StringTableCollection collection, string locale, string key, string value)
        {
            if (collection == null || string.IsNullOrEmpty(key))
                return;

            var table = collection.GetTable(locale) as StringTable;
            if (table == null)
                table = collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
            if (table == null)
                return;

            var sharedData = collection.SharedData;
            if (sharedData != null && !sharedData.Contains(key))
            {
                sharedData.AddKey(key);
                EditorUtility.SetDirty(sharedData);
            }

            var entry = table.GetEntry(key);
            if (entry != null)
                entry.Value = value;
            else
                table.AddEntry(key, value);

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection);
        }

        private static void EnsureFolder(string path)
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

        private static List<AffixPoolSO> LoadAllPools()
        {
            var pools = new List<AffixPoolSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:AffixPoolSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var pool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(path);
                if (pool != null)
                    pools.Add(pool);
            }

            return pools;
        }

        private static ItemAffixSO ResolveReplacementAssetForObsolete(
            StatType stat,
            ItemAffixSO obsolete,
            IReadOnlyDictionary<string, GeneratedAffixDefinition> desiredByPath,
            IReadOnlyDictionary<string, ItemAffixSO> createdOrUpdatedAssets)
        {
            var obsoleteStats = GetRepresentativeStats(obsolete);
            if (obsoleteStats.Length == 0)
                return null;

            var statData = obsoleteStats[0];
            if (statData.Stat != stat)
                return null;

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(obsolete))?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                return null;

            string strength = ParseStrengthFromGroupId(obsolete.GroupID);
            bool negativeFlat = statData.Type == StatModType.PercentSub || (statData.Type == StatModType.Flat && statData.MaxValue < 0f);

            StatAffixModifierKind replacementKind = statData.Type switch
            {
                StatModType.PercentAdd => StatAffixModifierKind.Flat,
                StatModType.PercentSub => StatAffixModifierKind.Flat,
                StatModType.PercentMult => StatAffixModifierKind.More,
                StatModType.PercentLess => StatAffixModifierKind.Less,
                _ => StatPresentation.FromStatModType(statData.Type)
            };

            string kindDisplayName = GetGeneratedKindDisplayName(replacementKind, negativeFlat);
            string replacementPath = $"{folder}/{stat}_{kindDisplayName}_{strength}.asset";

            if (!desiredByPath.ContainsKey(replacementPath))
                return null;

            if (createdOrUpdatedAssets.TryGetValue(replacementPath, out var replacement))
                return replacement;

            return AssetDatabase.LoadAssetAtPath<ItemAffixSO>(replacementPath);
        }

        private static bool LooksLikeManagedGeneratedAffix(StatType stat, ItemAffixSO affix)
        {
            if (affix == null)
                return false;

            string statPrefix = stat + "_";
            string assetName = affix.name ?? string.Empty;
            string groupId = affix.GroupID ?? string.Empty;
            return assetName.StartsWith(statPrefix, StringComparison.OrdinalIgnoreCase) ||
                   groupId.StartsWith(statPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMissingLocalizationValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value.Trim() == "No translation found";
        }

        private static string ParseStrengthFromGroupId(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return StrengthMedium;

            var parts = groupId.Split('_');
            if (parts.Length >= 3)
            {
                string last = parts[parts.Length - 1];
                if (last == StrengthStrong || last == StrengthMedium || last == StrengthLight)
                    return last;
            }

            return StrengthMedium;
        }

        private static string SanitizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            }

            return new string(chars).ToLowerInvariant();
        }
    }
}
