using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class BlinkTests
    {
        private static readonly Vector2 Origin = new Vector2(3400, 2200);
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(BlinkBuilder.SkillPath);

        private BoxCollider2D Box(Vector2 offset, Vector2 size, int layer = 0)
        {
            var go = new GameObject("Blink test") { layer = layer }; _objects.Add(go);
            go.transform.position = Origin + offset;
            var collider = go.AddComponent<BoxCollider2D>(); collider.size = size;
            Physics2D.SyncTransforms(); return collider;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var visual in Object.FindObjectsByType<BlinkStabVisual>(FindObjectsSortMode.None))
                if (Vector2.Distance(visual.transform.position, Origin) < 20) Object.DestroyImmediate(visual.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        private static Vector2 Destination(BoxCollider2D body, int facing) =>
            ForwardBlinkGeometry.Destination(body, Origin, facing, 5, 1 << 6, .04f);

        [TestCase(1)] [TestCase(-1)]
        public void BlinksFiveTilesInFacingDirectionWithoutGroundSupport(int facing)
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            Assert.That(Destination(body, facing), Is.EqualTo(Origin + Vector2.right * (5 * facing)));
        }

        [TestCase(1)] [TestCase(-1)]
        public void StopsFullBodyBeforeWallEvenWhenOnlyShoulderIntersects(int facing)
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            Box(new Vector2(3 * facing, .6f), new Vector2(.5f, .5f), 6);
            Vector2 destination = Destination(body, facing);
            Assert.That((destination.x - Origin.x) * facing, Is.EqualTo(2.46f).Within(.005f));
            Assert.That(destination.y, Is.EqualTo(Origin.y));
        }

        [Test]
        public void IgnoresTriggerEnemiesAndOneWayPlatforms()
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            Box(Vector2.right, Vector2.one, 6).isTrigger = true;
            Box(Vector2.right * 2, Vector2.one, 7);
            Box(Vector2.right * 3, Vector2.one, 8);
            Physics2D.SyncTransforms();
            Assert.That(Destination(body, 1).x, Is.EqualTo(Origin.x + 5));
        }

        [Test]
        public void GroundContactDoesNotPreventHorizontalBlink()
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            Box(new Vector2(2, -1), new Vector2(15, 1), 6);
            Assert.That(Destination(body, 1).x, Is.EqualTo(Origin.x + 5));
        }

        [Test]
        public void WallAtStartCannotPushPlayerBackwards()
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            Box(new Vector2(.5f, 0), new Vector2(.5f, 2), 6);
            Assert.That(Destination(body, 1).x, Is.EqualTo(Origin.x).Within(.001f));
        }

        [Test]
        public void RuntimeStepPreservesAirborneVelocityAndDoesNotDamage()
        {
            var body = Box(Vector2.zero, new Vector2(.5f, 1));
            var go = body.gameObject; go.SetActive(false); go.transform.localScale = new Vector3(-1, 1, 1);
            var stats = go.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
            var rigidbody = go.AddComponent<Rigidbody2D>(); rigidbody.gravityScale = 0;
            go.SetActive(true); rigidbody.linearVelocity = new Vector2(2, -4);
            var runnerObject = new GameObject("Blink runner"); _objects.Add(runnerObject); runnerObject.SetActive(false);
            var runner = runnerObject.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, stats);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner,
                new SkillStepContext { OwnerStats = stats });
            typeof(SkillStepRunner).GetMethod("ExecuteForwardBlink", Flags).Invoke(runner, new object[] { Skill.Recipe.Steps.Single() });
            Assert.That(go.transform.position.x, Is.EqualTo(Origin.x - 5).Within(.001f));
            Assert.That(rigidbody.linearVelocity, Is.EqualTo(new Vector2(2, -4)));
            Assert.That(Object.FindObjectsByType<BlinkStabVisual>(FindObjectsSortMode.None).Count(v =>
                Vector2.Distance(v.transform.position, Origin) < 20), Is.EqualTo(2));
        }

        [Test]
        public void SkillDataAndLocalizedDescriptionsMatchBootsRequest()
        {
            Assert.That(Skill, Is.Not.Null);
            Assert.That(Skill.Cooldown, Is.EqualTo(4)); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.Icon, Is.Not.Null);
            Assert.That(Skill.Recipe.Steps.Single().StepDefinition.Id, Is.EqualTo("ForwardBlink"));
            Assert.That(Skill.Recipe.Steps.Single().StepDefinition.IsDurationStep, Is.False);
            string en = SkillDescriptionGenerator.Build(Skill, "en"), ru = SkillDescriptionGenerator.Build(Skill, "ru");
            Assert.That(en, Does.Contain("5").And.Contain("facing")); Assert.That(ru, Does.Contain("5").And.Contain("взгляда"));
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(BlinkBuilder.Root + "EvasionBootsSkillPool.asset");
            Assert.That(pool.PossibleSkills.Any(e => e.Skill == Skill), Is.True);
            Assert.That(pool.PossibleSkills.Any(e => e.Skill.ID == "RushSkill"), Is.True);
        }
    }
}
