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
    public static class FrostAnabiosisBuilder
    {
        public const string Root = "Assets/Resources/Skills/Shields/";
        public const string SkillPath = Root + "FrostAnabiosis.asset";

        [MenuItem("Tools/Skills/Build Frost Anabiosis")]
        public static void Apply()
        {
            string iconPath = Root + "FrostAnabiosisIcon.png";
            ImportSprite(iconPath);
            ImportSprite(Root + "FrostAnabiosisCrystal.png");
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_SelfStasis.asset");
            definition.Id = "SelfStasis"; definition.NameEn = "Self stasis"; definition.NameRu = "Анабиоз";
            definition.DescriptionEn = "Fixed-duration invulnerable, immobile, healing stasis. Blocks skills and dodges.";
            definition.DescriptionRu = "Анабиоз: неуязвимость, неподвижность и лечение. Блокирует умения и уклонения.";
            definition.DefaultParams = new List<StepParamValue> { Float("Duration", 5), Float("HealPercentPerSecond", 10) };
            EditorUtility.SetDirty(definition);
            var stasis = new StepEntry { StepDefinition = definition, StartPercentPipeline = 0 };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FrostAnabiosis.asset");
            recipe.Steps = new List<StepEntry> { stasis }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "FrostAnabiosis"; skill.SkillName = "Frost Anabiosis";
            skill.NameKey = "skills.FrostAnabiosis"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 15; skill.ManaCost = 50; skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.ActionSpeedMode = SkillActionSpeedMode.Universal; skill.SkillSpeedMultiplier = 5f;
            skill.DamageContextTags = StatContextTagFlags.None;
            skill.HoldStanceId = "shield_guard"; skill.SwingStyleId = "shield_parry";
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "MysticShieldSkillPool.asset");
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Frost Anabiosis" : "Ледяной анабиоз");
                table.AddEntry(skill.DescriptionKey, "");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.maxTextureSize = 64; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
