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
    public static class NeedlePrickBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Dagger/NeedlePrick/";
        public const string SkillPath = Root + "NeedlePrickSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/1HWeapon/Dagger/1HDaggerSkillPool.asset";
        [MenuItem("Tools/Skills/Build Needle Prick")]
        public static void Apply()
        {
            Import("NeedlePrickIcon", false); Import("Prick", true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "NeedlePrick.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "NeedlePrick.mat");
            }
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "Prick.png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
            var host = new GameObject("Needle Prick"); GameObject prefab;
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                var visual = host.AddComponent<NeedlePrickVisual>(); var settings = new SerializedObject(visual);
                var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Prick.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_NeedlePrick.asset");
            definition.Id = "NeedlePrick"; definition.NameEn = "Needle Prick"; definition.NameRu = "Игольный укол";
            definition.DescriptionEn = "One very short, fast stab against the nearest enemy in front.";
            definition.DescriptionRu = "Один быстрый короткий укол ближайшего врага перед героем.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("DamageMultiplier", .75f), Float("Range", .9f), Float("Width", .5f),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            }; EditorUtility.SetDirty(definition);
            var hit = new StepEntry { StepDefinition = definition, StartPercentPipeline = .2f, EndPercentPipeline = .2f };
            hit.SetOverrideObject("VfxPrefab", prefab);
            hit.ScopedStatModifiers = new List<SerializableStatModifier> { new SerializableStatModifier { Stat = StatType.CritMultiplier, Value = 100, Type = StatModType.Flat } };
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_NeedlePrick.asset");
            recipe.Steps = new List<StepEntry> { Present("MovementLock", 0, 1), Present("WeaponWindup", 0, .2f), Present("WeaponStrike", .2f, .2f), hit, Present("WeaponRecovery", .2f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "NeedlePrickSkill"; skill.SkillName = "Needle Prick";
            skill.NameKey = "skills.NeedlePrickSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 2;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.CritChance, SourcePercent = 200, TargetStat = StatType.CritMultiplier, TargetModifierType = StatModType.Flat } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "NeedlePrickIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Игольный укол" : "Needle Prick");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Один очень короткий укол ближайшего врага перед героем: 75% урона оружия и удвоенная скорость атаки. Дальность — 0,9 м. Это умение получает +100 процентных пунктов к множителю критического удара и ещё +2 пункта за каждый 1% шанса критического удара. Требуется держаться вплотную к цели."
                    : "One very short stab at the nearest enemy in front: 75% weapon damage at twice attack speed. Reach: 0.9 metres. This skill gains +100 percentage points of critical multiplier, plus another +2 points per 1% critical hit chance. Stay close to the target.");
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
    }
}
