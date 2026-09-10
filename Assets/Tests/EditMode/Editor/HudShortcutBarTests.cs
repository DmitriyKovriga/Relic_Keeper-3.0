using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class HudShortcutBarTests
    {
        [Test]
        public void EvaluateBreathAlpha_StaysInSoftYellowRange()
        {
            const float period = 1.6f;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i <= 32; i++)
            {
                float t = period * (i / 32f);
                float alpha = HudShortcutBar.EvaluateBreathAlpha(t, period);
                min = Mathf.Min(min, alpha);
                max = Mathf.Max(max, alpha);
            }

            Assert.That(min, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(max, Is.EqualTo(0.62f).Within(0.001f));
        }

        [Test]
        public void EvaluateBreathAlpha_PeaksAtQuarterPeriod()
        {
            const float period = 1.6f;
            float trough = HudShortcutBar.EvaluateBreathAlpha(period * 0.75f, period);
            float peak = HudShortcutBar.EvaluateBreathAlpha(period * 0.25f, period);
            float mid = HudShortcutBar.EvaluateBreathAlpha(0f, period);

            Assert.That(trough, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(peak, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(mid, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(peak, Is.GreaterThan(mid));
            Assert.That(mid, Is.GreaterThan(trough));
        }

        [Test]
        public void CreateBarElement_UsesIntegerPixelLayout()
        {
            VisualElement bar = HudShortcutBar.CreateBarElement(default, out VisualElement breath);

            Assert.That(bar.style.left.value.value, Is.EqualTo(HudShortcutBar.ScreenInsetPixels));
            Assert.That(bar.style.top.value.value, Is.EqualTo(HudShortcutBar.ScreenInsetPixels));
            Assert.That(bar.style.width.value.value, Is.EqualTo(HudShortcutBar.BarWidthPixels));
            Assert.That(bar.style.height.value.value, Is.EqualTo(HudShortcutBar.BarHeightPixels));
            Assert.IsNotNull(bar.Q("Inventory"));
            Assert.IsNotNull(bar.Q("Craft"));
            Assert.IsNotNull(bar.Q("Passives"));
            Assert.IsNotNull(bar.Q("Stats"));
            Assert.IsNotNull(bar.Q("Pause"));
            Assert.IsNotNull(breath);
            Assert.AreEqual(PickingMode.Position, bar.pickingMode);
            Assert.AreEqual(PickingMode.Position, bar.Q("Inventory").pickingMode);
        }
    }
}
