using System.Collections.Generic;
using System.Linq;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class PlasmaFieldBuilder
    {
        public const string Root = "Assets/Resources/Skills/Helmet/Mystic/";
        public const string SkillPath = Root + "PlasmaFieldSkill.asset";

        [MenuItem("Tools/Skills/Build Plasma Field")]
        public static void Apply()
        {
            string iconPath = Root + "PlasmaFieldIcon.png";
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.maxTextureSize = 32; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_PlasmaField.asset");
            definition.Id = "PlasmaField"; definition.NameEn = "Expanding / returning plasma field"; definition.NameRu = "Расширяющееся и сжимающееся плазменное поле";
            definition.DescriptionEn = "A ring follows the caster and damages each target once per outward/return pass.";
            definition.DescriptionRu = "Кольцо следует за героем и поражает каждую цель по одному разу при расширении и возвращении.";
            definition.DefaultParams = new List<StepParamValue>
            { Float("Radius", 4), Float("WaveWidth", .3f), Float("OffsetY", 0), Float("DamageMultiplier", 1) };
            EditorUtility.SetDirty(definition);
            var field = new StepEntry { StepDefinition = definition, StartPercentPipeline = 0, EndPercentPipeline = 1,
                DamageConversions = new List<DamageConversionRule>
                {
                    Convert(DamageChannel.Physical), Convert(DamageChannel.Fire), Convert(DamageChannel.Cold)
                } };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_PlasmaField.asset");
            recipe.Steps = new List<StepEntry> { field }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "PlasmaFieldSkill"; skill.SkillName = "Plasma Field";
            skill.NameKey = "skills.PlasmaFieldSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 10; skill.ManaCost = 25; skill.WeaponRole = SkillWeaponRole.Equipment;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = .7f;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "MysticHelmetSkillPool.asset");
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Plasma Field" : "Плазменное поле");
                table.AddEntry(skill.DescriptionKey, "");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
        private static DamageConversionRule Convert(DamageChannel source) => new DamageConversionRule { Source = source, Target = DamageChannel.Lightning, Percent = 100 };
    }
}
