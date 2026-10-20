using System;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class LuckyShotBuilder
    {
        public const string Root = BowShotBuilder.Root + "LuckyShot/";
        public const string SkillPath = Root + "LuckyShotSkill.asset";
        [Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private class Frame { public FrameRect frame; }
        [Serializable] private class FrameRect { public int x, y, w, h; }
        [Serializable] private class Meta { public FrameRect size; public Anchor anchor; }
        [Serializable] private class Anchor { public int x, y; }

        [MenuItem("Tools/Skills/Build Lucky Shot")]
        public static void Apply()
        {
            ImportTexture(Root + "LuckyShotIcon.png", false);
            ImportTexture(Root + "LuckyShotRicochet.png", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "LuckyShotRicochet.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length != 7) throw new InvalidOperationException("Lucky Shot needs all seven PixelLab ricochet frames.");
            var clip = Load<AnimationClip>(Root + "LuckyShotRicochet.anim"); clip.frameRate = 20;
            var keys = frames.Select((sprite, i) => new ObjectReferenceKeyframe { time = i / 20f, value = sprite }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 20f, value = null });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "LuckyShotRicochet.controller");
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(Root + "LuckyShotRicochet.controller", clip);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "LuckyShotRicochet.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "LuckyShotRicochet.mat");
            }
            GameObject sparkPrefab;
            var host = new GameObject("Lucky Shot Ricochet");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                sparkPrefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "LuckyShotRicochet.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            var recipe = AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(Root + "Recipe_LuckyShot.asset");
            if (recipe == null)
            {
                recipe = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillRecipeSO>(BowShotBuilder.Root + "Recipe_Simple_Bow_Shot_Skill.asset"));
                recipe.name = "Recipe_LuckyShot"; AssetDatabase.CreateAsset(recipe, Root + "Recipe_LuckyShot.asset");
            }
            var shot = recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            shot.SetOverrideBool("EnvironmentRicochet", true);
            shot.SetOverrideInt("WorldLayerMask", LayerMask.GetMask("Ground", "OneWayPlatform"));
            shot.SetOverrideInt("BaseWorldBounces", 1); shot.SetOverrideFloat("RicochetDamagePercent", 75);
            shot.SetOverrideFloat("RicochetSearchRadius", 12); shot.SetOverrideObject("RicochetVfxPrefab", sparkPrefab);
            shot.SetOverrideBool("IgnoreChain", true); shot.SetOverrideBool("Homing", false);
            shot.SetOverrideFloat("BaseSpeed", 18); shot.SetOverrideFloat("Lifetime", 4);
            shot.SetOverrideFloat("DamageMultiplier", 1);
            EditorUtility.SetDirty(recipe);
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(SkillPath);
            if (skill == null)
            {
                skill = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillDataSO>(BowShotBuilder.Root + "SimpleBowShotSkill.asset"));
                skill.name = "LuckyShotSkill"; AssetDatabase.CreateAsset(skill, SkillPath);
            }
            skill.ID = "LuckyShotSkill"; skill.SkillName = "Lucky Shot";
            skill.NameKey = "skills.LuckyShotSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.WeaponRole = SkillWeaponRole.MainHand;
            skill.Recipe = recipe; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "LuckyShotIcon.png");
            EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(BowShotBuilder.Root + "BowSkillPool.asset");
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill);
            EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "en" ? "Lucky Shot" : "Удачный выстрел");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "Fire an arrow that ricochets from surfaces and then aims toward the nearest reachable enemy. The ricochet empowers the hit. Projectile Chain adds wall bounces instead of enemy chains; Projectile Speed shortens travel time."
                    : "Выпускает стрелу, которая отскакивает от поверхностей и направляется к ближайшему доступному врагу. Рикошет усиливает попадание. Цепь снарядов даёт отскоки от поверхностей вместо цепи между врагами; скорость снарядов сокращает время полёта.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static void ImportTexture(string path, bool sheet)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + "LuckyShotRicochet.json"));
                importer.spritesheet = data.frames.Select((frame, i) => new SpriteMetaData
                {
                    name = "LuckyShot_" + i.ToString("D2"),
                    rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = new Vector2((float)data.meta.anchor.x / frame.frame.w, 1f - (float)data.meta.anchor.y / frame.frame.h)
                }).ToArray();
            }
            importer.SaveAndReimport();
        }

        private static T Load<T>(string path) where T : Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = typeof(T) == typeof(AnimationClip) ? new AnimationClip() as T : ScriptableObject.CreateInstance(typeof(T)) as T;
            AssetDatabase.CreateAsset(value, path); return value;
        }
    }
}
