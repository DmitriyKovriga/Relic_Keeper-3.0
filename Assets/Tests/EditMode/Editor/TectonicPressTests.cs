using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class TectonicHitRecorder : MonoBehaviour, IDamageable
    {
        public readonly List<DamageSnapshot> Hits = new List<DamageSnapshot>();
        public bool Accept = true;
        public bool TakeDamage(DamageSnapshot damage)
        {
            Hits.Add(damage);
            if (Accept) PushbackImpact.TryApply(damage, this);
            return Accept;
        }
    }

    public class TectonicPressTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(8100, 6100);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _owner;
        private SkillStepRunner _runner;
        private bool _oldEnemyCollision;
        private Random.State _oldRandom;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(TectonicPressBuilder.SkillPath);
        private static StepEntry Step => Skill.Recipe.Steps.Single(value => value.StepDefinition.Id == "TectonicPress");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp]
        public void Setup()
        {
            _oldRandom = Random.state;
            _oldEnemyCollision = Physics2D.GetIgnoreLayerCollision(7, 7); Physics2D.IgnoreLayerCollision(7, 7, true);
            var host = Keep(new GameObject("Tectonic owner")); host.transform.position = Origin;
            _owner = host.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_owner, null);
            _owner.GetStat(StatType.MaxHealth).BaseValue = 100; _owner.Health.RestoreFull();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 100; _owner.GetStat(StatType.CritChance).BaseValue = 0;
            _owner.GetStat(StatType.ShockwaveDamage).BaseValue = 100;
            host.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _runner = host.AddComponent<SkillStepRunner>();
            typeof(SkillStepRunner).GetMethod("InitializePulseEmitter", Flags).Invoke(_runner, new object[] { _owner, Skill, 1 });
            Physics2D.SyncTransforms();
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); Physics2D.IgnoreLayerCollision(7, 7, _oldEnemyCollision); Random.state = _oldRandom;
        }
        private TectonicHitRecorder Enemy(float x, float y = 0, bool compound = false)
        {
            var host = Keep(new GameObject("Tectonic victim") { layer = 7 }); host.transform.position = Origin + new Vector2(x, y);
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Zombie");
            host.AddComponent<EnemyStats>().Initialize(data, 1);
            host.AddComponent<EnemyLocomotion2D>().Initialize(null, data);
            host.GetComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            host.GetComponent<Rigidbody2D>().gravityScale = 0;
            if (compound)
            {
                var child = new GameObject("Additional hurtbox") { layer = 7 }; child.transform.SetParent(host.transform, false);
                child.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            }
            var recorder = host.AddComponent<TectonicHitRecorder>(); Physics2D.SyncTransforms(); return recorder;
        }
        private void Wall(float x, float y = 0, float width = .3f, float height = 4, string layer = "Ground")
        {
            var host = Keep(new GameObject("Tectonic obstacle") { layer = LayerMask.NameToLayer(layer) }); host.transform.position = Origin + new Vector2(x, y);
            host.AddComponent<BoxCollider2D>().size = new Vector2(width, height); Physics2D.SyncTransforms();
        }
        private void Cast()
        {
            var ctx = (SkillStepContext)typeof(SkillStepRunner).GetField("_ctx", Flags).GetValue(_runner);
            ctx.AoeScale = 1 + _owner.GetValue(StatType.AreaOfEffect) / 100;
            typeof(SkillStepRunner).GetMethod("ExecuteTectonicPress", Flags).Invoke(_runner, new object[] { 2, Step });
        }
        private void Advance(TectonicHitRecorder target, int ticks = 12)
        {
            var motion = target.GetComponent<EnemyLocomotion2D>(); var body = target.GetComponent<Rigidbody2D>();
            for (int i = 0; i < ticks; i++)
            {
                typeof(EnemyLocomotion2D).GetMethod("ApplyMovement", Flags).Invoke(motion, null);
                target.transform.position += (Vector3)(body.linearVelocity * Time.fixedDeltaTime); Physics2D.SyncTransforms();
            }
        }
        [Test]
        public void WallCollisionDeals250PercentStrikeAndOne200PercentShockwave()
        {
            var victim = Enemy(1.5f, compound: true); Wall(3.5f); Cast();
            Assert.That(victim.Hits.Count, Is.EqualTo(1)); Assert.That(victim.Hits[0].Physical, Is.EqualTo(250).Within(.01f));
            Assert.That(victim.Hits[0].EmpoweredPushback, Is.Not.Null);
            Advance(victim, 30);
            Assert.That(victim.Hits.Count, Is.EqualTo(2)); Assert.That(victim.Hits[1].Physical, Is.EqualTo(200).Within(.01f));
            Assert.That(victim.Hits[1].PushbackRating, Is.Zero); Assert.That(victim.Hits[1].EmpoweredPushback, Is.Null);
            Assert.That(victim.Hits[0].EmpoweredPushback.HasTriggered, Is.True);
        }
        [Test]
        public void EnemyCollisionWorksWhenPhysicalEnemyCollisionsAreDisabled()
        {
            var victim = Enemy(1.5f); var obstacle = Enemy(4, compound: true); Cast();
            Assert.That(obstacle.Hits, Is.Empty); Advance(victim);
            Assert.That(obstacle.Hits.Count, Is.EqualTo(1)); Assert.That(obstacle.Hits[0].Physical, Is.EqualTo(200).Within(.01f));
            Assert.That(victim.Hits.Count, Is.EqualTo(2));
        }
        [TestCase("Ground")]
        [TestCase("OneWayPlatform")]
        public void FloorAndCeilingDoNotConsumeTheChargeInOpenSpace(string layer)
        {
            var victim = Enemy(1.5f); Wall(3, -.75f, 20, .5f, layer); Wall(3, .75f, 20, .5f, layer);
            Cast(); var charge = victim.Hits.Single().EmpoweredPushback; Advance(victim);
            Assert.That(victim.Hits.Count, Is.EqualTo(1)); Assert.That(charge.HasTriggered, Is.False);
            Assert.That(victim.transform.position.x, Is.GreaterThan(Origin.x + 3));
        }
        [Test]
        public void EachInitialVictimOwnsAnIndependentSingleUseCharge()
        {
            var first = Enemy(1); var second = Enemy(2); Cast();
            var a = first.Hits.Single().EmpoweredPushback; var b = second.Hits.Single().EmpoweredPushback;
            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a.TryTrigger(Origin + Vector2.right), Is.True); Assert.That(a.TryTrigger(Origin), Is.False);
            Assert.That(b.TryTrigger(Origin + Vector2.right * 2), Is.True); Assert.That(b.TryTrigger(Origin), Is.False);
        }
        [Test]
        public void IncreasedPushbackTravelsFurtherWithoutChangingStrikeDamage()
        {
            var victim = Enemy(1.5f); Cast(); Advance(victim); float ordinary = victim.transform.position.x;
            victim.transform.position = Origin + Vector2.right * 1.5f; Physics2D.SyncTransforms();
            _owner.GetStat(StatType.Pushback).AddModifier(new StatModifier(500, StatModType.Flat));
            Cast(); Advance(victim);
            Assert.That(victim.transform.position.x, Is.GreaterThan(ordinary + .7f));
            Assert.That(victim.Hits.All(value => Mathf.Abs(value.Physical - 250) < .01f), Is.True);
        }
        [Test]
        public void ShockwaveDamageUsesNormalStatLayersAndDoesNotScaleTheStrike()
        {
            var stat = _owner.GetStat(StatType.ShockwaveDamage);
            stat.AddModifier(new StatModifier(50, StatModType.Flat)); stat.AddModifier(new StatModifier(20, StatModType.PercentAdd));
            stat.AddModifier(new StatModifier(50, StatModType.PercentMult));
            var victim = Enemy(1.5f); Wall(3.5f); Cast(); Advance(victim);
            Assert.That(stat.Value, Is.EqualTo(270).Within(.01f));
            Assert.That(victim.Hits[0].Physical, Is.EqualTo(250).Within(.01f));
            Assert.That(victim.Hits[1].Physical, Is.EqualTo(540).Within(.01f));
        }
        [Test]
        public void CharacterInitializationResetsShockwaveDamageToIts100PercentBase()
        {
            _owner.GetStat(StatType.ShockwaveDamage).BaseValue = 0;
            _owner.Initialize(null); Assert.That(_owner.GetValue(StatType.ShockwaveDamage), Is.EqualTo(100));
            _owner.GetStat(StatType.ShockwaveDamage).AddModifier(new StatModifier(50, StatModType.Flat));
            _owner.Initialize(null); Assert.That(_owner.GetValue(StatType.ShockwaveDamage), Is.EqualTo(100));
        }
        [Test]
        public void AreaExpandsBothCollisionPaddingAndShockwaveRadius()
        {
            var distant = Enemy(5.2f);
            var normal = new TectonicShockwave(_owner, _owner, Step, 1 << 7);
            normal.Detonate(Origin + Vector2.right * 3); Assert.That(distant.Hits, Is.Empty);
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var larger = new TectonicShockwave(_owner, _owner, Step, 1 << 7);
            Assert.That(larger.CollisionRadius, Is.EqualTo(normal.CollisionRadius * 2).Within(.001f));
            larger.Detonate(Origin + Vector2.right * 3); Assert.That(distant.Hits.Count, Is.EqualTo(1));
        }
        [Test]
        public void ExpandedCollisionRadiusCatchesANearMissedEnemy()
        {
            var victim = Enemy(1.5f); Enemy(3.5f, 1.2f);
            var motion = victim.GetComponent<EnemyLocomotion2D>();
            var normal = new TectonicShockwave(_owner, _owner, Step, 1 << 7).CreateCharge();
            motion.TryApplyPushbackFromHit(Origin.x, 1500); motion.ArmEmpoweredPushback(normal); Advance(victim);
            Assert.That(normal.HasTriggered, Is.False);
            victim.transform.position = Origin + Vector2.right * 1.5f; Physics2D.SyncTransforms();
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var larger = new TectonicShockwave(_owner, _owner, Step, 1 << 7).CreateCharge();
            motion.TryApplyPushbackFromHit(Origin.x, 1500); motion.ArmEmpoweredPushback(larger); Advance(victim);
            Assert.That(larger.HasTriggered, Is.True);
        }
        [Test]
        public void WaveCapturesWeaponStatsBeforeSwapOrOtherChanges()
        {
            var victim = Enemy(1.5f); Wall(3.5f); Cast();
            _owner.GetStat(StatType.DamagePhysical).BaseValue = 1000; _owner.GetStat(StatType.ShockwaveDamage).BaseValue = 500;
            Advance(victim); Assert.That(victim.Hits[1].Physical, Is.EqualTo(200).Within(.01f));
        }
        [Test]
        public void WavePreservesTheStrikesConversionAndCriticalRoll()
        {
            _owner.GetStat(StatType.CritChance).BaseValue = 100;
            _owner.GetStat(StatType.CritMultiplier).BaseValue = 200;
            _owner.GetStat(StatType.MeleeDamage).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            var converted = new StepEntry
            {
                StepDefinition = Step.StepDefinition, Overrides = new List<StepParamValue>(Step.Overrides),
                DamageConversions = PrismaticCycleCast.ConvertAllTo(DamageChannel.Cold)
            };
            var wave = new TectonicShockwave(_owner, _owner, converted, 1 << 7);
            _owner.GetStat(StatType.CritChance).BaseValue = 0; _owner.GetStat(StatType.DamagePhysical).BaseValue = 1000;
            var target = Enemy(3); wave.Detonate(Origin + Vector2.right * 3);
            var hit = target.Hits.Single(); Assert.That(hit.IsCrit, Is.True);
            Assert.That(hit.Physical, Is.Zero); Assert.That(hit.Cold, Is.EqualTo(600).Within(.01f));
        }
        [Test]
        public void ShockwavesDoNotDamageTargetsThroughWalls()
        {
            _owner.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var target = Enemy(5); Wall(4);
            new TectonicShockwave(_owner, _owner, Step, 1 << 7).Detonate(Origin + Vector2.right * 3);
            Assert.That(target.Hits, Is.Empty);
        }
        [TestCase("frozen")]
        [TestCase("immune")]
        [TestCase("fixed")]
        [TestCase("disabled")]
        [TestCase("rejected")]
        public void TargetsThatCannotBePushedNeverProduceCollisionWaves(string mode)
        {
            var victim = Enemy(1.5f); Wall(2.1f);
            if (mode == "frozen") victim.GetComponent<EnemyLocomotion2D>().SetFrozen(true);
            if (mode == "immune") { victim.GetComponent<EnemyStats>().TryGetStat(StatType.PushbackResist, out var stat); stat.BaseValue = 100; }
            if (mode == "fixed") victim.GetComponent<Rigidbody2D>().constraints |= RigidbodyConstraints2D.FreezePositionX;
            if (mode == "disabled") victim.GetComponent<EnemyLocomotion2D>().enabled = false;
            if (mode == "rejected") victim.Accept = false;
            Cast(); var charge = victim.Hits.Single().EmpoweredPushback; Advance(victim);
            Assert.That(victim.Hits.Count, Is.EqualTo(1)); Assert.That(charge.HasTriggered, Is.False);
        }
        [Test]
        public void UnrelatedPushAndPoolReuseCancelOldEmpoweredCollision()
        {
            var victim = Enemy(1.5f); Wall(3.5f); Cast(); var charge = victim.Hits.Single().EmpoweredPushback;
            victim.GetComponent<EnemyLocomotion2D>().TryApplyPushbackFromHit(Origin.x, 1500);
            Advance(victim); Assert.That(charge.HasTriggered, Is.False);
            victim.transform.position = Origin + Vector2.right * 1.5f; Physics2D.SyncTransforms();
            Cast(); charge = victim.Hits.Last().EmpoweredPushback;
            victim.GetComponent<EnemyLocomotion2D>().Initialize(null, Resources.Load<EnemyDataSO>("Enemy/SO_Zombie"));
            Advance(victim); Assert.That(charge.HasTriggered, Is.False);
        }
        [Test]
        public void DisablingMovementCancelsItsExistingCollisionCharge()
        {
            var victim = Enemy(1.5f); Wall(3.5f); Cast(); var charge = victim.Hits.Single().EmpoweredPushback;
            typeof(EnemyLocomotion2D).GetMethod("OnDisable", Flags).Invoke(victim.GetComponent<EnemyLocomotion2D>(), null);
            Advance(victim); Assert.That(charge.HasTriggered, Is.False); Assert.That(victim.Hits.Count, Is.EqualTo(1));
        }
        [Test]
        public void FacingAndWallsRestrictTheInitialStrike()
        {
            _owner.transform.localScale = new Vector3(-1, 1, 1);
            var front = Enemy(-1.5f); var behind = Enemy(1.5f); Cast();
            Assert.That(front.Hits.Count, Is.EqualTo(1)); Assert.That(behind.Hits, Is.Empty);
            Advance(front); Assert.That(front.transform.position.x, Is.LessThan(Origin.x - 3));
            front.transform.position = Origin - Vector2.right * 1.5f; Wall(-.8f); Cast();
            Assert.That(front.Hits.Count, Is.EqualTo(1));
        }
        [TestCase("en", "Tectonic Press", "one wave", "Shockwave Damage")]
        [TestCase("ru", "Тектонический пресс", "одной волны", "Урон ударной волны")]
        public void AssetsStatsAndLocalesMatchTheAcceptedNumbers(string code, string title, string once, string statName)
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(10)); Assert.That(Skill.ManaCost, Is.EqualTo(25));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special)); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SkillPoolSO>(TectonicPressBuilder.PoolPath).PossibleSkills.Count(value => value.Skill == Skill), Is.EqualTo(1));
            Assert.That(SkillDpsPreview.Build(Skill, _owner, 1).Hit.TotalPerHit, Is.EqualTo(250).Within(.01f));
            var text = SkillDescriptionGenerator.BuildAutomatic(Skill, code); Assert.That(text, Does.Contain(once));
            Assert.That(text, Does.Contain("250%")); Assert.That(text, Does.Contain("200%"));
            Assert.That(AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset").GetEntry(Skill.NameKey).LocalizedValue, Is.EqualTo(title));
            Assert.That(AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/MenuLabels_{code}.asset").GetEntry("stats.ShockwaveDamage").LocalizedValue, Is.EqualTo(statName));
            Assert.That(Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath).TryGetValue(StatType.ShockwaveDamage, out var value), Is.True);
            Assert.That(value, Is.EqualTo(100)); Assert.That(StatsDatabaseSO.DefaultCategoryFor(StatType.ShockwaveDamage), Is.EqualTo("Damage"));
        }
    }
}
