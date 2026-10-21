using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class TectonicPressBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/TectonicPress/";
        public const string SkillPath = Root + "TectonicPressSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Hammer/Adventurer/2HHammerSkillPoolRKM.asset";
        [MenuItem("Tools/Skills/Build Tectonic Press")]
        public static void Apply()
        {
            Import("TectonicPressIcon", false); Import("Strike", true); Import("Shockwave", true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "TectonicPress.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "TectonicPress.mat");
            }
            GameObject Visual(string name)
            {
                var frames = AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
                var host = new GameObject("Tectonic Press " + name);
                try
                {
                    var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                    var visual = host.AddComponent<TectonicPressVisual>();
                    var settings = new SerializedObject(visual); var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                    if (name == "Shockwave") settings.FindProperty("_referenceSize").vector2Value = new Vector2(62f / 24, 44f / 24);
                    for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    return PrefabUtility.SaveAsPrefabAsset(host, Root + name + ".prefab");
                }
                finally { Object.DestroyImmediate(host); }
            }
            var strike = Visual("Strike"); var shockwave = Visual("Shockwave");
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_TectonicPress.asset");
            definition.Id = "TectonicPress"; definition.NameEn = "Tectonic Press"; definition.NameRu = "Тектонический пресс";
            definition.DescriptionEn = "Empowered knockback: each pushed target creates one shockwave on a wall or enemy collision.";
            definition.DescriptionRu = "Усиленное отталкивание: каждая отброшенная цель создаёт одну ударную волну при столкновении со стеной или врагом.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("DamageMultiplier", 2.5f), Float("ShockwaveDamageMultiplier", 2), Float("ShockwaveRadius", 1.5f),
                Float("CollisionRadius", .12f), Float("SizeX", 2.4f), Float("SizeY", 1.5f), Float("OffsetX", 1.1f), Float("OffsetY", .35f),
                new StepParamValue { Key = "PushbackInFacingDirection", Type = StepParamValue.ParamKind.Bool, BoolVal = true },
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            }; EditorUtility.SetDirty(definition);
            var hit = new StepEntry { StepDefinition = definition, StartPercentPipeline = .35f, EndPercentPipeline = .35f };
            hit.SetOverrideObject("VfxPrefab", strike); hit.SetOverrideObject("ShockwaveVfxPrefab", shockwave);
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_TectonicPress.asset");
            recipe.Steps = new List<StepEntry> { Present("WeaponWindup", 0, .35f), Present("WeaponStrike", .35f, .35f), hit, Present("WeaponRecovery", .35f, 1) };
            recipe.HitStopFrames = 3; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = AssetDatabase.LoadAssetAtPath<SkillDataSO>(FallingAnvilBuilder.SkillPath);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "TectonicPressSkill"; skill.SkillName = "Tectonic Press";
            skill.NameKey = "skills.TectonicPressSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = WeaponSwingStyle.OverheadStab; skill.SwingStyleId = "overhead_stab";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area;
            skill.EnablePushback = true; skill.PushbackRating = 1500; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "TectonicPressIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            var bases = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
            bases.SetValue(StatType.ShockwaveDamage, 100); EditorUtility.SetDirty(bases);
            var stats = AssetDatabase.LoadAssetAtPath<StatsDatabaseSO>("Assets/Resources/Databases/StatsDatabase.asset");
            var meta = stats.GetOrCreateEntry(StatType.ShockwaveDamage);
            meta.AffixGenType = StatAffixGenType.FullCalcStat; meta.AllowedAffixKinds = StatAffixModifierKindFlags.Full;
            EditorUtility.SetDirty(stats);
            foreach (string code in new[] { "en", "ru" })
            {
                bool ru = code == "ru";
                SetText("SkillsLabels", code, skill.NameKey, ru ? "Тектонический пресс" : "Tectonic Press");
                SetText("SkillsLabels", code, skill.DescriptionKey, ru
                    ? "Удар наносит 250% урона оружия и сильно отталкивает врагов. При столкновении отброшенной цели со стеной или другим врагом возникает ударная волна с 200% урона оружия. Каждая отброшенная цель создаёт не более одной волны. Отталкивание увеличивает дальность толчка, область действия — радиус столкновения и волны. Урон ударной волны масштабирует только волну; базовое значение — 100%."
                    : "Strike for 250% weapon damage with empowered knockback. A pushed target colliding with a wall or another enemy generates a shockwave dealing 200% weapon damage, once per pushed target. Pushback increases travel; Area of Effect expands collision and shockwave radii. Shockwave Damage scales only the wave and starts at 100%.");
                SetText("MenuLabels", code, "stats.ShockwaveDamage", ru ? "Урон ударной волны" : "Shockwave Damage");
            }
            AssetDatabase.SaveAssets();
        }
        private static void SetText(string collection, string code, string key, string value)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/{collection}_{code}.asset");
            table.AddEntry(key, value); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
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
