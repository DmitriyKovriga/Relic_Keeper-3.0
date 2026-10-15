using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RelicKeeper.Editor.Skills;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class PlasmaFieldTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<Object> _created = new List<Object>();
        private static readonly Vector2 Origin = new Vector2(2200, 2200);
        private static SkillDataSO Skill => AssetDatabase.LoadAssetAtPath<SkillDataSO>(PlasmaFieldBuilder.SkillPath);
        private T Keep<T>(T value) where T : Object { _created.Add(value); return value; }
        [TearDown]
        public void Cleanup()
        {
            foreach (var visual in Object.FindObjectsByType<PlasmaFieldVisual>(FindObjectsSortMode.None))
                if (visual.transform.position.x > 2000) Object.DestroyImmediate(visual.gameObject);
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private SkillStepRunner Runner(out PlayerStats stats, float aoe = 0)
        {
            var player = Keep(new GameObject("Plasma caster")); player.SetActive(false); player.transform.position = Origin;
            stats = player.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Flags).Invoke(stats, null);
            foreach (var pair in new[] { (StatType.DamagePhysical, 10f), (StatType.DamageFire, 20f), (StatType.DamageCold, 30f), (StatType.DamageLightning, 40f), (StatType.CritChance, 0f), (StatType.AreaOfEffect, aoe) })
                stats.GetStat(pair.Item1).BaseValue = pair.Item2;
            var runner = player.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", Flags).SetValue(runner, stats);
            typeof(SkillBehaviour).GetField("_data", Flags).SetValue(runner, Skill);
            typeof(SkillStepRunner).GetField("_ctx", Flags).SetValue(runner, new SkillStepContext { OwnerStats = stats, AoeScale = 1 + aoe / 100 });
            return runner;
        }

        private ElectricBeamHitRecorder Target(Vector2 offset)
        {
            var host = Keep(new GameObject("Plasma target")); host.transform.position = Origin + offset;
            var recorder = host.AddComponent<ElectricBeamHitRecorder>();
            host.AddComponent<BoxCollider2D>().size = Vector2.one * .2f;
            var child = new GameObject("Child collider"); child.transform.SetParent(host.transform, false); child.AddComponent<CircleCollider2D>().radius = .1f;
            return recorder;
        }

        private static void Sample(SkillStepRunner runner, float progress) => typeof(SkillStepRunner).GetMethod("ExecutePlasmaField", Flags)
            .Invoke(runner, new object[] { 0, Skill.Recipe.Steps[0], progress });

        [Test]
        public void WaveExpandsReversesAndShrinksWithoutRetriggeringStationarySamples()
        {
            var wave = new PlasmaFieldWave(4, .3f);
            Assert.That(wave.RadiusAt(0), Is.Zero); Assert.That(wave.RadiusAt(.5f), Is.EqualTo(4).Within(.001)); Assert.That(wave.RadiusAt(1), Is.Zero);
            wave.Advance(.4f, out var outward, out var inward); Assert.That(outward.Active, Is.True); Assert.That(inward.Active, Is.False);
            wave.Advance(.6f, out outward, out inward); Assert.That(outward.Active && inward.Active, Is.True);
            Assert.That(outward.Outer, Is.EqualTo(4.15f).Within(.001));
            wave.Advance(.6f, out outward, out inward); Assert.That(outward.Active || inward.Active, Is.False);
        }

        [Test]
        public void EnemiesReceiveOneHitPerPassWithFullLightningConversion()
        {
            var runner = Runner(out var owner);
            var near = Target(Vector2.right * 1); var far = Target(Vector2.left * 3.8f);
            var above = Target(Vector2.up * 2); var outside = Target(Vector2.right * 6);
            Physics2D.SyncTransforms();
            for (int i = 0; i <= 10; i++) Sample(runner, i / 20f);
            foreach (var target in new[] { near, far, above }) Assert.That(target.Hits, Is.EqualTo(1));
            for (int i = 11; i <= 20; i++) Sample(runner, i / 20f);
            foreach (var target in new[] { near, far, above })
            {
                Assert.That(target.Hits, Is.EqualTo(2), "A compound collider must count as one enemy per pass.");
                Assert.That(target.Last.Physical + target.Last.Fire + target.Last.Cold, Is.Zero.Within(.001));
                Assert.That(target.Last.Lightning, Is.EqualTo(100).Within(.001)); Assert.That(target.Last.Source, Is.SameAs(owner));
            }
            Assert.That(outside.Hits, Is.Zero);
        }

        [Test]
        public void OneLargeFrameStillSweepsBothPasses()
        {
            var runner = Runner(out _); var target = Target(Vector2.right * 3);
            Physics2D.SyncTransforms(); Sample(runner, 1);
            Assert.That(target.Hits, Is.EqualTo(2));
        }

        [Test]
        public void AreaScalingAndPlayerMovementMoveTheField()
        {
            var runner = Runner(out var owner, 100); var target = Target(Vector2.right * 7);
            Physics2D.SyncTransforms(); Sample(runner, .5f); Assert.That(target.Hits, Is.EqualTo(1));
            owner.transform.position += Vector3.right * 2; Physics2D.SyncTransforms(); Sample(runner, 1);
            Assert.That(target.Hits, Is.EqualTo(2));
            var visual = Object.FindObjectsByType<PlasmaFieldVisual>(FindObjectsSortMode.None).Single();
            Assert.That((Vector2)visual.transform.position, Is.EqualTo((Vector2)owner.transform.position));
            typeof(SkillStepRunner).GetMethod("Cleanup", Flags).Invoke(runner, null);
            Assert.That(visual == null, Is.True, "Cancel/cleanup must remove the field visual.");
        }

        [Test]
        public void DataPreviewLocalizationAndNativeIconMatchTheSkill()
        {
            Assert.That(Skill.ManaCost, Is.EqualTo(25)); Assert.That(Skill.Cooldown, Is.EqualTo(10));
            Assert.That(Skill.Icon.rect.size, Is.EqualTo(new Vector2(24, 24)));
            Assert.That(Skill.Recipe.Steps[0].StepDefinition.IsDurationStep, Is.True);
            Runner(out var owner); var preview = SkillDpsPreview.Build(Skill, owner, 0);
            Assert.That(preview.Hit.LightningPerHit, Is.EqualTo(100).Within(.001));
            Assert.That(preview.Hit.PhysicalPerHit + preview.Hit.FirePerHit + preview.Hit.ColdPerHit, Is.Zero.Within(.001));
            string en = SkillDescriptionGenerator.Build(Skill, "en"), ru = SkillDescriptionGenerator.Build(Skill, "ru");
            Assert.That(en, Does.Contain("once outward and once on the return")); Assert.That(en, Does.Contain("100%"));
            Assert.That(ru, Does.Contain("возвращении"));
            var pool = AssetDatabase.LoadAssetAtPath<SkillPoolSO>(PlasmaFieldBuilder.Root + "MysticHelmetSkillPool.asset");
            Assert.That(pool.PossibleSkills.Count, Is.GreaterThanOrEqualTo(2)); Assert.That(pool.PossibleSkills.Any(s => s.Skill == Skill), Is.True);
        }

        [Test]
        public void VfxChangesShapeAtTheSameRadiusAndRetainsItsBuildMaterial()
        {
            var visual = PlasmaFieldVisual.Create(4); Keep(visual.gameObject);
            visual.Sample(Origin, .2f, 2); var renderer = visual.GetComponent<SpriteRenderer>();
            var first = renderer.sprite.texture.GetPixels32();
            visual.Sample(Origin, .8f, 2);
            Assert.That(renderer.sprite.texture.GetPixels32().SequenceEqual(first), Is.False);
            Assert.That(renderer.sharedMaterial, Is.Not.Null); Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            Assert.That(renderer.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }
}
