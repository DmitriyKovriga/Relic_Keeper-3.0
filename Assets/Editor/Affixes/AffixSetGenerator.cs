using UnityEngine;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;

namespace Scripts.Editor.Affixes
{
    /// <summary>
    /// Генерация наборов аффиксов по StatAffixGenType: FullCalcStat (flat+increase+more × strong/medium/light × T1-5),
    /// PercentStat/NOCalcStat (только flat × strong/medium/light × T1-5, значения 1-7).
    /// </summary>
    public static class AffixSetGenerator
    {
        private const string StrengthStrong = "Strong";
        private const string StrengthMedium = "Medium";
        private const string StrengthLight = "Light";
        private static readonly string[] ModTypeSuffixes = { "Flat", "Increase", "More" };
        private static readonly string[] Strengths = { StrengthStrong, StrengthMedium, StrengthLight };

        public static int DeleteAllAffixes(List<AffixPoolSO> pools)
        {
            foreach (var pool in pools)
            {
                if (pool.Affixes != null && pool.Affixes.Count > 0)
                {
                    pool.Affixes.Clear();
                    EditorUtility.SetDirty(pool);
                }
            }
            string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO");
            int removed = 0;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                AssetDatabase.DeleteAsset(path);
                removed++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return removed;
        }

        /// <summary> Статы, для которых ещё нет ни одного аффикса (по Stats[0].Stat). </summary>
        public static HashSet<StatType> GetStatsWithoutAffixSet(List<ItemAffixSO> allAffixes)
        {
            var withSet = new HashSet<StatType>();
            foreach (var a in allAffixes)
            {
                if (a?.Stats != null && a.Stats.Length > 0)
                    withSet.Add(a.Stats[0].Stat);
            }
            var result = new HashSet<StatType>();
            foreach (StatType t in Enum.GetValues(typeof(StatType)))
                if (!withSet.Contains(t)) result.Add(t);
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
            if (statsToGenerate == null || statsToGenerate.Count == 0) return 0;
            if (statsDb == null) return 0;
            int created = 0;
            foreach (StatType stat in statsToGenerate)
            {
                StatAffixGenType genType = statsDb.GetAffixGenType(stat);
                string category = GetStatCategory(statsDb, stat);
                string statName = stat.ToString();
                string folder = $"{affixesBaseFolder}/ByStat/{category}/{statName}";
                EnsureFolder(affixesBaseFolder + "/ByStat");
                EnsureFolder(affixesBaseFolder + "/ByStat/" + category);
                EnsureFolder(folder);

                if (genType == StatAffixGenType.FullCalcStat)
                {
                    foreach (string modSuffix in ModTypeSuffixes)
                    {
                        StatModType modType = modSuffix == "Flat" ? StatModType.Flat : modSuffix == "Increase" ? StatModType.PercentAdd : StatModType.PercentMult;
                        foreach (string strength in Strengths)
                        {
                            for (int tier = 1; tier <= 5; tier++)
                            {
                                var affix = CreateAffix(stat, modType, strength, tier, genType);
                                string fileName = $"{statName}_{modSuffix}_{strength}_T{tier}.asset";
                                string path = Path.Combine(folder, fileName);
                                if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null) continue;
                                AssetDatabase.CreateAsset(affix, path);
                                affix.UniqueID = path.Replace("Assets/", "").Replace(".asset", "").Replace('\\', '/');
                                WriteLocalization(affix, stat, modSuffix, strength, tier, genType, menuLabels, affixesLabels);
                                SyncTagFromCategory(affix, statsDb, stat, tagDatabase);
                                EditorUtility.SetDirty(affix);
                                created++;
                            }
                        }
                    }
                }
                else
                {
                    foreach (string strength in Strengths)
                    {
                        for (int tier = 1; tier <= 5; tier++)
                        {
                            var affix = CreateAffix(stat, StatModType.Flat, strength, tier, genType);
                            string fileName = $"{statName}_Flat_{strength}_T{tier}.asset";
                            string path = Path.Combine(folder, fileName);
                            if (AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path) != null) continue;
                            AssetDatabase.CreateAsset(affix, path);
                            affix.UniqueID = path.Replace("Assets/", "").Replace(".asset", "").Replace('\\', '/');
                            WriteLocalization(affix, stat, "Flat", strength, tier, genType, menuLabels, affixesLabels);
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

        private static string GetStatCategory(StatsDatabaseSO db, StatType type)
        {
            return db != null ? db.GetCategory(type) : "Misc";
        }

        private static ItemAffixSO CreateAffix(StatType stat, StatModType modType, string strength, int tier, StatAffixGenType genType)
        {
            var affix = ScriptableObject.CreateInstance<ItemAffixSO>();
            string modSuffix = modType == StatModType.Flat ? "Flat" : modType == StatModType.PercentAdd ? "Increase" : "More";
            affix.GroupID = $"{stat}_{modSuffix}_{strength}";
            affix.Tier = tier;
            affix.TranslationKey = $"affix_{modSuffix.ToLowerInvariant()}_{stat.ToString().ToLowerInvariant()}";
            affix.NameKey = $"affix_name_{stat.ToString().ToLowerInvariant()}_{modSuffix.ToLowerInvariant()}_{strength.ToLowerInvariant()}_t{tier}";
            affix.Stats = new ItemAffixSO.AffixStatData[1];
            affix.Stats[0].Stat = stat;
            affix.Stats[0].Type = modType;
            affix.Stats[0].Scope = StatScope.Global;

            if (genType == StatAffixGenType.FullCalcStat)
                SetValuesFullCalc(ref affix.Stats[0], stat, modType, tier, strength);
            else
                SetValuesSmallFlat(ref affix.Stats[0], tier, strength);

            if (affix.TagIds == null) affix.TagIds = new List<string>();
            return affix;
        }

        private static void SetValuesFullCalc(ref ItemAffixSO.AffixStatData data, StatType stat, StatModType type, int tier, string strength)
        {
            int stepIndex = 5 - tier;
            float mult = (stat.ToString().Contains("Health") || stat.ToString().Contains("Mana")) ? 5f : 1f;

            float stepMin, stepMax, baseMin, baseMax;
            if (strength == StrengthStrong) { baseMin = 5f; baseMax = 10f; stepMin = 5f; stepMax = 5f; }
            else if (strength == StrengthMedium) { baseMin = 4f; baseMax = 8f; stepMin = 4f; stepMax = 4f; }
            else { baseMin = 3f; baseMax = 7f; stepMin = 3f; stepMax = 3f; }

            if (type == StatModType.Flat)
            {
                data.MinValue = (baseMin + stepIndex * stepMin) * mult;
                data.MaxValue = (baseMax + stepIndex * stepMax) * mult;
            }
            else if (type == StatModType.PercentAdd)
            {
                if (strength == StrengthStrong) { baseMin = 5f; baseMax = 10f; stepMin = 5f; stepMax = 5f; }
                else if (strength == StrengthMedium) { baseMin = 4f; baseMax = 8f; stepMin = 4f; stepMax = 4f; }
                else { baseMin = 3f; baseMax = 7f; stepMin = 3f; stepMax = 3f; }
                data.MinValue = baseMin + stepIndex * stepMin;
                data.MaxValue = baseMax + stepIndex * stepMax;
            }
            else
            {
                if (strength == StrengthStrong) { baseMin = 2f; baseMax = 5f; stepMin = 2f; stepMax = 2f; }
                else if (strength == StrengthMedium) { baseMin = 1.5f; baseMax = 4f; stepMin = 1.5f; stepMax = 1.5f; }
                else { baseMin = 1f; baseMax = 3f; stepMin = 1f; stepMax = 1f; }
                data.MinValue = baseMin + stepIndex * stepMin;
                data.MaxValue = baseMax + stepIndex * stepMax;
            }
        }

        private static void SetValuesSmallFlat(ref ItemAffixSO.AffixStatData data, int tier, string strength)
        {
            // T1 = сильнее, T5 = слабее. Значения 1–7, пик Strong = 7.
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
                data.MinValue = 1;
                data.MaxValue = Mathf.Clamp(4 - tier, 1, 3);
            }
        }

        private static void SyncTagFromCategory(ItemAffixSO affix, StatsDatabaseSO db, StatType stat, AffixTagDatabaseSO tagDb)
        {
            if (affix.TagIds == null) affix.TagIds = new List<string>();
            string cat = db != null ? db.GetCategory(stat) : "Misc";
            if (string.IsNullOrEmpty(cat)) return;
            if (!affix.TagIds.Contains(cat)) affix.TagIds.Add(cat);
            if (tagDb != null && !tagDb.HasTag(cat))
            {
                tagDb.AddTag(cat, "tag_" + cat.ToLowerInvariant());
                EditorUtility.SetDirty(tagDb);
            }
        }

        private static void WriteLocalization(ItemAffixSO affix, StatType stat, string modSuffix, string strength, int tier,
            StatAffixGenType genType, StringTableCollection menuLabels, StringTableCollection affixesLabels)
        {
            string statKey = "stats." + stat;
            string statNameEn = GetLocalizedString(menuLabels, "en", statKey);
            string statNameRu = GetLocalizedString(menuLabels, "ru", statKey);
            if (string.IsNullOrEmpty(statNameEn)) statNameEn = stat.ToString();
            if (string.IsNullOrEmpty(statNameRu)) statNameRu = stat.ToString();

            string strengthEn = strength;
            string strengthRu = strength == StrengthStrong ? "Сильный" : strength == StrengthMedium ? "Средний" : "Лёгкий";
            string typeEn = modSuffix == "Flat" ? "flat" : modSuffix == "Increase" ? "increase" : "more";
            string typeRu = modSuffix == "Flat" ? "flat" : modSuffix == "Increase" ? "увеличение" : "больше";

            string nameEn = $"{strengthEn} {statNameEn} {typeEn}";
            string nameRu = $"{strengthRu} {statNameRu} {typeRu}";

            SetOrAddEntry(affixesLabels, "en", affix.NameKey, nameEn);
            SetOrAddEntry(affixesLabels, "ru", affix.NameKey, nameRu);

            string rawStat = stat.ToString();
            string valueEn = GenerateValueTemplateEn(modSuffix, rawStat, statNameEn);
            string valueRu = GenerateValueTemplateRu(modSuffix, rawStat, statNameRu);
            SetOrAddEntry(affixesLabels, "en", affix.TranslationKey, valueEn);
            SetOrAddEntry(affixesLabels, "ru", affix.TranslationKey, valueRu);
        }

        private static string GetLocalizedString(StringTableCollection col, string locale, string key)
        {
            if (col == null || string.IsNullOrEmpty(key)) return "";
            var table = col.GetTable(locale) as StringTable;
            if (table == null) return "";
            var entry = table.GetEntry(key);
            return entry?.Value ?? "";
        }

        private static void SetOrAddEntry(StringTableCollection col, string locale, string key, string value)
        {
            if (col == null || string.IsNullOrEmpty(key)) return;
            var table = col.GetTable(locale) as StringTable;
            if (table == null) return;
            var entry = table.GetEntry(key);
            if (entry != null) entry.Value = value;
            else table.AddEntry(key, value);
            EditorUtility.SetDirty(table);
        }

        private static bool IsPercentageStat(string raw)
        {
            return raw.Contains("Resist") || raw.Contains("Multiplier") || raw.Contains("Chance") || raw.Contains("Mitigation") || raw.Contains("Mult") || raw.Contains("Percent");
        }

        private static string GenerateValueTemplateEn(string modType, string rawStat, string statName)
        {
            if (modType == "Flat" && !IsPercentageStat(rawStat)) return $"Adds {{0}} to {statName}";
            if (modType == "Flat" && IsPercentageStat(rawStat)) return $"+{{0}}% to {statName}";
            if (modType == "Increase") return $"{{0}}% increased {statName}";
            if (modType == "More") return $"{{0}}% more {statName}";
            return $"+{{0}} {statName}";
        }

        private static string GenerateValueTemplateRu(string modType, string rawStat, string statName)
        {
            if (modType == "Flat" && !IsPercentageStat(rawStat)) return $"Добавляет {{0}} к {statName}";
            if (modType == "Flat" && IsPercentageStat(rawStat)) return $"+{{0}}% к {statName}";
            if (modType == "Increase") return $"{{0}}% увеличение {statName}";
            if (modType == "More") return $"{{0}}% больше {statName}";
            return $"+{{0}} {statName}";
        }

        /// <summary> Заполняет имя и value text в таблице, если они пустые. Использует тот же формат, что и при генерации (strength + stat + type). </summary>
        public static void FillMissingLocalization(ItemAffixSO affix, StringTableCollection menuLabels, StringTableCollection affixesLabels)
        {
            if (affix == null || affixesLabels == null) return;
            if (affix.Stats == null || affix.Stats.Length == 0) return;

            var s = affix.Stats[0];
            StatType stat = s.Stat;
            string modSuffix = s.Type == StatModType.PercentAdd ? "Increase" : s.Type == StatModType.PercentMult ? "More" : "Flat";
            string strength = ParseStrengthFromGroupId(affix.GroupID);

            string statKey = "stats." + stat;
            string statNameEn = GetLocalizedString(menuLabels, "en", statKey);
            string statNameRu = GetLocalizedString(menuLabels, "ru", statKey);
            if (string.IsNullOrEmpty(statNameEn)) statNameEn = stat.ToString();
            if (string.IsNullOrEmpty(statNameRu)) statNameRu = stat.ToString();

            string nameKey = string.IsNullOrEmpty(affix.NameKey) ? "affix_name_" + SanitizeKey(affix.name) : affix.NameKey;
            string nameEnExisting = GetLocalizedString(affixesLabels, "en", nameKey);
            if (string.IsNullOrWhiteSpace(nameEnExisting))
            {
                string strengthRu = strength == StrengthStrong ? "Сильный" : strength == StrengthMedium ? "Средний" : "Лёгкий";
                string typeEn = modSuffix == "Flat" ? "flat" : modSuffix == "Increase" ? "increase" : "more";
                string typeRu = modSuffix == "Flat" ? "flat" : modSuffix == "Increase" ? "увеличение" : "больше";
                string nameEn = $"{strength} {statNameEn} {typeEn}";
                string nameRu = $"{strengthRu} {statNameRu} {typeRu}";
                SetOrAddEntry(affixesLabels, "en", nameKey, nameEn);
                SetOrAddEntry(affixesLabels, "ru", nameKey, nameRu);
                if (string.IsNullOrEmpty(affix.NameKey)) affix.NameKey = nameKey;
            }

            string valueKey = string.IsNullOrEmpty(affix.TranslationKey) ? "affix_" + modSuffix.ToLowerInvariant() + "_" + stat.ToString().ToLowerInvariant() : affix.TranslationKey;
            string valueEnExisting = GetLocalizedString(affixesLabels, "en", valueKey);
            if (string.IsNullOrWhiteSpace(valueEnExisting))
            {
                string rawStat = stat.ToString();
                string valueEn = GenerateValueTemplateEn(modSuffix, rawStat, statNameEn);
                string valueRu = GenerateValueTemplateRu(modSuffix, rawStat, statNameRu);
                SetOrAddEntry(affixesLabels, "en", valueKey, valueEn);
                SetOrAddEntry(affixesLabels, "ru", valueKey, valueRu);
                if (string.IsNullOrEmpty(affix.TranslationKey)) affix.TranslationKey = valueKey;
            }
        }

        private static string ParseStrengthFromGroupId(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return StrengthMedium;
            var parts = groupId.Split('_');
            if (parts.Length >= 3)
            {
                string last = parts[parts.Length - 1];
                if (last == StrengthStrong || last == StrengthMedium || last == StrengthLight) return last;
            }
            return StrengthMedium;
        }

        private static string SanitizeKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var arr = s.ToCharArray();
            for (int i = 0; i < arr.Length; i++)
                if (!char.IsLetterOrDigit(arr[i])) arr[i] = '_';
            return new string(arr).ToLowerInvariant();
        }
    }
}
