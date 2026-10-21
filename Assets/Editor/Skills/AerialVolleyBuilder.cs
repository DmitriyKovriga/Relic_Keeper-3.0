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
    public static class AerialVolleyBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Bow/AerialVolley/";
        public const string SkillPath = Root + "AerialVolleySkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Bow/PiercingShot/BowSecondarySkillPool.asset";
        [MenuItem("Tools/Skills/Build Aerial Volley")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            var takeoff = BuildVisuals("Takeoff");
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            StepEntry Volley(float time)
            {
                var shot = Step("SpawnProjectile", time, time, Float("DamageMultiplier", 2),
                    Float("BaseSpeed", 18), Float("Lifetime", 3), Float("HitRadius", .2f), Float("WorldHitRadius", .06f), Float("TargetHitRadius", .09f),
                    Float("RotationDegreesPerSecond", 0), Float("OffsetX", .5f), Float("OffsetY", .35f), Float("TotalConeAngle", 18),
                    Bool("UseProjectileCountStat", true), Bool("AerialVolley", true));
                shot.SetOverrideInt("BaseProjectileCount", 3); shot.SetOverrideInt("VolleyCount", 3);
                shot.SetOverrideObject("ProjectilePrefab", prefab); return shot;
            }
            StepEntry Draw(float start, float end)
            {
                var draw = Step("WeaponWindup", start, end); draw.SetOverrideObject("DrawnProjectilePrefab", prefab); return draw;
            }
            var leap = Step("PlayerImpulse", .02f, .02f, Float("AngleDegrees", 115), Float("Force", 12),
                Bool("RelativeToFacing", true), Bool("ClearCurrentVelocity", true), Bool("PreserveFacing", true), Bool("ScaleWithJumpForce", true), Bool("AnimateAsJump", true));
            var dust = Step("SpawnVFX", .02f, .3f, Float("OffsetX", 0), Float("OffsetY", -.45f), Float("ScaleMultiplier", .8f),
                Bool("AttachToParent", false), Bool("FadeOutEnabled", false)); dust.SetOverrideObject("VfxPrefab", takeoff);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_AerialVolley.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .9f), leap, dust,
                Draw(.02f, .2f), Volley(.2f), Step("WeaponStrike", .2f, .2f), Step("WeaponRecovery", .2f, .29f),
                Draw(.3f, .44f), Volley(.44f), Step("WeaponStrike", .44f, .44f), Step("WeaponRecovery", .44f, .53f),
                Draw(.54f, .68f), Volley(.68f), Step("WeaponStrike", .68f, .68f), Step("WeaponRecovery", .68f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "AerialVolleySkill"; skill.SkillName = "Aerial Volley";
            skill.NameKey = "skills.AerialVolleySkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 10;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1.4f;
            skill.HoldStance = WeaponHoldStance.Default; skill.HoldStanceId = "bow_stance";
            skill.SwingStyle = WeaponSwingStyle.BowDraw; skill.SwingStyleId = "bow_draw";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks.Clear();
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "AerialVolleyIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Воздушный залп" : "Aerial Volley");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Прыгает назад, сохраняя направление взгляда, и выпускает вперёд 3 последовательных залпа по 3 стрелы. Каждая стрела наносит 200% урона оружия. Сила прыжка увеличивает высоту и дальность отскока. Число снарядов добавляет стрелы в каждый залп. Прыжок не даёт неуязвимости. Уклонение прерывает оставшиеся залпы. Перезарядка: 10 секунд. Затраты маны: 10."
                    : "Leap backward without turning away and fire 3 consecutive forward volleys of 3 arrows. Each arrow deals 200% weapon damage. Jump Force increases leap height and distance. Projectile Count adds arrows to every volley. The leap grants no invulnerability. Dodging interrupts remaining volleys. Cooldown: 10 seconds. Mana cost: 10.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Aerial Volley Art")]
        public static void RefreshArt()
        {
            BuildVisuals(); BuildVisuals("Takeoff");
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals(string name = "Arrow")
        {
            Import("AerialVolleyIcon", false); Import(name, true);
            float fps = name == "Arrow" ? 20 : 24;
            var frames = Frames(name);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + name + ".anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + name + ".anim"); } clip.frameRate = fps;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / fps, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = name == "Arrow";
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState(name); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "AerialVolley.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "AerialVolley.mat"); }
            GameObject prefab; var host = new GameObject("Aerial Volley " + name);
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + name + ".prefab");
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
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(name == "Arrow" ? 21f / 32 : .5f, .5f)
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

