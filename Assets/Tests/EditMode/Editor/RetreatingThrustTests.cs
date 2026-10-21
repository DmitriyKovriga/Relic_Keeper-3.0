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
    public class RetreatingThrustTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8500, 6500);
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private Random.State _random;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(RetreatingThrustBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "RetreatingThrust");
        private GameObject Keep(GameObject go) { _objects.Add(go); return go; }
        [SetUp]
        public void Setup()
        {
            _random = Random.state;
            var owner = Keep(new GameObject("Retreating thrust owner")); owner.transform.position = Origin;
            _owner = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100;
            foreach (var stat in new[] { StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning, StatType.CritChance, StatType.Evasion }) _owner.GetStat(stat).BaseValue = 0;
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
        private LuckyShotHitRecorder Target(float x, float y = 0, bool compound = false)
        {
            var go = Keep(new GameObject("Thrust target")); go.layer = 7; go.transform.position = Origin + new Vector2(x, y);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            if (compound)
            {
                var child = new GameObject("Second hurtbox"); child.layer = 7; child.transform.SetParent(go.transform, false); child.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            }
            return go.AddComponent<LuckyShotHitRecorder>();
        }
        private void Strike() { Physics2D.SyncTransforms(); typeof(SkillStepRunner).GetMethod("ExecuteRetreatingThrust", Flags).Invoke(_runner, new object[] { 3, Step }); }
        private PlayerMovement Movement(float facing)
        {
            var body = _owner.gameObject.AddComponent<Rigidbody2D>(); body.mass = 1; body.gravityScale = 2;
            var movement = _owner.gameObject.AddComponent<PlayerMovement>(); typeof(PlayerMovement).GetMethod("Awake", Flags).Invoke(movement, null);
            movement.ForceFaceDirection(facing); movement.SetMovementLock(true); return movement;
        }
        [Test]
        public void SpecialSwordSkillHasCooldownManaAndPoolRegistration()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(5)); Assert.That(Skill.ManaCost, Is.EqualTo(10)); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(Skill.DamageContextTags, Is.EqualTo(StatContextTagFlags.Attack | StatContextTagFlags.Melee)); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(RetreatingThrustBuilder.PoolPath).PossibleSkills.Any(value => value.Skill == Skill), Is.True);
            Assert.That(Resources.Load<ItemDatabaseSO>(ProjectPaths.ResourcesItemDatabase).AllSkills, Does.Contain(Skill));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ThrustHitsNarrowFrontLineOnceAndExcludesBehindHighAndOutOfRangeTargets(float facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var near = Target(facing * 1, compound: true); var far = Target(facing * 3);
            var behind = Target(-facing); var high = Target(facing * 2, 1); var outside = Target(facing * 3.6f);
            Strike(); Assert.That(near.Hits.Single().Physical, Is.EqualTo(250).Within(.001)); Assert.That(far.Hits.Count, Is.EqualTo(1));
            Assert.That(behind.Hits.Count + high.Hits.Count + outside.Hits.Count, Is.Zero);
        }
        [Test]
        public void WallsBlockThrustButTriggersDoNot()
        {
            var before = Target(1); var after = Target(3);
            var wall = Keep(new GameObject("Thrust wall")); wall.layer = 6; wall.transform.position = Origin + Vector2.right * 2;
            var collider = wall.AddComponent<BoxCollider2D>(); collider.size = new Vector2(.2f, 3);
            Strike(); Assert.That(before.Hits.Count, Is.EqualTo(1)); Assert.That(after.Hits, Is.Empty);
            collider.isTrigger = true; Strike(); Assert.That(after.Hits.Count, Is.EqualTo(1));
        }
        [TestCase(0, 8)] [TestCase(100, 9)] [TestCase(1000, 18)]
        public void EvasionAddsFlatCritOnlyInsideThisSkill(float evasion, float chance)
        {
            _owner.GetStat(StatType.CritChance).BaseValue = 8; _owner.GetStat(StatType.Evasion).BaseValue = evasion;
            var scoped = StatLinkModifiers.Apply(_owner, Skill.StatLinks);
            Assert.That(scoped.GetValue(StatType.CritChance), Is.EqualTo(chance).Within(.001));
            Assert.That(_owner.GetValue(StatType.CritChance), Is.EqualTo(8)); Assert.That(_owner.GetValue(StatType.Evasion), Is.EqualTo(evasion));
        }
        [Test]
        public void ConversionUsesModifiedEvasionAndRespectsExistingCritModifiers()
        {
            _owner.GetStat(StatType.Evasion).BaseValue = 200;
            _owner.GetStat(StatType.Evasion).AddModifier(new StatModifier(100, StatModType.Flat));
            _owner.GetStat(StatType.Evasion).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            _owner.GetStat(StatType.CritChance).BaseValue = 8;
            _owner.GetStat(StatType.CritChance).AddModifier(new StatModifier(100, StatModType.PercentAdd));
            Assert.That(StatLinkModifiers.Apply(_owner, Skill.StatLinks).GetValue(StatType.CritChance), Is.EqualTo(28).Within(.001));
        }
        [Test]
        public void EvasionCanGuaranteeCritAndUsesExistingCriticalMultiplier()
        {
            _owner.GetStat(StatType.Evasion).BaseValue = 10000; _owner.GetStat(StatType.CritMultiplier).BaseValue = 250;
            var target = Target(2); Strike(); Assert.That(target.Hits.Single().IsCrit, Is.True);
            Assert.That(target.Hits.Single().Physical, Is.EqualTo(625).Within(.001));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 1).Hit.TotalPerHit, Is.EqualTo(625).Within(.001));
        }
        [TestCase(1)] [TestCase(-1)]
        public void ImmediateBackwardUpwardHopPreservesFacingAndNormalGravity(float facing)
        {
            var movement = Movement(facing); var body = _owner.GetComponent<Rigidbody2D>(); body.gravityScale = 2;
            var target = Target(facing * 2); Strike();
            Assert.That(target.Hits.Count, Is.EqualTo(1)); Assert.That(body.linearVelocity.x * facing, Is.LessThan(0)); Assert.That(body.linearVelocity.y, Is.GreaterThan(0));
            Assert.That(Mathf.Sign(_owner.transform.localScale.x), Is.EqualTo(facing)); Assert.That(body.gravityScale, Is.EqualTo(2));
            typeof(PlayerMovement).GetMethod("FixedUpdate", Flags).Invoke(movement, null);
            Assert.That(body.linearVelocity.x * facing, Is.LessThan(0), "Recovery lock must not erase the retreat impulse.");
        }
        [Test]
        public void MissStillRetreatsAndCancellationReleasesMovementLock()
        {
            var movement = Movement(1); Strike(); Assert.That(_owner.GetComponent<Rigidbody2D>().linearVelocity.x, Is.LessThan(0));
            var bridge = _owner.GetComponent<SkillMovementControl>(); bridge.Initialize(_owner);
            typeof(SkillStepRunner).GetField("_moveCtrl", Flags).SetValue(_runner, bridge); _runner.Cancel(); Assert.That(movement.IsMovementLocked, Is.False);
        }
        [Test]
        public void RetreatDoesNotStartAnInvulnerableDodge()
        {
            Movement(1); var attack = _owner.gameObject.AddComponent<PlayerAttackInput>(); Strike();
            Assert.That(attack.IsDamageImmune, Is.False); Assert.That(Skill.Recipe.Steps.Any(value => value.StepDefinition.Id.Contains("Status") || value.StepDefinition.Id.Contains("Parry")), Is.False);
        }
        [TestCase(1)] [TestCase(-1)]
        public void PhysicalRetreatCannotPassThroughRearWall(float facing)
        {
            var movement = Movement(facing);
            var wall = Keep(new GameObject("Rear wall")); wall.layer = 6; wall.transform.position = Origin - Vector2.right * (facing * 1.5f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 10);
            var previous = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
            try
            {
                Strike();
                for (int i = 0; i < 25; i++) { typeof(PlayerMovement).GetMethod("FixedUpdate", Flags).Invoke(movement, null); Physics2D.Simulate(.02f); }
                float travelled = (_owner.transform.position.x - Origin.x) * -facing;
                Assert.That(travelled, Is.GreaterThan(.3f)); Assert.That(travelled, Is.LessThan(1.1f));
            }
            finally { Physics2D.simulationMode = previous; }
        }
        [TestCase("en", "Retreating Thrust", "no invulnerability")]
        [TestCase("ru", "Укол с отходом", "не даёт неуязвимости")]
        public void BothLocalesDescribeThrustRetreatAndScopedCrit(string code, string name, string warning)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset");
            Assert.That(table.GetEntry(Skill.NameKey)?.Value, Is.EqualTo(name));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain("250%")); Assert.That(text, Does.Contain(warning));
            Assert.That(text, Does.Contain("1%")); Assert.That(SkillDescriptionGenerator.IsKnownStepId("RetreatingThrust"), Is.True);
        }
    }
}
