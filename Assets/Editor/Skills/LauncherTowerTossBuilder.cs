using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class LauncherTowerTossBuilder
    {
        public const string Root = "Assets/Resources/Skills/Shields/LauncherTowerToss/";
        public const string SkillPath = Root + "LauncherTowerToss.asset";
        [MenuItem("Tools/Skills/Build Launcher Tower Toss")]
        public static void Apply()
        {
            ImportSprite(Root + "LauncherTowerTossIcon.png");
            var framePaths = Directory.GetFiles(Root + "Frames/", "*.png").OrderBy(path => path).ToArray();
            foreach (string path in framePaths) ImportSprite(path);
            var frames = framePaths.Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            if (frames.Length < 5) throw new System.InvalidOperationException("Five generated impact frames are required.");
            var clip = Load<AnimationClip>(Root + "LauncherTowerTossImpact.anim"); clip.frameRate = 18;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((frame, i) => new ObjectReferenceKeyframe { time = i / 18f, value = frame }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "LauncherTowerTossImpact.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "LauncherTowerTossImpact.mat");
            if (material == null)
            {
                material = new Material(Resources.Load<Material>("Skills/1HWeapon/Wand/ElectricBeam"));
                AssetDatabase.CreateAsset(material, Root + "LauncherTowerTossImpact.mat");
            }
            var host = new GameObject("Launcher Tower Toss impact"); GameObject prefab;
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "LauncherTowerTossImpact.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            var swings = WeaponSwingStyleTableSO.LoadDefault();
            if (!swings.TryGetById("shield_launcher", out _)) swings.Add(new WeaponSwingStyleTableSO.SwingKeyframes
            {
                Id = "shield_launcher", DisplayName = "Shield Launcher", Style = WeaponSwingStyle.Slash,
                AllowedStanceIds = new[] { "shield_guard" }, WindupZ = -18, WindupLocalPos = new Vector2(-.1f, -.15f),
                ImpactZ = 38, ImpactLocalPos = new Vector2(.2f, .2f)
            });
            EditorUtility.SetDirty(swings);
            var hit = Step("DealDamageRectangle", .3f, .3f, Float("SizeX", 1.2f), Float("SizeY", 1.4f),
                Float("OffsetX", .75f), Float("OffsetY", .2f), Float("DamageMultiplier", 1));
            hit.OnHitEffects.Add(new SkillOnHitEffectRule { Type = SkillOnHitEffectType.StunAndLaunch, StunSeconds = 1, LaunchHeight = .55f });
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_LauncherTowerToss.asset");
            recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, 1), Step("WeaponWindup", 0, .24f),
                Step("WeaponStrike", .24f, .24f, Bool("KeepWeaponVisible", false)),
                Step("SpawnVFX", .24f, .76f, Obj("VfxPrefab", prefab), Float("OffsetX", .75f), Float("OffsetY", .2f),
                    Float("ScaleMultiplier", 1.05f), Bool("AttachToParent", false), Bool("FadeOutEnabled", true),
                    Float("FadeOutStartLifePercent", .75f), Float("FadeStartAlphaMultiplier", 1)),
                hit, Step("WeaponRecovery", .76f, 1)
            };
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "LauncherTowerToss"; skill.SkillName = "Launcher Tower Toss";
            skill.NameKey = "skills.LauncherTowerToss"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 8; skill.ManaCost = 10;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1.5f;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.HoldStanceId = "shield_guard"; skill.SwingStyleId = "shield_launcher";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "LauncherTowerTossIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>("Assets/Resources/Skills/Shields/TowerShieldSkillPool.asset");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Launcher Tower Toss" : "Подбрасывающий удар щитом");
                table.AddEntry(skill.DescriptionKey, ""); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        private static void ImportSprite(string path)
        {
            path = path.Replace('\\', '/'); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            asset = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, FloatVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
        private static StepParamValue Obj(string key, Object value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Object, ObjectVal = value };
        private static StepEntry Step(string id, float start, float end, params StepParamValue[] parameters) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end, Overrides = parameters.ToList()
        };
    }
}
