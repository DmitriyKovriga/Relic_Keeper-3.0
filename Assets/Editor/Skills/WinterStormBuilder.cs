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
    public static class WinterStormBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Cold/";
        public const string SkillPath = Root + "WinterStorm.asset";

        [MenuItem("Tools/Skills/Build Winter Storm")]
        public static void Apply()
        {
            MoveExistingAssets();
            string iconPath = Root + "WinterStormIcon.png";
            ImportSprite(iconPath);
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_WinterStorm.asset");
            definition.Id = "WinterStorm"; definition.NameEn = "Winter storm"; definition.NameRu = "Зимняя буря";
            definition.DescriptionEn = "Stationary storm; repeated cold hits, other skills allowed, continues during stasis.";
            definition.DescriptionRu = "Стационарная буря: повторные холодные попадания, другие умения доступны; работает во время анабиоза.";
            definition.DefaultParams = new List<StepParamValue>
            { Float("Duration", 5), Float("Radius", 3), Float("PulseInterval", .5f), Float("DamageMultiplier", .5f) };
            EditorUtility.SetDirty(definition);
            var storm = new StepEntry { StepDefinition = definition, StartPercentPipeline = 0,
                DamageConversions = new List<DamageConversionRule>
                {
                    new DamageConversionRule { Source = DamageChannel.Physical, Target = DamageChannel.Cold, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Fire, Target = DamageChannel.Cold, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Lightning, Target = DamageChannel.Cold, Percent = 100 }
                },
                ScopedStatModifiers = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.DamageCold, Type = StatModType.Flat, Value = 20 },
                    new SerializableStatModifier { Stat = StatType.FreezeChance, Type = StatModType.Flat, Value = 100 }
                }
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_WinterStorm.asset");
            recipe.Steps = new List<StepEntry> { storm }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "WinterStorm"; skill.SkillName = "Winter Storm";
            skill.NameKey = "skills.WinterStorm"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 15; skill.ManaCost = 50; skill.WeaponRole = SkillWeaponRole.Special;
            skill.ActionSpeedMode = SkillActionSpeedMode.Universal; skill.SkillSpeedMultiplier = 5f;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.HoldStanceId = "staff"; skill.SwingStyleId = "";
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(skill);
            ConfigurePools(skill);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Winter Storm" : "Зимняя буря");
                table.AddEntry(skill.DescriptionKey, "");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        public static void MoveToColdStaff()
        {
            MoveExistingAssets();
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
            if (skill == null) throw new System.InvalidOperationException("Winter Storm skill is missing.");
            skill.WeaponRole = SkillWeaponRole.Special;
            skill.HoldStanceId = "staff"; skill.SwingStyleId = "";
            EditorUtility.SetDirty(skill);
            ConfigurePools(skill);
            AssetDatabase.SaveAssets();
        }

        private static void MoveExistingAssets()
        {
            foreach (string file in new[] { "WinterStorm.asset", "Recipe_WinterStorm.asset", "WinterStormIcon.png" })
            {
                string previous = "Assets/Resources/Skills/Shields/" + file;
                if (AssetDatabase.LoadMainAssetAtPath(previous) == null) continue;
                string error = AssetDatabase.MoveAsset(previous, Root + file);
                if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
            }
        }

        private static void ConfigurePools(SkillDataSO skill)
        {
            var shield = AssetDatabase.LoadAssetAtPath<SkillPoolSO>("Assets/Resources/Skills/Shields/MysticShieldSkillPool.asset");
            if (shield.PossibleSkills.RemoveAll(entry => entry.Skill == skill) > 0) EditorUtility.SetDirty(shield);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "2HColdStaffSkillPoolRBM.asset");
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill))
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var helmet = AssetDatabase.LoadAssetAtPath<SkillPoolSO>("Assets/Resources/Skills/Helmet/Mystic/MysticHelmetSkillPool.asset");
            helmet.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!helmet.PossibleSkills.Any(entry => entry.Skill == skill))
                helmet.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(helmet);
        }

        public static void UpdateSkillPools()
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
            if (skill == null) throw new System.InvalidOperationException("Winter Storm skill is missing.");
            ConfigurePools(skill);
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
