using System.Collections.Generic;
using System.Linq;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Items;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class NinjaStubBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Dagger/";
        public const string SkillPath = Root + "NinjaStubSkill.asset";

        [MenuItem("Tools/Skills/Build Ninja Stub")]
        public static void Apply()
        {
            string iconPath = Root + "NinjaStubIcon.png";
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 32; importer.maxTextureSize = 32;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_BlinkStab.asset");
            definition.Id = "BlinkStab"; definition.NameEn = "Blink and stab"; definition.NameRu = "Телепортация и удар";
            definition.DescriptionEn = "Blink behind an unhit enemy, or the least recently hit living enemy, and deal normal weapon damage. Remembers targets between uses. Requires a safe supported destination.";
            definition.DescriptionRu = "Телепортация за спину ещё не поражённого живого врага, затем атакованного раньше остальных, и удар оружием. Помнит цели между применениями. Требуется безопасная точка с опорой.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Range", 6), Float("Clearance", 0.12f), Float("DamageMultiplier", 1),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            EditorUtility.SetDirty(definition);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_NinjaStub.asset");
            recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, 0.29f), Step("WeaponWindup", 0, 0.1f),
                new StepEntry { StepDefinition = definition, StartPercentPipeline = 0.1f, EndPercentPipeline = 0.1f },
                Step("WeaponRecovery", 0.1f, 0.29f)
            };
            EditorUtility.SetDirty(recipe);
            var source = AssetDatabase.LoadAssetAtPath<SkillDataSO>(Root + "VenomStrike/VenomStrikeSkill.asset");
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "NinjaStubSkill"; skill.SkillName = "Ninja Stub";
            skill.NameKey = "skills.NinjaStubSkill"; skill.DescriptionKey = "skills.NinjaStubSkill.description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.EnablePushback = false; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) ?? source.Icon;
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "1HDaggerSkillPool.asset");
            if (!pool.PossibleSkills.Any(s => s.Skill == skill))
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            foreach (string code in new[] { "en", "ru" })
            {
                var table = collection.GetTable(code) as StringTable;
                table.AddEntry(skill.NameKey, code == "en" ? "Ninja Stub" : "Удар ниндзя");
                table.AddEntry(skill.DescriptionKey, ""); EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData); AssetDatabase.SaveAssets();
        }

        private static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static StepEntry Step(string id, float start, float end) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end
        };
        private static StepParamValue Float(string key, float value) => new StepParamValue
            { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
