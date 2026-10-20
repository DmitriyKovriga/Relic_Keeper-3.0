using System.Collections.Generic;
using System.Linq;
using Scripts.Combat;
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
    public static class FrostWallBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Cold/FrostWall/";
        public const string SkillPath = Root + "FrostWallSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Cold/2HColdStaffSkillPoolRBM.asset";

        [MenuItem("Tools/Skills/Build Frost Wall")]
        public static void Apply()
        {
            ImportSprite("FrostWallIcon.png", 24);
            ImportSprite("FrostWallBarrier.png", 24);
            ImportSprite("FrostWallShard.png", 24);
            ImportImpact();
            var material = LoadMaterial();
            var host = new GameObject("Frost Wall");
            GameObject prefab;
            try
            {
                var barrier = Child(host, "Ice barrier", material);
                var crown = Child(host, "Empowered ice", material);
                var visual = host.AddComponent<FrostWallVisual>();
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("_barrier").objectReferenceValue = barrier;
                serialized.FindProperty("_empoweredCrown").objectReferenceValue = crown;
                serialized.FindProperty("_impact").objectReferenceValue = Child(host, "Ice impact", material);
                var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "FrostWallImpact.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
                var sprites = serialized.FindProperty("_impactFrames"); sprites.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "FrostWallField.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_FrostWall.asset");
            definition.Id = "FrostWall"; definition.NameEn = "Frost Wall"; definition.NameRu = "Ледяная стена";
            definition.DescriptionEn = "Fixed barrier; intercepts attacks and retaliates with cold shards. One Mystic Shield charge empowers damage and duration.";
            definition.DescriptionRu = "Неподвижная стена: перехватывает атаки и отвечает ледяными осколками. Один заряд Мистического щита усиливает урон и длительность.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Duration", 5), Float("DamageMultiplier", .5f), Float("EmpoweredDamageMultiplier", 2),
                Float("EmpoweredDurationMultiplier", 2), Float("Width", .5f), Float("Height", 2), Float("OffsetX", 1.1f),
                Float("ShardSpeed", 12), Float("ShardLifetime", 3),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            EditorUtility.SetDirty(definition);
            var step = new StepEntry { StepDefinition = definition, StartPercentPipeline = .2f, EndPercentPipeline = .2f,
                DamageConversions = new List<DamageConversionRule>
                {
                    new DamageConversionRule { Source = DamageChannel.Physical, Target = DamageChannel.Cold, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Fire, Target = DamageChannel.Cold, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Lightning, Target = DamageChannel.Cold, Percent = 100 }
                }
            };
            step.SetOverrideObject("FieldVfxPrefab", prefab);
            step.SetOverrideObject("ShardSprite", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FrostWallShard.png"));
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_FrostWall.asset");
            recipe.Steps = new List<StepEntry> { step }; recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "FrostWallSkill"; skill.SkillName = "Frost Wall"; skill.NameKey = "skills.FrostWallSkill";
            skill.DescriptionKey = skill.NameKey + ".description"; skill.DescriptionMode = SkillDescriptionMode.Automatic;
            skill.IsActive = true; skill.Cooldown = 5; skill.ManaCost = 35; skill.WeaponRole = SkillWeaponRole.Special;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 3;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Projectile;
            skill.HoldStanceId = "staff"; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FrostWallIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Frost Wall" : "Ледяная стена");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "Creates an ice barrier ahead that intercepts enemy attacks and projectiles and retaliates with cold shards based on main-hand weapon damage. Consumes one available Mystic Shield charge to increase damage and duration."
                    : "Создаёт ледяную стену перед героем: перехватывает вражеские атаки и снаряды и отвечает ледяными осколками на основе урона оружия в основной руке. Расходует один доступный заряд Мистического щита, усиливая урон и длительность.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static SpriteRenderer Child(GameObject host, string name, Material material)
        {
            var child = new GameObject(name); child.transform.SetParent(host.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "FrostWallBarrier.png"); renderer.sharedMaterial = material;
            return renderer;
        }
        private static Material LoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FrostWall.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "FrostWall.mat"); }
            return material;
        }
        private static void ImportSprite(string name, float ppu)
        {
            string path = Root + name; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        private static void ImportImpact()
        {
            ImportSprite("FrostWallImpact.png", 24);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "FrostWallImpact.png");
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = Enumerable.Range(0, 10).Select(i => new SpriteMetaData
            {
                name = "FrostWallImpact_" + i.ToString("D2"),
                rect = new Rect(i % 4 * 37, 141 - (i / 4 + 1) * 47, 37, 47),
                alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(19f / 37, 1 - 18f / 47)
            }).ToArray();
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
