using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Scripts.Editor.Items
{
    public static class UniqueItemAuthoring
    {
        public const string Root = "Assets/Resources/Items/Unique";

        public static EquipmentItemSO Create(string name, EquipmentItemSO template, bool armor,
            ItemDatabaseSO database, string root = Root)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Enter an item name.");
            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || root.Contains(".."))
                throw new ArgumentException("Unique items must be created inside Assets.");
            string id = "Unique_" + Guid.NewGuid().ToString("N");
            string folder = root.TrimEnd('/') + "/" + id;
            EnsureFolder(folder);
            var item = template != null ? Object.Instantiate(template)
                : armor ? (EquipmentItemSO)ScriptableObject.CreateInstance<ArmorItemSO>() : ScriptableObject.CreateInstance<WeaponItemSO>();
            item.ID = id; item.ItemName = name.Trim(); item.name = name.Trim();
            item.IsUnique = true; item.AffixPool = null;
            if (template == null)
            {
                item.Slot = armor ? EquipmentSlot.Helmet : EquipmentSlot.MainHand;
                item.Width = armor ? 2 : 1; item.Height = armor ? 2 : 3;
            }
            item.UniqueAffixes = new List<ItemAffixSO>();
            AssetDatabase.CreateAsset(item, folder + "/Item.asset");
            if (template != null && template.IsUnique && template.UniqueAffixes != null)
                foreach (var affix in template.UniqueAffixes)
                    if (affix != null) AddAffix(item, database, affix);
            item.SkillPool = CopyPool(template != null && template.IsUnique ? template.SkillPool : null, folder + "/Skills.asset");
            if (template == null || !template.IsUnique) item.SkillCount = 0;
            if (item is WeaponItemSO weapon)
                weapon.SecondarySkillPool = CopyPool(template is WeaponItemSO source && source.IsUnique ? source.SecondarySkillPool : null, folder + "/SecondarySkills.asset");
            if (database != null)
            {
                Undo.RecordObject(database, "Register unique item");
                if (database.AllItems == null) database.AllItems = new List<EquipmentItemSO>();
                database.AllItems.Add(item); EditorUtility.SetDirty(database);
                database.AllSkills ??= new List<SkillDataSO>();
                var pools = new[] { item.SkillPool, item is WeaponItemSO w ? w.SecondarySkillPool : null };
                foreach (var skill in pools.Where(p => p?.PossibleSkills != null).SelectMany(p => p.PossibleSkills).Select(s => s.Skill).Where(s => s != null))
                    if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            }
            EditorUtility.SetDirty(item);
            return item;
        }

        private static SkillPoolSO CopyPool(SkillPoolSO source, string path)
        {
            var pool = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<SkillPoolSO>();
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            AssetDatabase.CreateAsset(pool, path);
            return pool;
        }

        public static ItemAffixSO AddAffix(EquipmentItemSO item, ItemDatabaseSO database, ItemAffixSO source = null)
        {
            RequireUnique(item);
            var affix = source != null ? Object.Instantiate(source) : ScriptableObject.CreateInstance<ItemAffixSO>();
            affix.UniqueID = "UniqueAffix_" + Guid.NewGuid().ToString("N");
            affix.GroupID = affix.UniqueID; affix.NameKey = "affix." + affix.UniqueID + ".name";
            affix.TranslationKey = "affix." + affix.UniqueID; affix.LockAutoLocalization = true;
            affix.LegacyTierIds = new List<ItemAffixSO.LegacyTierId>();
            if (source != null && !source.UsesEmbeddedTiers)
                affix.Tiers = new List<ItemAffixSO.AffixTierData> { new ItemAffixSO.AffixTierData
                { Tier = 1, Stats = source.GetStatsForTier(source.GetDefaultTier()).ToArray() } };
            if (source == null)
                affix.Tiers = new List<ItemAffixSO.AffixTierData> { new ItemAffixSO.AffixTierData
                {
                    Tier = 1, Stats = new[] { new ItemAffixSO.AffixStatData { Stat = StatType.MaxHealth,
                        Type = StatModType.Flat, Scope = StatScope.Global, MinValue = 10, MaxValue = 20 } }
                } };
            string path = AssetDatabase.GenerateUniqueAssetPath(FolderOf(item) + "/Affix.asset");
            affix.name = source != null ? source.name : "New unique affix";
            AssetDatabase.CreateAsset(affix, path);
            Undo.RecordObject(item, "Add unique affix"); item.UniqueAffixes ??= new List<ItemAffixSO>();
            item.UniqueAffixes.Add(affix); EditorUtility.SetDirty(item);
            if (database != null)
            {
                Undo.RecordObject(database, "Register unique affix");
                database.AllAffixes ??= new List<ItemAffixSO>(); database.AllAffixes.Add(affix);
                EditorUtility.SetDirty(database);
            }
            return affix;
        }

        public static void SetSkill(EquipmentItemSO item, SkillDataSO skill, ItemDatabaseSO database, bool secondary = false)
        {
            RequireUnique(item);
            if (secondary && !(item is WeaponItemSO)) throw new ArgumentException("Secondary skills require a weapon.");
            var pool = secondary ? ((WeaponItemSO)item).SecondarySkillPool : item.SkillPool;
            string folder = FolderOf(item);
            // Never rewrite a shared pool when changing this item's guaranteed skill.
            bool shared = pool != null && AssetDatabase.FindAssets("t:EquipmentItemSO")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<EquipmentItemSO>)
                .Any(other => other != null && other != item && (other.SkillPool == pool || other is WeaponItemSO w && w.SecondarySkillPool == pool));
            Undo.RecordObject(item, "Set unique skill");
            if (pool == null || shared || !AssetDatabase.GetAssetPath(pool).StartsWith(folder + "/", StringComparison.Ordinal))
            {
                pool = CopyPool(null, AssetDatabase.GenerateUniqueAssetPath(folder + (secondary ? "/SecondarySkills.asset" : "/Skills.asset")));
                if (secondary) ((WeaponItemSO)item).SecondarySkillPool = pool; else item.SkillPool = pool;
            }
            Undo.RecordObject(pool, "Set unique skill");
            pool.PossibleSkills = skill == null ? new List<SkillPoolSO.SkillWeight>()
                : new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 } };
            if (!secondary) item.SkillCount = skill != null ? 1 : 0;
            EditorUtility.SetDirty(pool); EditorUtility.SetDirty(item);
            if (skill != null && database != null)
            {
                database.AllSkills ??= new List<SkillDataSO>();
                if (!database.AllSkills.Contains(skill)) { Undo.RecordObject(database, "Register unique skill"); database.AllSkills.Add(skill); EditorUtility.SetDirty(database); }
            }
        }

        public static string ReadText(string table, string key, string locale)
        {
            return LoadTable(table, locale)?.GetEntry(key)?.LocalizedValue ?? "";
        }

        public static void WriteText(string table, string key, string en, string ru, bool smart = false)
        {
            foreach (string locale in new[] { "en", "ru" })
            {
                var target = LoadTable(table, locale);
                if (target == null) throw new InvalidOperationException($"Missing {table}_{locale} localization table.");
                Undo.RecordObjects(new Object[] { target, target.SharedData }, "Edit unique localization");
                target.AddEntry(key, locale == "en" ? en : ru).IsSmart = smart;
                EditorUtility.SetDirty(target); EditorUtility.SetDirty(target.SharedData);
            }
        }

        public static void CopyAffixText(ItemAffixSO source, ItemAffixSO target)
        {
            foreach (string suffix in new[] { "", ".name" })
            {
                string oldKey = suffix == "" ? source.GetResolvedTranslationKey() : source.NameKey;
                string key = suffix == "" ? target.TranslationKey : target.NameKey;
                string en = ReadText("AffixesLabels", oldKey, "en"), ru = ReadText("AffixesLabels", oldKey, "ru");
                if (string.IsNullOrEmpty(en) && string.IsNullOrEmpty(ru)) continue;
                WriteText("AffixesLabels", key, en, ru, true); WriteText("Affixes", key, en, ru, true);
            }
        }

        public static bool Delete(EquipmentItemSO item, ItemDatabaseSO database)
        {
            RequireUnique(item);
            string path = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrEmpty(path)) return false;
            int index = database?.AllItems?.IndexOf(item) ?? -1;
            if (index >= 0) database.AllItems.RemoveAt(index);
            if (!AssetDatabase.MoveAssetToTrash(path))
            {
                if (index >= 0) database.AllItems.Insert(index, item);
                return false;
            }
            if (database != null) { EditorUtility.SetDirty(database); AssetDatabase.SaveAssetIfDirty(database); }
            return true;
        }

        private static StringTable LoadTable(string table, string locale) => AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{table}_{locale}.asset");
        private static string FolderOf(EquipmentItemSO item) => System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(item)).Replace('\\', '/');
        private static void RequireUnique(EquipmentItemSO item)
        {
            if (item == null || !item.IsUnique) throw new ArgumentException("Select a Unique item.");
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
