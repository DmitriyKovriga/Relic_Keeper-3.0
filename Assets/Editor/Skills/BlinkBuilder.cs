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
    public static class BlinkBuilder
    {
        public const string Root = "Assets/Resources/Skills/Boots/Evasion/";
        public const string SkillPath = Root + "BlinkSkill.asset";

        [MenuItem("Tools/Skills/Build Blink")]
        public static void Apply()
        {
            string iconPath = Root + "11.png";
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_ForwardBlink.asset");
            definition.Id = "ForwardBlink"; definition.NameEn = "Forward blink"; definition.NameRu = "Телепортация вперёд";
            definition.DescriptionEn = "Blink in the facing direction, stopping before solid terrain.";
            definition.DescriptionRu = "Телепортация в сторону взгляда с остановкой перед твёрдыми препятствиями.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Distance", 5), Float("Clearance", .04f),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            EditorUtility.SetDirty(definition);
            var blink = new StepEntry { StepDefinition = definition, StartPercentPipeline = 0 };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_Blink.asset");
            recipe.Steps = new List<StepEntry> { blink }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "BlinkSkill"; skill.SkillName = "Blink";
            skill.NameKey = "skills.BlinkSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 4; skill.ManaCost = 0; skill.WeaponRole = SkillWeaponRole.Equipment;
            skill.ActionSpeedMode = SkillActionSpeedMode.Universal; skill.SkillSpeedMultiplier = 5f;
            skill.DamageContextTags = StatContextTagFlags.None;
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "EvasionBootsSkillPool.asset");
            pool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Blink" : "Скачок");
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
    }
}
