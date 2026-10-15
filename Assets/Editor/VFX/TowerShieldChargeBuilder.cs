using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.VFX
{
    public static class TowerShieldChargeBuilder
    {
        public const string TowerPath = "Assets/Scripts/Items/Shields/LowTier/Armore/Adventurer's Tower Shield/Adventurer's Tower Shield.asset";
        public const string SkillPath = "Assets/Resources/Skills/Shields/TowerCharge.asset";
        private const string Root = "Assets/Resources/Skills/Shields/";

        [MenuItem("Tools/Skills/Build Tower Shield and Charge")]
        public static void Apply()
        {
            Directory.CreateDirectory(Root);
            var held = Import("Docs/Art/Shields/TowerShieldHeldSource.png", Root + "TowerShieldHeld.png");
            var icon = Import("Docs/Art/Shields/TowerChargeIcon.png", Root + "TowerChargeIcon.png");
            var poses = WeaponStancePoseTableSO.LoadDefault();
            int accidental = poses.IndexOfId("shield");
            if (accidental >= 0 && !poses.RemoveAt(accidental))
                throw new System.InvalidOperationException("Could not remove accidental shield stance.");
            EditorUtility.SetDirty(poses);
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDataSO", new[] { "Assets" }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<SkillDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing.HoldStanceId != "shield") continue;
                existing.HoldStanceId = "shield_guard";
                EditorUtility.SetDirty(existing);
            }
            var swings = WeaponSwingStyleTableSO.LoadDefault();
            if (!swings.TryGetById("shield_charge", out _)) swings.Add(new WeaponSwingStyleTableSO.SwingKeyframes
            {
                Id = "shield_charge", DisplayName = "Shield Charge", Style = WeaponSwingStyle.Slash,
                AllowedStanceIds = new[] { "shield_guard" }, WindupZ = -8,
                WindupLocalPos = new Vector2(-0.1f, 0.02f), ImpactZ = 0,
                ImpactLocalPos = new Vector2(0.16f, -0.04f)
            });
            EditorUtility.SetDirty(swings);
            var charge = Get<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_PlayerCharge.asset");
            charge.Id = "PlayerCharge"; charge.NameEn = "Forward Charge"; charge.NameRu = "Рывок вперёд";
            charge.DescriptionEn = "Maintain forward motion during this window. Walls still block movement; enemies do not. Distance is independent of attack speed.";
            charge.DescriptionRu = "Рывок на заданную дистанцию в течение окна. Стены блокируют движение; враги — нет.";
            charge.DefaultParams = new List<StepParamValue> { Float("Distance", 3) };
            EditorUtility.SetDirty(charge);
            var damageDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_PersistentDamageRectangle.asset");
            if (!damageDefinition.DefaultParams.Any(p => p.Key == "SweepBetweenSamples"))
            {
                damageDefinition.DefaultParams.Add(Bool("SweepBetweenSamples", false));
                EditorUtility.SetDirty(damageDefinition);
            }
            // The override is opt-in; other attacks retain their normal visibility behavior.
            var dustFrames = AssetDatabase.LoadAllAssetsAtPath("Assets/Resources/VFX/Grounds/Dust_Dash_Small-Sheet.png")
                .OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (dustFrames.Length == 0) throw new System.InvalidOperationException("Missing charge dust sprites.");
            var clip = Get<AnimationClip>(Root + "TowerChargeDust.anim");
            clip.frameRate = 18;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                dustFrames.Select((s, i) => new ObjectReferenceKeyframe { time = i / 18f, value = s }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "TowerChargeDust.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var vfxObject = new GameObject("Tower Charge Dust");
            GameObject dust;
            try
            {
                var renderer = vfxObject.AddComponent<SpriteRenderer>(); renderer.sprite = dustFrames[0];
                renderer.color = new Color(0.85f, 0.79f, 0.66f, 1);
                vfxObject.AddComponent<Animator>().runtimeAnimatorController = controller;
                dust = PrefabUtility.SaveAsPrefabAsset(vfxObject, Root + "TowerChargeDust.prefab");
            }
            finally { Object.DestroyImmediate(vfxObject); }
            var recipe = Get<SkillRecipeSO>(Root + "TowerChargeRecipe.asset");
            recipe.HitStopFrames = 0; // Continuous motion through multiple enemies.
            recipe.Steps = new List<StepEntry>
            {
                Step("MovementLock", 0, 0.72f), Step("WeaponWindup", 0, 0.16f),
                Step("WeaponStrike", 0.16f, 0.16f, Bool("KeepWeaponVisible", true)),
                new StepEntry { StepDefinition = charge, StartPercentPipeline = 0.16f, EndPercentPipeline = 0.52f },
                Step("PersistentDamageRectangle", 0.16f, 0.52f, Float("SizeX", 0.8f), Float("SizeY", 1.15f),
                    Float("OffsetX", 0.35f), Float("OffsetY", 0), Float("DamageMultiplier", 1),
                    Bool("PullTowardOwner", false), Bool("SweepBetweenSamples", true), Bool("PushbackInFacingDirection", true)),
                Step("SpawnVFX", 0.16f, 0.52f,
                    new StepParamValue { Key = "VfxPrefab", Type = StepParamValue.ParamKind.Object, ObjectVal = dust },
                    Float("OffsetX", -0.45f), Float("OffsetY", -0.45f), Float("ScaleMultiplier", 1.3f),
                    Bool("AttachToParent", true), Float("FadeStartAlphaMultiplier", 0.85f), Float("FadeOutStartLifePercent", 0.72f)),
                Step("WeaponRecovery", 0.52f, 0.72f), Step("Wait", 0.72f, 1)
            };
            EditorUtility.SetDirty(recipe);
            var skill = Get<SkillDataSO>(SkillPath);
            skill.ID = "TowerCharge"; skill.SkillName = "Tower Charge";
            skill.NameKey = "skills.TowerCharge.name"; skill.DescriptionKey = "skills.TowerCharge.description";
            skill.IsActive = true; skill.Icon = icon; skill.Recipe = recipe;
            skill.Cooldown = 5; skill.ManaCost = 20; skill.SkillSpeedMultiplier = 1;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.HoldStanceId = "shield_guard"; skill.SwingStyleId = "shield_charge";
            skill.EnablePushback = true; skill.PushbackRating = 200; skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier
            { SourceStat = StatType.Armor, SourcePercent = 5, TargetStat = StatType.DamagePhysical, TargetModifierType = StatModType.Flat } };
            EditorUtility.SetDirty(skill);
            var skillPool = Get<SkillPoolSO>(Root + "TowerShieldSkillPool.asset");
            skillPool.PossibleSkills ??= new List<SkillPoolSO.SkillWeight>();
            if (!skillPool.PossibleSkills.Any(entry => entry.Skill == skill))
                skillPool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 });
            EditorUtility.SetDirty(skillPool);
            var sourcePool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>("Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/HelmetsArmorePool.asset");
            var pool = Get<AffixPoolSO>("Assets/Resources/Affixes/Pools/Shields/TowerShieldDefensivePool.asset");
            pool.Slot = EquipmentSlot.OffHand; pool.DefenseType = ArmorDefenseType.Armour;
            pool.Affixes = new List<ItemAffixSO>(sourcePool.Affixes); EditorUtility.SetDirty(pool);
            var tower = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(TowerPath);
            tower.InHandSprite = held; tower.InHandSpriteTiltZ = 0; tower.InHandSpriteLocalOffset = Vector2.zero;
            tower.AttacksPerSecond = 1; tower.SkillPool = skillPool; tower.AffixPool = pool; EditorUtility.SetDirty(tower);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            if (collection != null)
            {
                foreach (string locale in new[] { "en", "ru" })
                {
                    var table = collection.GetTable(locale) as StringTable; if (table == null) continue;
                    table.AddEntry(skill.NameKey, locale == "en" ? "Tower Charge" : "Таран");
                    table.AddEntry(skill.DescriptionKey, ""); EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();
            if (File.Exists("Docs/Art/Shields/TowerChargeBody-Sheet.png")) ApplyVisuals();
            Debug.Log($"Tower shield configured: {pool.Affixes.Count} defensive affixes, Tower Charge, 5% current armor as physical base damage.");
        }

        [MenuItem("Tools/Skills/Update Tower Shield Charge Visuals")]
        public static void ApplyVisuals()
        {
            var held = Import("Docs/Art/Shields/TowerShieldHeldSource.png", Root + "TowerShieldHeld.png");
            var tower = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(TowerPath);
            tower.InHandSprite = held;
            EditorUtility.SetDirty(tower);
            string sheetPath = Root + "TowerChargeBody-Sheet.png";
            File.Copy("Docs/Art/Shields/TowerChargeBody-Sheet.png", sheetPath, true);
            AssetDatabase.ImportAsset(sheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var textureSettings = new TextureImporterSettings(); importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(textureSettings);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            importer.spritesheet = Enumerable.Range(0, width / height).Select(i => new SpriteMetaData
            {
                name = "ChargeBody_" + i.ToString("D2"), rect = new Rect(i * height, 0, height, height),
                alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * 0.5f
            }).ToArray();
            importer.SaveAndReimport();
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
            var clip = Get<AnimationClip>(Root + "TowerChargeBody.anim"); clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((s, i) => new ObjectReferenceKeyframe { time = i / 24f, value = s }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "TowerChargeBody.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(controllerPath, clip);
            var visual = new GameObject("Tower Charge Body VFX"); GameObject prefab;
            try
            {
                visual.AddComponent<SpriteRenderer>().sprite = frames[0];
                visual.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(visual, Root + "TowerChargeBody.prefab");
            }
            finally { Object.DestroyImmediate(visual); }
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
            var recipe = skill.Recipe;
            // Replace this visual only; preserve user-authored timing, balance, and other steps.
            recipe.Steps.RemoveAll(s => s.StepDefinition?.Id == "SpawnVFX" &&
                s.GetObject<GameObject>("VfxPrefab")?.name == "TowerChargeBody");
            var charge = recipe.Steps.Find(s => s.StepDefinition?.Id == "PlayerCharge");
            recipe.Steps.Add(Step("SpawnVFX", charge.StartPercentPipeline, charge.EndPercentPipeline,
                new StepParamValue { Key = "VfxPrefab", Type = StepParamValue.ParamKind.Object, ObjectVal = prefab },
                Float("OffsetX", 0.08f), Float("OffsetY", 0), Float("ScaleMultiplier", 0.9f),
                Bool("AttachToParent", true), Float("FadeStartAlphaMultiplier", 0.75f), Float("FadeOutStartLifePercent", 0.75f)));
            EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssets();
            Debug.Log($"Tower shield held model updated; {frames.Length} animated body charge frames attached to the charge window.");
        }

        private static T Get<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset;
            asset = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static Sprite Import(string source, string destination)
        {
            File.Copy(source, destination, true); AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(destination);
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, FloatVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
        private static StepEntry Step(string id, float start, float end, params StepParamValue[] parameters) => new StepEntry
        {
            StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
            StartPercentPipeline = start, EndPercentPipeline = end, Overrides = parameters.ToList()
        };
    }
}
