using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PiercingBowShotTests
    {
        private const string Root = "Skills/2HWeapon/Bow/PiercingShot/";
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _owner;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _spawned) if (item != null) Object.DestroyImmediate(item);
            _spawned.Clear();
            if (_owner != null) Object.DestroyImmediate(_owner);
        }

        [Test]
        public void PiercingShot_Costs25ManaWith6SecondCooldown_AndPreservesBowTiming()
        {
            var skill = Resources.Load<SkillDataSO>(Root + "PiercingBowShotSkill");
            var normal = Resources.Load<SkillDataSO>("Skills/2HWeapon/Bow/SimpleBowShotSkill");
            Assert.That(skill, Is.Not.Null);
            Assert.That(skill, Is.Not.SameAs(normal));
            Assert.That(skill.ManaCost, Is.EqualTo(25));
            Assert.That(skill.Cooldown, Is.EqualTo(6));
            Assert.That(skill.WeaponRole, Is.EqualTo(SkillWeaponRole.Special));
            Assert.That(WeaponSwingStyleResolver.ResolveStyleId(skill), Is.EqualTo("bow_draw"));
            Assert.That(skill.Icon, Is.Not.Null);
            foreach (string id in new[] { "WeaponWindup", "SpawnProjectile", "WeaponRecovery" })
            {
                var actual = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == id);
                var expected = normal.Recipe.Steps.Single(step => step.StepDefinition.Id == id);
                Assert.That(actual.StartPercentPipeline, Is.EqualTo(expected.StartPercentPipeline));
                Assert.That(actual.EndPercentPipeline, Is.EqualTo(expected.EndPercentPipeline));
            }
            var bow = Resources.Load<WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow");
            Assert.That(bow.SecondarySkillPool.PossibleSkills.Any(entry => entry.Skill == skill), Is.True);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void EmpoweredShot_IgnoresExtraProjectilesForkAndChain_AndPiercesWithoutLimit(int facing)
        {
            _owner = new GameObject("Piercing shot test");
            _owner.SetActive(false);
            _owner.transform.position = new Vector3(2000, 0, 0);
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(_owner.transform, false);
            var pivot = new GameObject("HandPivot").transform;
            pivot.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").transform;
            holder.SetParent(pivot, false);
            holder.gameObject.AddComponent<SpriteRenderer>();
            var stats = _owner.AddComponent<PlayerStats>();
            stats.GetStat(StatType.ProjectileCount).BaseValue = 7;
            stats.GetStat(StatType.ProjectileFork).BaseValue = 4;
            stats.GetStat(StatType.ProjectileChain).BaseValue = 5;
            stats.GetStat(StatType.ProjectilePierce).BaseValue = 0;
            var runner = _owner.AddComponent<SkillStepRunner>();
            var animation = _owner.GetComponent<SkillHandAnimation>();
            typeof(SkillStepRunner).GetField("_animCtrl", Flags).SetValue(runner, animation);
            typeof(SkillStepRunner).GetField("_moveCtrl", Flags).SetValue(runner, _owner.GetComponent<SkillMovementControl>());
            var skill = Resources.Load<SkillDataSO>(Root + "PiercingBowShotSkill");
            runner.Initialize(stats, skill);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner, new SkillStepContext { OwnerStats = stats, TotalDuration = 1 });
            animation.SetActiveStyleId("bow_draw");
            var shot = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            animation.PrepareDrawnProjectile(shot.GetObject<GameObject>("ProjectilePrefab"));
            animation.LerpWindup(1);
            animation.TryGetDrawnProjectilePosition(out var origin);
            var previous = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None);
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", Flags).Invoke(runner, new object[] { shot });
            var instances = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Except(previous).ToArray();
            _spawned.AddRange(instances.Select(projectile => projectile.gameObject));
            Assert.That(instances.Length, Is.EqualTo(1), "Projectile-count bonuses must not add arrows.");
            var projectile = instances[0];
            var data = (SkillProjectileLaunchData)typeof(SkillProjectile).GetField("_data", Flags).GetValue(projectile);
            Assert.That(data.DamageMultiplier, Is.EqualTo(3));
            Assert.That(data.IgnoreFork && data.IgnoreChain && data.InfinitePierce, Is.True);
            Assert.That(data.RemainingForks, Is.GreaterThan(0), "Test must exercise a nonzero fork bonus.");
            Assert.That(data.RemainingChains, Is.GreaterThan(0));
            Assert.That(Vector2.Distance(origin, projectile.transform.position), Is.LessThan(0.001f));
            Assert.That((bool)typeof(SkillProjectile).GetMethod("TryFork", Flags).Invoke(projectile, null), Is.False);
            Assert.That((bool)typeof(SkillProjectile).GetMethod("TryChain", Flags).Invoke(projectile, new object[] { _owner.transform }), Is.False);
            for (int i = 0; i < 30; i++)
                Assert.That((bool)typeof(SkillProjectile).GetMethod("TryPierce", Flags).Invoke(projectile, null), Is.True);
            Assert.That(data.RemainingPierces, Is.Zero);
            Assert.That(projectile.GetComponent<CircleCollider2D>().radius,
                Is.EqualTo(SkillProjectile.MinPlayableWorldRadius).Within(0.0001f));
            // Increase the test hit scale above the shared radius floor, so a wrongly
            // included trail cannot be masked by the minimum playable collision size.
            data.HitRadius = 1;
            typeof(SkillProjectile).GetMethod("ApplyHitbox", Flags).Invoke(projectile, new object[] { true });
            float coreRadius = SkillProjectile.CalculateLocalRadius(16f / 24, 7f / 24, 1, 1, 1, 1);
            Assert.That(projectile.GetComponent<CircleCollider2D>().radius, Is.EqualTo(coreRadius).Within(0.0001f),
                "Wide energy trail must not enlarge arrow collision.");
        }
    }
}
