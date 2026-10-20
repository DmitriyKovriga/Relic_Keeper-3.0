using System.Collections.Generic;
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
    public static class CrimsonHookBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Axe/CrimsonHook/";
        public const string SkillPath = Root + "CrimsonHookSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/AxeSkillsPoolRB.asset";
        [MenuItem("Tools/Skills/Build Crimson Hook")]
        public static void Apply()
        {
            foreach (string name in new[] { "CrimsonHookIcon", "CrimsonHookBlade", "CrimsonHookTether" }) ImportSprite(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "CrimsonHook.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                AssetDatabase.CreateAsset(material, Root + "CrimsonHook.mat");
            }
            var host = new GameObject("Crimson Hook grapple"); GameObject prefab;
            try
            {
                var visual = host.AddComponent<CrimsonHookVisual>();
                var serialized = new SerializedObject(visual);
                foreach (string field in new[] { "_tethers", "_hooks" })
                {
                    var array = serialized.FindProperty(field); array.arraySize = 3;
                    for (int i = 0; i < 3; i++)
                    {
                        var child = new GameObject(field + i); child.transform.SetParent(host.transform, false);
                        var renderer = child.AddComponent<SpriteRenderer>(); renderer.sharedMaterial = material;
                        renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + (field == "_hooks" ? "CrimsonHookBlade.png" : "CrimsonHookTether.png"));
                        array.GetArrayElementAtIndex(i).objectReferenceValue = renderer;
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "CrimsonHookVfx.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            var definition = Load<StepDefinitionSO>("Assets/Resources/Skills/StepDefinitions/Step_CrimsonHook.asset");
            definition.Id = "CrimsonHook"; definition.NameEn = "Crimson Hook"; definition.NameRu = "Багровый крюк";
            definition.DescriptionEn = "Damage enemies ahead once and pull movable targets toward the caster; Area of Effect scales width only.";
            definition.DescriptionRu = "Одно попадание по врагам впереди; притягивает подвижные цели к герою. Область действия увеличивает только ширину.";
            definition.DefaultParams = new List<StepParamValue>
            {
                Float("Range", 6), Float("Width", 1.5f), Float("DamageMultiplier", 1), Float("PullDuration", .35f),
                Float("StopGap", .2f), Float("HeavyResistanceLimit", 70),
                new StepParamValue { Key = "WorldLayerMask", Type = StepParamValue.ParamKind.Int, IntVal = 1 << 6 }
            };
            EditorUtility.SetDirty(definition);
            var hit = new StepEntry { StepDefinition = definition, StartPercentPipeline = .3f, EndPercentPipeline = .3f };
            hit.SetOverrideObject("VfxPrefab", prefab);
            StepEntry Presentation(string id, float start, float end) => new StepEntry
            {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end
            };
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_CrimsonHook.asset");
            recipe.Steps = new List<StepEntry> { Presentation("WeaponWindup", 0, .3f), Presentation("WeaponStrike", .3f, .3f), hit, Presentation("WeaponRecovery", .3f, 1) };
            recipe.HitStopFrames = 2; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath);
            skill.ID = "CrimsonHookSkill"; skill.SkillName = "Crimson Hook";
            skill.NameKey = "skills.CrimsonHookSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true;
            skill.Cooldown = 5; skill.ManaCost = 10; skill.WeaponRole = SkillWeaponRole.Special;
            skill.ActionSpeedMode = SkillActionSpeedMode.Attack; skill.SkillSpeedMultiplier = 1.5f;
            skill.DamageContextTags = StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area;
            skill.EnablePushback = false; skill.Recipe = recipe;
            skill.SwingStyleId = "overhead"; skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "CrimsonHookIcon.png");
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
                table.AddEntry(skill.NameKey, code == "en" ? "Crimson Hook" : "Багровый крюк");
                table.AddEntry(skill.DescriptionKey, code == "en"
                    ? "A long-reaching grapple strikes enemies ahead and pulls movable targets toward you. Heavy and immobile targets take damage without displacement. Area of Effect widens the grapple, helping gather groups."
                    : "Дальний захват поражает врагов перед героем и притягивает подвижные цели. Тяжёлые и неподвижные враги получают урон без перемещения. Область действия увеличивает ширину захвата, помогая собирать группы.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        private static void ImportSprite(string name)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); importer.SaveAndReimport();
        }
        private static T Load<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path); if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        private static StepParamValue Float(string key, float value) => new StepParamValue { Key = key, Type = StepParamValue.ParamKind.Float, FloatVal = value };
    }
}
