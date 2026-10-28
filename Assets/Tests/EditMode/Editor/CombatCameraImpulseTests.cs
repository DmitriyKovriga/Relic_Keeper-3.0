using NUnit.Framework;
using Scripts.Combat;
using Scripts.Visuals;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CombatCameraImpulseTests
    {
        [Test]
        public void SharedCastAllowsOneImpulseAcrossTargetsAndReturnPasses()
        {
            var attack = new AttackImpactContext(null, 0, enableHitStop: false);
            Assert.That(attack.TryConsumeCameraImpulse(), Is.True);
            for (int i = 0; i < 100; i++) Assert.That(attack.TryConsumeCameraImpulse(), Is.False);
            Assert.That(new AttackImpactContext(null, 0).TryConsumeCameraImpulse(), Is.True);
        }

        [Test]
        public void ThousandRequestsCannotStackOrExtendThePulse()
        {
            var pulse = new CombatCameraPulse();
            Assert.That(pulse.Trigger(10, Vector2.right, 2, .14f, .4f), Is.True);
            Vector3 before = pulse.Sample(10.05);
            for (int i = 0; i < 1000; i++) Assert.That(pulse.Trigger(10.05, Vector2.left, 2, .2f, .4f), Is.False);
            Assert.That(pulse.Sample(10.05), Is.EqualTo(before));
            Assert.That(pulse.Sample(10.15), Is.EqualTo(Vector3.zero));
            Assert.That(pulse.Trigger(10.39, Vector2.left, 2, .14f, .4f), Is.False);
            Assert.That(pulse.Trigger(10.41, Vector2.left, 2, .14f, .4f), Is.True);
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void PulseReturnsExactlyToNeutralAtEverySamplingRate(int fps)
        {
            var pulse = new CombatCameraPulse(); pulse.Trigger(0, Vector2.right, 200, 10, 0);
            float maximum = 0;
            for (int i = 0; i < fps; i++)
            {
                Vector3 offset = pulse.Sample((double)i / fps);
                maximum = Mathf.Max(maximum, offset.magnitude);
                Assert.That(offset.x, Is.GreaterThanOrEqualTo(0));
                Assert.That(offset.y, Is.Zero); Assert.That(offset.z, Is.Zero);
                if ((double)i / fps > .2) Assert.That(offset, Is.EqualTo(Vector3.zero));
            }
            Assert.That(maximum, Is.InRange(.5f, 2.001f));
            pulse.GetSignal(.05f, out _, out var rotation); Assert.That(rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void InvalidOrDisabledInputCannotProduceAnImpulse()
        {
            var pulse = new CombatCameraPulse();
            Assert.That(pulse.Trigger(0, Vector2.right, 0, .14f, .4f), Is.False);
            Assert.That(pulse.Trigger(0, Vector2.zero, 2, .14f, .4f), Is.False);
            Assert.That(pulse.Trigger(double.NaN, Vector2.right, 2, .14f, .4f), Is.False);
            Assert.That(pulse.Trigger(0, Vector2.right, float.PositiveInfinity, .14f, .4f), Is.False);
            Assert.That(pulse.Trigger(0, Vector2.right, 2, 0, .4f), Is.False);
            Assert.That(pulse.Sample(0), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ClearCancelsPulseAndDropsOldScopeCooldown()
        {
            var pulse = new CombatCameraPulse(); pulse.Trigger(0, Vector2.left, 2, .14f, .4f);
            Assert.That(pulse.Sample(.03).x, Is.LessThan(0));
            pulse.Clear(); Assert.That(pulse.Sample(.03), Is.EqualTo(Vector3.zero));
            Assert.That(pulse.Trigger(.04, Vector2.right, 2, .14f, .4f), Is.True);
        }

        [TestCase(5.625f)] [TestCase(7.5f)]
        public void TranslationUsesWholeReferencePixelsAndNeverExceedsTwoPixels(float ortho)
        {
            float unit = 2 * ortho / 270;
            for (int angle = 0; angle < 360; angle++)
            {
                Vector3 input = Quaternion.Euler(0, 0, angle) * Vector3.right * 200;
                Vector3 output = CombatCameraImpulse.PixelOffset(input, ortho) / unit;
                Assert.That(output.magnitude, Is.LessThanOrEqualTo(2.001f));
                Assert.That(output.x, Is.EqualTo(Mathf.Round(output.x)).Within(.0001));
                Assert.That(output.y, Is.EqualTo(Mathf.Round(output.y)).Within(.0001));
                Assert.That(output.z, Is.Zero);
            }
        }

        [Test]
        public void OffscreenAndBehindCameraImpactsAreRejected()
        {
            var go = new GameObject("Visibility"); var camera = go.AddComponent<Camera>();
            try
            {
                camera.orthographic = true; camera.orthographicSize = 5; camera.aspect = 16f / 9; go.transform.position = new Vector3(0, 0, -10);
                Assert.That(CombatCameraImpulse.IsVisible(camera, Vector3.zero), Is.True);
                Assert.That(CombatCameraImpulse.IsVisible(camera, new Vector3(50, 0, 0)), Is.False);
                Assert.That(CombatCameraImpulse.IsVisible(camera, new Vector3(0, 0, -11)), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void StrengthPersistsClampsAndZeroReallyDisablesWithoutChangingOtherSettings()
        {
            string key = GameplayPresentationSettings.CameraImpulseStrengthKey;
            bool existed = PlayerPrefs.HasKey(key); float previous = PlayerPrefs.GetFloat(key);
            float hud = GameplayPresentationSettings.HudScale;
            try
            {
                GameplayPresentationSettings.SetCameraImpulseStrength(0); Assert.That(GameplayPresentationSettings.CameraImpulseStrength, Is.Zero);
                GameplayPresentationSettings.SetCameraImpulseStrength(.75f); Assert.That(PlayerPrefs.GetFloat(key), Is.EqualTo(.75f));
                GameplayPresentationSettings.SetCameraImpulseStrength(50); Assert.That(GameplayPresentationSettings.CameraImpulseStrength, Is.EqualTo(1));
                GameplayPresentationSettings.SetCameraImpulseStrength(-1); Assert.That(GameplayPresentationSettings.CameraImpulseStrength, Is.Zero);
                Assert.That(GameplayPresentationSettings.HudScale, Is.EqualTo(hud));
            }
            finally { if (existed) PlayerPrefs.SetFloat(key, previous); else PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }

        [Test]
        public void SettingsHasOffToFullControlInExistingScrollableLayout()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/SettingsUI/SettingUXML.uxml");
            var root = asset.CloneTree(); var slider = root.Q<SliderInt>("CameraImpulseSlider");
            Assert.That(slider, Is.Not.Null); Assert.That(slider.lowValue, Is.Zero); Assert.That(slider.highValue, Is.EqualTo(100));
            Assert.That(root.Q<ScrollView>("BindingScrollView").Contains(slider), Is.True);
        }
    }
}
