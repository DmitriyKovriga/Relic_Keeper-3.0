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
    public static class PendulumBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Chakram/lb/Pendulum/";
        public const string SkillPath = Root + "PendulumSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Chakram/lb/ChacramLBSkillPool.asset";
        [MenuItem("Tools/Skills/Build Pendulum")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var shot = Step("SpawnProjectile", .2f, .2f, Float("DamageMultiplier", 1),
                Float("BaseSpeed", 10), Float("Lifetime", 1.8f), Float("HitRadius", .7f), Float("WorldHitRadius", .18f), Float("TargetHitRadius", .2f),
                Float("RotationDegreesPerSecond", -900), Float("OffsetX", .55f), Float("OffsetY", .35f),
                Float("FirstReverseAtSeconds", .6f), Float("ReverseInterval", .6f), Float("ReturnDamagePercent", ReturningProjectileDamageResolver.DefaultReturnDamagePercent),
                Bool("UseProjectileCountStat", false), Bool("InfinitePierce", true), Bool("IgnoreFork", true), Bool("IgnoreChain", true),
                Bool("Homing", false), Bool("StopOnWorld", true), Bool("ClearHitHistoryOnReverse", true), Bool("FixedReversalTiming", true), Bool("Pendulum", true));
            shot.SetOverrideInt("BaseProjectileCount", 1); shot.SetOverrideInt("ReversalCount", 2); shot.SetOverrideInt("ReversalMode", 0);
            shot.SetOverrideInt("WorldLayerMask", (1 << 6) | LayerMask.GetMask("OneWayPlatform"));
            shot.SetOverrideObject("ProjectilePrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_Pendulum.asset");
            recipe.Steps = new List<StepEntry> { Step("WeaponWindup", 0, .2f), Step("WeaponStrike", .2f, .2f), shot, Step("WeaponRecovery", .2f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "PendulumSkill"; skill.SkillName = "Pendulum";
            skill.NameKey = "skills.PendulumSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Chakram/lb/HomingWeaponThrow");
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks.Clear();
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "PendulumIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Маятник" : "Pendulum");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Один диск летит по прямой и дважды меняет направление: через 0,6 и 1,2 с. Через 1,8 с он исчезает. Первый проход наносит 100% урона оружия; оба прохода после первого разворота — 50% обычного урона плюс урон возвращающихся снарядов. Каждая цель получает не более одного попадания за проход. Скорость снарядов увеличивает длину проходов, не меняя интервал разворотов. Диск не наводится на героя, останавливается у стен; число, разветвление и цепь снарядов не добавляют диски. Перезарядка и затраты маны отсутствуют."
                    : "One disc follows a fixed straight line, reversing at 0.6 and 1.2 seconds and disappearing at 1.8 seconds. The first pass deals 100% weapon damage; both later passes use 50% of normal damage plus Returning Projectile Damage. Each target can be hit once per pass. Projectile Speed lengthens each pass without changing turn intervals. The disc does not track the character and stops at walls; Projectile Count, Fork and Chain add no discs. No cooldown or mana cost.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Pendulum Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("PendulumIcon", false); Import("Disk", false); Import("Wind", true);
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
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Pendulum.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "Pendulum.mat"); }
            GameObject prefab; var host = new GameObject("Pendulum disc");
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

