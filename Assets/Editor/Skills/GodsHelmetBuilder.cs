using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class GodsHelmetBuilder
    {
        public const string Root = "Assets/Resources/Items/Unique/GodsHelmet/";
        public const string ItemPath = Root + "GodsHelmet.asset";
        public const string SkillPath = Root + "GodOfStrenghtSkill.asset";
        public const string BuffPath = Root + "GodOfStrengthBuff.asset";

        [MenuItem("Tools/Items/Repair God's Helmet Localization")]
        public static void RepairLocalization()
        {
            foreach (string code in new[] { "en", "ru" })
            {
                var source = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/Affixes_{code}.asset");
                var target = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/AffixesLabels_{code}.asset");
                foreach (string id in new[] { "GodsVitality", "GodsProtection", "GodsReach" })
                    foreach (string suffix in new[] { "", ".name" })
                    {
                        string key = "affix." + id + suffix;
                        var entry = source.GetEntry(key);
                        if (entry == null) throw new System.InvalidOperationException($"Missing authored translation {key} ({code}).");
                        target.AddEntry(key, entry.LocalizedValue).IsSmart = true;
                    }
                EditorUtility.SetDirty(target); EditorUtility.SetDirty(target.SharedData);
                AssetDatabase.SaveAssetIfDirty(target); AssetDatabase.SaveAssetIfDirty(target.SharedData);
            }
        }

        [MenuItem("Tools/Items/Build God's Helmet")]
        public static void Apply()
        {
            Folder(Root.TrimEnd('/'));
            const string affixRoot = "Assets/Resources/Affixes/Unique/GodsHelmet/";
            Folder(affixRoot.TrimEnd('/'));
            var source = AssetDatabase.LoadAssetAtPath<ArmorItemSO>("Assets/Scripts/Items/Helmets/LowTierBases/ArmorType/Stout Adventure's Helmet/Stout Adventurer's Helmet.asset");
            Sprite icon = ImportIcon(Root + "GodsHelmet.png", 48);
            Sprite skillIcon = ImportIcon(Root + "GodOfStrength.png", 24);
            var life = Affix(affixRoot, "GodsVitality", "God's Vitality", "Божественная жизнь", "+{0} to maximum life", "+{0} к максимуму здоровья",
                Stat(StatType.MaxHealth, 50, 150));
            var resist = Affix(affixRoot, "GodsProtection", "God's Protection", "Божественная защита", "+{0}% to all resistances", "+{0}% ко всем сопротивлениям",
                Stat(StatType.FireResist, 15, 15), Stat(StatType.ColdResist, 15, 15), Stat(StatType.LightningResist, 15, 15), Stat(StatType.PhysicalResist, 15, 15));
            var area = Affix(affixRoot, "GodsReach", "God's Reach", "Божественный размах", "+{0}% to area of effect", "+{0}% к области действия",
                Stat(StatType.AreaOfEffect, 30, 30));
            var buff = Load<StatusEffectSO>(BuffPath);
            buff.Id = "GodOfStrengthBuff"; buff.NameEn = "God Of Strenght"; buff.NameRu = "Бог силы";
            buff.DescriptionEn = "For every full 50 maximum life, grants +10 flat physical damage and +5% area of effect. Based on maximum life when activated.";
            buff.DescriptionRu = "За каждые полные 50 максимума здоровья даёт +10 физического урона и +5% к области действия. Рассчитывается при применении.";
            buff.Kind = StatusEffectKind.Buff; buff.Icon = skillIcon; buff.ShowInHud = true;
            buff.BaseDurationSeconds = 20; buff.AuraColor = StatusAuraColor.Red;
            buff.DerivedModifiers = new List<DerivedStatModifier> { Link(StatType.DamagePhysical, 20), Link(StatType.AreaOfEffect, 10) };
            EditorUtility.SetDirty(buff);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_GodOfStrength.asset");
            var step = new StepEntry
            {
                StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_ApplyStatusSelf.asset"),
                StartPercentPipeline = 0, EndPercentPipeline = 0
            };
            step.SetOverrideObject("StatusEffect", buff);
            recipe.Steps = new List<StepEntry> { step }; recipe.HitStopFrames = 0;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "GodOfStrenghtSkill"; skill.SkillName = "God Of Strenght";
            skill.NameKey = "skills.GodOfStrenghtSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 60; skill.ManaCost = 50; skill.Icon = skillIcon; skill.WeaponRole = SkillWeaponRole.Equipment;
            skill.ActionSpeedMode = SkillActionSpeedMode.Universal; skill.SkillSpeedMultiplier = 1;
            skill.Recipe = recipe; EditorUtility.SetDirty(skill);
            var pool = Load<SkillPoolSO>(Root + "GodsHelmetSkillPool.asset");
            pool.PossibleSkills = new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 } };
            EditorUtility.SetDirty(pool);
            var item = Load<ArmorItemSO>(ItemPath);
            item.ID = "GodsHelmet"; item.ItemName = "God's Helmet"; item.Icon = icon;
            item.Width = 2; item.Height = 2; item.Slot = EquipmentSlot.Helmet; item.DropLevel = 1;
            item.DefenseType = source.DefenseType; item.BaseArmor = source.BaseArmor;
            item.IsUnique = true; item.AffixPool = null;
            item.ImplicitModifiers.Clear(); item.UniqueAffixes = new List<ItemAffixSO> { life, resist, area };
            item.SkillPool = pool; item.SkillCount = 1; EditorUtility.SetDirty(item);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            database.UniqueItemDropChance = 0.001f;
            if (!database.AllItems.Contains(item)) database.AllItems.Add(item);
            foreach (var affix in item.UniqueAffixes) if (!database.AllAffixes.Contains(affix)) database.AllAffixes.Add(affix);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            Localize("ItemsLabels", "items.GodsHelmet", "God's Helmet", "Шлем бога");
            Localize("SkillsLabels", skill.NameKey, skill.SkillName, "Бог силы");
            Localize("SkillsLabels", skill.DescriptionKey, "", "");
            AssetDatabase.SaveAssets();
        }

        private static Sprite ImportIcon(string path, int size)
        {
            var pixels = new Texture2D(2, 2);
            try
            {
                if (!pixels.LoadImage(System.IO.File.ReadAllBytes(path)) || pixels.width != size || pixels.height != size)
                    throw new System.InvalidOperationException($"{path} must be authored at exactly {size}x{size} pixels.");
            }
            finally { Object.DestroyImmediate(pixels); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.maxTextureSize = Mathf.NextPowerOfTwo(size);
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static DerivedStatModifier Link(StatType target, float percent) => new DerivedStatModifier
            { SourceStat = StatType.MaxHealth, SourcePercent = percent, SourceStep = 50, TargetStat = target, TargetModifierType = StatModType.Flat };
        private static ItemAffixSO.AffixStatData Stat(StatType type, float min, float max) => new ItemAffixSO.AffixStatData
            { Stat = type, Type = StatModType.Flat, Scope = StatScope.Global, MinValue = min, MaxValue = max };

        private static ItemAffixSO Affix(string root, string id, string en, string ru, string textEn, string textRu, params ItemAffixSO.AffixStatData[] stats)
        {
            var affix = Load<ItemAffixSO>(root + id + ".asset");
            affix.UniqueID = id; affix.GroupID = id; affix.NameKey = "affix." + id + ".name";
            affix.TranslationKey = "affix." + id; affix.LockAutoLocalization = true;
            affix.Tiers = new List<ItemAffixSO.AffixTierData> { new ItemAffixSO.AffixTierData { Tier = 5, Stats = stats } };
            Localize("Affixes", affix.NameKey, en, ru); Localize("Affixes", affix.TranslationKey, textEn, textRu);
            Localize("AffixesLabels", affix.NameKey, en, ru); Localize("AffixesLabels", affix.TranslationKey, textEn, textRu);
            EditorUtility.SetDirty(affix); return affix;
        }

        private static void Localize(string collectionName, string key, string en, string ru)
        {
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{collectionName}_{code}.asset");
                if (table == null) throw new System.InvalidOperationException($"Missing localization table {collectionName}_{code}.");
                var entry = table.AddEntry(key, code == "en" ? en : ru);
                entry.IsSmart = collectionName == "Affixes" || collectionName == "AffixesLabels";
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); Folder(path.Substring(0, split));
            AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
        }
    }
}
