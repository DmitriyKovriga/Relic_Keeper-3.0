using NUnit.Framework;
using Scripts.Combat;
using Scripts.GameplayEvents;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class DefenseFeedbackTests
    {
        private GameObject _player, _other;
        [SetUp] public void Setup() { _player = new GameObject("Player"); _other = new GameObject("Other"); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_player); Object.DestroyImmediate(_other); }
        private GameplayEventContext Event(GameplayEventType type) => new GameplayEventContext
        { Type = type, Target = _player, Source = _other, Count = 1, Damage = new DamageSnapshot(_other) { Physical = 10 } };

        [TestCase(GameplayEventType.Parried, DefenseFeedbackKind.Parry)]
        [TestCase(GameplayEventType.Evaded, DefenseFeedbackKind.Evade)]
        [TestCase(GameplayEventType.MysticShieldConsumed, DefenseFeedbackKind.MysticShield)]
        public void ConfirmedDefenseMapsToItsOwnKind(GameplayEventType type, DefenseFeedbackKind expected)
        { Assert.That(DefenseFeedbackPulse.Classify(Event(type), _player, out var kind), Is.True); Assert.That(kind, Is.EqualTo(expected)); }

        [Test] public void DotZeroDamageWrongTargetAndEmptyConsumptionDoNotRequestDefense()
        {
            var context = Event(GameplayEventType.Evaded); context.Damage.IsDirectHit = false;
            Assert.That(DefenseFeedbackPulse.Classify(context, _player, out _), Is.False);
            context.Damage.IsDirectHit = true; context.Damage.Physical = 0;
            Assert.That(DefenseFeedbackPulse.Classify(context, _player, out _), Is.False);
            context = Event(GameplayEventType.Parried); context.Target = _other;
            Assert.That(DefenseFeedbackPulse.Classify(context, _player, out _), Is.False);
            context = Event(GameplayEventType.MysticShieldConsumed); context.Count = 0;
            Assert.That(DefenseFeedbackPulse.Classify(context, _player, out _), Is.False);
        }
        [Test] public void DamageTakenNeverCreatesAnExtraDefenseEffect()
        { Assert.That(DefenseFeedbackPulse.Classify(Event(GameplayEventType.DamageTaken), _player, out _), Is.False); }

        [TestCase(DefenseFeedbackKind.Parry)] [TestCase(DefenseFeedbackKind.Evade)] [TestCase(DefenseFeedbackKind.MysticShield)]
        public void ThousandRetriggersCannotStackOrExtend(DefenseFeedbackKind kind)
        {
            var pulse = new DefenseFeedbackPulse(); Assert.That(pulse.Trigger(kind, 10, .2f, .3f), Is.True);
            for (int i = 0; i < 1000; i++) Assert.That(pulse.Trigger(kind, 10.05f, .2f, .3f), Is.False);
            Assert.That(pulse.Sample(kind, 10.1f, out var progress), Is.True); Assert.That(progress, Is.EqualTo(.5f).Within(.001));
            Assert.That(pulse.Sample(kind, 10.25f, out _), Is.False);
            Assert.That(pulse.Trigger(kind, 10.31f, .2f, .3f), Is.True);
            pulse.Clear(); Assert.That(pulse.Sample(kind, 10.32f, out _), Is.False);
        }
        [Test] public void DifferentDefensesHaveIndependentBoundedLanes()
        {
            var pulse = new DefenseFeedbackPulse();
            Assert.That(pulse.Trigger(DefenseFeedbackKind.Parry, 1, .2f, .3f), Is.True);
            Assert.That(pulse.Trigger(DefenseFeedbackKind.MysticShield, 1, .2f, .3f), Is.True);
            Assert.That(pulse.Trigger(DefenseFeedbackKind.Evade, 1, .2f, .3f), Is.True);
            Assert.That(pulse.Trigger(DefenseFeedbackKind.Parry, float.NaN, .2f, .3f), Is.False);
        }
        [TestCase(DefenseFeedbackKind.Parry, 4)] [TestCase(DefenseFeedbackKind.Evade, 5)] [TestCase(DefenseFeedbackKind.MysticShield, 6)]
        public void PixelLabClipsHaveOrderedFramesAndCrispImport(DefenseFeedbackKind kind, int count)
        {
            var frames = PlayerDefenseFeedback.Frames(kind); Assert.That(frames.Length, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(frames[i].name, Is.EqualTo("Defense_" + i.ToString("D2")));
                Assert.That(frames[i].pixelsPerUnit, Is.EqualTo(24));
                Assert.That(frames[i].texture.filterMode, Is.EqualTo(FilterMode.Point));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frames[i]));
                Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
        }
        [Test] public void SharedDefenseMaterialUsesAvailableWorldShader()
        {
            var material = Resources.Load<Material>("Visuals/DefenseFeedback/DefenseAccent");
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Is.EqualTo("RelicKeeper/Sprites/World Color Unlit"));
        }
        [Test] public void WarmPulseSchedulingAndSamplingAllocateNoManagedMemory()
        {
            var pulse = new DefenseFeedbackPulse();
            pulse.Trigger(DefenseFeedbackKind.Parry, 0, .16f, .3f); pulse.Sample(DefenseFeedbackKind.Parry, .05f, out _);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i < 10000; i++)
            {
                pulse.Trigger(DefenseFeedbackKind.Parry, i, .16f, .3f);
                pulse.Sample(DefenseFeedbackKind.Parry, i + .05f, out _);
            }
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero);
        }
    }
}
