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
    public static class NearFanBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Bow/NearFan/";
        public const string SkillPath = Root + "NearFanSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Bow/PiercingShot/BowSecondarySkillPool.asset";
        [MenuItem("Tools/Skills/Build Near Fan")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var shot = Step("SpawnProjectile", .55f, .55f, Float("DamageMultiplier", 1),
                Float("BaseSpeed", 18), Float("Lifetime", 3), Float("HitRadius", .2f), Float("WorldHitRadius", .06f), Float("TargetHitRadius", .09f),
                Float("RotationDegreesPerSecond", 0), Float("OffsetX", .5f), Float("OffsetY", .35f), Float("TotalConeAngle", 70),
                Bool("UseProjectileCountStat", true), Bool("NearFan", true));
            shot.SetOverrideInt("BaseProjectileCount", 5);
            shot.SetOverrideObject("ProjectilePrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_NearFan.asset");
            recipe.Steps = new List<StepEntry> { Step("WeaponWindup", 0, .55f), shot,
                Step("WeaponStrike", .55f, .55f), Step("WeaponRecovery", .55f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "NearFanSkill"; skill.SkillName = "Near Fan";
            skill.NameKey = "skills.NearFanSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 20;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = WeaponHoldStance.Default; skill.HoldStanceId = "bow_stance";
            skill.SwingStyle = WeaponSwingStyle.BowDraw; skill.SwingStyleId = "bow_draw";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks.Clear();
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "NearFanIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Ближний веер" : "Near Fan");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Одновременно выпускает 5 стрел веером 70°, каждая наносит 100% урона оружия. Вблизи несколько стрел могут поразить одного врага; на расстоянии веер охватывает несколько целей. Число снарядов добавляет стрелы внутри того же угла, повышая плотность залпа. Скорость снарядов ускоряет полёт, скорость атаки — подготовку выстрела. Перезарядка: 10 секунд. Затраты маны: 20."
                    : "Fire 5 arrows simultaneously across a 70° fan, each dealing 100% weapon damage. Several arrows can hit one enemy at close range; the wider spread covers multiple targets at distance. Projectile Count adds arrows inside the same angle, increasing volley density. Projectile Speed accelerates travel; Attack Speed accelerates the shot. Cooldown: 10 seconds. Mana cost: 20.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Near Fan Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("NearFanIcon", false); Import("Arrow", true);
            var frames = Frames("Arrow");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Arrow.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Arrow.anim"); } clip.frameRate = 20;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 20f, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 20f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Arrow.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Arrow"); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "NearFan.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "NearFan.mat"); }
            GameObject prefab; var host = new GameObject("Near Fan arrow");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Arrow.prefab");
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
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(21f / 32, .5f)
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

