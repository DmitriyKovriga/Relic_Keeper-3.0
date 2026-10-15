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
    public sealed class ElectricBeamHitRecorder : MonoBehaviour, IDamageable
    {
        public int Hits;
        public DamageSnapshot Last;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; Last = damage; return true; }
    }

    public class ElectricBeamTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _created = new List<Object>();
        private static readonly Vector2 Origin = new Vector2(2000, 2000);
        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(ElectricBeamBuilder.SkillPath);

        [TearDown]
        public void TearDown()
        {
            foreach (var visual in Object.FindObjectsByType<ElectricBeamVisual>(FindObjectsSortMode.None))
                if (visual.transform.position.x > 1500f) Object.DestroyImmediate(visual.gameObject);
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private BoxCollider2D Wall(float x, bool trigger = false)
        {
            var go = Keep(new GameObject("Beam test wall") { layer = 6 });
            go.transform.position = Origin + Vector2.right * x;
            var box = go.AddComponent<BoxCollider2D>(); box.size = new Vector2(0.2f, 4f); box.isTrigger = trigger;
            Physics2D.SyncTransforms(); return box;
        }

        [TestCase(1)] [TestCase(-1)]
        public void BeamClipsAtWallsInEitherDirection(int direction)
        {
            Vector2 muzzle = Origin + Vector2.right * direction * 0.45f;
            Assert.That(InstantBeamGeometry.ResolveLength(Origin, muzzle, direction, 12, 1 << 6), Is.EqualTo(12));
            Wall(6 * direction);
            Assert.That(InstantBeamGeometry.ResolveLength(Origin, muzzle, direction, 12, 1 << 6), Is.EqualTo(5.45f).Within(0.002));
        }

        [TestCase(1)] [TestCase(-1)]
        public void BeamCannotSkipWallBetweenCasterAndMuzzle(int direction)
        {
            Wall(0.2f * direction);
            Assert.That(InstantBeamGeometry.ResolveLength(Origin, Origin + Vector2.right * direction * 0.45f,
                direction, 12, 1 << 6), Is.Zero);
        }

        [Test]
        public void TriggerVolumesDoNotBlockBeamOrMuzzle()
        {
            Wall(0.2f, true); Wall(4f, true);
            Assert.That(InstantBeamGeometry.ResolveLength(Origin, Origin + Vector2.right * 0.45f, 1, 12, 1 << 6), Is.EqualTo(12));
        }

        private PlayerStats Player(int direction = 1)
        {
            var go = Keep(new GameObject("Beam test caster")); go.SetActive(false);
            go.transform.position = Origin; go.transform.localScale = new Vector3(direction, 1, 1);
            var stats = go.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
            foreach (var pair in new[] { (StatType.DamagePhysical, 10f), (StatType.DamageFire, 20f),
                         (StatType.DamageCold, 30f), (StatType.DamageLightning, 40f), (StatType.CritChance, 0f) })
                stats.GetStat(pair.Item1).BaseValue = pair.Item2;
            return stats;
        }

        private ElectricBeamHitRecorder Target(float x, float y = 0.25f)
        {
            var go = Keep(new GameObject("Beam test target")); go.transform.position = Origin + new Vector2(x, y);
            go.AddComponent<BoxCollider2D>().size = new Vector2(0.3f, 0.5f);
            go.AddComponent<CircleCollider2D>().radius = 0.15f;
            return go.AddComponent<ElectricBeamHitRecorder>();
        }

        [TestCase(1)] [TestCase(-1)]
        public void InstantBeamHitsAllForwardTargetsOnceAndConvertsHalfOfEachChannel(int direction)
        {
            var stats = Player(direction);
            var go = Keep(new GameObject("Beam test runner")); go.SetActive(false);
            var runner = go.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, stats);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, Skill);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner, new SkillStepContext { OwnerStats = stats });
            var first = Target(direction * 2); var second = Target(direction * 5);
            var behind = Target(-direction * 2); var offAxis = Target(direction * 3, 2);
            var beyondRange = Target(direction * 14); var behindWall = Target(direction * 10);
            Wall(direction * 8);
            Physics2D.SyncTransforms();
            var step = Skill.Recipe.Steps.Single(s => s.StepDefinition.Id == "InstantBeam");
            typeof(SkillStepRunner).GetMethod("ExecuteInstantBeam", Flags).Invoke(runner, new object[] { 3, step });
            foreach (var target in new[] { first, second })
            {
                Assert.That(target.Hits, Is.EqualTo(1), "Multiple colliders must not duplicate beam hits.");
                Assert.That(target.Last.Physical, Is.EqualTo(5).Within(0.001));
                Assert.That(target.Last.Fire, Is.EqualTo(10).Within(0.001));
                Assert.That(target.Last.Cold, Is.EqualTo(15).Within(0.001));
                Assert.That(target.Last.Lightning, Is.EqualTo(70).Within(0.001));
                Assert.That(target.Last.TotalDamage, Is.EqualTo(100).Within(0.001));
                Assert.That(target.Last.Source, Is.SameAs(stats));
            }
            foreach (var target in new[] { behind, offAxis, beyondRange, behindWall })
                Assert.That(target.Hits, Is.Zero);
        }

        [Test]
        public void PreviewAndDescriptionReflectBeamConversionAndRange()
        {
            var stats = Player();
            var preview = SkillDpsPreview.Build(Skill, stats, 0);
            Assert.That(preview.Hit.PhysicalPerHit, Is.EqualTo(5).Within(0.001));
            Assert.That(preview.Hit.LightningPerHit, Is.EqualTo(70).Within(0.001));
            Assert.That(SkillDescriptionGenerator.IsKnownStepId("InstantBeam"), Is.True);
            string english = SkillDescriptionGenerator.Build(Skill, "en");
            string russian = SkillDescriptionGenerator.Build(Skill, "ru");
            Assert.That(english, Does.Contain("12 m").And.Contain("50%").And.Contain("Lightning"));
            Assert.That(russian, Does.Contain("12 м").And.Contain("50%"));
        }

        [Test]
        public void SkillIsAvailableOnWandsWithoutReplacingSeekerAndHasNoManaOrCooldown()
        {
            Assert.That(Skill.Cooldown, Is.Zero); Assert.That(Skill.ManaCost, Is.Zero);
            Assert.That(Skill.IsActive, Is.True); Assert.That(Skill.WeaponRole, Is.EqualTo(SkillWeaponRole.MainHand));
            Assert.That(Skill.ActionSpeedMode, Is.EqualTo(SkillActionSpeedMode.Spell));
            Assert.That(Skill.Icon, Is.Not.Null); Assert.That(Skill.Recipe.IsChanneling, Is.False);
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(ElectricBeamBuilder.Root + "1hWandSkillPool.asset");
            Assert.That(pool.PossibleSkills.Count(s => s.Skill == Skill && s.Weight > 0), Is.EqualTo(1));
            Assert.That(pool.PossibleSkills.Any(s => s.Skill.ID == "SeekerSkill"), Is.True);
        }

        [Test]
        public void BeamAppearsAtFullLengthAndChangesShapeDuringDissipation()
        {
            var visual = ElectricBeamVisual.Spawn(Origin, Origin + Vector2.right * 12, 0.28f);
            var renderer = visual.GetComponent<SpriteRenderer>();
            Assert.That(renderer.bounds.size.x, Is.EqualTo(12f).Within(1f / 24));
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("RelicKeeper/ElectricBeam"));
            Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            var texture = renderer.sprite.texture;
            Color32[] first = texture.GetPixels32();
            Assert.That(first.Where((pixel, index) => index % texture.width == texture.width - 1).Any(pixel => pixel.a > 0), Is.True);
            float initialAlpha = renderer.color.a;
            visual.Sample(0.9f);
            Assert.That(texture.GetPixels32().SequenceEqual(first), Is.False);
            Assert.That(renderer.color.a, Is.LessThan(initialAlpha));
        }
    }
}
