using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Editor.Affixes;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class ExtractionBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Axe/Extraction/";
        public const string SkillPath = Root + "ExtractionSkill.asset";
        public const int VfxFrameCount = 11;
        public const string AxePath = "Assets/Scripts/Items/Weapon/LowTier/OneHandedWeapon/Axes/Small Adventurer's Axe.asset";

        [Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private class Frame { public FrameRect frame; }
        [Serializable] private class FrameRect { public int x, y, w, h; }
        [Serializable] private class Meta { public FrameRect size; public Anchor anchor; }
        [Serializable] private class Anchor { public int x, y; }

        [MenuItem("Tools/Skills/Build Extraction")]
        public static void Apply()
        {
            ImportTexture(Root + "ExtractionIcon.png", false);
            ImportTexture(Root + "ExtractionHook.png", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "ExtractionHook.png")
                .OfType<Sprite>().OrderBy(frame => frame.name).ToArray();
            if (frames.Length != VfxFrameCount) throw new InvalidOperationException("Extraction requires all eleven PixelLab frames.");
            var clip = Load<AnimationClip>(Root + "ExtractionHook.anim");
            clip.frameRate = 24f;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / 24f, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 24f, value = frames[frames.Length - 1] });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            string controllerPath = Root + "ExtractionHook.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "ExtractionHook.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "ExtractionHook.mat");
            }
            GameObject prefab;
            var host = new GameObject("Extraction hook and rip");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "ExtractionHook.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            var hit = Step("DealDamageRectangle", .34f, .34f,
                Float("SizeX", 1.8f), Float("SizeY", 1.2f), Float("OffsetX", 1f),
                Float("DamageMultiplier", 1f), Bool("PullTowardOwner", false),
                new StepParamValue { Key = "MaxTargets", Type = StepParamValue.ParamKind.Int, IntVal = 1 });
            hit.ScopedStatModifiers.Add(new SerializableStatModifier { Stat = StatType.BleedExtractionChance, Type = StatModType.Flat, Value = 100f });
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_Extraction.asset");
            recipe.HitStopFrames = 3; recipe.IsChanneling = false;
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, .62f), Step("WeaponWindup", 0, .18f),
                Step("WeaponStrike", .18f, .18f),
                Step("SpawnVFX", .16f, .58f, Obj("VfxPrefab", prefab), Bool("AttachToParent", true),
                    Bool("FadeOutEnabled", true), Float("FadeOutStartLifePercent", .9f), Float("FadeStartAlphaMultiplier", 1)),
                hit, Step("WeaponRecovery", .34f, .62f)
            };
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "ExtractionSkill"; skill.SkillName = "Extraction";
            skill.NameKey = "skills.ExtractionSkill"; skill.DescriptionKey = "skills.ExtractionSkill.description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.IsActive = true; skill.Cooldown = 0f; skill.ManaCost = 0f;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1f;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            var source = Resources.Load<SkillDataSO>("Skills/1HWeapon/Axe/WeaponThrowSkill");
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ExtractionIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = Resources.Load<SkillPoolSO>("Skills/1HWeapon/Axe/1HAxeSkillPool");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill))
                pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);

            var bases = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
            bases.SetValue(StatType.BleedExtraction, 50f);
            bases.SetValue(StatType.BleedExtractionChance, 0f);
            EditorUtility.SetDirty(bases);
            foreach (string locale in new[] { "en", "ru" })
            {
                bool ru = locale == "ru";
                SetText("SkillsLabels", locale, skill.NameKey, ru ? "Извлечение" : "Extraction");
                SetText("SkillsLabels", locale, skill.DescriptionKey, ru
                    ? "Зацепите ближайшего врага перед собой топором и вырвите лезвие из раны. Этот удар получает +100% к шансу извлечения кровотечения. Извлечение расходует сильнейшее старое кровотечение и наносит часть оставшегося урона в размере «Извлечения кровотечения». Этот урон не может быть критическим или накладывать состояния. Сам удар может наложить новое кровотечение."
                    : "Hook the nearest enemy in front of you and wrench the axe from the wound. This hit gains +100% Bleed Extraction Chance. Extraction consumes the strongest old bleed to deal Bleed Extraction of its remaining damage. This extra damage cannot crit or inflict ailments. The weapon hit can apply a new bleed.");
                SetText("MenuLabels", locale, "stats.BleedExtraction", ru ? "Извлечение кровотечения" : "Bleed Extraction");
                SetText("MenuLabels", locale, "stats.BleedExtractionChance", ru ? "Шанс извлечения кровотечения" : "Bleed Extraction Chance");
            }
            var stats = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>(EditorPaths.StatsDatabase);
            stats.GetOrCreateEntry(StatType.BleedExtraction);
            stats.GetOrCreateEntry(StatType.BleedExtractionChance);
            EditorUtility.SetDirty(stats);
            AffixSetGenerator.GenerateSetsForStats(new HashSet<StatType> { StatType.BleedExtraction, StatType.BleedExtractionChance }, stats,
                AssetDatabase.LoadAssetAtPath<AffixTagDatabaseSO>(EditorPaths.AffixTagDatabase),
                AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.MenuLabels),
                AssetDatabase.LoadAssetAtPath<StringTableCollection>(EditorPaths.AffixesLabelsTable), EditorPaths.AffixesBaseFolder);
            var axe = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(AxePath);
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>(EditorPaths.ItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            foreach (var type in new[] { StatType.BleedExtraction, StatType.BleedExtractionChance })
            {
                string affixRoot = EditorPaths.AffixesBaseFolder + "/ByStat/Ailments/" + type;
                var medium = AssetDatabase.LoadAssetAtPath<ItemAffixSO>($"{affixRoot}/{type}_Flat_Medium.asset");
                if (medium == null) throw new InvalidOperationException(type + " affixes were not generated.");
                if (!axe.AffixPool.Affixes.Contains(medium)) axe.AffixPool.Affixes.Add(medium);
                foreach (string guid in AssetDatabase.FindAssets("t:ItemAffixSO", new[] { affixRoot }))
                {
                    var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!database.AllAffixes.Contains(affix)) database.AllAffixes.Add(affix);
                }
            }
            EditorUtility.SetDirty(axe.AffixPool);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
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
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + "ExtractionHook.json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData
                {
                    name = "Extraction_" + index.ToString("D2"),
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
        private static void SetText(string collection, string locale, string key, string value)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{collection}_{locale}.asset");
            table.AddEntry(key, value); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
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
