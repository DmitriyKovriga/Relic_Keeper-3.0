using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
namespace RelicKeeper.Editor.Skills
{
    public static class FrostInducedJoltBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Cold/FrostInducedJolt/";
        public const string SkillPath = Root + "FrostInducedJoltSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Cold/2HColdStaffSkillPoolRBM.asset";
        [MenuItem("Tools/Skills/Build Frost-induced Jolt")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var hit = Step("DealDamageCircle", .3f, .3f, Float("DamageMultiplier", 2.5f), Float("Radius", 1.75f),
                Float("OffsetX", 0), Float("OffsetY", 0), Bool("PullTowardOwner", false), Bool("RadialSweep", true), Bool("FrostInducedJolt", true));
            hit.DamageConversions = PrismaticCycleCast.ConvertAllTo(Scripts.Combat.DamageChannel.Cold);
            var vfx = Step("SpawnVFX", .3f, .7f, Float("OffsetX", 0), Float("OffsetY", 0), Float("ScaleMultiplier", 1),
                Bool("AttachToParent", false), Bool("FadeOutEnabled", false));
            vfx.SetOverrideObject("VfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FrostInducedJolt.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .5f), Step("WeaponWindup", 0, .3f), Step("WeaponStrike", .3f, .3f),
                vfx, hit, Step("WeaponRecovery", .3f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "FrostInducedJoltSkill"; skill.SkillName = "Frost-induced Jolt";
            skill.NameKey = "skills.FrostInducedJoltSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 15; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = WeaponHoldStance.Staff; skill.HoldStanceId = "staff";
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.EnablePushback = true; skill.PushbackRating = 750; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.ColdResist, SourceStep = 1,
                SourcePercent = 1000, TargetStat = StatType.Pushback, TargetModifierType = StatModType.Flat } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FrostInducedJoltIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Морозный толчок" : "Frost-induced Jolt");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Короткая волна холода поражает ближайших врагов с обеих сторон на 250% урона оружия, преобразованного в холод, и отталкивает их от героя. Радиус: 1,75. Базовое отталкивание: 750. Каждый полный 1% сопротивления холоду добавляет 10 отталкивания только этому умению. Область действия увеличивает радиус, а обычные параметры заморозки помогают сдерживать врагов. Перезарядка: 15 с. Мана: 25."
                    : "A short cold wave hits nearby enemies on both sides for 250% weapon damage converted to cold, pushing them away from the character. Radius: 1.75. Base knockback rating: 750. Every complete 1% Cold Resistance adds 10 knockback rating to this skill only. Area of Effect expands the wave; normal freeze stats help control enemies. Cooldown: 15 seconds. Mana: 25.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Frost-induced Jolt Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("FrostInducedJoltIcon", false); Import("Strike", true);
            var frames = Frames("Strike");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Strike.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Strike.anim"); } clip.frameRate = 30;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 30f, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 30f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Strike.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Strike"); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FrostInducedJolt.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "FrostInducedJolt.mat"); }
            GameObject prefab; var host = new GameObject("Frost-induced Jolt strike");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Strike.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            return prefab;
        }
        private static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
        [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [System.Serializable] private class Frame { public Box frame; }
        [System.Serializable] private class Box { public int x, y, w, h; }
        [System.Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, 1f - 30f / 56f)
                }).ToArray();
            }
            importer.SaveAndReimport();
        }
        private static T Load<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
    }
}

