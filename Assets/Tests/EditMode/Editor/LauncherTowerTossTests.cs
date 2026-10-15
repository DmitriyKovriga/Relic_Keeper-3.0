using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class LauncherTowerTossTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly Vector3 Origin = new Vector3(5200, 2700);
        private readonly List<Object> _objects = new List<Object>();
        private PlayerStats _player;
        private SkillStepRunner _runner;
        private SkillDataSO _skill;
        private StepEntry Hit => _skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "DealDamageRectangle");
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        [SetUp] public void Setup()
        {
            _skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(LauncherTowerTossBuilder.SkillPath);
            var owner = Keep(new GameObject("Toss caster")); owner.SetActive(false); owner.transform.position = Origin;
            _player = owner.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(_player, null);
            _player.GetStat(StatType.MaxHealth).BaseValue = 100; _player.Health.RestoreFull();
            _player.GetStat(StatType.MaxMana).BaseValue = 100; _player.Mana.RestoreFull();
            _player.GetStat(StatType.DamagePhysical).BaseValue = 40; _player.GetStat(StatType.CritChance).BaseValue = 0;
            var host = Keep(new GameObject("Toss runner")); host.SetActive(false); _runner = host.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(_runner, _player);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(_runner, _skill);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(_runner, new SkillStepContext { OwnerStats = _player });
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        private EnemyHealth Enemy(float x)
        {
            var go = Keep(new GameObject("Launch target") { layer = 7 });
            var stats = go.AddComponent<EnemyStats>(); stats.Initialize(null, 1);
            stats.TryGetStat(StatType.MaxHealth, out var hp); hp.BaseValue = 1000;
            var health = go.AddComponent<EnemyHealth>(); health.Initialize();
            go.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 1);
            var movement = go.AddComponent<EnemyLocomotion2D>(); movement.Initialize(null, Resources.Load<EnemyDataSO>("Enemy/SO_Mold"));
            var stun = go.AddComponent<EnemyStunController>(); stun.Initialize(null);
            go.transform.position = Origin + Vector3.right * x;
            var child = new GameObject("Additional hurtbox") { layer = 7 }; child.transform.SetParent(go.transform, false);
            child.AddComponent<BoxCollider2D>().size = new Vector2(.4f, 1); Physics2D.SyncTransforms(); return health;
        }
        private void Strike() => typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", Flags).Invoke(_runner, new object[] { 4, Hit, 1f, 0f });

        [Test] public void SkillHasRequestedCostCooldownIconAndCoexistsWithCharge()
        {
            Assert.That(_skill.ManaCost, Is.EqualTo(10)); Assert.That(_skill.Cooldown, Is.EqualTo(8));
            Assert.That(_skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>("Assets/Resources/Skills/Shields/TowerShieldSkillPool.asset");
            Assert.That(pool.PossibleSkills.Count(entry => entry.Skill == _skill), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(entry => entry.Skill.ID == "TowerCharge"), Is.True);
            Assert.That(WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId("shield_guard", "shield_launcher"), Is.True);
            var steps = _skill.Recipe.Steps;
            Assert.That(steps.Any(step => step.StepDefinition.Id == "WeaponWindup"), Is.True);
            Assert.That(steps.Single(step => step.StepDefinition.Id == "WeaponStrike").GetBool("KeepWeaponVisible"), Is.False);
            Assert.That(steps.Any(step => step.StepDefinition.Id == "WeaponRecovery"), Is.True);
            Assert.That(steps.Single(step => step.StepDefinition.Id == "SpawnVFX").GetObject<GameObject>("VfxPrefab"), Is.Not.Null);
            StringAssert.Contains("stuns for 1s", SkillDescriptionGenerator.Build(_skill, "en"));
            StringAssert.Contains("оглушает на 1", SkillDescriptionGenerator.Build(_skill, "ru"));
            Assert.That(SkillDpsPreview.Build(_skill, _player, 1).Hit.PhysicalPerHit, Is.EqualTo(40).Within(.001));
        }

        [TestCase(1)] [TestCase(-1)] public void ForwardStrikeHitsOnceStunsOneSecondAndLaunchesButMissesBehind(int direction)
        {
            _player.transform.localScale = new Vector3(direction, 1, 1);
            var front = Enemy(.8f * direction); var behind = Enemy(-.8f * direction);
            float hp = front.CurrentHealth; Strike();
            float actualDamage = hp - front.CurrentHealth;
            float resistance = ArmorMitigation.ResolveTotalPhysicalResist(front.GetComponent<EnemyStats>(), out _, out _, out _, out _);
            Assert.That(actualDamage, Is.EqualTo(40 * (1 - resistance / 100)).Within(.001));
            Assert.That(behind.CurrentHealth, Is.EqualTo(1000));
            Assert.That(front.GetComponent<EnemyStunController>().RemainingStunSeconds, Is.EqualTo(1).Within(.01));
            float velocity = front.GetComponent<Rigidbody2D>().linearVelocity.y;
            Assert.That(velocity * velocity / (2 * -Physics2D.gravity.y * 3), Is.EqualTo(.55f).Within(.001));
            Assert.That(front.GetComponent<Rigidbody2D>().gravityScale, Is.EqualTo(3));
        }

        [Test] public void StunExpiresRefillsMeterAndDoesNotShortenLongerExistingStun()
        {
            var enemy = Enemy(.8f); var stun = enemy.GetComponent<EnemyStunController>();
            Assert.That(stun.TryApplyTimedStun(3), Is.True); Strike(); Assert.That(stun.RemainingStunSeconds, Is.EqualTo(3).Within(.01));
            typeof(EnemyStunController).GetField("_stunnedUntil", Flags).SetValue(stun, Time.time - .01f);
            typeof(EnemyStunController).GetMethod("Update", Flags).Invoke(stun, null);
            Assert.That(stun.IsStunned, Is.False); Assert.That(stun.CurrentMeter, Is.EqualTo(stun.MaxMeter));
            Assert.That(enemy.GetComponent<EnemyLocomotion2D>().TryJump(), Is.False); // Not grounded; normal physics remains authoritative.
        }

        [TestCase(0, .55f)] [TestCase(50, .275f)] [TestCase(100, 0)]
        public void ResistanceReducesLaunchHeightWithoutRemovingTimedStun(float resist, float expectedHeight)
        {
            var enemy = Enemy(.8f); enemy.GetComponent<EnemyStats>().AddModifier(StatType.PushbackResist, new StatModifier(resist, StatModType.Flat));
            var rb = enemy.GetComponent<Rigidbody2D>(); Strike();
            Assert.That(rb.linearVelocity.y * rb.linearVelocity.y / (2 * -Physics2D.gravity.y * rb.gravityScale), Is.EqualTo(expectedHeight).Within(.001));
            Assert.That(enemy.GetComponent<EnemyStunController>().IsStunned, Is.True);
        }

        [Test] public void FrozenTargetsStayStillAndGravityReturnsWhenFreezeEndsDuringStun()
        {
            var enemy = Enemy(.8f); var locomotion = enemy.GetComponent<EnemyLocomotion2D>(); var rb = enemy.GetComponent<Rigidbody2D>();
            locomotion.SetFrozen(true); Strike(); Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.zero)); Assert.That(rb.gravityScale, Is.Zero);
            locomotion.SetFrozen(false); Assert.That(rb.gravityScale, Is.EqualTo(3));
            Assert.That(enemy.GetComponent<EnemyStunController>().IsStunned, Is.True);
        }

        [Test] public void LethalAndEvadedHitsDoNotApplyStunOrLaunch()
        {
            var dead = Enemy(.8f); dead.DestroyOnDeath = false;
            dead.ApplyPureDamage(2000, _player); Strike();
            Assert.That(dead.GetComponent<EnemyStunController>().IsStunned, Is.False);
            Assert.That(dead.GetComponent<Rigidbody2D>().linearVelocity.y, Is.Zero);
            // An IDamageable that rejects the hit exercises the same gate used by evasion.
            var go = Keep(new GameObject("Rejected hit target")); go.transform.position = Origin + Vector3.right * .8f;
            go.AddComponent<BoxCollider2D>().size = Vector2.one;
            var rejected = go.AddComponent<LauncherRejectedHit>(); go.AddComponent<EnemyStats>().Initialize(null, 1);
            go.AddComponent<EnemyHealth>().Initialize(); Physics2D.SyncTransforms(); Strike();
            Assert.That(rejected.Hits, Is.EqualTo(1)); Assert.That(go.GetComponent<EnemyStunController>(), Is.Null);
        }

        [Test] public void LaunchKeepsHigherUpwardMomentumAndRejectsStaticOrZeroGravityBodies()
        {
            var enemy = Enemy(.8f); var rb = enemy.GetComponent<Rigidbody2D>(); var move = enemy.GetComponent<EnemyLocomotion2D>();
            rb.linearVelocity = new Vector2(0, 12); Assert.That(move.TryLaunchUp(.55f), Is.True); Assert.That(rb.linearVelocity.y, Is.EqualTo(12));
            rb.gravityScale = 0; Assert.That(move.TryLaunchUp(.55f), Is.False);
            rb.gravityScale = 3; rb.bodyType = RigidbodyType2D.Kinematic; Assert.That(move.TryLaunchUp(.55f), Is.False);
            Assert.That(move.TryLaunchUp(float.NaN), Is.False);
            Assert.That(enemy.GetComponent<EnemyStunController>().TryApplyTimedStun(float.PositiveInfinity), Is.False);
        }

        [Test] public void ReinitializedEnemyHasNoLingeringStunOrLaunchVelocity()
        {
            var enemy = Enemy(.8f); Strike();
            var move = enemy.GetComponent<EnemyLocomotion2D>();
            move.Initialize(null, Resources.Load<EnemyDataSO>("Enemy/SO_Mold"));
            var stun = enemy.GetComponent<EnemyStunController>(); stun.Initialize(null);
            Assert.That(stun.IsStunned, Is.False); Assert.That(stun.Normalized, Is.EqualTo(1));
            Assert.That(enemy.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
        }

        [Test] public void ProjectileOnHitCanReuseTheSameStunAndLaunchRule()
        {
            var target = Enemy(.8f);
            var host = Keep(new GameObject("Reusable projectile control")); var projectile = host.AddComponent<SkillProjectile>();
            typeof(SkillProjectile).GetField("_data", Flags).SetValue(projectile, new SkillProjectileLaunchData { OwnerStats = _player, Step = Hit });
            typeof(SkillProjectile).GetMethod("ExecuteOnHitEffects", Flags).Invoke(projectile,
                new object[] { target, target.transform, new DamageSnapshot(_player) { Physical = 1 } });
            Assert.That(target.GetComponent<EnemyStunController>().IsStunned, Is.True);
            Assert.That(target.GetComponent<Rigidbody2D>().linearVelocity.y, Is.GreaterThan(0));
        }
    }
    public sealed class LauncherRejectedHit : MonoBehaviour, IDamageable
    {
        public int Hits;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; return false; }
    }
}
