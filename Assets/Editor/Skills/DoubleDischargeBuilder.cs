using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public static class DoubleDischargeBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Lightning/DoubleDischarge/";
        public const string SkillPath = Root + "DoubleDischargeSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Lightning/2HLightningStaffSkillPool.asset";

        [MenuItem("Tools/Skills/Build Double Discharge")]
        public static void Apply()
        {
            Import("DoubleDischargeIcon", false); Import("Discharge", true);
            var frames = AssetDatabase.LoadAllAssetsAtPath(Root + "Discharge.png").OfType<Sprite>().OrderBy(s => s.name).ToArray();
            if (frames.Length != 8) throw new InvalidOperationException("Double Discharge requires eight PixelLab animation frames.");
            var material = LoadMaterial();
            GameObject prefab; var host = new GameObject("Double Discharge filament");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                var visual = host.AddComponent<AuthoredBeamVisual>();
                var settings = new SerializedObject(visual); var array = settings.FindProperty("_frames"); array.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Discharge.prefab");
            }
            finally { Object.DestroyImmediate(host); }

            StepEntry Step(string id, float start, float end) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(definition => definition.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            StepEntry Discharge(int index, float time)
            {
                var step = Step("InstantBeam", time, time);
                step.SetOverrideInt("DischargeIndex", index);
                step.SetOverrideFloat("DamageMultiplier", 1);
                step.SetOverrideFloat("Range", 8); step.SetOverrideFloat("Width", .35f);
                step.SetOverrideFloat("OffsetX", .45f); step.SetOverrideFloat("OffsetY", .25f);
                step.SetOverrideFloat("VisualLifetime", .16f); step.SetOverrideObject("VfxPrefab", prefab);
                return step;
            }
            var first = Discharge(1, .15f); var second = Discharge(2, .35f);
            first.ScopedStatModifiers = new List<SerializableStatModifier> {
                new SerializableStatModifier { Stat = StatType.ShockApplicationEffectiveness, Type = StatModType.PercentAdd, Value = 50 },
                new SerializableStatModifier { Stat = StatType.ShockChance, Type = StatModType.Flat, Value = 50 }
            };
            second.ScopedStatModifiers = new List<SerializableStatModifier> {
                new SerializableStatModifier { Stat = StatType.CritMultiplier, Type = StatModType.Flat, Value = 50 }
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_DoubleDischarge.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .45f), Step("WeaponWindup", 0, .15f),
                Step("WeaponStrike", .15f, .15f), first, second, Step("WeaponRecovery", .35f, 1) };
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "DoubleDischargeSkill"; skill.SkillName = "Double Discharge";
            skill.NameKey = "skills.DoubleDischargeSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 0; skill.ManaCost = 0;
            skill.WeaponRole = SkillWeaponRole.MainHand; skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = WeaponHoldStance.Staff; skill.HoldStanceId = "staff";
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "DoubleDischargeIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Двойной разряд" : "Double Discharge");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Два быстрых разряда подряд, каждый наносит 100% урона оружия. Первый получает 50% увеличения накопления шока и +50 процентных пунктов к шансу шока. Второй получает +50 процентных пунктов к множителю критического удара. Бонусы действуют только на соответствующий разряд. Лучи останавливаются на препятствиях. Скорость заклинаний ускоряет оба разряда. Без перезарядки и затрат маны."
                    : "Two rapid discharges, each dealing 100% weapon damage. The first gains 50% increased shock buildup and +50 percentage points of Shock Chance. The second gains +50 percentage points of Critical Strike Multiplier. Bonuses apply only to their respective discharge. Beams stop at terrain; Spell Speed accelerates the pair. No cooldown or mana cost.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }

        private static Material LoadMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Discharge.mat");
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(material, Root + "Discharge.mat"); return material;
        }
        [Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [Serializable] private class Frame { public Box frame; }
        [Serializable] private class Box { public int x, y, w, h; }
        [Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
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
    }
}
