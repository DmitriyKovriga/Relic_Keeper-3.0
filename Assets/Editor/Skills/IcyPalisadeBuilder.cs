using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;
namespace RelicKeeper.Editor.Skills
{
    public static class IcyPalisadeBuilder
    {
        public const string Root = "Assets/Resources/Skills/2HWeapon/Staff/Cold/IcyPalisade/";
        public const string SkillPath = Root + "IcyPalisadeSkill.asset";
        public const string PoolPath = "Assets/Resources/Skills/2HWeapon/Staff/Cold/2HColdStaffSkillPoolRBM.asset";
        [MenuItem("Tools/Skills/Build Icy Palisade")]
        public static void Apply()
        {
            var prefab = BuildVisuals();
            StepEntry Step(string id, float start, float end, params StepParamValue[] values) => new StepEntry {
                StepDefinition = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions").First(value => value.Id == id),
                StartPercentPipeline = start, EndPercentPipeline = end, Overrides = values.ToList()
            };
            var hits = new List<StepEntry>();
            for (int i = 0; i < 3; i++) {
                float at = .3f + i * .15f;
                var hit = Step("DealDamageRectangle", at, at, Float("DamageMultiplier", 2.5f), Float("SizeX", .6f), Float("SizeY", 1.65f),
                    Float("OffsetX", 1.1f + i * 1.3f), Float("SpikeSpacing", .55f), Float("GroundSnapUp", .75f), Float("GroundSnapDown", 2),
                    Float("GroundYOffset", .02f), Float("VfxLifetime", .4f), Bool("IcyPalisade", true), Bool("PullTowardOwner", false));
                hit.SetOverrideInt("SpikesPerGroup", 3); hit.SetOverrideInt("GroundLayerMask", 1 << 6); hit.SetOverrideObject("VfxPrefab", prefab);
                hit.DamageConversions = PrismaticCycleCast.ConvertAllTo(Scripts.Combat.DamageChannel.Cold); hits.Add(hit);
            }
            var recipe = Load<SkillRecipeSO>(Root + "Recipe_IcyPalisade.asset");
            recipe.Steps = new List<StepEntry> { Step("MovementLock", 0, .8f), Step("WeaponWindup", 0, .3f), Step("WeaponStrike", .3f, .3f) };
            recipe.Steps.AddRange(hits); recipe.Steps.Add(Step("WeaponRecovery", .6f, 1));
            recipe.HitStopFrames = 0; recipe.IsChanneling = false; EditorUtility.SetDirty(recipe);
            var skill = Load<SkillDataSO>(SkillPath); skill.ID = "IcyPalisadeSkill"; skill.SkillName = "Icy Palisade";
            skill.NameKey = "skills.IcyPalisadeSkill"; skill.DescriptionKey = skill.NameKey + ".description";
            skill.DescriptionMode = SkillDescriptionMode.Automatic; skill.IsActive = true; skill.Cooldown = 10; skill.ManaCost = 25;
            skill.WeaponRole = SkillWeaponRole.Special; skill.ActionSpeedMode = SkillActionSpeedMode.Spell; skill.SkillSpeedMultiplier = 1;
            skill.HoldStance = WeaponHoldStance.Staff; skill.HoldStanceId = "staff";
            skill.SwingStyle = WeaponSwingStyle.Slash; skill.SwingStyleId = "slash";
            skill.DamageContextTags = StatContextTagFlags.Spell | StatContextTagFlags.Area;
            skill.EnablePushback = false; skill.PushbackRating = 0; skill.Recipe = recipe;
            skill.StatLinks = new List<DerivedStatModifier> { new DerivedStatModifier { SourceStat = StatType.MaxMysticShield, SourceStep = 1,
                SourcePercent = 2000, TargetStat = StatType.SpellDamage, TargetModifierType = StatModType.PercentMult } };
            skill.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "IcyPalisadeIcon.png"); EditorUtility.SetDirty(skill);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PoolPath);
            if (!pool.PossibleSkills.Any(entry => entry.Skill == skill)) pool.PossibleSkills.Add(new SkillPoolSO.SkillWeight { Skill = skill, Weight = 1 }); EditorUtility.SetDirty(pool);
            var database = Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase);
            if (!database.AllSkills.Contains(skill)) database.AllSkills.Add(skill); EditorUtility.SetDirty(database);
            foreach (string code in new[] { "en", "ru" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
                table.AddEntry(skill.NameKey, code == "ru" ? "Ледяной частокол" : "Icy Palisade");
                table.AddEntry(skill.DescriptionKey, code == "ru"
                    ? "Три группы шипов вырастают перед героем с интервалом около 0,15 с. Каждая группа наносит 250% урона оружия с преобразованием в холод, затем разбивается. Каждый максимальный заряд Mystic Shield даёт 20% дополнительного урона: ёмкость 3 даёт 400% урона оружия на группу. Текущие заряды не влияют на бонус и не расходуются. Область действия увеличивает шипы. Каждый шип находит свою поверхность, пропасти пропускаются; препятствия не создаются. Перезарядка: 10 с. Мана: 25."
                    : "Three groups of spikes erupt ahead about 0.15 seconds apart. Each group deals 250% weapon damage converted to cold, then shatters. Each maximum Mystic Shield charge grants 20% additional damage: capacity 3 gives 400% weapon damage per group. Current charges do not affect the bonus and are not consumed. Area of Effect enlarges the spikes. Each spike finds its own ground and skips gaps; no obstacles are created. Cooldown: 10 seconds. Mana: 25.");
                EditorUtility.SetDirty(table); EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Skills/Refresh Icy Palisade Art")]
        public static void RefreshArt()
        {
            BuildVisuals();
            AssetDatabase.SaveAssets();
        }
        private static GameObject BuildVisuals()
        {
            Import("IcyPalisadeIcon", false); Import("Strike", true);
            var frames = Frames("Strike");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Strike.anim");
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, Root + "Strike.anim"); } clip.frameRate = 30;
            var keys = frames.Select((frame, index) => new ObjectReferenceKeyframe { time = index / 30f, value = frame }).ToList();
            keys.Add(new ObjectReferenceKeyframe { time = frames.Length / 30f, value = frames.Last() });
            AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip); clipSettings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings); EditorUtility.SetDirty(clip);
            string controllerPath = Root + "Strike.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var stateMachine = controller.layers[0].stateMachine; var state = stateMachine.states.FirstOrDefault().state;
            if (state == null) state = stateMachine.AddState("Strike"); state.motion = clip; stateMachine.defaultState = state; EditorUtility.SetDirty(controller);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "IcyPalisade.mat");
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "IcyPalisade.mat"); }
            GameObject prefab; var host = new GameObject("Icy Palisade strike");
            try
            {
                var renderer = host.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.sharedMaterial = material;
                host.AddComponent<Animator>().runtimeAnimatorController = controller;
                prefab = PrefabUtility.SaveAsPrefabAsset(host, Root + "Strike.prefab");
            }
            finally { Object.DestroyImmediate(host); }
            return prefab;
        }
        private static Sprite[] Frames(string name) => AssetDatabase.LoadAllAssetsAtPath(Root + name + ".png").OfType<Sprite>().OrderBy(value => value.name).ToArray();
        [System.Serializable] private class Sheet { public Frame[] frames; public Meta meta; }
        [System.Serializable] private class Frame { public Box frame; }
        [System.Serializable] private class Box { public int x, y, w, h; }
        [System.Serializable] private class Meta { public Box size; }
        private static void Import(string name, bool sheet)
        {
            string path = Root + name + ".png"; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = sheet ? SpriteImportMode.Multiple : SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.npotScale = TextureImporterNPOTScale.None; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            if (sheet)
            {
                var data = JsonUtility.FromJson<Sheet>(File.ReadAllText(Root + name + ".json"));
                importer.spritesheet = data.frames.Select((frame, index) => new SpriteMetaData {
                    name = name + "_" + index.ToString("D2"), rect = new Rect(frame.frame.x, data.meta.size.h - frame.frame.y - frame.frame.h, frame.frame.w, frame.frame.h),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, 1f - 48f / 56f)
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

