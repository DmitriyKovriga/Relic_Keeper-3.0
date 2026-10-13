using System;
using System.Collections.Generic;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Editor.Skills
{
    public static class PiercingBowShotBuilder
    {
        public const string Root = BowShotBuilder.Root + "PiercingShot/";

        [MenuItem("Tools/Skills/Apply Piercing Bow Shot")]
        public static void Apply()
        {
            string sheetPath = Root + "PiercingArrowEnergy-Sheet.png";
            var importer = Import(sheetPath, SpriteImportMode.Multiple);
            importer.spritesheet = Enumerable.Range(0, 8).Select(i => new SpriteMetaData
            {
                name = "PiercingArrowEnergy_" + i.ToString("D2"),
                rect = new Rect(i % 3 * 36, (2 - i / 3) * 16, 36, 16),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(26f / 36, 0.5f)
            }).ToArray();
            importer.SaveAndReimport();
            var frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            if (frames.Length != 8) throw new InvalidOperationException("Piercing arrow frame import failed.");
            Import(Root + "PiercingBowShotIcon.png", SpriteImportMode.Single).SaveAndReimport();

            string clipPath = Root + "PiercingArrowEnergy.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.frameRate = 24;
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("Energy", typeof(SpriteRenderer), "m_Sprite"),
                frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / 24f, value = sprite }).ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            string controllerPath = Root + "PiercingArrowEnergy.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddMotion(clip);
            }
            var arrow = new GameObject("PiercingBowArrowProjectile");
            GameObject prefab;
            try
            {
                var core = arrow.AddComponent<SpriteRenderer>();
                core.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BowShotBuilder.Root + "BowArrow.png");
                core.color = new Color(0.7f, 1, 0.72f, 1);
                core.sortingOrder = 1;
                var energy = new GameObject("Energy");
                energy.transform.SetParent(arrow.transform, false);
                energy.AddComponent<SpriteRenderer>().sprite = frames[0];
                arrow.AddComponent<Animator>().runtimeAnimatorController = controller;
                // Energy is a child renderer; collision remains based on the core arrow.
                prefab = PrefabUtility.SaveAsPrefabAsset(arrow, Root + "PiercingBowArrowProjectile.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(arrow); }

            string recipePath = Root + "Recipe_Piercing_Bow_Shot.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(recipePath);
            if (recipe == null)
            {
                recipe = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(BowShotBuilder.Root + "Recipe_Simple_Bow_Shot_Skill.asset"));
                AssetDatabase.CreateAsset(recipe, recipePath);
            }
            var shot = recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            recipe.name = "Recipe_Piercing_Bow_Shot";
            shot.SetOverrideObject("ProjectilePrefab", prefab);
            shot.SetOverrideFloat("DamageMultiplier", 3);
            shot.SetOverrideInt("BaseProjectileCount", 1);
            shot.SetOverrideBool("UseProjectileCountStat", false);
            shot.SetOverrideBool("IgnoreFork", true);
            shot.SetOverrideBool("IgnoreChain", true);
            shot.SetOverrideBool("InfinitePierce", true);
            EditorUtility.SetDirty(recipe);

            string skillPath = Root + "PiercingBowShotSkill.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(skillPath);
            if (skill == null)
            {
                skill = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillDataSO>(BowShotBuilder.Root + "SimpleBowShotSkill.asset"));
                AssetDatabase.CreateAsset(skill, skillPath);
            }
            skill.ID = "PiercingBowShotSkill";
            skill.name = "PiercingBowShotSkill";
            skill.SkillName = "Piercing Shot";
            skill.NameKey = "skills.PiercingBowShotSkill";
            skill.DescriptionKey = "skills.PiercingBowShotSkill.description";
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "PiercingBowShotIcon.png");
            skill.Recipe = recipe;
            skill.WeaponRole = SkillWeaponRole.Special;
            skill.ManaCost = 25;
            skill.Cooldown = 6;
            EditorUtility.SetDirty(skill);

            string poolPath = Root + "BowSecondarySkillPool.asset";
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(poolPath);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<SkillPoolSO>();
                pool.PossibleSkills = new List<SkillPoolSO.SkillWeight> { new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 } };
                AssetDatabase.CreateAsset(pool, poolPath);
            }
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponItemSO>("Assets/Resources/Items/2HWeapon/Bow/Adventurer's Bow.asset");
            if (weapon.SecondarySkillPool == null) weapon.SecondarySkillPool = pool;
            else if (weapon.SecondarySkillPool != pool)
            {
                var skills = weapon.SecondarySkillPool.PossibleSkills;
                if (!skills.Any(entry => entry.Skill == skill)) skills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
                EditorUtility.SetDirty(weapon.SecondarySkillPool);
            }
            EditorUtility.SetDirty(weapon);
            var collection = LocalizationEditorSettings.GetStringTableCollection("SkillsLabels");
            if (collection != null)
            {
                foreach (string locale in new[] { "en", "ru" })
                {
                    var table = collection.GetTable(locale) as StringTable;
                    if (table == null) continue;
                    table.AddEntry(skill.NameKey, locale == "en" ? "Piercing Shot" : "Пронзающий выстрел");
                    table.AddEntry(skill.DescriptionKey, "");
                    EditorUtility.SetDirty(table);
                }
                EditorUtility.SetDirty(collection.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static TextureImporter Import(string path, SpriteImportMode mode)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            return importer;
        }
    }
}
