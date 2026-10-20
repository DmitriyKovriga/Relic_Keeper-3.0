using System.Collections.Generic;
using System.Linq;
using System.IO;
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
    public static class PrismaticCycleBuilder
    {
        public const string Root = "Assets/Resources/Skills/1HWeapon/Wand/PrismaticCycle/";
        public const string SkillPath = Root + "PrismaticCycleSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/1HWeapon/Wand/1hWandSkillPool.asset";
        [MenuItem("Tools/Skills/Build Prismatic Cycle")]
        public static void Apply()
        {
            foreach (string name in new[] { "PrismaticCycleIcon", "PrismaticFire", "PrismaticCold", "PrismaticLightning", "PrismaticBurst" })
            {
                string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "PrismaticCycle.mat");
            var fireFrames = ImportClip("Fire", new Vector2(33f / 48, .5f));
            var coldFrames = ImportClip("Cold", new Vector2(33f / 48, .5f));
            var lightningFrames = ImportClip("Lightning", new Vector2(33f / 48, .5f));
            var prismFrames = ImportClip("Prism", new Vector2(33f / 48, .5f));
            var impactFrames = ImportClip("Impact", new Vector2(.5f, .5f));
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "PrismaticCycle.mat"); }
            var host = new GameObject("Prismatic Cycle projectile"); GameObject projectilePrefab, burstPrefab;
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite("PrismaticFire"); renderer.sharedMaterial = material;
                host.AddComponent<CircleCollider2D>().isTrigger = true;
                var body = host.AddComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                host.AddComponent<SkillProjectile>();
                var artObject = new GameObject("Animated pixel art"); artObject.transform.SetParent(host.transform, false);
                artObject.transform.localScale = Vector3.one * .5f;
                var art = artObject.AddComponent<SpriteRenderer>(); art.sharedMaterial = material; art.sprite = fireFrames[0];
                var visual = host.AddComponent<PrismaticProjectileVisual>();
                var settings = new SerializedObject(visual);
                settings.FindProperty("_coldSource").objectReferenceValue = Sprite("PrismaticCold");
                settings.FindProperty("_lightningSource").objectReferenceValue = Sprite("PrismaticLightning");
                settings.FindProperty("_art").objectReferenceValue = art;
                SetFrames(settings, "_fireFrames", fireFrames); SetFrames(settings, "_coldFrames", coldFrames);
                SetFrames(settings, "_lightningFrames", lightningFrames); SetFrames(settings, "_prismFrames", prismFrames);
                settings.ApplyModifiedPropertiesWithoutUndo();
                projectilePrefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "PrismaticProjectile.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            host = new GameObject("Prismatic impact");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = impactFrames[0]; renderer.sharedMaterial = material;
                host.transform.localScale = Vector3.one * .5f;
                var visual = host.AddComponent<PrismaticBurstVisual>();
                var settings = new SerializedObject(visual); SetFrames(settings, "_frames", impactFrames); settings.ApplyModifiedPropertiesWithoutUndo();
                burstPrefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "PrismaticImpact.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_PrismaticCycle.asset");
            definition.Id = "PrismaticCycle"; definition.NameEn = "Prismatic Cycle"; definition.NameRu = "Призматический цикл";
            definition.DescriptionEn = "One element per cast: fire, cold, lightning. The fourth cast fires a prismatic burst.";
            definition.DescriptionRu = "Одна стихия за применение: огонь, холод, молния. Четвёртое применение выпускает призматический залп.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("DamageMultiplier", 1), Float("BonusMoreSpellDamage", 100), Float("BaseSpeed", 12), Float("Lifetime", 4),
                Float("OffsetX", .55f), Float("OffsetY", .35f), Float("HitRadius", .18f), Float("SizeX", .75f), Float("SizeY", .75f),
                Float("RotationDegreesPerSecond", 0),
                new StepParamValue { Key = "BaseProjectileCount", Type = StepParamValue.ParamKind.Int, IntVal = 1 },
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            }; EditorUtility.SetDirty(definition);
            var shot = new StepEntry { StepDefinition = definition, StartPercentPipeline = .15f, EndPercentPipeline = .15f };
            shot.SetOverrideObject("ProjectilePrefab", projectilePrefab);
            shot.SetOverrideObject("FireSprite", Sprite("PrismaticFire")); shot.SetOverrideObject("ColdSprite", Sprite("PrismaticCold"));
            shot.SetOverrideObject("LightningSprite", Sprite("PrismaticLightning")); shot.SetOverrideObject("BonusSprite", Sprite("PrismaticBurst"));
            shot.SetOverrideObject("BurstVfxPrefab", burstPrefab);
            StepEntry Present(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_PrismaticCycle.asset");
            recipe.Steps = new List<StepEntry> { Present("WeaponWindup", 0, .15f), Present("WeaponStrike", .15f, .15f), shot, Present("WeaponRecovery", .15f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/1HWeapon/Wand/SeekerSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "PrismaticCycleSkill"; skill.SkillName = "Prismatic Cycle";
            skill.NameKey = "skills.PrismaticCycleSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 0; skill.ManaCost = 0; skill.WeaponRole = source.WeaponRole;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Projectile;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = source.SwingStyle; skill.SwingStyleId = source.SwingStyleId;
            skill.Icon = Sprite("PrismaticCycleIcon"); skill.Recipe = recipe; EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Prismatic Cycle" : "Призматический цикл");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "Shots cycle through fire, cold, and lightning, converting all weapon damage to the current element. Every fourth use fires a prismatic volley, split equally between the three elements, with 100% more spell damage. Progress counts uses, not projectiles or hits."
                    : "Выстрелы чередуют огонь, холод и молнию, преобразуя весь урон оружия в текущую стихию. Каждое четвёртое применение выпускает призматический залп: урон поровну разделён между тремя стихиями, на 100% больше урона от чар. Цикл считает применения, а не снаряды или попадания.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + name + ".png");
        [System.Serializable] private class ClipFile { public ClipFrame[] frames; }
        [System.Serializable] private class ClipFrame { public ClipRect frame; }
        [System.Serializable] private class ClipRect { public int x, y, w, h; }
        private static Sprite[] ImportClip(string name, Vector2 pivot)
        {
            string path = Root + "Animations/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var clip = JsonUtility.FromJson<ClipFile>(File.ReadAllText(Root + "Animations/" + name + ".json"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.spritesheet = clip.frames.Select((entry, index) => new SpriteMetaData
            {
                name = name + "_" + index.ToString("D2"), alignment = (int)SpriteAlignment.Custom, pivot = pivot,
                rect = new Rect(entry.frame.x, texture.height - entry.frame.y - entry.frame.h, entry.frame.w, entry.frame.h)
            }).ToArray();
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        }
        private static void SetFrames(SerializedObject target, string key, Sprite[] frames)
        {
            var property = target.FindProperty(key); property.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
        private static T Load<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
