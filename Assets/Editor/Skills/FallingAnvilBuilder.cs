using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class FallingAnvilBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/FallingAnvil/";
        public const string SkillPath = Root + "FallingAnvilSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/2HHammerSkillPoolRKM.asset";

        [MenuItem("Tools/Skills/Build Falling Anvil")]
        public static void Apply()
        {
            ImportTexture(Root + "FallingAnvilIcon.png", false);
            ImportTexture(Root + "FallingAnvilImpact.png", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "FallingAnvilImpact.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length != 12) throw new InvalidOperationException("Falling Anvil needs all twelve PixelLab impact frames.");
            var clip = Load<AnimationClip>(Root + "FallingAnvilImpact.anim");
            clip.frameRate = 16;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / 16f, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 16f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "FallingAnvilImpact.controller");
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(Root + "FallingAnvilImpact.controller", clip);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FallingAnvilImpact.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "FallingAnvilImpact.mat");
            }
            GameObject prefab;
            var host = new GameObject("Falling Anvil Impact");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "FallingAnvilImpact.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_FallingAnvil.asset");
            definition.Id = "FallingAnvil"; definition.NameEn = "Falling Anvil"; definition.NameRu = "Падающая наковальня";
            definition.DescriptionEn = "Heavy ground strike or a single landing impact, scaled by capped fall height.";
            definition.DescriptionRu = "Тяжёлый удар на земле или удар при приземлении с ограниченным бонусом от высоты падения.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("DamageMultiplier", 2.5f), Float("Radius", 1.6f), Float("GroundOffsetX", .8f),
                Float("FallBonusPerUnit", .25f), Float("MaximumFallBonus", 1),
                Float("WindupSeconds", .3f), Float("RecoverySeconds", .4f), Float("MaximumAirTime", 15)
            };
            EditorUtility.SetDirty(definition);
            var hit = new StepEntry { StepDefinition = definition };
            hit.SetOverrideObject("VfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FallingAnvil.asset");
            recipe.Steps = new List<StepEntry> { hit }; recipe.HitStopFrames = 3; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "FallingAnvilSkill"; skill.SkillName = "Falling Anvil";
            skill.NameKey = "skills.FallingAnvilSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 25;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.WeaponRole = SkillWeaponRole.Special;
            skill.SwingStyle = WeaponSwingStyle.OverheadStab; skill.SwingStyleId = "overhead_stab";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area;
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FallingAnvilIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Falling Anvil" : "Падающая наковальня");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "A heavy hammer strike. In the air, hold the hammer downward and strike upon landing. Fall height increases impact damage up to a cap. Jump Force helps gain height; Area of Effect widens the impact. You remain vulnerable during descent."
                    : "Тяжёлый удар молотом. В воздухе герой направляет молот вниз и бьёт при приземлении. Высота падения увеличивает урон до предела. Сила прыжка помогает набрать высоту, а область действия расширяет удар. Во время падения герой уязвим.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportTexture(string path, bool sheet)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = sheet ? 28.4375f : 24;
            importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            if (sheet)
                importer.spritesheet = Enumerable.Range(0, 12).Select(i => new SpriteMetaData
                {
                    name = "FallingAnvil_" + i.ToString("D2"), rect = new Rect(i % 4 * 91, 144 - (i / 4 + 1) * 48, 91, 48),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(45f / 91, 30f / 48)
                }).ToArray();
            importer.SaveAndReimport();
        }

        private static T Load<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(value, path); return value;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
