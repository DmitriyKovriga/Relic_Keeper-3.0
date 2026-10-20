using System;
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
    public static class LightningRodBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Lightning/LightningRod/";
        public const string SkillPath = Root + "LightningRodSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Lightning/2HLightningStaffSkillPoolRBM.asset";

        [MenuItem("Tools/Skills/Build Lightning Rod")]
        public static void Apply()
        {
            ImportTexture(Root + "LightningRodIcon.png", false, 24);
            ImportTexture(Root + "LightningRodElectrode.png", false, 48);
            ImportTexture(Root + "LightningRodFilament.png", true, 24);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "LightningRodFilament.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length != 8) throw new InvalidOperationException("Lightning Rod needs all eight PixelLab filament frames.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "LightningRod.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "LightningRod.mat");
            }
            GameObject prefab;
            var host = new GameObject("Lightning Rod electrodes");
            try
            {
                SpriteRenderer Child(string name, Sprite sprite)
                {
                    var child = new GameObject(name); child.transform.SetParent(host.transform, false);
                    var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; return renderer;
                }
                var electrode = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "LightningRodElectrode.png");
                var first = Child("First electrode", electrode); var second = Child("Second electrode", electrode);
                var arc = Child("Travelling discharge", frames[0]);
                var visual = host.AddComponent<LightningRodVisual>();
                var serialized = new SerializedObject(visual);
                serialized.FindProperty("_firstRod").objectReferenceValue = first;
                serialized.FindProperty("_secondRod").objectReferenceValue = second;
                serialized.FindProperty("_arc").objectReferenceValue = arc;
                var sprites = serialized.FindProperty("_arcFrames"); sprites.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) sprites.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "LightningRodField.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_LightningRod.asset");
            definition.Id = "LightningRod"; definition.NameEn = "Lightning Rod"; definition.NameRu = "Громоотвод";
            definition.DescriptionEn = "Fixed electrodes; alternating travelling lightning, one hit per enemy per pass.";
            definition.DescriptionRu = "Неподвижные электроды: разряд проходит между ними, поражая врага один раз за проход.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Distance", 6), Float("Width", .35f), Float("BeamHeight", .9f), Float("OffsetX", .65f),
                Float("PassDuration", .45f), Float("PassInterval", .75f), Float("DamageMultiplier", .6f),
                new StepParamValue { Key = "PassCount", Type = StepParamValue.ParamKind.Int, IntVal = 8 },
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = LayerMask.GetMask("Ground") }
            };
            EditorUtility.SetDirty(definition);
            var step = new StepEntry { StepDefinition = definition, StartPercentPipeline = .2f, EndPercentPipeline = .2f,
                DamageConversions = new List<DamageConversionRule>
                {
                    new DamageConversionRule { Source = DamageChannel.Physical, Target = DamageChannel.Lightning, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Fire, Target = DamageChannel.Lightning, Percent = 100 },
                    new DamageConversionRule { Source = DamageChannel.Cold, Target = DamageChannel.Lightning, Percent = 100 }
                }
            };
            step.SetOverrideObject("FieldVfxPrefab", prefab);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_LightningRod.asset");
            recipe.Steps = new List<StepEntry> { step }; recipe.HitStopFrames = 0; recipe.IsChanneling = false;
            EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "LightningRodSkill"; skill.SkillName = "Lightning Rod";
            skill.NameKey = "skills.LightningRodSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 15; skill.ManaCost = 25; skill.WeaponRole = SkillWeaponRole.Special;
            skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 3;
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.HoldStanceId = "staff"; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "LightningRodIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Lightning Rod" : "Громоотвод");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "Place two fixed electrodes. A discharge travels repeatedly between them, hitting each enemy on the line once per pass. Keep targets between the electrodes. Area of Effect increases their separation."
                    : "Создаёт два неподвижных электрода. Разряд несколько раз проходит между ними, поражая каждого врага на линии один раз за проход. Удерживайте врагов между электродами. Область действия увеличивает расстояние между ними.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportTexture(string path, bool sheet, float ppu)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            if (!sheet && path.EndsWith("Electrode.png")) { settings.spriteAlignment = (int)SpriteAlignment.Custom; settings.spritePivot = new Vector2(.5f, .875f); }
            importer.SetTextureSettings(settings);
            if (sheet)
                importer.spritesheet = Enumerable.Range(0, 8).Select(i => new SpriteMetaData
                {
                    name = "LightningRod_" + i.ToString("D2"), rect = new Rect(i % 3 * 64, 48 - (i / 3 + 1) * 16, 64, 16),
                    alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f)
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
