using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Skills;
using Scripts.Stats;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CombatBaselineTests
    {
        private GameObject _source, _target;
        private EnemyHealth _health;
        private EnemyStats _stats;
        private SkillDataSO _data;

        [SetUp]
        public void SetUp()
        {
            _source = new GameObject("Baseline test source"); _target = new GameObject("Baseline test target");
            _stats = _target.AddComponent<EnemyStats>(); _stats.Initialize(null, 1);
            _health = _target.AddComponent<EnemyHealth>(); _health.DestroyOnDeath = false; _health.Initialize();
        }
        [TearDown]
        public void TearDown()
        {
            if (_data != null) Object.DestroyImmediate(_data);
            Object.DestroyImmediate(_target); Object.DestroyImmediate(_source);
        }

        [Test]
        public void PositiveDirectHitRaisesTwoBusNotificationsForOneUnmodifiedSnapshot()
        {
            int taken = 0, dealt = 0; var damage = new DamageSnapshot(_source) { Physical = 10 };
            void Observe(GameplayEventContext c)
            {
                if (c.Source != _source) return;
                if (c.Type == GameplayEventType.DamageTaken) { taken++; Assert.That(c.Amount, Is.GreaterThan(0)); }
                if (c.Type == GameplayEventType.DamageDealt) dealt++;
                Assert.That(c.Damage, Is.SameAs(damage));
            }
            GameplayEventBus.EventRaised += Observe;
            try
            {
                Assert.That(_health.TakeDamage(damage), Is.True);
                Assert.That(taken, Is.EqualTo(1)); Assert.That(dealt, Is.EqualTo(1));
                Assert.That(damage.Physical, Is.EqualTo(10), "The shared snapshot must keep raw damage.");
            }
            finally { GameplayEventBus.EventRaised -= Observe; }
        }

        [Test]
        public void FullyMitigatedHitReturnsTrueButHasNoPositiveDamageNotification()
        {
            _stats.AddModifier(StatType.DamageTaken, new StatModifier(-100, StatModType.Flat, this));
            float before = _health.CurrentHealth; int notifications = 0;
            void Observe(GameplayEventContext c) { if (c.Source == _source) notifications++; }
            GameplayEventBus.EventRaised += Observe;
            try
            {
                Assert.That(_health.TakeDamage(new DamageSnapshot(_source) { Physical = 10 }), Is.True);
                Assert.That(_health.CurrentHealth, Is.EqualTo(before)); Assert.That(notifications, Is.Zero);
            }
            finally { GameplayEventBus.EventRaised -= Observe; }
        }

        [Test]
        public void PureDamageTickRaisesPositiveDamageWithNonDirectClassification()
        {
            int taken = 0; float before = _health.CurrentHealth;
            void Observe(GameplayEventContext c)
            {
                if (c.Source != _source || c.Type != GameplayEventType.DamageTaken) return;
                taken++; Assert.That(c.Damage.IsDirectHit, Is.False); Assert.That(c.Amount, Is.GreaterThan(0));
            }
            GameplayEventBus.EventRaised += Observe;
            try
            {
                _health.ApplyPureDamage(3, _source, "Ignite");
                Assert.That(taken, Is.EqualTo(1)); Assert.That(_health.CurrentHealth, Is.LessThan(before));
            }
            finally { GameplayEventBus.EventRaised -= Observe; }
        }

        private BaselineCountingSkill Skill()
        {
            var owner = _source.AddComponent<PlayerStats>();
            if (owner.Mana == null) typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            owner.Initialize(null); owner.GetStat(StatType.MaxMana).BaseValue = 100; owner.NotifyChanged(); owner.Mana.RestoreFull();
            _data = ScriptableObject.CreateInstance<SkillDataSO>(); _data.ManaCost = 1;
            var skill = _source.AddComponent<BaselineCountingSkill>(); skill.Initialize(owner, _data); return skill;
        }

        [Test]
        public void AcceptedCastCounterIgnoresCooldownAndInsufficientManaRejections()
        {
            var skill = Skill(); var owner = _source.GetComponent<PlayerStats>();
            owner.Mana.Decrease(100); skill.TryCast(); Assert.That(skill.EditorAcceptedCastSequence, Is.Zero);
            owner.Mana.RestoreFull(); _data.Cooldown = 5;
            skill.TryCast(); skill.TryCast();
            Assert.That(skill.EditorAcceptedCastSequence, Is.EqualTo(1)); Assert.That(skill.Executions, Is.EqualTo(1));
        }

        [Test]
        public void AcceptedCastCounterIgnoresBusyInputAndCountsIndependentAcceptedUses()
        {
            var skill = Skill(); skill.SetBusy(true); skill.TryCast(); Assert.That(skill.EditorAcceptedCastSequence, Is.Zero);
            skill.SetBusy(false); skill.TryCast(); skill.TryCast();
            Assert.That(skill.EditorAcceptedCastSequence, Is.EqualTo(2)); Assert.That(skill.Executions, Is.EqualTo(2));
        }
    }

    public sealed class BaselineCountingSkill : SkillBehaviour
    {
        public int Executions { get; private set; }
        public void SetBusy(bool busy) => _isCasting = busy;
        protected override void Execute() => Executions++;
    }
}
