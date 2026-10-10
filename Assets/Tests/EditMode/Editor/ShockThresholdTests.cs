using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Items.Affixes;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ShockThresholdTests
    {
        private GameObject _source;
        private GameObject _enemy;
        private PlayerStats _stats;
        private EnemyStats _targetStats;
        private EnemyHealth _health;
        private AilmentController _ailments;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = Random.state;
            _source = new GameObject("ShockSource");
            _stats = _source.AddComponent<PlayerStats>();
            _stats.GetStat(StatType.ShockChance).BaseValue = 100f;
            _stats.GetStat(StatType.ShockDuration).BaseValue = 2f;
            _enemy = new GameObject("ShockEnemy");
            _targetStats = _enemy.AddComponent<EnemyStats>();
            _targetStats.Initialize(null, 1);
            Assert.That(_targetStats.TryGetStat(StatType.MaxHealth, out CharacterStat hp), Is.True);
            hp.BaseValue = 1000f;
            _health = _enemy.AddComponent<EnemyHealth>();
            _health.Initialize();
            _ailments = _enemy.AddComponent<AilmentController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_enemy);
            Object.DestroyImmediate(_source);
            Random.state = _randomState;
        }

        [Test]
        public void Defaults_ArePercentageScalars()
        {
            Assert.That(_stats.GetValue(StatType.ShockApplicationEffectiveness), Is.EqualTo(100f));
            Assert.That(_stats.GetValue(StatType.ShockEffectMagnitude), Is.EqualTo(50f));
            _stats.Initialize(null);
            Assert.That(_stats.GetValue(StatType.ShockApplicationEffectiveness), Is.EqualTo(100f));
            Assert.That(_stats.GetValue(StatType.ShockEffectMagnitude), Is.EqualTo(50f));
            foreach (StatType type in new[] { StatType.ShockApplicationEffectiveness, StatType.ShockEffectMagnitude })
            {
                Assert.That(StatsDatabaseSO.DefaultFormatFor(type), Is.EqualTo(StatDisplayFormat.Percent));
                Assert.That(StatsDatabaseSO.DefaultValueUnitFor(type), Is.EqualTo(StatValueUnit.Percent));
                Assert.That(StatsDatabaseSO.DefaultSemanticKindFor(type), Is.EqualTo(StatSemanticKind.CombatScalar));
            }
        }

        [Test]
        public void SuccessfulProcs_AccumulateUntilThirtyPercentOfMaxHealth()
        {
            Assert.That(Proc(100f), Is.False);
            Assert.That(Proc(199f), Is.False);
            Assert.That(_ailments.IsShocked, Is.False);
            Assert.That(Proc(1f), Is.True);
            Assert.That(_ailments.DamageTakenMoreMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void MixedHit_UsesOnlyLightningWithoutShareRequirement()
        {
            Assert.That(Proc(100f, 10000f), Is.False);
            Assert.That(Proc(200f, 10000f), Is.True);
        }

        [Test]
        public void FailedAndAvoidedProcs_DoNotReduceThreshold()
        {
            _stats.GetStat(StatType.ShockChance).BaseValue = 0f;
            Assert.That(Proc(1000f), Is.False);
            _stats.GetStat(StatType.ShockChance).BaseValue = 100f;
            _targetStats.AddModifier(StatType.ChanseToAvoidShock, new StatModifier(100f, StatModType.Flat));
            Assert.That(Proc(1000f), Is.False);
            Assert.That(_targetStats.TryGetStat(StatType.ChanseToAvoidShock, out CharacterStat avoid), Is.True);
            avoid.ClearAllModifiers();
            Assert.That(Proc(299f), Is.False);
            Assert.That(Proc(1f), Is.True);
        }

        [Test]
        public void PhysicalHit_DoesNotReduceThreshold()
        {
            Assert.That(Proc(0f, 10000f), Is.False);
            Assert.That(Proc(299f), Is.False);
        }

        [Test]
        public void ApplicationEffectiveness_ScalesThresholdDamage()
        {
            _stats.GetStat(StatType.ShockApplicationEffectiveness).AddModifier(new StatModifier(100f, StatModType.PercentAdd));
            Assert.That(Proc(149f), Is.False);
            Assert.That(Proc(1f), Is.True);
        }

        [Test]
        public void ZeroEffectiveness_IsRespected()
        {
            _stats.GetStat(StatType.ShockApplicationEffectiveness).AddModifier(new StatModifier(100f, StatModType.PercentLess));
            Assert.That(Proc(10000f), Is.False);
        }

        [Test]
        public void Threshold_UsesMaxHealthEvenAfterTakingDamage()
        {
            _health.ApplyPureDamage(800f, null, "Physical");
            Assert.That(Proc(299f), Is.False);
            Assert.That(Proc(1f), Is.True);
        }

        [Test]
        public void Shock_RefreshesAndRequiresFreshThresholdAfterExpiry()
        {
            Assert.That(Proc(300f), Is.True);
            Tick(1.5f);
            Assert.That(Proc(1f), Is.True);
            Tick(1.9f);
            Assert.That(_ailments.IsShocked, Is.True);
            Tick(0.2f);
            Assert.That(_ailments.IsShocked, Is.False);
            Assert.That(_ailments.DamageTakenMoreMultiplier, Is.EqualTo(1f));
            Assert.That(Proc(299f), Is.False);
            Assert.That(Proc(1f), Is.True);
        }

        [Test]
        public void ReinitializedEnemy_ClearsPartialThresholdAndActiveShock()
        {
            Proc(299f);
            _health.Initialize();
            Assert.That(Proc(1f), Is.False);
            Assert.That(Proc(299f), Is.True);
            _health.Initialize();
            Assert.That(_ailments.IsShocked, Is.False);
            Assert.That(Proc(299f), Is.False);
        }

        [Test]
        public void DisabledEnemy_ClearsPartialThreshold()
        {
            Proc(299f);
            // EditMode does not dispatch MonoBehaviour lifecycle messages for these objects.
            typeof(AilmentController).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_ailments, null);
            Assert.That(Proc(1f), Is.False);
        }

        [Test]
        public void ZeroMagnitude_IsRespectedAndDoesNotAmplifyDamage()
        {
            _stats.GetStat(StatType.ShockEffectMagnitude).AddModifier(new StatModifier(100f, StatModType.PercentLess));
            Assert.That(Proc(300f), Is.True);
            Assert.That(_ailments.DamageTakenMoreMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void PlayerTarget_RetainsImmediateShockApplication()
        {
            var playerAilments = _source.AddComponent<AilmentController>();
            Assert.That(playerAilments.TryApplyShock(_stats, _stats, new DamageSnapshot(_stats) { Lightning = 1f }), Is.True);
            Assert.That(playerAilments.DamageTakenMoreMultiplier, Is.EqualTo(1.5f));
        }

        [Test]
        public void Magnitude_IsSnapshotAndWeakerRefreshDoesNotReplaceIt()
        {
            _stats.GetStat(StatType.ShockEffectMagnitude).BaseValue = 80f;
            Assert.That(Proc(300f), Is.True);
            _stats.GetStat(StatType.ShockEffectMagnitude).BaseValue = 20f;
            Assert.That(Proc(1f), Is.True);
            Assert.That(_ailments.ShockIncreasedDamageTaken, Is.EqualTo(80f));
        }

        [Test]
        public void Shock_IsAdditiveWithIncreasedDamageTakenBeforeMore()
        {
            Proc(300f);
            _targetStats.AddModifier(StatType.DamageTaken, new StatModifier(20f, StatModType.PercentAdd));
            _targetStats.AddModifier(StatType.DamageTaken, new StatModifier(100f, StatModType.PercentMult));
            float damage = DamageTakenCalculator.Apply(100f, _targetStats, _enemy.transform, out _, out _, out _);
            Assert.That(damage, Is.EqualTo(340f).Within(0.001f));
        }

        [Test]
        public void Shock_AmplifiesDamageOverTime()
        {
            Proc(300f);
            _health.ApplyPureDamage(100f, null, "Poison");
            Assert.That(_health.CurrentHealth, Is.EqualTo(850f).Within(0.001f));
        }

        [TestCase(StatType.ShockApplicationEffectiveness)]
        [TestCase(StatType.ShockEffectMagnitude)]
        public void AffixFamilies_HaveFiveTiersAndGlobalPercentageStats(StatType type)
        {
            var affixes = Resources.LoadAll<ItemAffixSO>($"Affixes/ByStat/Ailments/{type}");
            Assert.That(affixes.Length, Is.EqualTo(15));
            foreach (ItemAffixSO affix in affixes)
            {
                Assert.That(affix.Tiers.Count, Is.EqualTo(5));
                foreach (var tier in affix.Tiers)
                    Assert.That(tier.Stats[0].Stat, Is.EqualTo(type));
            }
        }

        private bool Proc(float lightning, float physical = 0f)
        {
            return _ailments.TryApplyShock(_stats, _stats, new DamageSnapshot(_stats) { Lightning = lightning, Physical = physical });
        }

        private void Tick(float seconds)
        {
            typeof(AilmentController).GetMethod("UpdateShock", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_ailments, new object[] { seconds });
        }
    }
}
