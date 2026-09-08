using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scripts.Items.Affixes;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Scripts.Editor.Affixes
{
    public static class AffixTierMigrationTool
    {
        private const string MenuRoot = "Tools/Items/Affix Tier Migration/";

        [MenuItem(MenuRoot + "Validate (dry run)")]
        public static void ValidateFromMenu()
        {
            MigrationPlan plan = BuildPlan();
            Debug.Log(plan.BuildReport());
            EditorUtility.DisplayDialog(
                "Affix tier migration",
                plan.IsValid ? plan.BuildReport() : "Migration is blocked. See Console for details.",
                "OK");
        }

        [MenuItem(MenuRoot + "Migrate now")]
        public static void MigrateFromMenu()
        {
            MigrationPlan plan = BuildPlan();
            if (!plan.IsValid)
            {
                Debug.LogError(plan.BuildReport());
                EditorUtility.DisplayDialog("Affix tier migration", "Migration is blocked. See Console for details.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Migrate affix tiers",
                    $"Convert {plan.LegacyAssetCount} assets into {plan.Groups.Count} tiered assets and delete the redundant tier assets?\n\nGit can restore the deleted files.",
                    "Migrate",
                    "Cancel"))
                return;

            Execute(plan);
        }

        public static void RunMigrationFromCommandLine()
        {
            MigrationPlan plan = BuildPlan();
            if (!plan.IsValid)
                throw new InvalidOperationException(plan.BuildReport());
            Execute(plan);
        }

        private static MigrationPlan BuildPlan()
        {
            var plan = new MigrationPlan();
            var allAffixes = LoadAll<ItemAffixSO>();
            plan.AlreadyMigratedCount = allAffixes.Count(affix => affix.UsesEmbeddedTiers);
            List<ItemAffixSO> legacy = allAffixes.Where(affix => !affix.UsesEmbeddedTiers).ToList();
            plan.LegacyAssetCount = legacy.Count;

            foreach (IGrouping<string, ItemAffixSO> grouping in legacy.GroupBy(affix => affix.GroupID ?? string.Empty))
            {
                string groupId = grouping.Key.Trim();
                List<ItemAffixSO> members = grouping.ToList();
                if (string.IsNullOrEmpty(groupId))
                {
                    plan.Errors.Add($"{members.Count} affix asset(s) have an empty GroupID.");
                    continue;
                }

                List<int> tiers = members.Select(member => member.Tier).OrderBy(tier => tier).ToList();
                if (members.Count != 5 || !tiers.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
                {
                    plan.Errors.Add($"{groupId}: expected exactly T1-T5, found [{string.Join(", ", tiers)}].");
                    continue;
                }

                ItemAffixSO canonical = members.Single(member => member.Tier == 5);
                if (!HaveCompatibleStatShapes(members, out string compatibilityError))
                {
                    plan.Errors.Add($"{groupId}: {compatibilityError}");
                    continue;
                }

                plan.Groups.Add(new MigrationGroup(groupId, canonical, members.OrderBy(member => member.Tier).ToList()));
            }

            var migratedGroupIds = new HashSet<string>(
                allAffixes.Where(affix => affix.UsesEmbeddedTiers).Select(affix => affix.GroupID),
                StringComparer.OrdinalIgnoreCase);
            foreach (MigrationGroup group in plan.Groups)
            {
                if (migratedGroupIds.Contains(group.GroupId))
                    plan.Errors.Add($"{group.GroupId}: both a migrated asset and legacy tier assets exist.");
            }

            return plan;
        }

        private static bool HaveCompatibleStatShapes(IReadOnlyList<ItemAffixSO> members, out string error)
        {
            error = null;
            ItemAffixSO first = members[0];
            if (first.Stats == null || first.Stats.Length == 0)
            {
                error = "tier has no stat data";
                return false;
            }

            foreach (ItemAffixSO member in members.Skip(1))
            {
                if (member.Stats == null || member.Stats.Length != first.Stats.Length)
                {
                    error = "tiers have different stat counts";
                    return false;
                }

                for (int i = 0; i < first.Stats.Length; i++)
                {
                    var expected = first.Stats[i];
                    var actual = member.Stats[i];
                    if (expected.Stat != actual.Stat || expected.Type != actual.Type ||
                        expected.Scope != actual.Scope || expected.GetEffectiveValueMode() != actual.GetEffectiveValueMode())
                    {
                        error = $"T{member.Tier} stat shape differs at index {i}";
                        return false;
                    }
                }
            }

            return true;
        }

        private static void Execute(MigrationPlan plan)
        {
            if (plan.Groups.Count == 0)
            {
                Debug.Log($"[Affix Tier Migration] Nothing to migrate. Tiered assets: {plan.AlreadyMigratedCount}.");
                return;
            }

            var replacementByLegacy = new Dictionary<ItemAffixSO, ItemAffixSO>();
            StringTableCollection affixLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable);

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (MigrationGroup group in plan.Groups)
                {
                    ItemAffixSO canonical = group.Canonical;
                    string oldNameKey = canonical.NameKey;
                    string newNameKey = Regex.Replace(oldNameKey ?? string.Empty, "_t[1-5]$", string.Empty, RegexOptions.IgnoreCase);

                    canonical.Tiers = group.Members
                        .Select(member => new ItemAffixSO.AffixTierData
                        {
                            Tier = member.Tier,
                            Stats = member.Stats != null
                                ? (ItemAffixSO.AffixStatData[])member.Stats.Clone()
                                : Array.Empty<ItemAffixSO.AffixStatData>()
                        })
                        .ToList();
                    canonical.LegacyTierIds = BuildLegacyIds(group.Members);
                    canonical.UniqueID = group.GroupId;
                    canonical.NameKey = string.IsNullOrEmpty(newNameKey) ? oldNameKey : newNameKey;
                    canonical.Tier = 0;
                    canonical.Stats = Array.Empty<ItemAffixSO.AffixStatData>();
                    CopyLocalization(affixLabels, oldNameKey, canonical.NameKey);
                    EditorUtility.SetDirty(canonical);

                    foreach (ItemAffixSO member in group.Members)
                        replacementByLegacy[member] = canonical;
                }

                ReplacePoolReferences(replacementByLegacy);
                ReplaceDatabaseReferences(replacementByLegacy);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();

            int deleted = 0;
            foreach (MigrationGroup group in plan.Groups)
            {
                foreach (ItemAffixSO obsolete in group.Members)
                {
                    if (obsolete == group.Canonical) continue;
                    string obsoletePath = AssetDatabase.GetAssetPath(obsolete);
                    if (!string.IsNullOrEmpty(obsoletePath) && AssetDatabase.DeleteAsset(obsoletePath))
                        deleted++;
                }
            }

            int renamed = 0;
            foreach (MigrationGroup group in plan.Groups)
            {
                string path = AssetDatabase.GetAssetPath(group.Canonical);
                if (string.IsNullOrEmpty(path)) continue;
                string error = AssetDatabase.RenameAsset(path, SanitizeFileName(group.GroupId));
                if (string.IsNullOrEmpty(error))
                    renamed++;
                else
                    Debug.LogError($"[Affix Tier Migration] Could not rename '{path}': {error}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[Affix Tier Migration] Complete. Groups: {plan.Groups.Count}, redundant assets deleted: {deleted}, " +
                $"assets renamed: {renamed}, expected remaining affixes: {plan.Groups.Count + plan.AlreadyMigratedCount}.");
        }

        private static List<ItemAffixSO.LegacyTierId> BuildLegacyIds(IEnumerable<ItemAffixSO> members)
        {
            var result = new List<ItemAffixSO.LegacyTierId>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemAffixSO member in members)
            {
                AddLegacyId(result, seen, member.UniqueID, member.Tier);
                AddLegacyId(result, seen, member.name, member.Tier);
            }
            return result;
        }

        private static void AddLegacyId(List<ItemAffixSO.LegacyTierId> result, HashSet<string> seen, string id, int tier)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id.Trim())) return;
            result.Add(new ItemAffixSO.LegacyTierId { Id = id.Trim(), Tier = tier });
        }

        private static void ReplacePoolReferences(IReadOnlyDictionary<ItemAffixSO, ItemAffixSO> replacements)
        {
            foreach (AffixPoolSO pool in LoadAll<AffixPoolSO>())
            {
                if (pool.Affixes == null) continue;
                var updated = new List<ItemAffixSO>();
                foreach (ItemAffixSO oldAffix in pool.Affixes)
                {
                    ItemAffixSO replacement = oldAffix != null && replacements.TryGetValue(oldAffix, out ItemAffixSO mapped)
                        ? mapped
                        : oldAffix;
                    if (replacement != null && !updated.Contains(replacement))
                        updated.Add(replacement);
                }
                pool.Affixes = updated;
                EditorUtility.SetDirty(pool);
            }
        }

        private static void ReplaceDatabaseReferences(IReadOnlyDictionary<ItemAffixSO, ItemAffixSO> replacements)
        {
            foreach (ItemDatabaseSO database in LoadAll<ItemDatabaseSO>())
            {
                if (database.AllAffixes == null) continue;
                var updated = new List<ItemAffixSO>();
                foreach (ItemAffixSO oldAffix in database.AllAffixes)
                {
                    ItemAffixSO replacement = oldAffix != null && replacements.TryGetValue(oldAffix, out ItemAffixSO mapped)
                        ? mapped
                        : oldAffix;
                    if (replacement != null && !updated.Contains(replacement))
                        updated.Add(replacement);
                }
                database.AllAffixes = updated;
                EditorUtility.SetDirty(database);
            }
        }

        private static void CopyLocalization(StringTableCollection collection, string sourceKey, string destinationKey)
        {
            if (collection == null || string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(destinationKey) || sourceKey == destinationKey)
                return;

            foreach (string locale in new[] { "en", "ru" })
            {
                StringTable table = collection.GetTable(locale) as StringTable ??
                                    collection.GetTable(new LocaleIdentifier(locale)) as StringTable;
                if (table == null) continue;
                string value = table.GetEntry(sourceKey)?.Value;
                if (string.IsNullOrEmpty(value)) continue;

                if (collection.SharedData != null && !collection.SharedData.Contains(destinationKey))
                {
                    collection.SharedData.AddKey(destinationKey);
                    EditorUtility.SetDirty(collection.SharedData);
                }

                var destination = table.GetEntry(destinationKey);
                if (destination != null)
                    destination.Value = value;
                else
                    table.AddEntry(destinationKey, value);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection);
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return value;
        }

        private sealed class MigrationGroup
        {
            public string GroupId { get; }
            public ItemAffixSO Canonical { get; }
            public IReadOnlyList<ItemAffixSO> Members { get; }

            public MigrationGroup(string groupId, ItemAffixSO canonical, IReadOnlyList<ItemAffixSO> members)
            {
                GroupId = groupId;
                Canonical = canonical;
                Members = members;
            }
        }

        private sealed class MigrationPlan
        {
            public int LegacyAssetCount;
            public int AlreadyMigratedCount;
            public readonly List<MigrationGroup> Groups = new List<MigrationGroup>();
            public readonly List<string> Errors = new List<string>();
            public bool IsValid => Errors.Count == 0;

            public string BuildReport()
            {
                string summary =
                    $"[Affix Tier Migration] Legacy assets: {LegacyAssetCount}; valid groups: {Groups.Count}; " +
                    $"already migrated: {AlreadyMigratedCount}; errors: {Errors.Count}.";
                return Errors.Count == 0 ? summary : summary + "\n" + string.Join("\n", Errors.Take(50));
            }
        }
    }
}
