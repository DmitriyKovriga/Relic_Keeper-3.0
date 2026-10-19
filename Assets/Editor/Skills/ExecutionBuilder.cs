using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Editor.Affixes;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class ExecutionBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Dagger/Execution/";
        public const string SkillPath = Root + "ExecutionSkill.asset";
        public const int VfxFrameCount = 9;
        private const float VfxFps = 24f;
        public const string AffixPoolPath = "Assets/Resources/Affixes/Pools/TwoHandedMelee/Axe/pool/OneHandedGenericPhysAffixPool.asset";

        [Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private class Frame { public FrameRect frame; }
        [Serializable] private class FrameRect { public int x, y, w, h; }
        [Serializable] private class Meta { public FrameRect size; public Anchor anchor; }
        [Serializable] private class Anchor { public int x, y; }

        [MenuItem("Tools/Skills/Build Execution")]
        public static void Apply() => Build(true);

        [MenuItem("Tools/Skills/Refresh Execution Art")]
        public static void RefreshArt() => Build(false);

        private static void Build(bool rebuildGameplay)
        {
            ImportTexture(Root + "ExecutionIcon.png", false);
            ImportTexture(Root + "ExecutionThrust.png", true);
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(Root + "ExecutionThrust.png")
                .OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
            if (frames.Length != VfxFrameCount) throw new InvalidOperationException("Execution requires the nine PixelLab VFX frames.");

            var clip = Load<AnimationClip>(Root + "ExecutionThrust.anim");
            clip.frameRate = VfxFps;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / VfxFps, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / VfxFps, value = frames[frames.Length - 1] });
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            string controllerPath = Root + "ExecutionThrust.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "ExecutionThrust.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "ExecutionThrust.mat");
            }
            GameObject prefab;
            var host = new GameObject("Execution thrust");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "ExecutionThrust.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            if (!rebuildGameplay)
            {
                var existingSkill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
                existingSkill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ExecutionIcon.png");
                var visualStep = existingSkill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnVFX");
                visualStep.Overrides.RemoveAll(value => value.Key == "VfxPrefab");
                visualStep.Overrides.Add(Obj("VfxPrefab", prefab));
                visualStep.EndPercentPipeline = .36f;
                EditorUtility.SetDirty(existingSkill);
                EditorUtility.SetDirty(existingSkill.Recipe);
                AssetDatabase.SaveAssets();
                return;
            }

            var hit = Step("DealDamageRectangle", .14f, .14f, Float("SizeX", 2.2f), Float("SizeY", 1f),
                Float("OffsetX", 1.2f), Float("DamageMultiplier", 1f), Bool("PullTowardOwner", false));
            hit.ScopedStatModifiers.Add(new SerializableStatModifier { Stat = StatType.ExecutionThreshold, Type = StatModType.Flat, Value = 10 });
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_Execution.asset");
            recipe.HitStopFrames = 3; recipe.IsChanneling = false;
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, .45f), Step("WeaponWindup", 0, .08f), Step("WeaponStrike", .08f, .08f),
                Step("SpawnVFX", .08f, .36f, Obj("VfxPrefab", prefab), Bool("AttachToParent", true),
                    Bool("FadeOutEnabled", true), Float("FadeOutStartLifePercent", .85f), Float("FadeStartAlphaMultiplier", 1)),
                hit, Step("WeaponRecovery", .14f, .45f)
            };
            EditorUtility.SetDirty(recipe);
            var source = AssetDatabase.LoadAssetAtPath<SkillDataSO>(NinjaStubBuilder.Root + "VenomStrike/VenomStrikeSkill.asset");
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "ExecutionSkill"; skill.SkillName = "Execution";
            skill.NameKey = "skills.ExecutionSkill"; skill.DescriptionKey = "skills.ExecutionSkill.description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 0; skill.ManaCost = 0; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ExecutionIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(NinjaStubBuilder.Root + "1HDaggerSkillPool.asset");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill))
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);

            foreach (string code in new[] { "en", "ru" })
            {
                SetText("SkillsLabels", code, skill.NameKey, code == "en" ? "Execution" : "Казнь");
                SetText("SkillsLabels", code, skill.DescriptionKey, code == "en"
                    ? "Stab in front of you. This hit has +10% Execution Threshold, added to your equipment. Enemies left below the threshold are instantly killed."
                    : "Удар кинжалом перед собой. Это попадание даёт +10% к порогу казни в дополнение к экипировке. Враги, чьё здоровье после удара ниже порога, мгновенно погибают.");
                SetText("MenuLabels", code, "stats.ExecutionThreshold", code == "en" ? "Execution Threshold" : "Порог казни");
            }
            var stats = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            stats.GetOrCreateEntry(StatType.ExecutionThreshold);
            EditorUtility.SetDirty(stats);
            AffixSetGenerator.GenerateSetsForStats(new HashSet<StatType> { StatType.ExecutionThreshold }, stats,
                AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase),
                AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels),
                AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable), EditorPaths.AffixesBaseFolder);
            var affixPool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>(AffixPoolPath);
            var medium = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(EditorPaths.AffixesBaseFolder + "/ByStat/Combat/ExecutionThreshold/ExecutionThreshold_Flat_Medium.asset");
            if (medium == null) throw new InvalidOperationException("Execution affix family was not generated.");
            if (!affixPool.Affixes.Contains(medium)) affixPool.Affixes.Add(medium);
            EditorUtility.SetDirty(affixPool);
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(EditorPaths.ItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            foreach (string guid in AssetDatabase.FindAssets("t:ItemAffixSO", new[] { EditorPaths.AffixesBaseFolder + "/ByStat/Combat/ExecutionThreshold" }))
            {
                var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (!database.AllAffixes.Contains(affix)) database.AllAffixes.Add(affix);
            }
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        private static void SetText(string collection, string locale, string key, string value)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{collection}_{locale}.asset");
            table.AddEntry(key, value); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
        }

        private static void ImportTexture(string path, bool sheet)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + "ExecutionThrust.json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData
                {
                    name = "Execution_" + index.ToString("D2"),
                    rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2((float)data.meta.anchor.x / frame.frame.w, 1f - (float)data.meta.anchor.y / frame.frame.h)
                }).ToArray();
            }
            importer.SaveAndReimport();
        }

        private static T Load<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(value, path); return value;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, FloatVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
        private static StepParamValue Obj(string key, Object value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Object, ObjectVal = value };
        private static StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(EditorPaths.StepDefinitionsFolder + "/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
        };
    }
}
