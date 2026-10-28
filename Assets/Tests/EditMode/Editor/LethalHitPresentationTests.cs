using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LethalHitPresentationTests
    {
        private GameObject _target, _source;
        private EnemyHealth _health;
        private CombatPoseProfile _profile;
        [SetUp] public void Setup()
        {
            _target = new GameObject("Lethal target"); _source = new GameObject("Attacker");
            var stats = _target.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            stats.TryGetStat(StatType.MaxHealth, out var hp); hp.BaseValue = 100;
            stats.TryGetStat(StatType.Armor, out var armor); armor.BaseValue = 0;
            _health = _target.AddComponent<EnemyHealth>(); _health.DestroyOnDeath = false; _health.Initialize();
            _profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_target); Object.DestroyImmediate(_source); Object.DestroyImmediate(_profile); }
        private DamageSnapshot Hit(Vector2 direction, float multiplier = 1) => new DamageSnapshot(_source)
        { Physical = 1000, Impact = new AttackImpactContact(new AttackImpactContext(null, 0, enableHitStop: false), Vector2.zero, direction, multiplier) };

        [TestCase(1)] [TestCase(-1)] public void AllFragmentsFavorTheFatalDirection(int side)
        {
            var impact = new EnemyDeathImpact(_health, Hit(Vector2.right * side)); var config = new EnemyDeathEffectConfig();
            for (int i = 0; i < 360; i++)
            {
                Vector2 radial = Quaternion.Euler(0, 0, i) * Vector2.right;
                Assert.That(impact.FragmentImpulse(radial, config, _profile).x * side, Is.GreaterThan(0));
            }
        }
        [Test] public void HeavyDeathUsesTheSameFragmentsWithBoundedStrongerImpulse()
        {
            var light = new EnemyDeathImpact(_health, Hit(Vector2.right)); var heavy = new EnemyDeathImpact(_health, Hit(Vector2.right, 2.5f));
            var config = new EnemyDeathEffectConfig(); var radial = new Vector2(.5f, .5f);
            Assert.That(heavy.FragmentImpulse(radial, config, _profile), Is.EqualTo(light.FragmentImpulse(radial, config, _profile) * 1.25f));
            _profile.HeavyDeathImpulse = 100;
            Assert.That(heavy.FragmentImpulse(radial, config, _profile), Is.EqualTo(light.FragmentImpulse(radial, config, _profile) * 1.5f));
        }
        [Test] public void NeutralDotAndDisabledProfileKeepOriginalSpray()
        {
            var config = new EnemyDeathEffectConfig(); Vector2 radial = new Vector2(-.5f, -.5f);
            Vector2 expected = new Vector2(radial.x * config.ChunkHorizontalForce, (.5f + .45f) * config.ChunkVerticalForce);
            Assert.That(default(EnemyDeathImpact).FragmentImpulse(radial, config, _profile), Is.EqualTo(expected));
            var hit = Hit(Vector2.right); hit.IsDirectHit = false;
            Assert.That(new EnemyDeathImpact(_health, hit).FragmentImpulse(radial, config, _profile), Is.EqualTo(expected));
            hit.IsDirectHit = true; _profile.EnableDirectedDeaths = false;
            Assert.That(new EnemyDeathImpact(_health, hit).FragmentImpulse(radial, config, _profile), Is.EqualTo(expected));
        }
        [Test] public void FatalSnapshotSurvivesMutationAndReentrantDamageCannotStealTheKill()
        {
            int deaths = 0, confirmations = 0; var fatal = Hit(Vector2.left, 2.5f);
            _health.OnDeath += _ => deaths++;
            void Confirm(ConfirmedHit h) { if (h.Target == _target) confirmations++; }
            void Nested(GameplayEventContext c)
            {
                if (c.Target != _target || c.Type != GameplayEventType.DamageTaken) return;
                fatal.Impact = new AttackImpactContact(null, Vector2.zero, Vector2.right);
                Assert.That(_health.TakeDamage(Hit(Vector2.right)), Is.False);
                _health.ApplyPureDamage(1000, null);
            }
            CombatHitFeedback.HitConfirmed += Confirm; GameplayEventBus.EventRaised += Nested;
            try
            {
                Assert.That(_health.TakeDamage(fatal), Is.True);
                for (int i = 0; i < 20; i++) Assert.That(_health.TakeDamage(Hit(Vector2.right)), Is.False);
                Assert.That(deaths, Is.EqualTo(1)); Assert.That(confirmations, Is.EqualTo(1));
                Assert.That(_health.DeathImpact.Direction, Is.EqualTo(Vector2.left)); Assert.That(_health.DeathImpact.Weight, Is.EqualTo(AttackImpactWeight.Heavy));
                _health.Resurrect(); Assert.That(_health.DeathImpact.IsDirect, Is.False);
            }
            finally { CombatHitFeedback.HitConfirmed -= Confirm; GameplayEventBus.EventRaised -= Nested; }
        }
        [Test] public void DotDeathClearsLivingFeedbackBeforeDeathCallbacks()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 24);
            var material = new Material(Shader.Find("RelicKeeper/Sprites/World Color Unlit"));
            try
            {
                var source = _target.AddComponent<SpriteRenderer>(); source.sprite = sprite; source.sharedMaterial = material;
                var recoil = _target.AddComponent<EnemyHitRecoil>(); recoil.Initialize(source);
                var flash = _target.AddComponent<EnemyHitFlash>(); flash.Initialize(source);
                CombatHitFeedback.Confirm(_health, Hit(Vector2.right), 10, false); recoil.Sample(Time.time);
                Assert.That(flash.Amount, Is.GreaterThan(0));
                _health.OnDeath += _ => { Assert.That(flash.Amount, Is.Zero); Assert.That(recoil.DisplayRenderer.enabled, Is.False); Assert.That(source.forceRenderingOff, Is.False); };
                _health.ApplyPureDamage(1000, _source);
                Assert.That(_health.IsDead, Is.True); Assert.That(_health.DeathImpact.IsDirect, Is.False);
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(sprite); }
        }
        [Test] public void ShieldCallbackKillCannotBeOverwrittenByTheOuterHit()
        {
            var stats = _target.GetComponent<EnemyStats>();
            stats.AddModifier(StatType.MaxMysticShield, new StatModifier(1, StatModType.Flat, this));
            MysticShieldController.TryResolve(_target.transform, out var shield); shield.FillCharges();
            int deaths = 0; _health.OnDeath += _ => deaths++;
            void Nested(GameplayEventContext context)
            {
                if (context.Target == _target && context.Type == GameplayEventType.MysticShieldConsumed)
                    _health.ApplyPureDamage(1000, _source);
            }
            GameplayEventBus.EventRaised += Nested;
            try
            {
                Assert.That(_health.TakeDamage(Hit(Vector2.right, 2.5f)), Is.False);
                Assert.That(deaths, Is.EqualTo(1)); Assert.That(_health.DeathImpact.IsDirect, Is.False);
                Assert.That(_health.CurrentHealth, Is.EqualTo(-900));
            }
            finally { GameplayEventBus.EventRaised -= Nested; }
        }
    }
}
