using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CombatHitFeedbackTests
    {
        private GameObject _source, _target;
        private EnemyStats _stats;
        private EnemyHealth _health;
        private readonly List<ConfirmedHit> _hits = new List<ConfirmedHit>();
        private Random.State _random;
        private AttackImpactContext _attack;

        [SetUp]
        public void SetUp()
        {
            _random = Random.state;
            _source = new GameObject("Feedback attacker"); _source.transform.position = new Vector3(-3, 0);
            _target = new GameObject("Feedback receiver");
            _stats = _target.AddComponent<EnemyStats>(); _stats.Initialize(null, 1);
            _stats.TryGetStat(StatType.MaxHealth, out var maxHealth); maxHealth.BaseValue = 1000;
            _stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
            _health = _target.AddComponent<EnemyHealth>(); _health.DestroyOnDeath = false; _health.Initialize();
            _attack = new AttackImpactContext(null, 0);
            _hits.Clear(); CombatHitFeedback.HitConfirmed += Observe;
        }

        [TearDown]
        public void TearDown()
        {
            CombatHitFeedback.HitConfirmed -= Observe;
            Object.DestroyImmediate(_target); Object.DestroyImmediate(_source); Random.state = _random;
        }

        private void Observe(ConfirmedHit hit) { if (hit.Target == _target) _hits.Add(hit); }
        private DamageSnapshot Hit(float damage = 10, bool direct = true) => new DamageSnapshot(_source)
        {
            Physical = damage, IsDirectHit = direct,
            Impact = new AttackImpactContact(_attack, _source.transform.position, Vector2.zero)
        };

        [Test]
        public void PairedGameplayEventsYieldOnePostMitigationConfirmationWithoutMutatingSnapshot()
        {
            _stats.AddModifier(StatType.DamageTaken, new StatModifier(-50, StatModType.Flat, this));
            int notifications = 0; var snapshot = Hit();
            void ObserveBus(GameplayEventContext c)
            {
                if (c.Target == _target && (c.Type == GameplayEventType.DamageTaken || c.Type == GameplayEventType.DamageDealt)) notifications++;
            }
            GameplayEventBus.EventRaised += ObserveBus;
            try
            {
                Assert.That(_health.TakeDamage(snapshot), Is.True);
                Assert.That(notifications, Is.EqualTo(2)); Assert.That(_hits, Has.Count.EqualTo(1));
                Assert.That(_hits[0].Damage, Is.EqualTo(5).Within(.001f));
                Assert.That(snapshot.Physical, Is.EqualTo(10));
                Assert.That(_hits[0].Contact.Attack.CastId, Is.EqualTo(_attack.CastId));
            }
            finally { GameplayEventBus.EventRaised -= ObserveBus; }
        }

        [Test]
        public void MitigatedContactKeepsGameplayAcceptanceButDoesNotConsumeHitStop()
        {
            _stats.AddModifier(StatType.DamageTaken, new StatModifier(-100, StatModType.Flat, this));
            float hp = _health.CurrentHealth;
            Assert.That(_health.TakeDamage(Hit()), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(hp)); Assert.That(_hits, Is.Empty);
            Assert.That(_attack.HitStop.IsConsumed, Is.False);
        }

        [Test]
        public void EvadedContactDoesNotConfirmOrConsumeHitStop()
        {
            _stats.AddModifier(StatType.Evasion, new StatModifier(1000000, StatModType.Flat, this));
            // Find a deterministic successful evade roll, restoring it before the real receiver runs.
            Random.InitState(1729); Random.State state;
            do { state = Random.state; } while (!EvasionMitigation.RollDodge(EvasionMitigation.GetDodgeChancePercent(_stats)));
            Random.state = state;
            Assert.That(_health.TakeDamage(Hit()), Is.False);
            Assert.That(_hits, Is.Empty); Assert.That(_attack.HitStop.IsConsumed, Is.False);
        }

        [Test]
        public void BothPureTicksAndNonDirectSnapshotsRemainOutsideImpactFeedback()
        {
            float hp = _health.CurrentHealth;
            _health.ApplyPureDamage(3, _source, "Ignite"); _health.TakeDamage(Hit(4, false));
            Assert.That(_health.CurrentHealth, Is.EqualTo(hp - 7));
            Assert.That(_hits, Is.Empty); Assert.That(_attack.HitStop.IsConsumed, Is.False);
        }

        [Test]
        public void ExecutionConfirmsLethalOnceBeforeDeathAndRejectsFurtherHits()
        {
            var hit = Hit(910); hit.ExecutionThreshold = 10;
            _health.TakeDamage(hit);
            Assert.That(_health.IsDead, Is.True); Assert.That(_hits, Has.Count.EqualTo(1));
            Assert.That(_hits[0].IsLethal, Is.True); Assert.That(_hits[0].Damage, Is.EqualTo(910));
            Assert.That(_health.TakeDamage(Hit()), Is.False); Assert.That(_hits, Has.Count.EqualTo(1));
        }

        [Test]
        public void ContactCapturesOffsetBoundsAndProjectileDirectionBeforeTargetDestruction()
        {
            var body = _target.AddComponent<BoxCollider2D>(); body.offset = new Vector2(1, 2);
            _target.transform.position = new Vector3(5, 7);
            Physics2D.SyncTransforms();
            var hit = Hit(); hit.Impact = new AttackImpactContact(_attack, Vector2.zero, Vector2.up);
            _health.TakeDamage(hit); var confirmed = _hits[0];
            Object.DestroyImmediate(_target);
            Assert.That(confirmed.Position, Is.EqualTo(new Vector3(6, 9, 0)));
            Assert.That(confirmed.Direction, Is.EqualTo(Vector2.up));
            Assert.That(confirmed.PositionIsFallback, Is.True);
            Assert.That(confirmed.TargetId, Is.Not.Zero); Assert.That(confirmed.Damage, Is.EqualTo(10));
        }

        [Test]
        public void OneSharedSnapshotHasIndependentReceiverPositionsAndDamage()
        {
            var other = new GameObject("Second feedback target");
            try
            {
                var stats = other.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
                stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
                stats.AddModifier(StatType.DamageTaken, new StatModifier(-50, StatModType.Flat, this));
                var health = other.AddComponent<EnemyHealth>(); health.DestroyOnDeath = false; health.Initialize();
                other.transform.position = new Vector3(8, 4);
                ConfirmedHit second = default;
                void Capture(ConfirmedHit h) { if (h.Target == other) second = h; }
                CombatHitFeedback.HitConfirmed += Capture;
                try { var hit = Hit(); _health.TakeDamage(hit); health.TakeDamage(hit); Assert.That(hit.Physical, Is.EqualTo(10)); }
                finally { CombatHitFeedback.HitConfirmed -= Capture; }
                Assert.That(_hits, Has.Count.EqualTo(1)); Assert.That(_hits[0].Damage, Is.EqualTo(10));
                Assert.That(second.Damage, Is.EqualTo(5)); Assert.That(second.Position, Is.EqualTo(other.transform.position));
                Assert.That(second.Contact.Attack, Is.SameAs(_hits[0].Contact.Attack));
            }
            finally { Object.DestroyImmediate(other); }
        }

        [Test]
        public void IntentionalRepeatedHitsAllConfirmWhileHitStopIsConsumedOnce()
        {
            for (int i = 0; i < 12; i++) _health.TakeDamage(Hit());
            Assert.That(_hits, Has.Count.EqualTo(12));
            Assert.That(_health.CurrentHealth, Is.EqualTo(880));
            Assert.That(_attack.HitStop.IsConsumed, Is.True); Assert.That(_attack.HitStop.TryTrigger(), Is.False);
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void NonPositiveOrInvalidFinalDamageCannotConfirm(float amount)
        {
            Assert.That(CombatHitFeedback.Confirm(_health, Hit(), amount, false), Is.False);
            Assert.That(_hits, Is.Empty);
        }

        [Test]
        public void MissingHintsUseRootFallbackAndNeverInterpretWorldOriginAsHitContact()
        {
            _target.transform.position = new Vector3(9, 3);
            _health.TakeDamage(new DamageSnapshot(null) { Physical = 1 });
            Assert.That(_hits[0].Position, Is.EqualTo(_target.transform.position));
            Assert.That(_hits[0].Direction, Is.EqualTo(Vector2.right));
            Assert.That(_hits[0].PositionIsFallback, Is.True);
        }

        [Test]
        public void ProjectileClonesKeepCastWeaponAndSlotIdentityAfterSkillChanges()
        {
            var skill = ScriptableObject.CreateInstance<SkillDataSO>(); skill.ID = "original";
            var weapon = ScriptableObject.CreateInstance<WeaponItemSO>();
            try
            {
                var context = new AttackImpactContext(skill, 1, weapon);
                var data = new SkillProjectileLaunchData { ImpactContext = context, HitStopGate = context.HitStop };
                var fork = data.Clone(); var returning = fork.Clone(); skill.ID = "changed";
                Assert.That(returning.ImpactContext, Is.SameAs(context));
                Assert.That(returning.ImpactContext.SkillId, Is.EqualTo("original"));
                Assert.That(returning.ImpactContext.Weapon, Is.SameAs(weapon));
                Assert.That(returning.ImpactContext.SkillSlot, Is.EqualTo(1));
                Assert.That(returning.HitStopGate, Is.SameAs(context.HitStop));
            }
            finally { Object.DestroyImmediate(skill); Object.DestroyImmediate(weapon); }
        }

        [Test]
        public void ReusedProjectileResolvesRealDamageWithNewCastIdentityAndTravelDirection()
        {
            var owner = _source.AddComponent<PlayerStats>();
            if (owner.Mana == null) typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            owner.GetStat(StatType.DamagePhysical).BaseValue = 10;
            owner.GetStat(StatType.CritChance).BaseValue = 0;
            var host = new GameObject("Reusable feedback projectile", typeof(SpriteRenderer));
            try
            {
                host.transform.position = new Vector3(5000, 5000);
                var projectile = host.AddComponent<SkillProjectile>();
                var data = new SkillProjectileLaunchData { OwnerStats = owner, OwnerTransform = _source.transform,
                    Step = new StepEntry(), ImpactContext = _attack, StopOnWorld = false,
                    DamageContext = new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Projectile) };
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var initialize = typeof(SkillProjectile).GetMethod("Initialize", flags);
                var dealDamage = typeof(SkillProjectile).GetMethod("DealDamageScaled", flags);
                initialize.Invoke(projectile, new object[] { data, Vector2.up, false, null });
                var args = new object[] { _health, false, 1f }; dealDamage.Invoke(projectile, args);
                Assert.That(args[1], Is.True); Assert.That(_hits, Has.Count.EqualTo(1));
                Assert.That(_hits[0].Contact.Attack, Is.SameAs(_attack)); Assert.That(_hits[0].Direction, Is.EqualTo(Vector2.up));
                Assert.That(_hits[0].Damage, Is.GreaterThan(0));
                data.ImpactContext = new AttackImpactContext(null, 1);
                initialize.Invoke(projectile, new object[] { data, Vector2.left, false, null });
                dealDamage.Invoke(projectile, new object[] { _health, false, 1f });
                Assert.That(_hits, Has.Count.EqualTo(2)); Assert.That(_hits[1].Contact.Attack, Is.SameAs(data.ImpactContext));
                Assert.That(_hits[1].Contact.Attack.CastId, Is.Not.EqualTo(_hits[0].Contact.Attack.CastId));
                Assert.That(_hits[1].Direction, Is.EqualTo(Vector2.left));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void RejectedInputKeepsIdentityWhileEachAcceptedCastGetsANewOne()
        {
            var owner = _source.AddComponent<PlayerStats>();
            if (owner.Mana == null) typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            owner.Initialize(null); owner.GetStat(StatType.MaxMana).BaseValue = 100; owner.NotifyChanged(); owner.Mana.RestoreFull();
            var skillData = ScriptableObject.CreateInstance<SkillDataSO>(); skillData.ManaCost = 1;
            try
            {
                var skill = _source.AddComponent<BaselineCountingSkill>(); skill.Initialize(owner, skillData);
                skill.SetBusy(true); skill.TryCast(); Assert.That(skill.AcceptedAttack, Is.Null);
                skill.SetBusy(false); skill.TryCast(); var first = skill.AcceptedAttack;
                skill.SetBusy(true); skill.TryCast(); Assert.That(skill.AcceptedAttack, Is.SameAs(first));
                skill.SetBusy(false); skill.TryCast(); Assert.That(skill.AcceptedAttack.CastId, Is.Not.EqualTo(first.CastId));
            }
            finally { Object.DestroyImmediate(skillData); }
        }

        [Test]
        public void AuthoredContactHintDistinguishesImpactFromSwingVfx()
        {
            var step = new StepEntry(); step.SetOverrideObject("VfxPrefab", _source);
            Assert.That(AttackImpactContact.HasAuthoredContact(step), Is.False);
            step.SetOverrideObject("ImpactVfxPrefab", _source);
            Assert.That(AttackImpactContact.HasAuthoredContact(step), Is.True);
        }

    }
}
