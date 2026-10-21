using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Skills
{
    public static class ConningTowerBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Axe/ConningTower/";
        public const string SkillPath = Root + "ConningTowerSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/AxeSkillsPoolRB.asset";
        [MenuItem("Tools/Skills/Build Conning Tower")]
        public static void Apply()
        {
            Import("ConningTowerIcon", false);
            SpinAroundBuilder.ApplyEffects();
            var visuals = new[] { SpinAroundBuilder.Steel, SpinAroundBuilder.Steel, SpinAroundBuilder.Heavy };
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_ConningTower.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, 1) };
            void Strike(int index, float windup, float impact, float recovery, float vfxStart, float vfxEnd, float damage, float radius)
            {
                recipe.Steps.Add(Step("WeaponWindup", windup, vfxStart));

                var vfx = Step("SpinAround", vfxStart, vfxEnd, Float("OffsetY", -.15f), Float("Radius", index == 3 ? 1.4f : 1.2f), Float("Turns", 1), Bool("Reverse", index == 2));
                vfx.SetOverrideObject("VfxPrefab", visuals[index - 1]); recipe.Steps.Add(vfx);
                var hit = Step("DealDamageCircle", impact, impact, Float("DamageMultiplier", damage),
                    Float("Radius", radius), Float("SizeX", 1), Float("SizeY", .5f), Float("OffsetY", .4f),
                    Bool("RadialSweep", true), Bool("PullTowardOwner", false));
                hit.SetOverrideInt("ComboStrike", index); hit.SetOverrideInt("ComboStrikeCount", 3); recipe.Steps.Add(hit);

            }
            Strike(1, 0, .18f, .28f, .12f, .28f, 1, 2.2f);
            Strike(2, .28f, .42f, .53f, .36f, .52f, 1, 2.2f);
            Strike(3, .53f, .8f, 1, .72f, .96f, 3.5f, 2.6f);
            recipe.HitStopFrames = 2; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var source = Resources.Load<SkillDataSO>("Skills/2HWeapon/Axe/CrimsonHook/CrimsonHookSkill");
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "ConningTowerSkill"; skill.SkillName = "Conning Tower";
            skill.NameKey = "skills.ConningTowerSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = .625f;
            skill.HoldStance = source.HoldStance; skill.HoldStanceId = source.HoldStanceId;
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "ConningTowerIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(value => value.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 });
            EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Боевая рубка" : "Conning Tower");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Три круговых взмаха двуручным топором вокруг героя поражают врагов с обеих сторон: первые два наносят по 100% урона оружия, заключительный — 350%. Уклонение прерывает серию: оставшиеся удары теряются. Мана и перезарядка расходуются один раз при начале серии. Скорость атаки ускоряет всю серию, область действия расширяет взмахи."
                    : "Three full sweeps around the player with a two-handed axe hit enemies on both sides: the first two deal 100% weapon damage each, followed by a powerful 350% final swing. Dodging away interrupts the combo and forfeits the remaining strikes. Mana and cooldown are spent once when the combo starts. Attack Speed speeds up the sequence; Area of Effect expands the sweeps.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [System.Serializable] private class Frame { public Box frame; public Pivot pivot; }
        [System.Serializable] private class Box { public int x, y, w, h; }
        [System.Serializable] private class Pivot { public float x, y; }
        [System.Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(frame.pivot.x, frame.pivot.y)
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
