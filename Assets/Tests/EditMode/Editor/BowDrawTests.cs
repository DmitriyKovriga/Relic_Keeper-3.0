using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Skills.Projectiles;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class BowDrawTests
    {
        private GameObject _owner;
        private GameObject _template;
        private SkillHandAnimation _animation;
        private SpriteRenderer _bow;
        private GameObject _launched;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Bow draw test owner");
            _owner.SetActive(false);
            var visuals = new GameObject("Visuals").transform;
            visuals.SetParent(_owner.transform, false);
            var pivot = new GameObject("HandPivot").transform;
            pivot.SetParent(visuals, false);
            var holder = new GameObject("WeaponHolder").transform;
            holder.SetParent(pivot, false);
            holder.localPosition = new Vector3(0.3f, 0.2f, 0);
            _bow = holder.gameObject.AddComponent<SpriteRenderer>();
            _bow.sprite = Resources.Load<Scripts.Items.WeaponItemSO>("Items/2HWeapon/Bow/Adventurer's Bow").InHandSprite;
            var stats = _owner.AddComponent<PlayerStats>();
            _animation = _owner.AddComponent<SkillHandAnimation>();
            _animation.Initialize(stats);
            _animation.SetActiveStyle(WeaponSwingStyle.BowDraw);
            _template = new GameObject("Arrow template");
            _template.AddComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Skills/2HWeapon/Bow/BowArrow");
        }

        [TearDown]
        public void TearDown()
        {
            if (_launched != null) Object.DestroyImmediate(_launched);
            Object.DestroyImmediate(_owner);
            Object.DestroyImmediate(_template);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void Windup_PullsArrowBackRelativeToFacing_WithoutDamageCollider(int facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            _animation.PrepareDrawnProjectile(_template);
            Assert.That(_animation.TryGetDrawnProjectilePosition(out var before), Is.True);
            _animation.LerpWindup(1);
            Assert.That(_animation.TryGetDrawnProjectilePosition(out var after), Is.True);
            Assert.That((after.x - before.x) * facing, Is.LessThan(-0.1f));
            Assert.That(after.y, Is.EqualTo(before.y).Within(0.0001f));
            Assert.That(_owner.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(_bow.enabled, Is.True);
            Assert.That(_animation.KeepsWeaponVisibleAtImpact, Is.True);
        }

        [Test]
        public void CancelAndNextDraw_HideThenReuseTheNockedArrow()
        {
            _animation.PrepareDrawnProjectile(_template);
            _animation.LerpWindup(1);
            _animation.ForceReset();
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.False);
            _animation.PrepareDrawnProjectile(_template);
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.True);
            Assert.That(_owner.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(2));
            _animation.ReleaseDrawnProjectile();
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.False);
            Assert.That(_bow.enabled, Is.True);
        }

        [Test]
        public void SwitchingToMelee_ClearsArrowAndRetainsMeleeStrikeVisibility()
        {
            _animation.PrepareDrawnProjectile(_template);
            _animation.SetActiveStyle(WeaponSwingStyle.Slash);
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.False);
            Assert.That(_animation.KeepsWeaponVisibleAtImpact, Is.False);
            _animation.PrepareDrawnProjectile(_template);
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.False);
        }

        [Test]
        public void BowStance_ResolvesDraw_AndRecipeReleasesAfterWindup()
        {
            var skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Bow/SimpleBowShotSkill");
            Assert.That(WeaponSwingStyleResolver.ResolveStyleId(skill), Is.EqualTo("bow_draw"));
            Assert.That(WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId("bow_stance", "bow_draw"), Is.True);
            Assert.That(WeaponSwingStyleResolver.IsStyleIdAllowedForStanceId("default", "bow_draw"), Is.False);
            var shot = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            var windup = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "WeaponWindup");
            Assert.That(shot.StartPercentPipeline, Is.EqualTo(windup.EndPercentPipeline));
            Assert.That(shot.GetObject<GameObject>("ProjectilePrefab"), Is.Not.Null);
            Assert.That(shot.GetFloat("RotationDegreesPerSecond", 720), Is.Zero);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void Release_LaunchesOneArrowFromDrawnPosition_AndKeepsBowVisible(int facing)
        {
            _owner.transform.position = Vector3.right * 2000;
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            var runner = _owner.AddComponent<SkillStepRunner>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(SkillStepRunner).GetField("_animCtrl", flags).SetValue(runner, _animation);
            typeof(SkillStepRunner).GetField("_moveCtrl", flags).SetValue(runner, _owner.GetComponent<SkillMovementControl>());
            var skill = Resources.Load<SkillDataSO>("Skills/2HWeapon/Bow/SimpleBowShotSkill");
            var stats = _owner.GetComponent<PlayerStats>();
            runner.Initialize(stats, skill);
            typeof(SkillStepRunner).GetField("_ctx", flags).SetValue(runner,
                new SkillStepContext { OwnerStats = stats, TotalDuration = 1 });
            _animation.SetActiveStyle(WeaponSwingStyle.BowDraw);
            var shot = skill.Recipe.Steps.Single(step => step.StepDefinition.Id == "SpawnProjectile");
            _animation.PrepareDrawnProjectile(shot.GetObject<GameObject>("ProjectilePrefab"));
            _animation.LerpWindup(1);
            _animation.TryGetDrawnProjectilePosition(out var position);
            var previous = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None);
            typeof(SkillStepRunner).GetMethod("ExecuteSpawnProjectile", flags).Invoke(runner, new object[] { shot });
            var launched = Object.FindObjectsByType<SkillProjectile>(FindObjectsSortMode.None).Except(previous).ToArray();
            Assert.That(launched.Length, Is.EqualTo(1));
            _launched = launched[0].gameObject;
            Assert.That(Vector2.Distance(_launched.transform.position, position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(_launched.transform.right, Vector3.right * facing), Is.GreaterThan(0.99f));
            Assert.That(_animation.TryGetDrawnProjectilePosition(out _), Is.False);
            int index = skill.Recipe.Steps.FindIndex(step => step.StepDefinition.Id == "WeaponStrike");
            typeof(SkillStepRunner).GetMethod("ExecuteStepLogic", flags).Invoke(runner,
                new object[] { index, skill.Recipe.Steps[index], 1f, 0f });
            Assert.That(_bow.enabled, Is.True);
        }
    }
}
