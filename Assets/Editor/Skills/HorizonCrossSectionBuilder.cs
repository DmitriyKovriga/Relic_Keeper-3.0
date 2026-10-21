using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class HorizonCrossSectionBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Sword/HorizonCrossSection/";
        public const string SkillPath = Root + "HorizonCrossSectionSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Sword/Adventurer/2HSwordSkillRBPool.asset";
        [MenuItem("Tools/Skills/Build Horizon Cross-section")]
        public static void Apply()
        {
            Import("HorizonCrossSectionIcon", false); Import("Flight", true); Import("Impact", true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "HorizonCrossSection.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "HorizonCrossSection.mat"); }
            GameObject impact, projectile;
            var host = new GameObject("Horizon Cross-section impact");
            try
            {
                host.AddComponent<SpriteRenderer>().sharedMaterial = material;
                host.transform.localScale = Vector3.one * .55f;
                ConfigureVisual(host, Frames("Impact"), false, 30, null);
                impact = PrefabUtility.SaveAsPrefabAsset(host, Root + "Impact.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            host = new GameObject("Horizon Cross-section wave");
            try
            {
                host.AddComponent<SpriteRenderer>().sharedMaterial = material;
                host.AddComponent<CircleCollider2D>().isTrigger = true;
                var body = host.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                host.AddComponent<SkillProjectile>();
                ConfigureVisual(host, Frames("Flight"), true, 24, impact);
                projectile = PrefabUtility.SaveAsPrefabAsset(host, Root + "Projectile.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            StepEntry Step(string id, float start, float end) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var wave = Step("SpawnProjectile", .6f, .6f);
            wave.Overrides = new List<StepParamValue> {
                Float("DamageMultiplier", 3), Float("BaseSpeed", 11), Float("Lifetime", 1.4f),
                Float("WorldHitRadius", .08f), Float("TargetHitRadius", .72f), Float("OffsetX", .65f), Float("OffsetY", .05f),
                Float("RotationDegreesPerSecond", 0), Bool("HorizonCrossSection", true), Bool("UseProjectileCountStat", false),
                Bool("UseCurrentWeaponSprite", false), Bool("Homing", false), Bool("IgnoreFork", true), Bool("IgnoreChain", true),
                Bool("InfinitePierce", true), Bool("StopOnWorld", true), Bool("ScaleWithAreaOfEffect", true),
                new StepParamValue { Key = "BaseProjectileCount", Type = StepParamValue.ParamKind.Int, IntVal = 1 },
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            wave.SetOverrideObject("ProjectilePrefab", projectile);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_HorizonCrossSection.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, 1), Step("WeaponWindup", 0, .6f), Step("WeaponStrike", .6f, .6f), wave, Step("WeaponRecovery", .6f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Sword/Adventurer/LungeSlash/LungeSlashSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "HorizonCrossSectionSkill"; skill.SkillName = "Horizon Cross-section";
            skill.NameKey = "skills.HorizonCrossSectionSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 20; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = .8f;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Projectile;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier {
                SourceStat = StatType.MoveSpeed, SourcePercent = 500, TargetStat = StatType.DamagePhysical, TargetModifierType = StatModType.Flat
            } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "HorizonCrossSectionIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Сечение горизонта" : "Horizon Cross-section");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "После выраженного замаха выпускает одну широкую волну меча, наносящую 300% урона оружия далеко за пределами ближнего боя. Каждая единица скорости передвижения добавляет 5 физического урона до множителя навыка. Область действия увеличивает размер волны. Дополнительные снаряды, разветвление и цепь не создают новых волн. Волна проходит сквозь врагов и останавливается у стен. Уклонение до выпуска отменяет волну."
                    : "After a pronounced wind-up, releases one broad sword wave dealing 300% weapon damage well beyond melee range. Each point of Movement Speed adds 5 physical damage before the skill multiplier. Area of Effect increases the wave's size. Additional projectiles, Fork and Chain do not create extra waves. The wave passes through enemies and stops at walls. Dodging before release cancels the wave.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        private static void ConfigureVisual(GameObject host, Sprite[] frames, bool loop, float fps, GameObject impact)
        {
            host.GetComponent<SpriteRenderer>().sprite = frames[0];
            var settings = new SerializedObject(host.AddComponent<WeaponWaveVisual>());
            var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            settings.FindProperty("_loop").boolValue = loop; settings.FindProperty("_framesPerSecond").floatValue = fps;
            settings.FindProperty("_impactPrefab").objectReferenceValue = impact; settings.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
        [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [System.Serializable] private class Frame { public Box frame; }
        [System.Serializable] private class Box { public int x, y, w, h; }
        [System.Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
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
