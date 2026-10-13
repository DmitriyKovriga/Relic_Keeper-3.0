using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Skills
{
    public static class BowShotBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Bow/";

        [MenuItem("Tools/Skills/Apply Simple Bow Shot Icon")]
        public static void ApplyIcon()
        {
            string path = Root + "SimpleBowShotIcon.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(Root + "SimpleBowShotSkill.asset");
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (skill.Icon == null) throw new InvalidOperationException("Bow shot icon import failed.");
            EditorUtility.SetDirty(skill);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Skills/Apply Simple Bow Shot")]
        public static void Apply()
        {
            string texturePath = Root + "BowArrow.png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var arrow = new GameObject("BowArrowProjectile");
            GameObject prefab;
            try
            {
                arrow.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
                // SkillProjectile supplies collision, movement, damage and pooling at launch.
                prefab = PrefabUtility.SaveAsPrefabAsset(arrow, Root + "BowArrowProjectile.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(arrow); }

            var swings = WeaponSwingStyleTableSO.LoadDefault();
            swings.Set(WeaponSwingStyleTableSO.CreateDefaultStyleTable().Single(row => row.Style == WeaponSwingStyle.BowDraw));
            EditorUtility.SetDirty(swings);
            var stances = WeaponStancePoseTableSO.LoadDefault();
            if (!stances.TryGetById("bow_stance", out var bowPose))
                throw new InvalidOperationException("The existing bow stance is missing.");
            bowPose.DefaultSwingId = "bow_draw";
            int bowIndex = Array.FindIndex(stances.Poses, row => row.Id == "bow_stance");
            stances.SetPoseAt(bowIndex, bowPose);
            EditorUtility.SetDirty(stances);

            var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(Root + "Recipe_Simple_Bow_Shot_Skill.asset");
            var shot = Step("SpawnProjectile", 0.55f, 0.55f);
            shot.SetOverrideObject("ProjectilePrefab", prefab);
            shot.SetOverrideFloat("BaseSpeed", 18);
            shot.SetOverrideFloat("Lifetime", 3);
            shot.SetOverrideFloat("DamageMultiplier", 1);
            shot.SetOverrideFloat("HitRadius", 0.3f);
            shot.SetOverrideFloat("RotationDegreesPerSecond", 0);
            shot.SetOverrideFloat("OffsetX", 0.5f);
            shot.SetOverrideFloat("OffsetY", 0.35f);
            recipe.Steps = new List<StepEntry>
            {
                Step("WeaponWindup", 0, 0.55f),
                shot,
                Step("WeaponStrike", 0.55f, 0.55f),
                Step("WeaponRecovery", 0.55f, 0.85f)
            };
            EditorUtility.SetDirty(recipe);
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(Root + "SimpleBowShotSkill.asset");
            skill.HoldStanceId = "bow_stance";
            skill.SwingStyle = WeaponSwingStyle.BowDraw;
            skill.SwingStyleId = "bow_draw";
            if (skill.Icon == null) skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
            EditorUtility.SetDirty(skill);

            string poolPath = Root + "BowSkillPool.asset";
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(poolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<SkillPoolSO>();
                pool.PossibleSkills = new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 } };
                AssetDatabase.CreateAsset(pool, poolPath);
            }
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>("Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset");
            if (weapon.SkillPool == null) { weapon.SkillPool = pool; EditorUtility.SetDirty(weapon); }
            AssetDatabase.SaveAssets();
        }

        private static StepEntry Step(string id, float start, float end)
        {
            return new StepEntry
            {
                StepDefinition = AssetDatabase.LoadAssetAtPath<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_" + id + ".asset"),
                StartPercentPipeline = start,
                EndPercentPipeline = end
            };
        }
    }
}
