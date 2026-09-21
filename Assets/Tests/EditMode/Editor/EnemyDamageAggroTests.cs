using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills.Modules;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyDamageAggroTests
    {
        private GameObject _player;
        private GameObject _enemy;
        private EnemySensor2D _sensor;
        private EnemyHealth _health;
        private PlayerStats _playerStats;

        [SetUp]
        public void SetUp()
        {
            EnemyDataSO zombie = Resources.Load<EnemyDataSO>("Enemy/SO_Zombie");
            Assert.That(zombie, Is.Not.Null);

            _player = new GameObject("AggroTestPlayer");
            _player.transform.position = new Vector3(25f, 0f, 0f);
            _playerStats = _player.AddComponent<PlayerStats>();

            _enemy = new GameObject("AggroTestEnemy");
            EnemyStats stats = _enemy.AddComponent<EnemyStats>();
            stats.Initialize(zombie, 1);
            _sensor = _enemy.AddComponent<EnemySensor2D>();
            _sensor.Initialize(null, zombie);
            _health = _enemy.AddComponent<EnemyHealth>();
            _health.Initialize();

            Assert.That(_sensor.HasTarget, Is.False, "Player must start beyond ordinary aggro range.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_enemy != null)
                Object.DestroyImmediate(_enemy);
            if (_player != null)
                Object.DestroyImmediate(_player);
        }

        [Test]
        public void DirectPlayerHit_AggrosImmediatelyBeyondLoseRange()
        {
            Assert.That(_health.TakeDamage(new DamageSnapshot(_playerStats) { Physical = 5f }), Is.True);

            Assert.That(_sensor.HasTarget, Is.True);
            Assert.That(_sensor.TargetTransform, Is.EqualTo(_player.transform));
            Assert.That(_sensor.DirectionToTarget.x, Is.GreaterThan(0.99f));

            _sensor.Tick();
            Assert.That(_sensor.HasTarget, Is.True, "Next sensor tick must not discard damage aggro.");
        }

        [Test]
        public void PurePlayerDamage_AlsoAggrosBeyondLoseRange()
        {
            _health.ApplyPureDamage(5f, _player, "Poison");

            Assert.That(_sensor.HasTarget, Is.True);
            _sensor.Tick();
            Assert.That(_sensor.HasTarget, Is.True);
        }

        [Test]
        public void ScopedPlayerDamage_AlsoAggrosBeyondLoseRange()
        {
            var scoped = new ScopedStatsProvider(_playerStats, null);
            Assert.That(_health.TakeDamage(new DamageSnapshot(scoped) { Physical = 5f }), Is.True);

            Assert.That(_sensor.HasTarget, Is.True);
            Assert.That(_sensor.TargetTransform, Is.EqualTo(_player.transform));
        }

        [Test]
        public void LegacyMeleeDealer_PreservesPlayerAsDamageSource()
        {
            SkillDamageDealer dealer = _player.AddComponent<SkillDamageDealer>();
            dealer.Initialize(_playerStats);
            var target = new CaptureDamageable();
            dealer.DealDamage(new List<IDamageable> { target });

            Assert.That(target.Damage, Is.Not.Null);
            Assert.That(target.Damage.Source, Is.SameAs(_playerStats));
        }

        [Test]
        public void NonPlayerDamage_DoesNotAggroPlayer()
        {
            var hazard = new GameObject("AggroTestHazard");
            try
            {
                Assert.That(_health.TakeDamage(new DamageSnapshot(hazard) { Physical = 5f }), Is.True);
                Assert.That(_sensor.HasTarget, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(hazard);
            }
        }

        [Test]
        public void TrainingDummy_WithDetectionDisabled_RemainsPassiveWhenHit()
        {
            EnemyDataSO dummy = Resources.Load<EnemyDataSO>("Enemy/SO_Dummy");
            Assert.That(dummy, Is.Not.Null);
            _enemy.GetComponent<EnemyStats>().Initialize(dummy, 1);
            _health.Initialize();
            _sensor.Initialize(null, dummy);

            Assert.That(_health.TakeDamage(new DamageSnapshot(_playerStats) { Physical = 5f }), Is.True);
            Assert.That(_sensor.HasTarget, Is.False);
        }

        [Test]
        public void DamageAggro_ExpiresWhenPlayerStaysOutOfRange()
        {
            _health.TakeDamage(new DamageSnapshot(_playerStats) { Physical = 5f });
            FieldInfo untilField = typeof(EnemySensor2D).GetField(
                "_damageAggroUntil", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(untilField, Is.Not.Null);
            untilField.SetValue(_sensor, Time.time - 1f);

            _sensor.Tick();
            Assert.That(_sensor.HasTarget, Is.False);
        }

        private sealed class CaptureDamageable : IDamageable
        {
            public DamageSnapshot Damage;

            public bool TakeDamage(DamageSnapshot damage)
            {
                Damage = damage;
                return true;
            }
        }
    }
}
