using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class BucklerParryBuilder
    {
        public const string BucklerPath = "Assets/Scripts/Items/Shields/LowTier/Evasion/Adventurer's Buckler/Adventurer's Buckler.asset";
        public const string SkillPath = "Assets/Resources/Skills/Shields/SpectacularParry.asset";
        private const string Root = "Assets/Resources/Skills/Shields/";

        [MenuItem("Tools/Skills/Build Buckler and Spectacular Parry")]
        public static void Apply()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory("Assets/Resources/Affixes/Pools/Shields");
            AssetDatabase.Refresh();
            var held = ImportSprite("Docs/Art/Shields/BucklerHeldSource.png", Root + "BucklerHeld.png", false);
            var icon = ImportSprite("Docs/Art/Shields/SpectacularParryIcon.png", Root + "SpectacularParryIcon.png", false);
            ImportSprite("Docs/Art/Shields/ParryVfx-Sheet.png", Root + "ParryVfx-Sheet.png", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "ParryVfx-Sheet.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var clip = LoadOrCreate<AnimationClip>(Root + "ParryVfx.anim");
            clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((s, i) => new ObjectReferenceKeyframe { time = i / 24f, value = s }).ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);
            string controllerPath = Root + "ParryVfx.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var vfx = new GameObject("Spectacular Parry VFX");
            GameObject prefab;
            try
            {
                var renderer = vfx.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                vfx.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(vfx, Root + "SpectacularParryVFX.prefab");
            }
            finally { Object.DestroyImmediate(vfx); }

            var poses = WeaponStancePoseTableSO.LoadDefault();
            var guard = new WeaponVisualController.StancePose
            {
                Id = "shield_guard", DisplayName = "Shield Guard", DefaultSwingId = "shield_parry",
                Stance = WeaponHoldStance.Default, LocalPosition = new Vector2(0.5f, -0.03f), LocalEulerZ = 0
            };
            if (poses.IndexOfId(guard.Id) < 0) poses.AddPose(guard);
            else poses.SetPoseAt(poses.IndexOfId(guard.Id), guard);
            EditorUtility.SetDirty(poses);
            var swings = WeaponSwingStyleTableSO.LoadDefault();
            if (!swings.TryGetById("shield_parry", out _)) swings.Add(new WeaponSwingStyleTableSO.SwingKeyframes
            {
                Id = "shield_parry", DisplayName = "Shield Parry", Style = WeaponSwingStyle.Slash,
                AllowedStanceIds = new[] { "shield_guard" }, WindupZ = -14,
                WindupLocalPos = new Vector2(-0.08f, 0.06f), ImpactZ = 15, ImpactLocalPos = new Vector2(0.1f, 0.02f)
            });
            EditorUtility.SetDirty(swings);

            var definition = LoadOrCreate<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_ParryWindow.asset");
            definition.Id = "ParryWindow";
            definition.NameEn = "Parry Window"; definition.NameRu = "Окно парирования";
            definition.DescriptionEn = "Absorb every direct enemy hit in this window. Retaliation targets that hit's attacker.";
            definition.DescriptionRu = "Поглощает все прямые удары врагов в течение окна. Ответный эффект получает атакующий.";
            definition.DefaultParams = new List<StepParamValue> { Float("StunPercentOfMax", 25), Bool("UseBossStunPercent", false), Float("BossStunPercentOfMax", 25), Float("StunDamage", 0), Float("HealthDamage", 0), Float("HealthPercentOfMax", 0) };
            EditorUtility.SetDirty(definition);
            var recipe = LoadOrCreate<SkillRecipeSO>(Root + "SpectacularParryRecipe.asset");
            recipe.Steps = new List<StepEntry>
            {
                Step("WeaponWindup", 0, 0.18f), Step("WeaponStrike", 0.18f, 0.18f),
                new StepEntry { StepDefinition = definition, StartPercentPipeline = 0.18f, EndPercentPipeline = 0.58f,
                    Overrides = new List<StepParamValue> { Float("StunPercentOfMax", 100), Bool("UseBossStunPercent", true), Float("BossStunPercentOfMax", 25) } },
                Step("SpawnVFX", 0.18f, 0.58f, new StepParamValue { Key = "VfxPrefab", Type = StepParamValue.ParamKind.Object, ObjectVal = prefab },
                    Float("OffsetX", 0.34f), Float("OffsetY", 0), Bool("AttachToParent", true), Bool("FadeOutEnabled", true),
                    Float("FadeOutStartLifePercent", 0.5f), Float("FadeStartAlphaMultiplier", 1)),
                Step("WeaponRecovery", 0.58f, 0.8f), Step("Wait", 0.8f, 1)
            };
            EditorUtility.SetDirty(recipe);
            var skill = LoadOrCreate<SkillDataSO>(SkillPath);
            skill.ID = "SpectacularParry"; skill.SkillName = "Spectacular Parry";
            skill.NameKey = "skills.SpectacularParry.name";
            skill.DescriptionKey = "skills.SpectacularParry.description";
            skill.IsActive = true; skill.Icon = icon; skill.Recipe = recipe;
            skill.Cooldown = 4; skill.ManaCost = 10; skill.SkillSpeedMultiplier = 1;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.HoldStanceId = "shield_guard"; skill.SwingStyleId = "shield_parry";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            EditorUtility.SetDirty(skill);
            var skillPool = LoadOrCreate<SkillPoolSO>(Root + "BucklerSkillPool.asset");
            skillPool.PossibleSkills = new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 } };
            EditorUtility.SetDirty(skillPool);
            var helmetPool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>("Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/HelmetsEvasionPool.asset");
            var pool = LoadOrCreate<AffixPoolSO>("Assets/Resources/Affixes/Pools/Shields/BucklerDefensivePool.asset");
            pool.Slot = EquipmentSlot.OffHand; pool.DefenseType = ArmorDefenseType.Evasion;
            pool.Affixes = new List<ItemAffixSO>(helmetPool.Affixes);
            EditorUtility.SetDirty(pool);
            var buckler = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(BucklerPath);
            buckler.InHandSprite = held; buckler.InHandSpriteTiltZ = 0; buckler.InHandSpriteLocalOffset = Vector2.zero;
            buckler.AttacksPerSecond = 1; buckler.AffixPool = pool; buckler.SkillPool = skillPool;
            EditorUtility.SetDirty(buckler);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            if (collection != null)
            {
                foreach (string locale in new[] { "en", "ru" })
                {
                    var table = collection.GetTable(locale) as StringTable;
                    if (table == null) continue;
                    table.AddEntry(skill.NameKey, locale == "en" ? "Spectacular Parry" : "Эффектное парирование");
                    table.AddEntry(skill.DescriptionKey, "");
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Buckler configured: {pool.Affixes.Count} defensive affixes, Spectacular Parry, independent shield stance and swing.");
        }

        private static T LoadOrCreate<T>(string path) where T : Object
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            result = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static Sprite ImportSprite(string source, string target, bool sheet)
        {
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            if (sheet) importer.spritesheet = Enumerable.Range(0, 9).Select(i => new SpriteMetaData
            { name = "Parry_" + i.ToString("D2"), rect = new Rect(i * 32, 0, 32, 32), alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f }).ToArray();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(target);
        }

        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, FloatVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
        private static StepEntry Step(string id, float start, float end, params StepParamValue[] args) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end, Overrides = args.ToList()
        };
    }
}
