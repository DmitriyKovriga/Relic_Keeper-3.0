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
                if (affix?.Stats != null && affix.Stats.Length > 0)
                    withSet.Add(affix.Stats[0].Stat);
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

                    StatModType modType = StatPresentation.ToStatModType(kind);
                    string kindId = StatPresentation.GetModifierKindDisplayName(kind);

                    foreach (string strength in Strengths)
                    {
                        for (int tier = 1; tier <= 5; tier++)
                        {
                            string fileName = $"{statName}_{kindId}_{strength}_T{tier}.asset";
                            string path = Path.Combine(folder, fileName);
                            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null)
                                continue;

                            var affix = CreateAffix(stat, modType, kind, strength, tier, genType);
                            AssetDatabase.CreateAsset(affix, path);
                            affix.UniqueID = path.Replace("Assets/", string.Empty).Replace(".asset", string.Empty).Replace('\\', '/');
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

            if (affix.Stats == null || affix.Stats.Length == 0)
                return;

            EnsureValueUnitLocalizations(menuLabels);
            var stat = affix.Stats[0].Stat;
            var kind = StatPresentation.FromStatModType(affix.Stats[0].Type);
            string strength = ParseStrengthFromGroupId(affix.GroupID);

            string nameKey = string.IsNullOrEmpty(affix.NameKey) ? "affix_name_" + SanitizeKey(affix.name) : affix.NameKey;
            string valueKey = string.IsNullOrEmpty(affix.TranslationKey) ? BuildValueKey(stat, kind) : affix.TranslationKey;

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
            if (affix == null || affixesLabels == null || affix.Stats == null || affix.Stats.Length == 0)
                return;

            EnsureValueUnitLocalizations(menuLabels);
            var stat = affix.Stats[0].Stat;
            var kind = StatPresentation.FromStatModType(affix.Stats[0].Type);
            string strength = ParseStrengthFromGroupId(affix.GroupID);

            WriteNameLocalization(affix, stat, kind, strength, menuLabels, affixesLabels);
            WriteValueLocalization(affix, stat, kind, menuLabels, affixesLabels, Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase));
            EditorUtility.SetDirty(affix);
        }

        public static string GetValueKey(StatType stat, StatAffixModifierKind kind)
        {
            return BuildValueKey(stat, kind);
        }

        public static string GetTypeDisplayName(StatModType type)
        {
            return StatPresentation.GetModifierKindDisplayName(StatPresentation.FromStatModType(type));
        }

        private static bool IsKindAllowedForGenType(StatAffixModifierKind kind, StatAffixGenType genType)
        {
            switch (genType)
            {
                case StatAffixGenType.NOCalcStat:
                    return kind == StatAffixModifierKind.Flat;
                case StatAffixGenType.PercentStat:
                    return kind == StatAffixModifierKind.Flat || kind == StatAffixModifierKind.Increase || kind == StatAffixModifierKind.Decrease;
                default:
                    return true;
            }
        }

        private static ItemAffixSO CreateAffix(StatType stat, StatModType modType, StatAffixModifierKind kind, string strength, int tier, StatAffixGenType genType)
        {
            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            string kindId = StatPresentation.GetModifierKindDisplayName(kind);
            affix.GroupID = $"{stat}_{kindId}_{strength}";
            affix.Tier = tier;
            affix.TranslationKey = BuildValueKey(stat, kind);
            affix.NameKey = $"affix_name_{stat.ToString().ToLowerInvariant()}_{StatPresentation.GetModifierKindId(kind)}_{strength.ToLowerInvariant()}_t{tier}";
            affix.Stats = new ItemAffixSO.AffixStatData[1];
            affix.Stats[0].Stat = stat;
            affix.Stats[0].Type = modType;
            affix.Stats[0].Scope = StatScope.Global;

            if (genType == StatAffixGenType.FullCalcStat)
                SetValuesFullCalc(ref affix.Stats[0], stat, kind, tier, strength);
            else
                SetValuesSmallFlat(ref affix.Stats[0], tier, strength);

            if (affix.TagIds == null)
                affix.TagIds = new List<string>();

            return affix;
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
            string kindEn = StatPresentation.GetModifierKindDisplayName(kind).ToLowerInvariant();
            string kindRu = GetModifierKindRu(kind);
            string nameKey = string.IsNullOrEmpty(affix.NameKey) ? "affix_name_" + SanitizeKey(affix.name) : affix.NameKey;

            SetOrAddEntry(affixesLabels, "en", nameKey, $"{strength} {statNameEn} {kindEn}");
            SetOrAddEntry(affixesLabels, "ru", nameKey, $"{strengthRu} {statNameRu} {kindRu}");
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
            string valueKey = string.IsNullOrEmpty(affix.TranslationKey) ? BuildValueKey(stat, kind) : affix.TranslationKey;

            SetOrAddEntry(affixesLabels, "en", valueKey, GenerateValueTemplateEn(kind, statNameEn, unit, unitEn));
            SetOrAddEntry(affixesLabels, "ru", valueKey, GenerateValueTemplateRu(kind, statNameRu, unit, unitRu));
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

        private static string BuildValueKey(StatType stat, StatAffixModifierKind kind)
        {
            return $"affix_{StatPresentation.GetModifierKindId(kind)}_{stat.ToString().ToLowerInvariant()}";
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

        private static string GenerateValueTemplateEn(StatAffixModifierKind kind, string statName, StatValueUnit unit, string localizedUnit)
        {
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
                    if (unit == StatValueUnit.Percent)
                        return $"+{{0}}% to {statName}";

                    if (string.IsNullOrEmpty(localizedUnit))
                        return $"Adds {{0}} to {statName}";

                    return StatPresentation.IsSymbolUnit(unit)
                        ? $"+{{0}}{localizedUnit} to {statName}"
                        : $"Adds {{0}} {localizedUnit} to {statName}";
            }
        }

        private static string GenerateValueTemplateRu(StatAffixModifierKind kind, string statName, StatValueUnit unit, string localizedUnit)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return $"{{0}}% увеличение {statName}";
                case StatAffixModifierKind.Decrease:
                    return $"{{0}}% уменьшение {statName}";
                case StatAffixModifierKind.More:
                    return $"{{0}}% больше {statName}";
                case StatAffixModifierKind.Less:
                    return $"{{0}}% меньше {statName}";
                default:
                    if (unit == StatValueUnit.Percent)
                        return $"+{{0}}% к {statName}";

                    if (string.IsNullOrEmpty(localizedUnit))
                        return $"Добавляет {{0}} к {statName}";

                    return StatPresentation.IsSymbolUnit(unit)
                        ? $"+{{0}}{localizedUnit} к {statName}"
                        : $"Добавляет {{0}} {localizedUnit} к {statName}";
            }
        }

        private static string GetModifierKindRu(StatAffixModifierKind kind)
        {
            switch (kind)
            {
                case StatAffixModifierKind.Increase:
                    return "увеличение";
                case StatAffixModifierKind.Decrease:
                    return "уменьшение";
                case StatAffixModifierKind.More:
                    return "больше";
                case StatAffixModifierKind.Less:
                    return "меньше";
                default:
                    return "плоский";
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
