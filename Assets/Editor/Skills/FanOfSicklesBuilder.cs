using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
namespace RelicKeeper.Editor.Skills
{
    public static class FanOfSicklesBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Chakram/lb/FanOfSickles/";
        public const string SkillPath = Root + "FanOfSicklesSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Chakram/lb/ChacramLBSkillPool.asset";
        [MenuItem("Tools/Skills/Build A Fan of Sickles")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var shot = Step("SpawnProjectile", .2f, .2f, Float("DamageMultiplier", 1),
                Float("BaseSpeed", 10), Float("Lifetime", 4), Float("HitRadius", .7f), Float("WorldHitRadius", .18f), Float("TargetHitRadius", .2f),
                Float("RotationDegreesPerSecond", -900), Float("OffsetX", .55f), Float("OffsetY", .35f), Float("TotalConeAngle", 50),
                Float("FirstReverseAtSeconds", .4f), Float("ReverseInterval", .6f), Float("ReturnDamagePercent", ReturningProjectileDamageResolver.DefaultReturnDamagePercent),
                Bool("UseProjectileCountStat", true), Bool("InfinitePierce", true), Bool("IgnoreFork", true), Bool("IgnoreChain", true),
                Bool("Homing", false), Bool("StopOnWorld", false), Bool("ClearHitHistoryOnReverse", true), Bool("FixedReversalTiming", true), Bool("FanOfSickles", true));
            shot.SetOverrideInt("BaseProjectileCount", 3); shot.SetOverrideInt("ReversalCount", 1); shot.SetOverrideInt("ReversalMode", 2);
            shot.SetOverrideInt("WorldLayerMask", (1 << 6) | LayerMask.GetMask("OneWayPlatform"));
            shot.SetOverrideObject("ProjectilePrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FanOfSickles.asset");
            recipe.Steps = new List<StepEntry> { Step("WeaponWindup", 0, .2f), Step("WeaponStrike", .2f, .2f), shot, Step("WeaponRecovery", .2f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "FanOfSicklesSkill"; skill.SkillName = "A Fan of Sickles";
            skill.NameKey = "skills.FanOfSicklesSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Chakram/lb/HomingWeaponThrow");
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks.Clear();
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FanOfSicklesIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Веер серпов" : "A Fan of Sickles");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Одновременно выпускает 3 диска веером 50°, каждый наносит 100% урона оружия на вылете. Через 0,4 с диски возвращаются к текущей позиции героя: движение меняет обратные траектории. Каждый диск может поразить цель один раз на вылете и ещё раз на возврате. На возврате действует 50% обычного урона плюс урон снарядов при возврате. Число снарядов добавляет диски внутри того же веера. Диски проходят сквозь врагов и стены и исчезают при возвращении. Перезарядка и затраты маны отсутствуют."
                    : "Throw 3 discs simultaneously across a 50° fan, each dealing 100% weapon damage outward. After 0.4 seconds they home toward the character's current position, so movement changes their return paths. Each disc can hit a target once outward and once returning. Return hits use 50% of normal damage plus Returning Projectile Damage. Projectile Count adds discs inside the same fan. Discs pass through enemies and walls and disappear when caught. No cooldown or mana cost.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Fan of Sickles Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("FanOfSicklesIcon", false); Import("Disk", false); Import("Wind", true);
            var frames = Frames("Wind");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Wind.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Wind.anim"); } clip.frameRate = 20;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 20f, value = frame }).ToList();
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Wind", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Wind.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Wind"); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FanOfSickles.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "FanOfSickles.mat"); }
            GameObject prefab; var host = new GameObject("FanOfSickles disc");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Disk.png"); renderer.sharedMaterial = material;
                var wind = new GameObject("Wind"); wind.transform.SetParent(host.transform, false);
                var windRenderer = wind.AddComponent<SpriteRenderer>(); windRenderer.sprite = frames[0]; windRenderer.sharedMaterial = material; windRenderer.sortingOrder = 1;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Disk.prefab");
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
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 48;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
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

