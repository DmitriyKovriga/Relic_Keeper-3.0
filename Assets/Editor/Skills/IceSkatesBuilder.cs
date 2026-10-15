using System.Collections.Generic;
using System.Linq;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class IceSkatesBuilder
    {
        public const string Root = "Assets/Resources/Skills/Boots/Mystic/";
        public const string SkillPath = Root + "IceSkates.asset";
        [MenuItem("Tools/Skills/Build Ice Skates")]
        public static void Apply()
        {
            ImportSprite(Root + "IceSkatesIcon.png", false);
            ImportSprite(Root + "IceSurface.png", true);
            var surface = Load<GroundSurfaceEffectSO>(Root + "IceSurface.asset");
            surface.Id = "IceSkates"; surface.Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "IceSurface.png");
            surface.PatchWidth = 1; surface.Lifetime = 3; surface.DamageInterval = .25f; surface.WeaponDamagePerSecond = .3f;
            surface.ContactModifiers.Clear();
            surface.DamageConversions = new List<DamageConversionRule>
            {
                new DamageConversionRule { Source = DamageChannel.Physical, Target = DamageChannel.Cold, Percent = 100 },
                new DamageConversionRule { Source = DamageChannel.Fire, Target = DamageChannel.Cold, Percent = 100 },
                new DamageConversionRule { Source = DamageChannel.Lightning, Target = DamageChannel.Cold, Percent = 100 }
            };
            EditorUtility.SetDirty(surface);
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_IceSkates.asset");
            definition.Id = "IceSkates"; definition.NameEn = "Ice skates"; definition.NameRu = "Ледяные коньки";
            definition.DescriptionEn = "Timed skating buff that leaves a reusable ground surface.";
            definition.DescriptionRu = "Временное скольжение со следом наземного эффекта.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Duration", 8), Float("SpeedBonusPercent", 35), Float("AccelerationPerSpeed", 4),
                Float("BrakePerSpeed", 1), Float("TurnPerSpeed", 3),
                new StepParamValue { Key = "SurfaceEffect", Type = StepParamValue.ParamKind.Object, ObjectVal = surface }
            };
            EditorUtility.SetDirty(definition);
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_IceSkates.asset");
            recipe.Steps = new List<StepEntry> { new StepEntry { StepDefinition = definition } };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "IceSkates"; skill.SkillName = "Ice Skates";
            skill.NameKey = "skills.IceSkates"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 15; skill.ManaCost = 30; skill.WeaponRole = SkillWeaponRole.Equipment;
            skill.ActionSpeedMode = SkillActionSpeedMode.Universal; skill.SkillSpeedMultiplier = 5;
            skill.DamageContextTags = Scripts.Stats.StatContextTagFlags.Attack | Scripts.Stats.StatContextTagFlags.Area;
            skill.HoldStanceId = ""; skill.SwingStyleId = "";
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "IceSkatesIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(Root + "MysticBootsSkillPool.asset");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Ice Skates" : "Ледяные коньки");
                table.AddEntry(skill.DescriptionKey, ""); EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportSprite(string path, bool ground)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.maxTextureSize = 64; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            if (ground)
            {
                // Place the upper opaque rim on the terrain, keeping the thin body of the ice just below it.
                importer.isReadable = true; importer.SaveAndReimport();
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                int top = 0;
                for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
                    if (texture.GetPixel(x, y).a > .1f) top = Mathf.Max(top, y);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(.5f, (top - 1f) / texture.height);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.isReadable = false;
            }
            importer.SaveAndReimport();
        }
        private static T Load<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
