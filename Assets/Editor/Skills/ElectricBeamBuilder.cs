using System.Collections.Generic;
using System.Linq;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class ElectricBeamBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Wand/";
        public const string SkillPath = Root + "ElectricBeamSkill.asset";

        [MenuItem("Tools/Skills/Build Electric Beam")]
        public static void Apply()
        {
            string iconPath = Root + "ElectricBeamIcon.png";
            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32; importer.maxTextureSize = 32;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            string materialPath = Root + "ElectricBeam.mat";
            var beamMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            var beamShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "ElectricBeam.shader");
            if (beamMaterial == null)
            {
                beamMaterial = new Material(beamShader) { name = "ElectricBeam" };
                AssetDatabase.CreateAsset(beamMaterial, materialPath);
            }
            else { beamMaterial.shader = beamShader; EditorUtility.SetDirty(beamMaterial); }
            var definition = LoadOrCreate<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_InstantBeam.asset");
            definition.Id = "InstantBeam";
            definition.NameEn = "Instant beam"; definition.NameRu = "Мгновенный луч";
            definition.DescriptionEn = "Hits the entire forward beam instantly, stopping at solid terrain.";
            definition.DescriptionRu = "Мгновенно поражает врагов вдоль луча перед героем. Останавливается на препятствиях.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Range", 12), Float("Width", 0.35f), Float("DamageMultiplier", 1),
                Float("OffsetX", 0.45f), Float("OffsetY", 0.25f), Float("VisualLifetime", 0.28f),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            EditorUtility.SetDirty(definition);

            var beam = new StepEntry { StepDefinition = definition, StartPercentPipeline = 0.1f, EndPercentPipeline = 0.1f };
            beam.DamageConversions = new List<DamageConversionRule>
            {
                Conversion(DamageChannel.Physical), Conversion(DamageChannel.Fire), Conversion(DamageChannel.Cold)
            };
            var recipe = LoadOrCreate<SkillRecipeSO>(Root + "Recipe_ElectricBeam.asset");
            recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, 0.16f), Step("WeaponWindup", 0, 0.1f),
                Step("WeaponStrike", 0.1f, 0.1f), beam, Step("WeaponRecovery", 0.15f, 0.3f)
            };
            EditorUtility.SetDirty(recipe);

            var skill = LoadOrCreate<SkillDataSO>(SkillPath);
            skill.ID = "ElectricBeamSkill"; skill.SkillName = "Electric Beam";
            skill.NameKey = "skills.ElectricBeamSkill";
            skill.DescriptionKey = "skills.ElectricBeamSkill.description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.IsActive = true; skill.ManaCost = 0; skill.Cooldown = 0;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Range | StatContextTagFlags.Area;
            skill.Recipe = recipe;
            var source = AssetDatabase.LoadAssetAtPath<SkillDataSO>(Root + "SeekerSkill.asset");
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ElectricBeamIcon.png");
            EditorUtility.SetDirty(skill);

            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "1hWandSkillPool.asset");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill))
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            foreach (string code in new[] { "en", "ru" })
            {
                var table = collection.GetTable(code) as StringTable;
                table.AddEntry(skill.NameKey, code == "en" ? "Electric Beam" : "Электрический луч");
                table.AddEntry(skill.DescriptionKey, "");
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static StepEntry Step(string id, float start, float end) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end
        };
        private static StepParamValue Float(string key, float value) => new StepParamValue
            { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
        private static DamageConversionRule Conversion(DamageChannel source) => new DamageConversionRule
            { Source = source, Target = DamageChannel.Lightning, Percent = 50 };
    }
}
