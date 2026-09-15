using NUnit.Framework;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class CraftingOrbApplyModeTests
    {
        [Test]
        public void ShiftClick_KeepsSelectionWhileOrbsRemain()
        {
            Assert.That(CraftingOrbApplyMode.ShouldKeepSelection(true, 3), Is.True);
        }

        [Test]
        public void ShiftClick_ClearsSelectionWhenOrbsRunOut()
        {
            Assert.That(CraftingOrbApplyMode.ShouldKeepSelection(true, 0), Is.False);
        }

        [Test]
        public void ClickWithoutShift_AlwaysClearsSelection()
        {
            Assert.That(CraftingOrbApplyMode.ShouldKeepSelection(false, 5), Is.False);
        }

        [Test]
        public void ApplyingClass_StaysOnlyOnSelectedRelic()
        {
            var first = new VisualElement();
            var second = new VisualElement();
            first.AddToClassList(CraftingOrbApplyMode.ApplyingClassName);
            second.AddToClassList(CraftingOrbApplyMode.ApplyingClassName);

            CraftingOrbApplyMode.SetApplying(first, first, true);
            CraftingOrbApplyMode.SetApplying(second, first, true);

            Assert.That(first.ClassListContains(CraftingOrbApplyMode.ApplyingClassName), Is.True);
            Assert.That(second.ClassListContains(CraftingOrbApplyMode.ApplyingClassName), Is.False);
        }
    }
}
