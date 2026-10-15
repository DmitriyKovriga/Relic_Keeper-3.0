using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine;

namespace RelicKeeper.Editor.VFX
{
    public static class MysticShieldRepriseBuilder
    {
        public const string ItemPath = "Assets/Scripts/Items/Shields/LowTier/MS/Adventurer's Mystic Shield/Adventurer's Mystic Shield.asset";
        public const string SkillPath = "Assets/Resources/Skills/Shields/ArcaneReprise.asset";
        private const string Root = "Assets/Resources/Skills/Shields/";

        [MenuItem("Tools/Skills/Build Mystic Shield and Arcane Reprise")]
        public static void Apply()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory("Assets/Resources/Affixes/Pools/Shields");
            AssetDatabase.Refresh();
            var held = ImportSprite("Docs/Art/Shields/MysticShieldHeldSource.png", Root + "MysticShieldHeld.png");
            var icon = ImportSprite("Docs/Art/Shields/ArcaneRepriseIcon.png", Root + "ArcaneRepriseIcon.png");
            var vfx = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "SpectacularParryVFX.prefab"));
            GameObject prefab;
            try
            {
                vfx.name = "Arcane Reprise Pulse";
                vfx.GetComponent<SpriteRenderer>().color = new Color(0.78f, 0.55f, 1f, 1f);
                prefab = PrefabUtility.SaveAsPrefabAsset(vfx, Root + "ArcaneRepriseVFX.prefab");
            }
            finally { Object.DestroyImmediate(vfx); }

            var recipe = LoadOrCreate<SkillRecipeSO>(Root + "ArcaneRepriseRecipe.asset");
            recipe.HitStopFrames = 0;
            recipe.Steps = new List<StepEntry>
            {
                Step("ModifyCooldown", 0, 0, Float("Seconds", 5),
                    new StepParamValue { Key = "TargetMode", Type = StepParamValue.ParamKind.Int, IntVal = 2 },
                    Bool("AddInsteadOfReduce", false), Bool("ScaleByHitCount", false),
                    new StepParamValue { Key = "MinHitCount", Type = StepParamValue.ParamKind.Int, IntVal = 0 }),
                Step("WeaponWindup", 0, 0.12f), Step("WeaponStrike", 0.12f, 0.12f),
                Step("SpawnVFX", 0.12f, 0.58f,
                    new StepParamValue { Key = "VfxPrefab", Type = StepParamValue.ParamKind.Object, ObjectVal = prefab },
                    Float("OffsetX", 0.25f), Float("OffsetY", 0), Bool("AttachToParent", true),
                    Bool("FadeOutEnabled", true), Float("FadeOutStartLifePercent", 0.55f), Float("FadeStartAlphaMultiplier", 1)),
                Step("WeaponRecovery", 0.58f, 0.8f), Step("Wait", 0.8f, 1)
            };
            EditorUtility.SetDirty(recipe);
            var skill = LoadOrCreate<SkillDataSO>(SkillPath);
            skill.ID = "ArcaneReprise"; skill.SkillName = "Arcane Reprise";
            skill.NameKey = "skills.ArcaneReprise.name";
            skill.DescriptionKey = "skills.ArcaneReprise.description";
            skill.IsActive = true; skill.Icon = icon; skill.Recipe = recipe;
            skill.Cooldown = 10; skill.ManaCost = 10; skill.SkillSpeedMultiplier = 1;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell;
            skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.HoldStanceId = "shield_guard"; skill.SwingStyleId = "shield_parry";
            skill.DescriptionMode = SkillDescriptionMode.Automatic;
            EditorUtility.SetDirty(skill);
            var skillPool = LoadOrCreate<SkillPoolSO>(Root + "MysticShieldSkillPool.asset");
            skillPool.PossibleSkills = new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 100 } };
            EditorUtility.SetDirty(skillPool);
            var helmetPool = AssetDatabase.LoadAssetAtPath<AffixPoolSO>("Assets/Resources/Affixes/Pools/Helmets/Armore/Pool/HelmetsMysticPool.asset");
            var pool = LoadOrCreate<AffixPoolSO>("Assets/Resources/Affixes/Pools/Shields/MysticShieldDefensivePool.asset");
            pool.Slot = EquipmentSlot.OffHand; pool.DefenseType = ArmorDefenseType.MysticShield;
            pool.Affixes = new List<ItemAffixSO>(helmetPool.Affixes);
            EditorUtility.SetDirty(pool);
            var shield = AssetDatabase.LoadAssetAtPath<WeaponItemSO>(ItemPath);
            shield.InHandSprite = held; shield.InHandSpriteTiltZ = 0; shield.InHandSpriteLocalOffset = Vector2.zero;
            shield.AttacksPerSecond = 1; shield.AffixPool = pool; shield.SkillPool = skillPool;
            EditorUtility.SetDirty(shield);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            if (collection != null)
            {
                foreach (string locale in new[] { "en", "ru" })
                {
                    var table = collection.GetTable(locale) as StringTable;
                    if (table == null) continue;
                    table.AddEntry(skill.NameKey, locale == "en" ? "Arcane Reprise" : "Мистический отклик");
                    table.AddEntry(skill.DescriptionKey, "");
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Mystic Shield configured: {pool.Affixes.Count} defensive affixes, Arcane Reprise, reduces other skills cooldowns by 5 seconds.");
        }

        private static T LoadOrCreate<T>(string path) where T : Object
        {
            var result = AssetDatabase.LoadAssetAtPath<T>(path);
            if (result != null) return result;
            result = ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static Sprite ImportSprite(string source, string target)
        {
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
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
