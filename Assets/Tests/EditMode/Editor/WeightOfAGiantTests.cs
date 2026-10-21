using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class WeightOfAGiantTests
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        PlayerStats _owner;
        SkillStepRunner _runner;
        Random.State _random;
        static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(WeightOfAGiantBuilder.SkillPath);
        static StepEntry Hit => Skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageCircle");
        T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [SetUp] public void Setup()
        {
            _random = Random.state;
            var go = Keep(new GameObject("Giant strike owner")); go.transform.position = new Vector2(9400, 7300);
            _owner = go.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", F).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 150; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.AreaOfEffect }) _owner.GetStat(stat).BaseValue = 0;
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, Skill, 0 });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        LuckyShotHitRecorder Target(float x, float y = 0, bool compound = false)
        {
            var go = Keep(new GameObject("Giant strike target")); go.layer = 7;
            go.transform.position = _owner.transform.position + new Vector3(x, y, 0);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            if (compound) { var extra = new GameObject("Second hurtbox"); extra.layer = 7; extra.transform.SetParent(go.transform, false); extra.AddComponent<CircleCollider2D>().radius = .05f; }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        void Strike()
        {
            Physics2D.SyncTransforms();
            var ctx = (SkillStepContext)typeof(SkillStepRunner).GetField("_ctx", F).GetValue(_runner);
            ctx.AoeScale = 1 + _owner.GetValue(StatType.AreaOfEffect) / 100;
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageCircle", F).Invoke(_runner, new object[] { Skill.Recipe.Steps.IndexOf(Hit), Hit });
        }
        [Test] public void HammerSpecialHasCostsSingleSlowStrikeAndOpaquePixelLabArt()
        {
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.Cooldown, Is.EqualTo(15)); Assert.That(Skill.ManaCost, Is.EqualTo(35));
            Assert.That(Hit.GetFloat("DamageMultiplier"), Is.EqualTo(5)); Assert.That(Skill.SwingStyleId, Is.EqualTo("overhead"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(WeightOfAGiantBuilder.PoolPath).PossibleSkills.Count(entry => entry.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            var icon = SkillIconBackgrounds.Read(Skill.Icon);
            try { Assert.That(icon.GetPixels32().All(pixel => pixel.a == 255), Is.True); } finally { Object.DestroyImmediate(icon); }
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(WeightOfAGiantBuilder.Root + "Strike.png").OfType<Sprite>().Count(), Is.EqualTo(12));
            Assert.That(Skill.Recipe.Steps.Count(step => step.StepDefinition.Id.StartsWith("DealDamage")), Is.EqualTo(1));
            Assert.That(Skill.Recipe.Steps.Any(step => step.StepDefinition.Id.StartsWith("ApplyStatus")), Is.False);
        }
        [TestCase(74, 0)] [TestCase(75, 5)] [TestCase(149, 5)] [TestCase(150, 10)] [TestCase(750, 50)]
        public void HealthBreakpointsAddPhysicalBeforeFiveTimesEffectiveness(float health, float bonus)
        {
            _owner.GetStat(StatType.MaxHealth).BaseValue = health; var target = Target(1.2f); Strike();
            Assert.That(target.Hits.Single().Physical, Is.EqualTo((100 + bonus) * 5).Within(.001));
            Assert.That(_owner.GetValue(StatType.DamagePhysical), Is.EqualTo(100));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 0).Hit.PhysicalPerHit, Is.EqualTo(target.Hits[0].Physical).Within(.001));
        }
        [Test] public void ModifiedMaximumHealthCountsEvenWhenCurrentHealthIsLowAndCritStillWorks()
        {
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100;
            _owner.GetStat(StatType.MaxHealth).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            _owner.Health.SetCurrent(1); _owner.GetStat(StatType.CritChance).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 200;
            var target = Target(1.2f); Strike(); Assert.That(target.Hits.Single().Physical, Is.EqualTo(1100).Within(.001)); Assert.That(target.Hits[0].IsCrit, Is.True);
        }
        [TestCase(1f)] [TestCase(-1f)] public void EllipticalImpactHitsEachFrontTargetOnceAndMissesOutsideItsRadius(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var near = Target(facing * .6f, compound: true); var far = Target(facing * 2.2f);
            var behind = Target(-facing); var corner = Target(facing * 2.4f, .6f); var distant = Target(facing * 3.2f);
            Strike(); Assert.That(near.Hits.Count, Is.EqualTo(1)); Assert.That(far.Hits.Count, Is.EqualTo(1));
            foreach (var target in new[] { behind, corner, distant }) Assert.That(target.Hits, Is.Empty);
        }
        [Test] public void AreaOfEffectExpandsTheImpactRadius()
        {
            var target = Target(3.2f); Strike(); Assert.That(target.Hits, Is.Empty);
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100; Strike(); Assert.That(target.Hits.Count, Is.EqualTo(1));
        }
        [Test] public void AttackSpeedScalesLongWindupAndRecoveryTogether()
        {
            var resolve = typeof(SkillBehaviour).GetMethod("ResolveActionSpeed", F);
            float duration = 1 / (float)resolve.Invoke(_runner, null);
            Assert.That(duration, Is.EqualTo(2.5f).Within(.001)); Assert.That(duration * Hit.StartPercentPipeline, Is.EqualTo(1.75f).Within(.001));
            _owner.GetStat(StatType.AttackSpeed).BaseValue = 2;
            Assert.That(1 / (float)resolve.Invoke(_runner, null), Is.EqualTo(1.25f).Within(.001));
        }
        [Test] public void CancelDuringWindupPreventsHitAndReleasesThePlantedMovementLock()
        {
            var movement = _owner.gameObject.AddComponent<PlayerMovement>(); movement.enabled = false;
            var move = _owner.GetComponent<SkillMovementControl>(); move.Initialize(_owner);
            typeof(SkillStepRunner).GetField("_moveCtrl", F).SetValue(_runner, move);
            var skill = Keep(Object.Instantiate(Skill)); var recipe = Keep(ScriptableObject.CreateInstance<SkillRecipeSO>());
            recipe.Steps = Skill.Recipe.Steps.Where(step => step.StepDefinition.Id == "MovementLock" || step == Hit).ToList(); skill.Recipe = recipe;
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", F).Invoke(_runner, new object[] { _owner, skill, 0 });
            typeof(SkillStepRunner).GetField("_moveCtrl", F).SetValue(_runner, move);
            typeof(SkillStepRunner).GetField("_ctx", F).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, TotalDuration = 100000, AoeScale = 1 });
            var target = Target(1.2f); var sequence = (IEnumerator)typeof(SkillStepRunner).GetMethod("RunRecipe", F).Invoke(_runner, null);
            try { Assert.That(sequence.MoveNext(), Is.True); Assert.That(movement.IsMovementLocked, Is.True); _runner.Cancel(); Assert.That(movement.IsMovementLocked, Is.False); Assert.That(sequence.MoveNext(), Is.False); Assert.That(target.Hits, Is.Empty); }
            finally { (sequence as System.IDisposable)?.Dispose(); }
        }
        [TestCase("en", "The Weight of a Giant", "Every 75", "very slow wind-up", "Area of Effect")]
        [TestCase("ru", "Вес великана", "Каждые 75", "очень долгого замаха", "область действия")]
        public void BothLocalesExplainHealthLinkSlowWindupAndArea(string code, string name, string health, string windup, string area)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name)); Assert.That(table.GetEntry(Skill.DescriptionKey)?.Value, Is.Not.Empty);
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            foreach (var part in new[] { health, windup, area, "+5", "500%" }) Assert.That(text, Does.Contain(part));
        }
    }
}
