using NUnit.Framework;
using Scripts.Combat;
using Scripts.GameplayEvents;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class PlayerHurtFeedbackTests
    {
        private GameObject _player, _source;
        private CombatPoseProfile _profile;
        private GameplayEventContext _hit;
        [SetUp] public void Setup()
        {
            _player = new GameObject("Player"); _source = new GameObject("Source"); _source.transform.position = Vector3.left;
            _profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
            _hit = new GameplayEventContext { Type = GameplayEventType.DamageTaken, Target = _player, Source = _source, Amount = 10, Damage = new DamageSnapshot(_source) };
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_player); Object.DestroyImmediate(_source); Object.DestroyImmediate(_profile); }
        [Test] public void DirectHitFlashesThenReturnsToNeutralWithoutExtendingOnSpam()
        {
            var pulse = new PlayerHurtPulse(); Assert.That(pulse.Trigger(_hit, _player, _profile, 0), Is.True);
            Assert.That(pulse.Sample(.01f, out var direction), Is.GreaterThan(0)); Assert.That(direction, Is.EqualTo(1));
            for (int i = 0; i < 100; i++) Assert.That(pulse.Trigger(_hit, _player, _profile, .05f), Is.False);
            Assert.That(pulse.Sample(.13f, out _), Is.Zero);
            Assert.That(pulse.Trigger(_hit, _player, _profile, .23f), Is.True);
            pulse.Clear(); Assert.That(pulse.Sample(.24f, out _), Is.Zero);
        }
        [TestCase(GameplayEventType.Evaded)] [TestCase(GameplayEventType.Parried)] [TestCase(GameplayEventType.MysticShieldConsumed)]
        public void DefenseEventsNeverShowHurt(GameplayEventType type)
        {
            _hit.Type = type; Assert.That(new PlayerHurtPulse().Trigger(_hit, _player, _profile, 0), Is.False);
        }
        [Test] public void DotsZeroOtherTargetsAndDisabledProfileDoNotReact()
        {
            var pulse = new PlayerHurtPulse(); _hit.Damage.IsDirectHit = false; Assert.That(pulse.Trigger(_hit, _player, _profile, 0), Is.False);
            _hit.Damage.IsDirectHit = true; _hit.Amount = 0; Assert.That(pulse.Trigger(_hit, _player, _profile, 0), Is.False);
            _hit.Amount = 10; _hit.Target = _source; Assert.That(pulse.Trigger(_hit, _player, _profile, 0), Is.False);
            _hit.Target = _player; _profile.EnablePlayerHurt = false; Assert.That(pulse.Trigger(_hit, _player, _profile, 0), Is.False);
        }
    }
}
