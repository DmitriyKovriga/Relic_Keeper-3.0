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
    public static class WeightOfAGiantBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/WeightOfAGiant/";
        public const string SkillPath = Root + "WeightOfAGiantSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/2HHammerSkillPoolRKM.asset";
        [MenuItem("Tools/Skills/Build The Weight of a Giant")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var hit = Step("DealDamageCircle", .7f, .7f, Float("DamageMultiplier", 5), Float("Radius", 1.35f), Float("SizeX", 1), Float("SizeY", .65f),
                Float("OffsetX", 1.2f), Float("OffsetY", -.15f), Bool("RadialSweep", true), Bool("PullTowardOwner", false), Bool("WeightOfAGiant", true));
            var vfx = Step("SpawnVFX", .65f, .95f, Float("OffsetX", .9f), Float("OffsetY", .05f), Float("ScaleMultiplier", .95f),
                Bool("AttachToParent", true), Bool("FadeOutEnabled", false));
            vfx.SetOverrideObject("VfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_WeightOfAGiant.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .92f), Step("WeaponWindup", 0, .7f), Step("WeaponStrike", .7f, .7f),
                vfx, hit, Step("WeaponRecovery", .7f, 1) };
            recipe.HitStopFrames = 4; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "WeightOfAGiantSkill"; skill.SkillName = "The Weight of a Giant";
            skill.NameKey = "skills.WeightOfAGiantSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 15; skill.ManaCost = 35;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = .4f;
            skill.HoldStance = WeaponHoldStance.Shoulder; skill.HoldStanceId = "shoulder";
            skill.SwingStyle = WeaponSwingStyle.OverheadStab; skill.SwingStyleId = "overhead";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.MaxHealth, SourceStep = 75,
                SourcePercent = 100f / 15f, TargetStat = StatType.DamagePhysical, TargetModifierType = StatModType.Flat } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "WeightOfAGiantIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Вес великана" : "The Weight of a Giant");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Герой принимает устойчивую стойку и после очень долгого замаха наносит один удар с 500% урона оружия. Каждые полные 75 максимального здоровья добавляют 5 базового физического урона этому навыку до множителя урона. Область действия увеличивает радиус удара. Скорость атаки ускоряет весь приём. Стойка ограничивает движение, но не даёт неуязвимости; уклонение прерывает замах. Перезарядка: 15 секунд. Затраты маны: 35."
                    : "Take a planted stance and deliver one devastating strike after a very slow wind-up, dealing 500% weapon damage. Every complete 75 Max Health adds 5 flat physical damage to this skill before its damage multiplier. Area of Effect expands the impact radius. Attack Speed accelerates the sequence. The stance restricts movement without granting invulnerability; dodging interrupts the wind-up. Cooldown: 15 seconds. Mana cost: 35.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh The Weight of a Giant Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("WeightOfAGiantIcon", false); Import("Strike", true);
            var frames = Frames("Strike");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Strike.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Strike.anim"); } clip.frameRate = 24;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 24f, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 24f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Strike.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Strike"); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "WeightOfAGiant.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "WeightOfAGiant.mat"); }
            GameObject prefab; var host = new GameObject("The Weight of a Giant strike");
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

