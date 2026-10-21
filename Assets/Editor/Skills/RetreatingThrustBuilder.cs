using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class RetreatingThrustBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Sword/Adventurer/RetreatingThrust/";
        public const string SkillPath = Root + "RetreatingThrustSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Sword/Adventurer/2HSwordSkillRBPool.asset";
        [MenuItem("Tools/Skills/Build Retreating Thrust")]
        public static void Apply()
        {
            Import("RetreatingThrustIcon", false); Import("Thrust", true); Import("Dust", true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "RetreatingThrust.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "RetreatingThrust.mat");
            }
            GameObject Visual(string name, float fps)
            {
                var frames = AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
                var host = new GameObject("Retreating Thrust " + name);
                try
                {
                    var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                    var visual = host.AddComponent<RetreatingThrustVisual>(); var settings = new SerializedObject(visual);
                    settings.FindProperty("_framesPerSecond").floatValue = fps;
                    var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    return PrefabUtility.SaveAsPrefabAsset(host, Root + name + ".prefab");
                }
                finally { Object.DestroyImmediate(host); }
            }
            var thrust = Visual("Thrust", 30); var dust = Visual("Dust", 24);
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_RetreatingThrust.asset");
            definition.Id = "RetreatingThrust"; definition.NameEn = "Retreating Thrust"; definition.NameRu = "Укол с отходом";
            definition.DescriptionEn = "Long narrow thrust, immediately followed by a backward hop without invulnerability.";
            definition.DescriptionRu = "Длинный узкий укол и немедленный прыжок назад без неуязвимости.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("DamageMultiplier", 2.5f), Float("Range", 3.2f), Float("Width", .45f), Float("RetreatAngle", 150), Float("RetreatForce", 8),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            }; EditorUtility.SetDirty(definition);
            var hit = new StepEntry { StepDefinition = definition, StartPercentPipeline = .3f, EndPercentPipeline = .3f };
            hit.SetOverrideObject("VfxPrefab", thrust); hit.SetOverrideObject("RetreatVfxPrefab", dust);
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_RetreatingThrust.asset");
            recipe.Steps = new List<StepEntry> { Present("MovementLock", 0, 1), Present("WeaponWindup", 0, .3f), Present("WeaponStrike", .3f, .3f), hit, Present("WeaponRecovery", .3f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "RetreatingThrustSkill"; skill.SkillName = "Retreating Thrust";
            skill.NameKey = "skills.RetreatingThrustSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 5; skill.ManaCost = 10;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1.5f;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.Evasion, SourcePercent = 1, TargetStat = StatType.CritChance, TargetModifierType = StatModType.Flat } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "RetreatingThrustIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Укол с отходом" : "Retreating Thrust");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Длинный узкий укол наносит 250% урона оружия на расстоянии до 3,2 м, после чего герой немедленно отпрыгивает назад. Добавляет 1 процентный пункт к шансу критического удара этого умения за каждые 100 уклонения. Работает с множителем критического урона оружия. Отход не даёт неуязвимости и останавливается препятствиями."
                    : "Thrust in a long narrow line for 250% weapon damage, reaching up to 3.2 metres, then immediately hop backward. Gain 1 percentage point of critical hit chance for this skill per 100 Evasion. Uses the weapon's critical multiplier. The retreat grants no invulnerability and respects obstacles.");
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
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData
                {
                    name = name + "_" + index.ToString("D2"),
                    rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = name == "Thrust" ? new Vector2(44f / 96, .5f) : new Vector2(frame.pivot.x, frame.pivot.y)
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
    }
}
