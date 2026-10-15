using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class NinjaStubHitRecorder : MonoBehaviour, IDamageable
    {
        public int Hits;
        public DamageSnapshot Last;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; Last = damage; return true; }
    }

    public class NinjaStubTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _objects = new List<Object>();
        private static readonly Vector2 Origin = new Vector2(3000, 2000);
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(NinjaStubBuilder.SkillPath);
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var visual in Object.FindObjectsByType<BlinkStabVisual>(FindObjectsSortMode.None))
                if (visual.transform.position.x > 2500) Object.DestroyImmediate(visual.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private BoxCollider2D Box(string name, Vector2 offset, Vector2 size, int layer = 0)
        {
            var go = Keep(new GameObject(name) { layer = layer });
            go.transform.position = Origin + offset;
            var box = go.AddComponent<BoxCollider2D>(); box.size = size;
            Physics2D.SyncTransforms(); return box;
        }

        private PlayerStats Player()
        {
            var go = Keep(new GameObject("Ninja test caster")); go.SetActive(false);
            go.transform.position = Origin;
            var stats = go.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
            stats.GetStat(StatType.DamagePhysical).BaseValue = 10;
            stats.GetStat(StatType.CritChance).BaseValue = 0;
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 1);
            go.SetActive(true); Physics2D.SyncTransforms();
            return stats;
        }

        private SkillStepRunner Runner(PlayerStats player)
        {
            var go = Keep(new GameObject("Ninja test runner")); go.SetActive(false);
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, player);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, Skill);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner, new SkillStepContext { OwnerStats = player });
            return runner;
        }

        private NinjaStubHitRecorder Target(float x, int facing = 1)
        {
            var collider = Box("Ninja test target", new Vector2(x, 0), new Vector2(0.6f, 1));
            collider.transform.localScale = new Vector3(facing, 1, 1);
            collider.gameObject.AddComponent<CircleCollider2D>().radius = 0.25f;
            return collider.gameObject.AddComponent<NinjaStubHitRecorder>();
        }

        private static void Strike(SkillStepRunner runner, int index)
        {
            var player = (PlayerStats)typeof(SkillBehaviour).GetField("_ownerStats", Flags).GetValue(runner);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner, new SkillStepContext { OwnerStats = player });
            var step = Skill.Recipe.Steps.Single(s => s.StepDefinition.Id == "BlinkStab");
            typeof(SkillStepRunner).GetMethod("ExecuteBlinkStab", Flags).Invoke(runner, new object[] { index, step });
        }

        [TestCase(6, 1)] [TestCase(6, -1)]
        [TestCase(8, 1)] [TestCase(8, -1)]
        public void ActualMoldsOnSeparatedPlatformsCanBothBeBackstabbed(int groundLayer, int facing)
        {
            Box("Left platform", new Vector2(0, -1), new Vector2(7, 1), groundLayer);
            Box("Right platform", new Vector2(6.5f, -1), new Vector2(5, 1), groundLayer);
            var data = Resources.Load<Scripts.Enemies.EnemyDataSO>("Enemy/SO_Mold");
            var molds = new Scripts.Enemies.EnemyEntity[2];
            var health = new Scripts.Enemies.EnemyHealth[2];
            var before = new float[2];
            for (int i = 0; i < 2; i++)
            {
                molds[i] = Object.Instantiate(data.Prefab); Keep(molds[i].gameObject);
                molds[i].transform.position = Origin + Vector2.right * (2 + i * 4);
                Physics2D.SyncTransforms(); molds[i].Setup(data, 1);
                molds[i].GetComponent<Scripts.Enemies.EnemyLocomotion2D>().SetMoveInput(facing);
                health[i] = molds[i].GetComponent<Scripts.Enemies.EnemyHealth>(); before[i] = health[i].CurrentHealth;
            }
            var player = Player(); var runner = Runner(player); Physics2D.SyncTransforms();
            for (int i = 0; i < 2; i++)
            {
                Strike(runner, i);
                Assert.That(health[i].CurrentHealth, Is.LessThan(before[i]), "The real Mold prefab must receive the stab on either ground layer.");
                Assert.That((player.transform.position.x - molds[i].transform.position.x) * facing, Is.LessThan(0));
            }
        }

        [TestCase(1)] [TestCase(-1)]
        public void ConsecutiveUsesBlinkBehindDifferentEnemiesAndDealNormalWeaponDamage(int facing)
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player);
            var first = Target(2, facing); var second = Target(4, facing); Target(5, facing);
            Physics2D.SyncTransforms();
            Strike(runner, 0);
            Assert.That(first.Hits, Is.EqualTo(1)); Assert.That(second.Hits, Is.Zero);
            Assert.That(first.Hits + second.Hits, Is.EqualTo(1), "A use blinks and deals damage immediately to just one target.");
            Assert.That((player.transform.position.x - first.transform.position.x) * facing, Is.LessThan(0));
            Assert.That(Mathf.Sign(player.transform.localScale.x), Is.EqualTo(facing));
            Assert.That(first.Last.Physical, Is.EqualTo(10).Within(0.001)); Assert.That(first.Last.Source, Is.SameAs(player));
            Strike(runner, 1);
            Assert.That(first.Hits, Is.EqualTo(1), "Never stab the previous target twice, including its extra collider.");
            Assert.That(second.Hits, Is.EqualTo(1));
            Assert.That((player.transform.position.x - second.transform.position.x) * facing, Is.LessThan(0));
        }

        [Test]
        public void OneEnemyIsHitOncePerUseAndANewCastCanTargetItAgain()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player); var target = Target(2);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Vector3 firstDestination = player.transform.position;
            Assert.That(player.transform.position, Is.EqualTo(firstDestination));
            Assert.That(target.Hits, Is.EqualTo(1));
            Strike(runner, 1); Assert.That(target.Hits, Is.EqualTo(2));
        }

        [Test]
        public void UnhitEnemiesThenLeastRecentlyHitEnemiesArePrioritizedAcrossCasts()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player);
            var first = Target(2); var second = Target(4); var third = Target(5);
            Physics2D.SyncTransforms(); Strike(runner, 0); Strike(runner, 1);
            Assert.That(first.Hits, Is.EqualTo(1)); Assert.That(second.Hits, Is.EqualTo(1));
            // Recreating a skill runner must not forget this player's living targets.
            runner = Runner(player);
            Strike(runner, 0);
            Assert.That(third.Hits, Is.EqualTo(1), "An unhit enemy wins even when an already-hit enemy is nearer.");
            Strike(runner, 1);
            Assert.That(first.Hits, Is.EqualTo(2), "When all nearby enemies have been hit, pick the oldest hit rather than the nearest.");
            Assert.That(second.Hits, Is.EqualTo(1));
            runner = Runner(player); Strike(runner, 0);
            Assert.That(second.Hits, Is.EqualTo(2));
            Strike(runner, 1); Assert.That(third.Hits, Is.EqualTo(2));
        }

        [Test]
        public void TargetMemorySurvivesLeavingSearchRadiusAndRemovesDeadOrDestroyedEnemies()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player);
            var first = Target(2); var second = Target(4);
            Physics2D.SyncTransforms(); Strike(runner, 0); Strike(runner, 1);
            first.transform.position += Vector3.right * 20; Physics2D.SyncTransforms();
            runner = Runner(player); Strike(runner, 0);
            Assert.That(second.Hits, Is.EqualTo(2));
            first.transform.position -= Vector3.right * 20; Physics2D.SyncTransforms();
            runner = Runner(player); Strike(runner, 0);
            Assert.That(first.Hits, Is.EqualTo(2), "A returning living enemy keeps its older hit priority.");
            var health = first.gameObject.AddComponent<Scripts.Enemies.EnemyHealth>();
            typeof(Scripts.Enemies.EnemyHealth).GetField("_isDead", Flags).SetValue(health, true);
            var history = player.GetComponent<BlinkStrikeTargetHistory>(); history.PruneDeadTargets();
            Assert.That(history.LastStrike(first), Is.Zero);
            Object.DestroyImmediate(second.gameObject);
            Assert.DoesNotThrow(history.PruneDeadTargets);
        }

        [Test]
        public void DifferentPlayersDoNotShareTargetMemory()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var firstPlayer = Player(); var secondPlayer = Player(); var target = Target(2);
            Physics2D.SyncTransforms(); Strike(Runner(firstPlayer), 0);
            Assert.That(firstPlayer.GetComponent<BlinkStrikeTargetHistory>().LastStrike(target), Is.GreaterThan(0));
            Strike(Runner(secondPlayer), 0);
            Assert.That(target.Hits, Is.EqualTo(2));
            Assert.That(secondPlayer.GetComponent<BlinkStrikeTargetHistory>(), Is.Not.SameAs(firstPlayer.GetComponent<BlinkStrikeTargetHistory>()));
        }

        [Test]
        public void NoEnemiesOrOutOfRangeEnemiesDoNotMoveOrDamage()
        {
            var player = Player(); var runner = Runner(player); var target = Target(9);
            Physics2D.SyncTransforms(); Strike(runner, 0); Strike(runner, 1);
            Assert.That(player.transform.position, Is.EqualTo((Vector3)Origin)); Assert.That(target.Hits, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void WallOrMissingGroundPreventsBlinkAndDamage(bool wall)
        {
            if (wall)
            {
                Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
                Box("Wall", new Vector2(1, 0), new Vector2(0.3f, 4), 6);
            }
            var player = Player(); var runner = Runner(player); var target = Target(3);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Assert.That(player.transform.position, Is.EqualTo((Vector3)Origin)); Assert.That(target.Hits, Is.Zero);
        }

        [Test]
        public void BlockedRearDestinationIsSkippedForNextSafeEnemy()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            Box("Blocked rear", new Vector2(1.3f, 0), new Vector2(0.25f, 1), 6);
            var player = Player(); var runner = Runner(player);
            var blocked = Target(2); var safe = Target(-3);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Assert.That(blocked.Hits, Is.Zero); Assert.That(safe.Hits, Is.EqualTo(1));
        }

        [Test]
        public void DaggerPoolAndSkillDataExposeOneFreeAttackSpeedScaledStrikePerUse()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Attack));
            Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand)); Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(Skill.Recipe.Steps.Count(s => s.StepDefinition.Id == "BlinkStab"), Is.EqualTo(1));
            Assert.That(Skill.Recipe.Steps.Count(s => s.StepDefinition.Id == "WeaponWindup"), Is.EqualTo(1));
            Assert.That(Skill.Recipe.Steps.Count(s => s.StepDefinition.Id == "WeaponRecovery"), Is.EqualTo(1));
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(NinjaStubBuilder.Root + "1HDaggerSkillPool.asset");
            Assert.That(pool.PossibleSkills.Count(s => s.Skill == Skill), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(s => s.Skill.ID == "VenomStrikeSkill"), Is.True);
            Assert.That(SkillDescriptionGenerator.Build(Skill, "en"), Does.Contain("one stab per use").And.Contain("6 m"));
            Assert.That(SkillDescriptionGenerator.Build(Skill, "ru"), Does.Contain("один удар за применение"));
            var preview = SkillDpsPreview.Build(Skill, Player(), 0);
            Assert.That(preview.Hit.PhysicalPerHit, Is.EqualTo(10).Within(0.001), "One use deals one normal-damage stab.");
        }

        [Test]
        public void DeadNearestEnemyIsSkipped()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player);
            var dead = Target(2); var alive = Target(4);
            var health = dead.gameObject.AddComponent<Scripts.Enemies.EnemyHealth>();
            typeof(Scripts.Enemies.EnemyHealth).GetField("_isDead", Flags).SetValue(health, true);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Assert.That(dead.Hits, Is.Zero); Assert.That(alive.Hits, Is.EqualTo(1));
        }

        [Test]
        public void TriggerVolumesDoNotBlockBlink()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            Box("Trigger", new Vector2(1, 0), new Vector2(0.4f, 4), 6).isTrigger = true;
            var player = Player(); var runner = Runner(player); var enemy = Target(3);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Assert.That(enemy.Hits, Is.EqualTo(1));
        }

        [Test]
        public void EnemyLocomotionFacingOverridesSpriteScale()
        {
            Box("Floor", new Vector2(0, -1), new Vector2(30, 1), 6);
            var player = Player(); var runner = Runner(player); var enemy = Target(3);
            var locomotion = enemy.gameObject.AddComponent<Scripts.Enemies.EnemyLocomotion2D>();
            locomotion.SetMoveInput(-1);
            Physics2D.SyncTransforms(); Strike(runner, 0);
            Assert.That(enemy.Hits, Is.EqualTo(1));
            Assert.That(player.transform.position.x, Is.GreaterThan(enemy.transform.position.x));
            Assert.That(player.transform.localScale.x, Is.LessThan(0));
        }
    }
}
