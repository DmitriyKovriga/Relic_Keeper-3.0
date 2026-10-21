using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class CircularSawBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Chakram/rb/CircularSaw/";
        public const string SkillPath = Root + "CircularSawSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Chakram/rb/ChakramRBSkillPool.asset";
        [MenuItem("Tools/Skills/Build Circular Saw")]
        public static void Apply()
        {
            Import("CircularSawIcon", false); Import("Disk", false); Import("Sparks", true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "CircularSaw.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "CircularSaw.mat");
            }
            var host = new GameObject("Circular Saw"); GameObject disk, sparks;
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Disk.png");
                renderer.sharedMaterial = material;
                host.AddComponent<CircleCollider2D>().isTrigger = true;
                var body = host.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                host.AddComponent<SkillProjectile>();
                disk = PrefabUtility.SaveAsPrefabAsset(host, Root + "Disk.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            host = new GameObject("Circular Saw Sparks");
            try
            {
                var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "Sparks.png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                var visual = host.AddComponent<CircularSawImpactVisual>();
                var settings = new SerializedObject(visual); var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                sparks = PrefabUtility.SaveAsPrefabAsset(host, Root + "Sparks.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var shot = Present("SpawnProjectile", .2f, .2f);
            shot.Overrides = new List<StepParamValue>
            {
                Float("DamageMultiplier", 2.5f), Float("BaseSpeed", 10), Float("Lifetime", 8), Float("OutboundRange", 8),
                Float("SawPauseDuration", 2.5f), Float("SawPulseInterval", .25f), Int("SawPulseLimit", 10), Float("SawPulseDamageMultiplier", .5f),
                Float("OffsetX", .55f), Float("OffsetY", .4f), Float("HitRadius", .85f), Float("SizeX", 1), Float("SizeY", 1),
                Float("RotationDegreesPerSecond", -1080), Float("ReturnDamagePercent", 50),
                Bool("CircularSaw", true), Bool("UseProjectileCountStat", false), Int("BaseProjectileCount", 1),
                Bool("UseCurrentWeaponSprite", false), Bool("InfinitePierce", true), Bool("IgnoreFork", true), Bool("IgnoreChain", true),
                Bool("StopOnWorld", true), Bool("Homing", false), Int("ReversalCount", 1), Int("ReversalMode", (int)SkillProjectileReversalMode.HomeToOwner),
                Int("WorldLayerMask", 1 << 6), Bool("ClearHitHistoryOnReverse", true)
            };
            shot.SetOverrideObject("ProjectilePrefab", disk); shot.SetOverrideObject("SawImpactPrefab", sparks);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_CircularSaw.asset");
            recipe.Steps = new List<StepEntry> { Present("WeaponWindup", 0, .2f), Present("WeaponStrike", .2f, .2f), shot, Present("WeaponRecovery", .2f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Chakram/lb/HomingWeaponThrow");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "CircularSawSkill"; skill.SkillName = "Circular Saw";
            skill.NameKey = "skills.CircularSawSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 5; skill.ManaCost = 10;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "CircularSawIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Циркулярная пила" : "Circular Saw");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Один диск наносит 250% урона оружия. При попадании он останавливается на 2,5 с и пилит на месте: до 10 повторных касаний каждые 0,25 с с 50% урона умения. Враг может выйти из зоны. Затем диск возвращается к герою, нанося 50% обычного урона. Длительность эффекта продлевает остановку, но не лимит касаний. Скорость снарядов ускоряет полёт; число снарядов не добавляет диски."
                    : "One disk strikes for 250% weapon damage. On contact it stops in place for 2.5 seconds, sawing up to 10 times at 0.25-second intervals for 50% of the skill's damage. Enemies can leave the saw. It then returns to the character, dealing 50% of normal damage. Effect Duration extends the pause, not the hit cap. Projectile Speed speeds up travel; Projectile Count adds no disks.");
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
        private static StepParamValue Int(string key, int value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Int, IntVal = value };
        private static StepParamValue Bool(string key, bool value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Bool, BoolVal = value };
    }
}
