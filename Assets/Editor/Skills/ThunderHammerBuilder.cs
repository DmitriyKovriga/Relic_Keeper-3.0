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
    public static class ThunderHammerBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/ThunderHammer/";
        public const string SkillPath = Root + "ThunderHammerSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/2HHammerSkillPool.asset";
        [MenuItem("Tools/Skills/Build Thunder Hammer")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var hit = Step("DealDamageRectangle", .45f, .45f, Float("DamageMultiplier", 1), Float("SizeX", 2), Float("SizeY", 1.3f),
                Float("OffsetX", 1), Float("OffsetY", 0), Bool("PullTowardOwner", false), Bool("ThunderHammer", true));
            var vfx = Step("SpawnVFX", .38f, .68f, Float("OffsetX", .75f), Float("OffsetY", .05f), Float("ScaleMultiplier", .8f),
                Bool("AttachToParent", true), Bool("FadeOutEnabled", false));
            vfx.SetOverrideObject("VfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_ThunderHammer.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .8f), Step("WeaponWindup", 0, .45f), Step("WeaponStrike", .45f, .45f),
                vfx, hit, Step("WeaponRecovery", .45f, 1) };
            recipe.HitStopFrames = 2; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "ThunderHammerSkill"; skill.SkillName = "Thunder Hammer";
            skill.NameKey = "skills.ThunderHammerSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = WeaponHoldStance.Shoulder; skill.HoldStanceId = "shoulder";
            skill.SwingStyle = WeaponSwingStyle.OverheadStab; skill.SwingStyleId = "overhead";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.Armor, SourceStep = 150,
                SourcePercent = 100f / 30f, TargetStat = StatType.DamageLightning, TargetModifierType = StatModType.Flat } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ThunderHammerIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Громовой молот" : "Thunder Hammer");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Тяжёлый удар наносит 100% урона оружия и сопровождается электрическим разрядом в той же области. Каждые полные 150 брони добавляют 5 урона молнией только этому навыку. Физический урон продолжает накапливать оглушение, а урон молнией помогает наложить шок по обычным правилам. Скорость атаки ускоряет удар; область действия увеличивает зону поражения. Перезарядка и затраты маны отсутствуют."
                    : "A heavy strike deals 100% weapon damage and releases an electrical discharge in the same area. Every complete 150 Armor adds 5 lightning damage for this skill only. Physical damage continues to build stun; lightning damage helps inflict shock under the normal rules. Attack Speed accelerates the strike; Area of Effect expands its area. No cooldown or mana cost.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Thunder Hammer Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("ThunderHammerIcon", false); Import("Strike", true);
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
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "ThunderHammer.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "ThunderHammer.mat"); }
            GameObject prefab; var host = new GameObject("Thunder Hammer strike");
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

