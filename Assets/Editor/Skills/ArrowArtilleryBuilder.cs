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
    public static class ArrowArtilleryBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Bow/Artillery/";
        public const string SkillPath = Root + "ArrowArtillery.asset";

        [MenuItem("Tools/Skills/Build Arrow Artillery")]
        public static void Apply()
        {
            string iconPath = Root + "ArrowArtilleryIcon.png";
            ImportSprite(iconPath);
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_ArrowRain.asset");
            definition.Id = "ArrowRain"; definition.NameEn = "Arrow rain"; definition.NameRu = "Град стрел";
            definition.DescriptionEn = "Real falling arrows around the cast position; one shared hit per target per barrage.";
            definition.DescriptionRu = "Падающие стрелы вокруг места применения; одна общая попытка попадания по каждой цели за залп.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Radius", 3), Float("SpawnHeight", 4), Float("RainDelay", .15f), Float("RainDuration", .65f),
                Float("ArrowSpeed", 16), Float("ArrowLifetime", 1.5f), Float("DamageMultiplier", 3),
                new StepParamValue { Key = "ArrowCount", Type = StepParamValue.ParamKind.Int, IntVal = 17 },
                new StepParamValue { Key = "ProjectilePrefab", Type = StepParamValue.ParamKind.Object,
                    ObjectVal = AssetDatabase.LoadAssetAtPath<GameObject>(BowShotBuilder.Root + "BowArrowProjectile.prefab") }
            };
            EditorUtility.SetDirty(definition);
            var rain = new StepEntry { StepDefinition = definition, StartPercentPipeline = .25f };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_ArrowArtillery.asset");
            recipe.Steps = new List<StepEntry>
            {
                new StepEntry { StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_WeaponWindup.asset"), StartPercentPipeline = 0, EndPercentPipeline = .25f },
                rain,
                new StepEntry { StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_WeaponStrike.asset"), StartPercentPipeline = .25f },
                new StepEntry { StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_WeaponRecovery.asset"), StartPercentPipeline = .25f, EndPercentPipeline = .75f }
            }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "ArrowArtillery"; skill.SkillName = "Arrow Artillery";
            skill.NameKey = "skills.ArrowArtillery"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 8; skill.ManaCost = 20; skill.WeaponRole = SkillWeaponRole.Special;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1f;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Range | StatContextTagFlags.Projectile | StatContextTagFlags.Area;
            skill.HoldStanceId = "bow_stance"; skill.SwingStyleId = "bow_draw"; skill.SwingStyle = WeaponSwingStyle.BowDraw;
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(BowShotBuilder.Root + "PiercingShot/BowSecondarySkillPool.asset");
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Arrow Artillery" : "Град стрел");
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
