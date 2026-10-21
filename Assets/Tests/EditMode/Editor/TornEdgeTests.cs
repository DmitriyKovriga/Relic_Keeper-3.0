using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class TornEdgeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8900, 6800);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(TornEdgeBuilder.SkillPath);
        private static StepEntry Hit => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "DealDamageRectangle");
        private GameObject Keep(GameObject value) { _objects.Add(value); return value; }
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            var owner = Keep(new GameObject("Torn Edge owner")); owner.transform.position = Origin;
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 150;
            _owner.GetStat(StatType.BleedChance).BaseValue = 100; _owner.GetStat(StatType.BleedDamageMult).BaseValue = 50;
            _owner.GetStat(StatType.BleedDuration).BaseValue = 6; _owner.GetStat(StatType.AttackSpeed).BaseValue = 1;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.BleedDamage, StatType.BleedExtractionChance }) _owner.GetStat(stat).BaseValue = 0;
            owner.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = owner.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 1 });
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Random.state = _random;
        }
        private EnemyHealth Target(float x, float y = .4f, bool compound = false)
        {
            var go = Keep(new GameObject("Torn Edge victim")); go.layer = 7; go.transform.position = Origin + new Vector2(x, y);
            var stats = go.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            foreach (var type in new[] { StatType.Armor, StatType.Evasion, StatType.ChanseToAvoidBleed }) { stats.TryGetStat(type, out var stat); if (stat != null) stat.BaseValue = 0; }
            stats.TryGetStat(StatType.MaxHealth, out var health); health.BaseValue = 100000;
            var target = go.AddComponent<EnemyHealth>(); target.Initialize(); target.DestroyOnDeath = false;
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .15f; go.AddComponent<AilmentController>();
            if (compound) { var child = new GameObject("Extra hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<CircleCollider2D>().radius = .08f; }
            return target;
        }
        private void Strike(float facing = 1)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(_runner, new SkillStepContext { OwnerStats = _owner, AoeScale = 1 });
            Physics2D.SyncTransforms();
            typeof(SkillStepRunner).GetMethod("ExecuteDealDamageRectangle", Flags).Invoke(_runner, new object[] { 4, Hit });
        }
        [Test]
        public void SharedSpecialHasRequestedCostAndSlowAttackTiming()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(20)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.SkillSpeedMultiplier, Is.EqualTo(.7f));
            Assert.That(Hit.GetFloat("DamageMultiplier"), Is.EqualTo(2.5f)); Assert.That(Hit.StartPercentPipeline, Is.EqualTo(.55f));
            Assert.That(Skill.Icon, Is.Not.Null); Assert.That(Skill.EnablePushback, Is.False);
            foreach (string path in TornEdgeBuilder.PoolPaths) Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(path).PossibleSkills.Count(value => value.Skill == Skill), Is.EqualTo(1));
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(TornEdgeBuilder.Root + "Sweep.png").OfType<Sprite>().Count(), Is.EqualTo(8));
            Assert.That(SkillDpsPreview.ResolveCastsPerSecond(Skill, _owner), Is.EqualTo(.05f).Within(.001));
        }
        [TestCase(1)] [TestCase(-1)]
        public void SweepsMultipleFrontTargetsOnceWithoutHittingBehindOrBeyondReach(float facing)
        {
            var near = Target(facing * .5f, compound: true); var far = Target(facing * 2.8f);
            var behind = Target(-facing); var distant = Target(facing * 4); var high = Target(facing * 2, 3);
            Strike(facing);
            Assert.That(100000 - near.CurrentHealth, Is.EqualTo(250).Within(.001)); Assert.That(100000 - far.CurrentHealth, Is.EqualTo(250).Within(.001));
            foreach (var target in new[] { near, far }) Assert.That(target.GetComponent<AilmentController>().GetStackCount(AilmentType.Bleed), Is.EqualTo(1));
            foreach (var target in new[] { behind, distant, high }) Assert.That(target.CurrentHealth, Is.EqualTo(100000));
        }
        [TestCase(0, 750)] [TestCase(150, 3000)] [TestCase(250, 4500)] [TestCase(400, 6750)]
        public void NonCriticalHitGainsBleedPoolFromCurrentCritMultiplier(float multiplier, float pool)
        {
            _owner.GetStat(StatType.CritMultiplier).BaseValue = multiplier;
            var target = Target(1); Strike();
            Assert.That(100000 - target.CurrentHealth, Is.EqualTo(250).Within(.001));
            Assert.That(target.GetComponent<AilmentController>().ExtractStrongestBleed(100, _owner), Is.EqualTo(pool).Within(.001));
            Assert.That(_owner.GetValue(StatType.BleedDamageMult), Is.EqualTo(50));
        }
        [Test]
        public void CriticalHitAmplifiesPhysicalBaseAndBleedButNotOtherSkills()
        {
            _owner.GetStat(StatType.CritChance).BaseValue = 100; _owner.GetStat(StatType.CritMultiplier).BaseValue = 200;
            var target = Target(1); Strike();
            Assert.That(100000 - target.CurrentHealth, Is.EqualTo(500).Within(.001));
            Assert.That(target.GetComponent<AilmentController>().ExtractStrongestBleed(100, _owner), Is.EqualTo(7500).Within(.001));
            Assert.That(DamageCalculator.CalculateBleedDPS(_owner), Is.EqualTo(50));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 1).Hit.TotalPerHit, Is.EqualTo(500).Within(.001));
        }
        [Test]
        public void LinkUsesModifiedCritMultiplierAndPreservesBleedModifierLayers()
        {
            _owner.GetStat(StatType.CritMultiplier).AddModifier(new StatModifier(100, StatModType.Flat));
            _owner.GetStat(StatType.CritMultiplier).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            _owner.GetStat(StatType.BleedDamageMult).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            var scoped = StatLinkModifiers.Apply(_owner, Skill.StatLinks);
            Assert.That(scoped.GetValue(StatType.BleedDamageMult), Is.EqualTo(1100).Within(.001));
            Assert.That(_owner.GetValue(StatType.BleedDamageMult), Is.EqualTo(100));
        }
        [Test]
        public void ZeroBleedChanceDoesNotForceAWound()
        {
            _owner.GetStat(StatType.BleedChance).BaseValue = 0; var target = Target(1); Strike();
            Assert.That(100000 - target.CurrentHealth, Is.EqualTo(250).Within(.001));
            Assert.That(target.GetComponent<AilmentController>().GetStackCount(AilmentType.Bleed), Is.Zero);
        }
        [TestCase("en", "Torn Edge")] [TestCase("ru", "Рваная кромка")]
        public void BothLocalesDescribeTheStatLinkAndDamage(string code, string title)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(title));
            Assert.That(table.GetEntry(Skill.DescriptionKey)?.Value, Is.Not.Null.And.Not.Empty);
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code);
            Assert.That(text, Does.Contain("250%")); Assert.That(text, Does.Contain("100%"));
        }
    }
}
