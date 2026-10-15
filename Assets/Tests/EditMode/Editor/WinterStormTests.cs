using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class WinterStormHitRecorder : MonoBehaviour, IDamageable
    {
        public readonly List<DamageSnapshot> Hits = new List<DamageSnapshot>();
        public bool TakeDamage(DamageSnapshot damage) { Hits.Add(damage); return true; }
    }

    public class WinterStormTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector2 Origin = new Vector2(3800, 2200);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _stats;
        private PlayerWinterStormController _storm;
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(WinterStormBuilder.SkillPath);
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Awake(object target) => target.GetType().GetMethod("Awake", Flags)?.Invoke(target, null);

        [SetUp]
        public void Setup()
        {
            var go = Keep(new GameObject("Storm test player")); go.transform.position = Origin;
            _stats = go.AddComponent<PlayerStats>(); Awake(_stats);
            _stats.GetStat(StatType.MaxHealth).BaseValue = 100; _stats.Health.SetCurrent(10);
            _stats.GetStat(StatType.MaxMana).BaseValue = 200; _stats.Mana.RestoreFull();
            foreach (var stat in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning })
                _stats.GetStat(stat).BaseValue = 10;
            _stats.GetStat(StatType.CritChance).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(.5f, 1);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 2.5f; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _storm = go.AddComponent<PlayerWinterStormController>(); Physics2D.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (_storm != null) _storm.End();
            var stasis = _stats != null ? _stats.GetComponent<PlayerStasisController>() : null; if (stasis != null) stasis.End();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private WinterStormHitRecorder Target(Vector2 offset, bool childOnly = false)
        {
            var go = Keep(new GameObject("Storm target") { layer = 7 }); go.transform.position = Origin + offset;
            var target = go.AddComponent<WinterStormHitRecorder>();
            if (!childOnly) go.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
            var child = new GameObject("Second hurtbox") { layer = 7 }; child.transform.SetParent(go.transform, false);
            child.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
            Physics2D.SyncTransforms(); return target;
        }

        private void Begin() => Assert.That(_storm.Begin(Skill, Skill.Recipe.Steps.Single()), Is.True);

        [Test]
        public void TenColdPulsesOverFiveSecondsDeduplicateCompoundTargetsAndIncludeChildOnlyHurtboxes()
        {
            var target = Target(Vector2.right); var childOnly = Target(Vector2.left, true); var outside = Target(Vector2.right * 5);
            Begin(); Assert.That(target.Hits.Count, Is.EqualTo(1));
            _storm.Tick(.49f); Assert.That(target.Hits.Count, Is.EqualTo(1));
            _storm.Tick(.01f); Assert.That(target.Hits.Count, Is.EqualTo(2));
            _storm.Tick(10);
            Assert.That(target.Hits.Count, Is.EqualTo(10)); Assert.That(childOnly.Hits.Count, Is.EqualTo(10));
            Assert.That(outside.Hits, Is.Empty); Assert.That(_storm.IsActive, Is.False);
            foreach (var hit in target.Hits)
            {
                Assert.That(hit.Cold, Is.EqualTo(30).Within(.01f));
                Assert.That(hit.Physical + hit.Fire + hit.Lightning, Is.Zero);
            }
            _storm.Tick(1); Assert.That(target.Hits.Count, Is.EqualTo(10));
        }

        [Test]
        public void ActualMoldReceivesColdDamageAndFreezeWithoutPlayerFreezeChance()
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
            var mold = Object.Instantiate(data.Prefab); Keep(mold.gameObject); mold.transform.position = Origin + Vector2.right;
            mold.Setup(data, 60); Physics2D.SyncTransforms();
            var health = mold.GetComponent<EnemyHealth>(); float before = health.CurrentHealth;
            Begin();
            Assert.That(health.CurrentHealth, Is.LessThan(before));
            Assert.That(mold.GetComponent<EnemyFreezeController>().IsFrozen, Is.True);
            Assert.That(_stats.GetValue(StatType.FreezeChance), Is.Zero);
        }

        [Test]
        public void ShieldWithNoWeaponDamageStillDealsIntrinsicColdDamage()
        {
            foreach (var stat in new[] { StatType.DamagePhysical, StatType.DamageFire, StatType.DamageCold, StatType.DamageLightning })
                _stats.GetStat(stat).BaseValue = 0;
            var target = Target(Vector2.right); Begin();
            Assert.That(target.Hits.Single().Cold, Is.EqualTo(10));
        }

        [Test]
        public void OtherSkillsCanCastAndAnabiosisDoesNotStopDamagePulses()
        {
            var target = Target(Vector2.right); Begin(); _storm.Tick(1);
            var skillHost = Keep(new GameObject("Another skill"));
            var cast = skillHost.AddComponent<StasisTestSkill>();
            cast.Initialize(_stats, AssetDatabase.LoadAssetAtPath<SkillDataSO>(FrostAnabiosisBuilder.SkillPath));
            cast.TryCast(); Assert.That(cast.Casts, Is.EqualTo(1)); Assert.That(_stats.Mana.Current, Is.EqualTo(150));
            var stasis = _stats.gameObject.AddComponent<PlayerStasisController>(); Assert.That(stasis.Begin(5, 10), Is.True);
            _storm.Tick(4); stasis.Tick(4);
            Assert.That(target.Hits.Count, Is.EqualTo(10)); Assert.That(_storm.IsActive, Is.False);
            Assert.That(stasis.IsActive, Is.True); Assert.That(_stats.GetComponent<Rigidbody2D>().constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
            stasis.Tick(1);
            Assert.That(_stats.GetComponent<Rigidbody2D>().constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
            Assert.That(_stats.GetComponent<Rigidbody2D>().gravityScale, Is.EqualTo(2.5f));
        }

        [Test]
        public void FinishingOtherSkillsOrStasisCannotReleaseTheStormMovementLock()
        {
            var movement = _stats.gameObject.AddComponent<PlayerMovement>(); Awake(movement);
            Begin(); movement.SetMovementLock(false); Assert.That(movement.IsMovementLocked, Is.True);
            var stasis = _stats.gameObject.AddComponent<PlayerStasisController>(); stasis.Begin(1, 10); stasis.Tick(1);
            Assert.That(movement.IsMovementLocked, Is.True);
            var body = _stats.GetComponent<Rigidbody2D>(); body.linearVelocity = new Vector2(4, -2);
            typeof(PlayerMovement).GetMethod("FixedUpdate", Flags).Invoke(movement, null);
            Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(0, -2)));
            _storm.Tick(5); Assert.That(movement.IsMovementLocked, Is.False);
        }

        [Test]
        public void StormSourceCancellationCannotStopPlayerOwnedEffect()
        {
            var target = Target(Vector2.right);
            var host = Keep(new GameObject("Source recipe")); host.SetActive(false);
            var runner = host.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, _stats);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, Skill);
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(runner, new object[] { 0, Skill.Recipe.Steps.Single(), 1f, 0f });
            runner.Cancel(); Assert.That(_storm.IsActive, Is.True);
            _storm.Tick(.5f); Assert.That(target.Hits.Count, Is.EqualTo(2));
        }

        [Test]
        public void SameStormCannotSpendManaOrRestartWhileActive()
        {
            Begin(); var host = Keep(new GameObject("Repeat cast")); var cast = host.AddComponent<StasisTestSkill>(); cast.Initialize(_stats, Skill);
            cast.TryCast(); Assert.That(cast.Casts, Is.Zero); Assert.That(_stats.Mana.Current, Is.EqualTo(200));
            Assert.That(cast.CooldownRemaining, Is.Zero);
        }

        [Test]
        public void AreaScalingAndCasterPositionAreUsedForEachPulse()
        {
            _stats.GetStat(StatType.AreaOfEffect).BaseValue = 100;
            var target = Target(Vector2.right * 5); Begin(); Assert.That(_storm.Radius, Is.EqualTo(6));
            Assert.That(target.Hits.Count, Is.EqualTo(1));
            _stats.transform.position = Origin + Vector2.left * 10; Physics2D.SyncTransforms(); _storm.Tick(.5f);
            Assert.That(target.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisableStopsPulsesAndCleansUpWithoutLeavingMovementLocked()
        {
            Begin(); _storm.enabled = false; typeof(PlayerWinterStormController).GetMethod("OnDisable", Flags).Invoke(_storm, null);
            Assert.That(_storm.IsActive, Is.False); _storm.Tick(5); Assert.That(_storm.PulseCount, Is.EqualTo(1));
            Assert.That(_stats.GetComponent<Rigidbody2D>().constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        }

        [Test]
        public void JumpAndDodgeAreBlockedButOtherSkillLocksSurviveStormExpiry()
        {
            var movement = _stats.gameObject.AddComponent<PlayerMovement>(); Awake(movement);
            var attack = _stats.gameObject.AddComponent<PlayerAttackInput>(); Begin();
            var context = default(UnityEngine.InputSystem.InputAction.CallbackContext);
            typeof(PlayerMovement).GetMethod("OnJumpPerformed", Flags).Invoke(movement, new object[] { context });
            typeof(PlayerAttackInput).GetMethod("QueueDodge", Flags).Invoke(attack, new object[] { context });
            attack.TickMovementActions();
            Assert.That(typeof(PlayerMovement).GetField("_hasQueuedJump", Flags).GetValue(movement), Is.False);
            Assert.That(typeof(PlayerAttackInput).GetField("_isDodging", Flags).GetValue(attack), Is.False);
            movement.SetMovementLock(true); _storm.Tick(5);
            Assert.That(movement.IsMovementLocked, Is.True);
            movement.SetMovementLock(false); Assert.That(movement.IsMovementLocked, Is.False);
        }

        [Test]
        public void DeadCasterCannotKeepDealingStormDamage()
        {
            var target = Target(Vector2.right); Begin();
            _stats.Health.SetCurrent(0); _storm.Tick(5);
            Assert.That(_storm.IsActive, Is.False); Assert.That(target.Hits.Count, Is.EqualTo(1));
        }

        [Test]
        public void SkillDataTooltipDpsLocalizationAndNativeIconMatchStorm()
        {
            Assert.That(Skill.Cooldown, Is.EqualTo(15)); Assert.That(Skill.ManaCost, Is.EqualTo(50));
            Assert.That(Skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(Skill.Recipe.Steps.Single().GetFloat("Duration"), Is.EqualTo(5));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "en"), Does.Contain("Frost Anabiosis").And.Contain("Freeze"));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "ru"), Does.Contain("Зимняя буря"));
            var preview = SkillDpsPreview.Build(Skill, _stats, -1);
            Assert.That(preview.Hit.ColdPerHit, Is.EqualTo(30).Within(.01f));
            Assert.That(preview.Hit.HitsPerCast, Is.EqualTo(10));
            Assert.That(preview.Hit.TotalDps, Is.EqualTo(300f / 15).Within(.01f));
            var pool = Resources.Load<SkillPoolSO>("Skills/Shields/MysticShieldSkillPool");
            foreach (string id in new[] { "FrostAnabiosis", "ArcaneReprise" }) Assert.That(pool.PossibleSkills.Any(s => s.Skill.ID == id), Is.True);
            Assert.That(pool.PossibleSkills.Any(s => s.Skill == Skill), Is.False);
            var coldPool = Resources.Load<SkillPoolSO>("Skills/2HWeapon/Staff/Cold/2HColdStaffSkillPoolRBM");
            Assert.That(coldPool.PossibleSkills.Any(s => s.Skill == Skill), Is.True);
            Assert.That(coldPool.PossibleSkills.Any(s => s.Skill.ID == "BigColdBallSkill"), Is.True);
            var helmetPool = Resources.Load<SkillPoolSO>("Skills/Helmet/Mystic/MysticHelmetSkillPool");
            Assert.That(helmetPool.PossibleSkills.Any(s => s.Skill == Skill), Is.True);
            Assert.That(helmetPool.PossibleSkills.Any(s => s.Skill.ID == "PlasmaFieldSkill"), Is.True);
            Assert.That(helmetPool.PossibleSkills.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(Skill.HoldStanceId, Is.EqualTo("staff"));
            Assert.That(Skill.SwingStyleId, Is.Not.EqualTo("shield_parry"));
        }

        [Test]
        public void BlizzardChangesShapeOverTimeAndUsesPointFilteredBuildMaterial()
        {
            Begin(); var visual = Object.FindObjectsByType<WinterStormVisual>(FindObjectsSortMode.None).Single(v => Vector2.Distance(v.transform.position, Origin) < 10);
            var renderer = visual.GetComponent<SpriteRenderer>(); var texture = renderer.sprite.texture;
            var first = texture.GetPixels32(); _storm.Tick(.2f); var next = texture.GetPixels32();
            Assert.That(first.SequenceEqual(next), Is.False); Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
        }

        [Test]
        public void GroundedBlizzardDoesNotDrawThroughTheFloor()
        {
            var floor = Keep(new GameObject("Storm floor") { layer = 6 }); floor.transform.position = Origin + Vector2.down;
            floor.AddComponent<BoxCollider2D>().size = new Vector2(20, 1); Physics2D.SyncTransforms();
            Begin();
            var visual = Object.FindObjectsByType<WinterStormVisual>(FindObjectsSortMode.None).Single(v => Vector2.Distance(v.transform.position, Origin) < 10);
            var texture = visual.GetComponent<SpriteRenderer>().sprite.texture;
            var pixels = texture.GetPixels32();
            Assert.That(pixels.Take(texture.width * (texture.height / 3)).All(p => p.a == 0), Is.True);
            Assert.That(pixels.Any(p => p.a > 0), Is.True);
        }
    }
}
