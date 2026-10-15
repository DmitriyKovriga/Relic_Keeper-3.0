using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Configuration;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class StasisTestSkill : SkillBehaviour
    {
        public int Casts;
        protected override float CurrentTime => 100;
        protected override void Execute() => Casts++;
    }

    public class FrostAnabiosisTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private Rigidbody2D _body;
        private PlayerStasisController _stasis;
        private PlayerDamageReceiver _receiver;
        private bool _immortal;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostAnabiosisBuilder.SkillPath);
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Awake(object value) => value.GetType().GetMethod("Awake", Flags)?.Invoke(value, null);

        [SetUp]
        public void Setup()
        {
            _immortal = PlaytestConfiguration.PlayerImmortal; PlaytestConfiguration.SetEditorPlayerImmortal(false);
            var go = Keep(new GameObject("Frost test player")); go.transform.position = new Vector3(3600, 2200);
            _stats = go.AddComponent<PlayerStats>(); Awake(_stats);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.SetCurrent(10);
            _stats.GetStat(StatType.MaxMana).BaseValue = 200; _stats.Mana.RestoreFull();
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            _body = go.AddComponent<Rigidbody2D>(); _body.gravityScale = 2.5f; _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _receiver = go.AddComponent<PlayerDamageReceiver>(); Awake(_receiver);
            _stasis = go.AddComponent<PlayerStasisController>(); Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_stasis != null) _stasis.End();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); PlaytestConfiguration.SetEditorPlayerImmortal(_immortal);
        }

        [Test]
        public void HealsTenPercentOfMaximumPerSecondAndOnlyFiftyPercentOverFullDuration()
        {
            Assert.That(_stasis.Begin(5, 10), Is.True);
            _stasis.Tick(1); Assert.That(_stats.Health.Current, Is.EqualTo(20));
            _stasis.Tick(1.5f); Assert.That(_stats.Health.Current, Is.EqualTo(35));
            _stasis.Tick(10); Assert.That(_stats.Health.Current, Is.EqualTo(60));
            Assert.That(_stasis.IsActive, Is.False);
            _stasis.Tick(10); Assert.That(_stats.Health.Current, Is.EqualTo(60));
        }

        [Test]
        public void HealingCapsAtMaximumAndUsesCurrentMaximumLife()
        {
            _stats.Health.SetCurrent(95); _stasis.Begin(5, 10); _stasis.Tick(1);
            Assert.That(_stats.Health.Current, Is.EqualTo(100));
            _stats.GetStat(StatType.MaxHealth).BaseValue = 200; _stats.Health.SetCurrent(100);
            _stasis.Tick(1); Assert.That(_stats.Health.Current, Is.EqualTo(120));
        }

        [Test]
        public void AllHitChannelsAndDamageOverTimeAreImmuneWithoutConsumingMysticShield()
        {
            _stasis.Begin(5, 10);
            var shield = _stats.GetComponent<MysticShieldController>(); int charges = shield.CurrentCharges;
            var hit = new DamageSnapshot(this) { Physical = 10, Fire = 10, Cold = 10, Lightning = 10 };
            Assert.That(_receiver.TakeDamageDetailed(hit).WasImmune, Is.True);
            Assert.That(_receiver.TakeDamage(hit), Is.False);
            _receiver.ApplyPureDamage(100, this, "Ignite");
            Assert.That(_stats.Health.Current, Is.EqualTo(10)); Assert.That(shield.CurrentCharges, Is.EqualTo(charges));
            _stasis.End(); _stats.Health.SetCurrent(100); _receiver.ApplyPureDamage(5, this, "Poison");
            Assert.That(_stats.Health.Current, Is.EqualTo(95));
        }

        [TestCase(false)] [TestCase(true)]
        public void ExpiryAndDisableRestoreOriginalPhysicsAndRemoveImmunity(bool disable)
        {
            _body.linearVelocity = new Vector2(4, 3); _stasis.Begin(5, 10);
            Assert.That(_body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
            Assert.That(_body.gravityScale, Is.Zero); Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
            if (disable) { _stasis.enabled = false; typeof(PlayerStasisController).GetMethod("OnDisable", Flags).Invoke(_stasis, null); }
            else _stasis.Tick(5);
            Assert.That(_body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(_body.gravityScale, Is.EqualTo(2.5f)); Assert.That(PlayerStasisController.IsActiveFor(_stats), Is.False);
        }

        [Test]
        public void SkillsCannotCastOrSpendManaDuringStasisButWorkAfterwards()
        {
            var go = Keep(new GameObject("Locked skill")); var cast = go.AddComponent<StasisTestSkill>(); cast.Initialize(_stats, Skill);
            _stasis.Begin(5, 10); cast.TryCast();
            Assert.That(cast.Casts, Is.Zero); Assert.That(_stats.Mana.Current, Is.EqualTo(200)); Assert.That(cast.CooldownRemaining, Is.Zero);
            _stasis.End(); cast.TryCast();
            Assert.That(cast.Casts, Is.EqualTo(1)); Assert.That(_stats.Mana.Current, Is.EqualTo(150)); Assert.That(cast.CooldownRemaining, Is.EqualTo(15));
        }

        [Test]
        public void MovementJumpAndDodgeInputsCannotEscapeCrystal()
        {
            var movement = _stats.gameObject.AddComponent<PlayerMovement>(); Awake(movement);
            var attack = _stats.gameObject.AddComponent<PlayerAttackInput>();
            _stasis.Begin(5, 10);
            typeof(PlayerMovement).GetField("_hasQueuedJump", Flags).SetValue(movement, true);
            typeof(PlayerMovement).GetMethod("FixedUpdate", Flags).Invoke(movement, null);
            Assert.That(typeof(PlayerMovement).GetField("_hasQueuedJump", Flags).GetValue(movement), Is.False);
            typeof(PlayerAttackInput).GetMethod("QueueDodge", Flags).Invoke(attack, new object[] { default(UnityEngine.InputSystem.InputAction.CallbackContext) });
            attack.TickMovementActions();
            Assert.That(typeof(PlayerAttackInput).GetField("_isDodging", Flags).GetValue(attack), Is.False);
            Assert.That(_body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ReentryDoesNotExtendDurationAndDeadPlayersAreNotRevived()
        {
            _stasis.Begin(5, 10); _stasis.Tick(2);
            Assert.That(_stasis.Begin(5, 10), Is.False); Assert.That(_stasis.Remaining, Is.EqualTo(3));
            _stats.Health.SetCurrent(0); _stasis.Tick(1);
            Assert.That(_stasis.IsActive, Is.False); Assert.That(_stats.Health.Current, Is.Zero);
            Assert.That(_stasis.Begin(5, 10), Is.False);
        }

        [Test]
        public void SourceSkillCancellationDoesNotRemovePlayerOwnedCrystal()
        {
            var go = Keep(new GameObject("Source recipe")); go.SetActive(false);
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, _stats);
            var step = Skill.Recipe.Steps.Single();
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(runner, new object[] { 0, step, 1f, 0f });
            runner.Cancel();
            Assert.That(_stasis.IsActive, Is.True); _stasis.Tick(5); Assert.That(_stasis.IsActive, Is.False);
        }

        [Test]
        public void SkillHasCorrectBalanceLocalizationAndNativeIconAndKeepsReprise()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(15)); Assert.That(Skill.ManaCost, Is.EqualTo(50));
            Assert.That(Skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(Skill.Recipe.Steps.Single().GetFloat("Duration"), Is.EqualTo(5));
            Assert.That(Skill.Recipe.Steps.Single().GetFloat("HealPercentPerSecond"), Is.EqualTo(10));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "en"), Does.Contain("10%").And.Contain("immune"));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "ru"), Does.Contain("10%").And.Contain("невосприимчивость"));
            foreach (string code in new[] { "en", "ru" })
                Assert.That(AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Localization/LocalizationTables/SkillsLabels_{code}.asset").GetEntry(Skill.NameKey).LocalizedValue, Is.Not.Empty);
            var pool = Resources.Load<SkillPoolSO>("Skills/Shields/MysticShieldSkillPool");
            Assert.That(pool.PossibleSkills.Any(s => s.Skill == Skill), Is.True);
            Assert.That(pool.PossibleSkills.Any(s => s.Skill.ID == "ArcaneReprise"), Is.True);
        }

        [Test]
        public void VisualEnclosesBodyWithBuildRetainedMaterialAndSixDifferentReleaseSections()
        {
            _stasis.Begin(5, 10);
            var visual = _stats.GetComponentInChildren<FrostAnabiosisVisual>(); Assert.That(visual, Is.Not.Null);
            var renderer = visual.GetComponent<SpriteRenderer>();
            Assert.That(renderer.bounds.size.y, Is.GreaterThan(_stats.GetComponent<Collider2D>().bounds.size.y));
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            visual.Release();
            Assert.That(visual.GetComponentsInChildren<SpriteRenderer>().Count(r => r != renderer), Is.EqualTo(6));
            var pieces = visual.GetComponentsInChildren<SpriteRenderer>().Where(r => r != renderer).ToArray();
            Vector3 before = pieces[0].transform.localPosition;
            visual.SampleRelease(.5f);
            Assert.That(pieces[0].transform.localPosition.x, Is.LessThan(before.x));
            Assert.That(pieces[0].color.a, Is.LessThan(.5f));
            Assert.That(pieces.Select(p => p.sprite.rect).Distinct().Count(), Is.EqualTo(6));
            _objects.Add(visual.gameObject);
        }
    }
}
