using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class IceSkatesTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(4300, 2500);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private PlayerIceSkatesController _skates;
        private GroundSurfaceEffectSO _definition;
        private GroundSurfaceSource _surface;
        private SkillDataSO _skill;
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [SetUp]
        public void Setup()
        {
            _skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(IceSkatesBuilder.SkillPath);
            var player = Keep(new GameObject("Skates player")); player.transform.position = Origin + Vector2.up * .5f;
            _stats = player.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_stats, null);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.RestoreFull();
            _stats.GetStat(StatType.MaxMana).BaseValue = 100; _stats.Mana.RestoreFull();
            _stats.GetStat(StatType.MoveSpeed).BaseValue = 10;
            foreach (var channel in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning }) _stats.GetStat(channel).BaseValue = 10;
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            player.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            player.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            _skates = player.AddComponent<PlayerIceSkatesController>();
            _definition = Keep(Object.Instantiate(_skill.Recipe.Steps.Single().GetObject<GroundSurfaceEffectSO>("SurfaceEffect")));
            Floor(Origin + Vector2.down * .5f, new Vector2(12, 1));
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_surface != null) _surface.Dispose();
            if (_skates != null && _skates.Surface != null) _skates.Surface.Dispose();
            if (_skates != null) _skates.End();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private BoxCollider2D Floor(Vector2 position, Vector2 size, int layer = 6)
        {
            var floor = Keep(new GameObject("Surface support") { layer = layer }); floor.transform.position = position;
            var collider = floor.AddComponent<BoxCollider2D>(); collider.size = size; return collider;
        }
        private WinterStormHitRecorder Enemy(float x = 0, float height = 0)
        {
            var go = Keep(new GameObject("Ground contact enemy") { layer = 7 }); go.transform.position = Origin + new Vector2(x, .5f + height);
            go.AddComponent<EnemyStats>().Initialize(null, 1);
            var recorder = go.AddComponent<WinterStormHitRecorder>(); go.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 1);
            var child = new GameObject("Extra hurtbox") { layer = 7 }; child.transform.SetParent(go.transform, false); child.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 1);
            Physics2D.SyncTransforms(); return recorder;
        }
        private void Begin()
        {
            Assert.That(_skates.Begin(_skill, _skill.Recipe.Steps.Single(), 5), Is.True);
            _surface = _skates.Surface;
        }
        private GroundSurfaceSource Surface()
        {
            _surface = GroundSurfaceSource.Create(_stats, _skill, _skill.Recipe.Steps.Single(), 5, _definition);
            Spawn(0); return _surface;
        }
        private GroundSurfacePatch Spawn(float x)
        {
            Assert.That(GroundSurfaceGeometry.TrySupport(Origin + Vector2.right * x, out var support), Is.True);
            return _surface.Spawn(support);
        }

        [Test]
        public void AuthoredSkillHasRequestedBalanceNativeIconAndKeepsExistingBootsSkill()
        {
            Assert.That(_skill.ManaCost, Is.EqualTo(30)); Assert.That(_skill.Cooldown, Is.EqualTo(15));
            Assert.That(_skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(_definition.Lifetime, Is.EqualTo(3)); Assert.That(_definition.ContactModifiers, Is.Empty);
            Assert.That(_skill.Recipe.Steps.Single().ScopedStatModifiers, Is.Empty);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(IceSkatesBuilder.Root + "MysticBootsSkillPool.asset");
            Assert.That(pool.PossibleSkills.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(pool.PossibleSkills.Count(p => p.Skill == _skill), Is.EqualTo(1));
            StringAssert.Contains("30%", SkillDescriptionGenerator.Build(_skill, "en"));
            StringAssert.Contains("холод", SkillDescriptionGenerator.Build(_skill, "ru"));
        }

        [Test]
        public void EightSecondSpeedBuffRefreshesWithoutStackingAndPreservesOtherModifiers()
        {
            var other = new StatModifier(2, StatModType.Flat); _stats.GetStat(StatType.MoveSpeed).AddModifier(other);
            Begin(); Assert.That(_skates.Remaining, Is.EqualTo(8)); Assert.That(_stats.GetValue(StatType.MoveSpeed), Is.EqualTo(16.2f).Within(.001));
            _skates.Tick(2); var source = _skates.Surface; Begin();
            Assert.That(_skates.Surface, Is.SameAs(source)); Assert.That(_skates.Remaining, Is.EqualTo(8));
            Assert.That(_stats.GetValue(StatType.MoveSpeed), Is.EqualTo(16.2f).Within(.001));
            _skates.Tick(8); Assert.That(_skates.IsActive, Is.False); Assert.That(_stats.GetValue(StatType.MoveSpeed), Is.EqualTo(12));
        }

        [Test]
        public void DurationStatScalesBothControllerAndHudOnce()
        {
            _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(50, StatModType.PercentAdd));
            Begin(); Assert.That(_skates.Remaining, Is.EqualTo(12));
            var effect = _stats.GetComponent<StatusEffectController>().ActiveEffects.Single(e => e.RuntimeId == "IceSkates");
            Assert.That(effect.DurationSeconds, Is.EqualTo(12));
        }

        [Test]
        public void AirbornePlayerLeavesNoIceAndMovementGlidesOnGroundOnly()
        {
            _stats.transform.position += Vector3.up * 2; Physics2D.SyncTransforms(); Begin(); Assert.That(_surface.Patches, Is.Empty);
            _stats.transform.position -= Vector3.up * 2; Physics2D.SyncTransforms(); _skates.Tick(.1f); Assert.That(_surface.Patches.Count, Is.EqualTo(1));
            Assert.That(_skates.Acceleration(13.5f, false, false), Is.LessThan(55));
            Assert.That(_skates.Acceleration(13.5f, true, true), Is.GreaterThan(_skates.Acceleration(13.5f, false, false)));
        }

        [Test]
        public void OverlappingPatchesAndCompoundCollidersDealThirtyPercentPerSecondOnlyOnce()
        {
            var enemy = Enemy(); var source = Surface(); Spawn(.4f); Assert.That(source.Patches.Count, Is.EqualTo(2));
            for (int i = 0; i < 10; i++) source.Tick(.1f);
            Assert.That(enemy.Hits.Count, Is.EqualTo(4)); Assert.That(enemy.Hits.Sum(h => h.Cold), Is.EqualTo(12).Within(.001));
            Assert.That(enemy.Hits.Sum(h => h.Physical + h.Fire + h.Lightning), Is.Zero);
            Assert.That(_stats.GetValue(StatType.FreezeChance), Is.Zero);
        }

        [Test]
        public void PartialContactFlushesAndExpiryNeverDealsPastThreeSeconds()
        {
            var enemy = Enemy(); var source = Surface(); source.Tick(.1f);
            enemy.transform.position += Vector3.up; Physics2D.SyncTransforms(); source.Tick(.2f);
            Assert.That(enemy.Hits.Sum(h => h.Cold), Is.EqualTo(1.2f).Within(.001));
            enemy.transform.position -= Vector3.up; Physics2D.SyncTransforms(); source.Tick(10);
            Assert.That(enemy.Hits.Sum(h => h.Cold), Is.EqualTo(33.6f).Within(.001)); // .1 + 2.7 seconds of actual contact
            Assert.That(source.Patches, Is.Empty);
        }

        [Test]
        public void AirborneEnemyIsNotHitAndContactDebuffsAreOptionalAndRemovedOnLeave()
        {
            var air = Enemy(0, .4f); var source = Surface(); source.Tick(.5f); Assert.That(air.Hits, Is.Empty);
            _definition.ContactModifiers.Add(new SerializableStatModifier { Stat = StatType.MoveSpeed, Type = StatModType.PercentLess, Value = 30 });
            var grounded = Enemy(); var stats = grounded.GetComponent<EnemyStats>();
            stats.TryGetStat(StatType.MoveSpeed, out var speed); speed.BaseValue = 10;
            source.Tick(.1f); Assert.That(stats.GetValue(StatType.MoveSpeed), Is.EqualTo(7));
            grounded.transform.position += Vector3.up; Physics2D.SyncTransforms(); source.Tick(.1f);
            Assert.That(stats.GetValue(StatType.MoveSpeed), Is.EqualTo(10));
        }

        [Test]
        public void IceFollowsMovingSupportAndClipsAtLedgesWithoutPhysicsBlockers()
        {
            var platform = Floor(Origin + new Vector2(20, -.5f), Vector2.one, 8); Physics2D.SyncTransforms();
            Assert.That(GroundSurfaceGeometry.TrySupport(Origin + new Vector2(20.45f, 0), out var support), Is.True);
            var source = Surface(); var patch = source.Spawn(support);
            Assert.That(patch.Width, Is.LessThanOrEqualTo(.55f)); Assert.That(patch.GetComponent<Collider2D>(), Is.Null);
            Vector3 before = patch.transform.position; platform.transform.position += Vector3.right * 2;
            Assert.That(patch.transform.position.x, Is.EqualTo(before.x + 2).Within(.001));
            Object.DestroyImmediate(platform.gameObject); source.Tick(.1f); Assert.That(source.Patches.Count, Is.EqualTo(1));
        }

        [Test]
        public void BuffEndingLeavesIceButDisablingPlayerCleansIceAndModifiers()
        {
            Begin(); var source = _surface; _skates.End(); Assert.That(source.Patches.Count, Is.EqualTo(1));
            source.Tick(2); Assert.That(source != null, Is.True);
            source.Tick(1); Assert.That(source == null, Is.True);
            Begin(); _stats.gameObject.SetActive(false);
            typeof(PlayerIceSkatesController).GetMethod("OnDisable", Flags).Invoke(_skates, null);
            Assert.That(_stats.GetValue(StatType.MoveSpeed), Is.EqualTo(10)); Assert.That(_surface == null, Is.True);
        }

        [Test]
        public void PreviewShowsColdDamagePerSecondOfContact()
        {
            var preview = SkillDpsPreview.Build(_skill, _stats, 5);
            Assert.That(preview.Hit.ColdDps, Is.EqualTo(12).Within(.001)); Assert.That(preview.Hit.PhysicalDps, Is.Zero);
        }

        [Test]
        public void CancelingSourceRecipeDoesNotCancelSkatingOrItsTrail()
        {
            var host = Keep(new GameObject("Skates recipe")); host.SetActive(false);
            var runner = host.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, _stats);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, _skill);
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(runner, new object[] { 0, _skill.Recipe.Steps.Single(), 1f, 0f });
            _surface = _skates.Surface; runner.Cancel(); Object.DestroyImmediate(host);
            _skates.Tick(1); Assert.That(_skates.IsActive, Is.True); Assert.That(_surface.Patches.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RealMoldTakesColdDamageAndOnlyFreezesWithNormalPlayerFreezeChance()
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            var mold = Object.Instantiate(data.Prefab); Keep(mold.gameObject); mold.Setup(data, 60);
            var collider = mold.GetComponent<Collider2D>();
            mold.transform.position = Origin;
            Physics2D.SyncTransforms(); mold.transform.position += Vector3.up * (Origin.y - collider.bounds.min.y);
            Physics2D.SyncTransforms(); var health = mold.GetComponent<EnemyHealth>(); float before = health.CurrentHealth;
            var source = Surface(); source.Tick(.25f);
            Assert.That(health.CurrentHealth, Is.LessThan(before));
            var freeze = mold.GetComponent<EnemyFreezeController>(); Assert.That(freeze == null || !freeze.IsFrozen, Is.True);
            _stats.GetStat(StatType.FreezeChance).BaseValue = 100;
            source.Tick(.25f); Assert.That(mold.GetComponent<EnemyFreezeController>().IsFrozen, Is.True);
        }

        [Test]
        public void GroundMovementUsesGlidingBrakeButAirKeepsItsNormalBrake()
        {
            var movement = _stats.gameObject.AddComponent<PlayerMovement>();
            typeof(PlayerMovement).GetMethod("Awake", Flags).Invoke(movement, null);
            Begin(); var rb = _stats.GetComponent<Rigidbody2D>();
            typeof(PlayerMovement).GetField("_horizontalInput", Flags).SetValue(movement, 0f);
            typeof(PlayerMovement).GetField("_isGrounded", Flags).SetValue(movement, true);
            rb.linearVelocity = new Vector2(10, 0);
            typeof(PlayerMovement).GetMethod("ApplyMovement", Flags).Invoke(movement, null);
            Assert.That(rb.linearVelocity.x, Is.EqualTo(10 - 13.5f * Time.fixedDeltaTime).Within(.001));
            typeof(PlayerMovement).GetField("_isGrounded", Flags).SetValue(movement, false);
            rb.linearVelocity = new Vector2(10, 0);
            typeof(PlayerMovement).GetMethod("ApplyMovement", Flags).Invoke(movement, null);
            Assert.That(rb.linearVelocity.x, Is.EqualTo(10 - 45 * Time.fixedDeltaTime).Within(.001));
        }

        [Test]
        public void TrailDoesNotBridgeGapsAndPatchesAlignToSlopes()
        {
            Floor(Origin + new Vector2(10, -.5f), new Vector2(4, 1));
            _stats.transform.position = Origin + new Vector2(5.8f, .5f); Physics2D.SyncTransforms(); Begin();
            _stats.transform.position = Origin + new Vector2(8.1f, .5f); Physics2D.SyncTransforms(); _skates.Tick(.1f);
            foreach (var patch in _surface.Patches)
            {
                float left = patch.transform.position.x - patch.Width * .5f - Origin.x;
                float right = patch.transform.position.x + patch.Width * .5f - Origin.x;
                Assert.That(right <= 6.01f || left >= 7.99f, Is.True, "Ice must not span the pit.");
            }
            var slope = Floor(Origin + new Vector2(20, -.5f), new Vector2(4, 1));
            slope.transform.rotation = Quaternion.Euler(0, 0, 15); Physics2D.SyncTransforms();
            Assert.That(GroundSurfaceGeometry.TrySupport(Origin + new Vector2(20, .02f), out var support), Is.True);
            var inclined = _surface.Spawn(support);
            Assert.That(Vector2.Dot(inclined.transform.up, support.normal), Is.EqualTo(1).Within(.001));
            Assert.That(inclined.Width, Is.EqualTo(1).Within(.001));
        }

        [Test]
        public void PlayerDeathRemovesSkatingSpeedAndOwnedDamageSurfaces()
        {
            Begin(); _stats.Health.SetCurrent(0); _skates.Tick(.1f); _surface.Tick(.1f);
            Assert.That(_skates.IsActive, Is.False); Assert.That(_stats.GetValue(StatType.MoveSpeed), Is.EqualTo(10));
            Assert.That(_surface == null, Is.True);
        }
    }
}
