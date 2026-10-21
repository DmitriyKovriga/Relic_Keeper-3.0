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
    public static class TornEdgeBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/TornEdge/";
        public const string SkillPath = Root + "TornEdgeSkill.asset";
        public static readonly string[] PoolPaths = {
            "Assets/Resources/Skills/2HWeapon/AxeSkillsPoolRB.asset",
            "Assets/Resources/Skills/2HWeapon/Sword/Adventurer/2HSwordSkillRBPool.asset"
        };

        [MenuItem("Tools/Skills/Build Torn Edge")]
        public static void Apply()
        {
            Import("TornEdgeIcon", false); Import("Sweep", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "Sweep.png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Sweep.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Sweep.anim"); }
            clip.frameRate = 24;
            var keys = frames.Select((frame, i) => new ObjectReferenceKeyframe { time = i / 24f, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 24f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Sweep.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Sweep");
            state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "TornEdge.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "TornEdge.mat");
            }
            GameObject prefab; var host = new GameObject("Torn Edge sweep");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Sweep.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var hit = Step("DealDamageRectangle", .55f, .55f,
                Float("DamageMultiplier", 2.5f), Float("SizeX", 3.2f), Float("SizeY", 2.5f),
                Float("OffsetX", 1.7f), Float("OffsetY", .4f), Bool("PullTowardOwner", false));
            var vfx = Step("SpawnVFX", .42f, .78f, Float("OffsetX", 0), Float("OffsetY", .4f),
                Bool("AttachToParent", true), Bool("FadeOutEnabled", false));
            vfx.SetOverrideObject("VfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_TornEdge.asset");
            recipe.Steps = new List<StepEntry> {
                Step("MovementLock", 0, 1), Step("WeaponWindup", 0, .55f),
                Step("WeaponStrike", .55f, .55f), vfx, hit, Step("WeaponRecovery", .55f, 1)
            };
            recipe.HitStopFrames = 3; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "TornEdgeSkill"; skill.SkillName = "Torn Edge";
            skill.NameKey = "skills.TornEdgeSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 20; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = .7f;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier {
                SourceStat = StatType.CritMultiplier, SourcePercent = 100,
                TargetStat = StatType.BleedDamageMult, TargetModifierType = StatModType.Flat
            } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "TornEdgeIcon.png"); EditorUtility.SetDirty(skill);
            foreach (string path in PoolPaths)
            {
                var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(path);
                if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
                EditorUtility.SetDirty(pool);
            }
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Рваная кромка" : "Torn Edge");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Медленный размашистый удар двуручным мечом или топором наносит 250% урона оружия. Каждый процентный пункт множителя критического урона добавляет один пункт к множителю урона кровотечения этого умения. Усиление действует и на некритические удары. Использует обычный шанс наложить кровотечение; не гарантирует его. Скорость удара — 70% от обычной."
                    : "A slow sweeping strike with a two-handed sword or axe deals 250% weapon damage. Each percentage point of Crit Multiplier adds one point to this skill's Bleed Damage Multiplier, even on non-critical hits. Uses your normal chance to inflict bleed; bleeding is not guaranteed. Attacks at 70% of normal speed.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [System.Serializable] private class Frame { public Box frame; public Pivot pivot; }
        [System.Serializable] private class Box { public int x, y, w, h; }
        [System.Serializable] private class Pivot { public float x, y; }
        [System.Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(frame.pivot.x, frame.pivot.y)
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
