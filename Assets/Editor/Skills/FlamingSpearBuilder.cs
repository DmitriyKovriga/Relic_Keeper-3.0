using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
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
    public static class FlamingSpearBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Fire/FlamingSpear/";
        public const string SkillPath = Root + "FlamingSpearSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Fire/Adventurers/2HStaffSkillPoolRBM.asset";
        [MenuItem("Tools/Skills/Build Flaming Spear")]
        public static void Apply()
        {
            Import("FlamingSpearIcon", false); Import("Flight", true); Import("Impact", true);
            var flight = Frames("Flight"); var impactFrames = Frames("Impact");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FlamingSpear.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "FlamingSpear.mat");
            }
            var host = new GameObject("Flaming Spear impact"); GameObject impact, projectile;
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = impactFrames[0]; renderer.sharedMaterial = material;
                host.transform.localScale = Vector3.one * .65f;
                var settings = new SerializedObject(host.AddComponent<ManaSpearVisual>()); SetFrames(settings, impactFrames);
                settings.FindProperty("_framesPerSecond").floatValue = 30; settings.FindProperty("_loop").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo(); impact = PrefabUtility.SaveAsPrefabAsset(host, Root + "Impact.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            host = new GameObject("Flaming Spear projectile");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = flight[0]; renderer.sharedMaterial = material;
                host.AddComponent<CircleCollider2D>().isTrigger = true;
                var body = host.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                host.AddComponent<SkillProjectile>();
                var art = new GameObject("Animated spear").AddComponent<SpriteRenderer>(); art.transform.SetParent(host.transform, false);
                art.transform.localScale = Vector3.one * .65f; art.sprite = flight[0]; art.sharedMaterial = material;
                var settings = new SerializedObject(host.AddComponent<ManaSpearVisual>()); SetFrames(settings, flight);
                settings.FindProperty("_art").objectReferenceValue = art; settings.FindProperty("_impactPrefab").objectReferenceValue = impact;
                settings.FindProperty("_framesPerSecond").floatValue = 20; settings.FindProperty("_loop").boolValue = true; settings.ApplyModifiedPropertiesWithoutUndo();
                projectile = PrefabUtility.SaveAsPrefabAsset(host, Root + "Projectile.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var shot = Present("SpawnProjectile", .15f, .15f);
            shot.Overrides = new List<StepParamValue>
            {
                Float("DamageMultiplier", 2.5f), Float("IgniteHitDamagePercentPerStack", 20), Float("BaseSpeed", 18), Float("Lifetime", 2), Float("WorldHitRadius", .1f),
                Float("OffsetX", .55f), Float("OffsetY", .35f), Float("RotationDegreesPerSecond", 0),
                Bool("UseProjectileCountStat", false), Bool("UseCurrentWeaponSprite", false), Bool("Homing", false),
                Bool("IgnoreFork", true), Bool("IgnoreChain", true), Bool("InfinitePierce", false), Bool("StopOnWorld", true),
                new StepParamValue { Key = "BaseProjectileCount", Type = StepParamValue.ParamKind.Int, IntVal = 1 },
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            shot.DamageConversions = PrismaticCycleCast.ConvertAllTo(DamageChannel.Fire);
            shot.SetOverrideObject("ProjectilePrefab", projectile);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FlamingSpear.asset");
            recipe.Steps = new List<StepEntry> { Present("MovementLock", 0, .4f), Present("WeaponWindup", 0, .15f), Present("WeaponStrike", .15f, .15f), shot, Present("WeaponRecovery", .15f, .4f) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Staff/Fire/Adventurers/BigFireBallSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "FlamingSpearSkill"; skill.SkillName = "Flaming Spear";
            skill.NameKey = "skills.FlamingSpearSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId; skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier>();
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FlamingSpearIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Пламенное копьё" : "Flaming Spear");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Выпускает одно длинное огненное копьё, наносящее 250% урона оружия с преобразованием в огонь. Каждый уже действующий на цели Поджог добавляет 20% прямого урона: три эффекта дают +60%. Поджоги не расходуются. Число поджогов позволяет усилить удар, а пробивание снарядов добавляет цели в линию. Бонус определяется отдельно при попадании в каждую цель. Перезарядка: 10 с. Мана: 25."
                    : "Fire one long spear for 250% weapon damage converted to fire. Each Ignite already on the target adds 20% direct hit damage: three effects grant +60%. Ignites remain active. Max Ignite Stacks enables stronger hits; Projectile Pierce adds targets along the line. The bonus is checked separately on each target at impact. Cooldown: 10 seconds. Mana: 25.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        private static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
        private static void SetFrames(SerializedObject settings, Sprite[] frames)
        {
            var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
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
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData
                {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = frame.pivot != null ? new Vector2(frame.pivot.x, frame.pivot.y) : new Vector2(.5f, .5f)
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
