using System;
using System.IO;
using System.Linq;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Skills
{
    public static class SpinAroundBuilder
    {
        public const string Root = "Assets/Resources/VFX/SkillSpin/";
        public const string DefinitionPath = "Assets/Resources/Skills/StepDefinitions/Step_SpinAround.asset";
        public static GameObject Steel => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Steel.prefab");
        public static GameObject Heavy => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Heavy.prefab");
        [MenuItem("Tools/Skills/Update Spin Placement")]
        public static void ApplyPlacement()
        {
            foreach (string path in new[] { "Assets/Resources/Skills/2HWeapon/Axe/RightButton/Recipe_Sweep.asset", ConningTowerBuilder.Root + "Recipe_ConningTower.asset" })
            {
                var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(path);
                if (recipe == null) continue;
                var spins = recipe.Steps.Where(step => step.StepDefinition != null && step.StepDefinition.Id == "SpinAround").ToArray();
                for (int i = 0; i < spins.Length; i++)
                {
                    spins[i].SetOverrideFloat("OffsetY", -.15f);
                    spins[i].SetOverrideFloat("Radius", spins.Length == 3 && i == 2 ? 1.4f : 1.2f);
                }
                EditorUtility.SetDirty(recipe);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Build Shared Spin Animations")]
        public static void Apply()
        {
            ApplyEffects();
            foreach (string hero in new[] { "Warrior", "Rogue", "Mage" }) ImportCharacter(hero);
            ApplySweep(); AssetDatabase.SaveAssets();
        }
        public static void ApplyEffects()
        {
            var definition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(DefinitionPath);
            if (definition == null) { definition = ScriptableObject.CreateInstance<StepDefinitionSO>(); AssetDatabase.CreateAsset(definition, DefinitionPath); }
            definition.Id = "SpinAround"; definition.NameEn = "Spin Around"; definition.NameRu = "Круговой разворот";
            definition.DescriptionEn = "Samples character, weapon and blade trail from one normalized step percentage. No gameplay rotation or invulnerability.";
            definition.DescriptionRu = "Персонаж, оружие и след клинка движутся по общему проценту шага. Не вращает физическое тело и не даёт неуязвимость.";
            definition.DefaultParams = new System.Collections.Generic.List<StepParamValue> {
                Float("Turns", 1), Float("Radius", 1.2f), Float("OffsetY", -.15f), Float("Acceleration", .65f), Float("PlaneDepth", .16f),
                new StepParamValue { Key = "Reverse", Type = StepParamValue.ParamKind.Bool, BoolVal = false }
            }; EditorUtility.SetDirty(definition);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "SpinSteel.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "SpinSteel.mat"); }
            foreach (string name in new[] { "Steel", "Heavy" })
            {
                string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); Configure(importer);
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), alignment = (int)SpriteAlignment.Custom,
                    rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    pivot = new Vector2(frame.pivot.x, frame.pivot.y)
                }).ToArray(); importer.SaveAndReimport();
                var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(value => value.name).ToArray();
                var host = new GameObject(name + " spin cut");
                try
                {
                    var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                    var settings = new SerializedObject(host.AddComponent<SkillSpinTrail>());
                    settings.FindProperty("_referenceRadius").floatValue = name == "Heavy" ? 2.6f : 2.2f;
                    settings.FindProperty("_referencePlaneDepth").floatValue = .16f;
                    var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    settings.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(host, Root + name + ".prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
            }
            AssetDatabase.SaveAssets();
        }
        private static void ImportCharacter(string hero)
        {
            string root = "Assets/Resources/Heroes/" + hero + "Resources/Refined/Spin/";
            Directory.CreateDirectory(root); AssetDatabase.Refresh();
            var frames = new Sprite[9];
            Vector2 referenceFeet = Vector2.zero;
            for (int i = 0; i < frames.Length; i++)
            {
                string path = root + hero + "Spin_" + i.ToString("D2") + ".png";
                File.Copy("Docs/Art/Spin/" + hero + "/" + i.ToString("D2") + ".png", path, true);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); Configure(importer); importer.spriteImportMode = SpriteImportMode.Single;
                var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(path));
                try
                {
                    Vector2 feet = Feet(image); if (i == 0) referenceFeet = feet;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = Vector2.one * .5f + new Vector2((feet.x - referenceFeet.x) / image.width, (feet.y - referenceFeet.y) / image.height);
                    importer.SetTextureSettings(settings);
                }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                importer.SaveAndReimport(); frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            var data = Resources.Load<CharacterDataSO>("Heroes/" + hero + "Resources/" + hero);
            data.SpinAnimation.Frames = frames; data.SpinAnimation.Loop = false; data.SpinAnimation.FrameDuration = 1f / 24;
            data.SpinAnimation.SpriteSheetResourcesPath = ""; EditorUtility.SetDirty(data);
        }
        private static Vector2 Feet(Texture2D image)
        {
            var pixels = image.GetPixels32(); int bottom = image.height, left = image.width, right = -1;
            for (int y = 0; y < image.height && bottom == image.height; y++)
                for (int x = 0; x < image.width; x++) if (pixels[y * image.width + x].a > 127) bottom = y;
            for (int y = bottom; y < Mathf.Min(bottom + 2, image.height); y++)
                for (int x = 0; x < image.width; x++) if (pixels[y * image.width + x].a > 127) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
            return right >= 0 ? new Vector2((left + right) * .5f, bottom) : new Vector2(image.width * .5f, 0);
        }
        public static void ApplySweep()
        {
            var recipe = Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Axe/RightButton/Recipe_Sweep");
            var definition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>(DefinitionPath);
            var spin = new StepEntry { StepDefinition = definition, StartPercentPipeline = .2f, EndPercentPipeline = .456f };
            spin.SetOverrideObject("VfxPrefab", Steel); spin.SetOverrideFloat("Radius", 1.2f); spin.SetOverrideFloat("OffsetY", -.15f);
            recipe.Steps[3] = spin;
            recipe.Steps[2].StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == "Wait");
            recipe.Steps[2].Overrides.Clear();
            var hit = recipe.Steps[4]; hit.SetOverrideInt("SourceStepIndex", -1); hit.SetOverrideFloat("VfxLifetimePercent", 0);
            hit.SetOverrideFloat("Radius", 2.2f); hit.SetOverrideFloat("SizeX", 1); hit.SetOverrideFloat("SizeY", .5f);
            hit.SetOverrideFloat("OffsetX", 0); hit.SetOverrideFloat("OffsetY", .4f); hit.SetOverrideBool("RadialSweep", true); hit.SetOverrideBool("PullTowardOwner", false);
            hit.StartPercentPipeline = hit.EndPercentPipeline = .2f + .256f * .35f;
            var skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/RightButton/SweepRB");
            skill.SwingStyleId = "slash"; EditorUtility.SetDirty(skill); EditorUtility.SetDirty(recipe);
        }
        private static void Configure(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true; importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        }
        [Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private class Frame { public Box frame; public Pivot pivot; }
        [Serializable] private class Box { public int x, y, w, h; }
        [Serializable] private class Pivot { public float x, y; }
        [Serializable] private class Meta { public Box size; }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
