using NUnit.Framework;
using Scripts.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace RelicKeeper.Tests.EditMode
{
    public class HudSkillTooltipTests
    {
        [Test]
        public void HudSkillTooltipSitsAboveTheSlotAndStaysOnScreen()
        {
            Vector2 pos = ItemTooltipController.CalculateHudSkillTooltipPosition(
                new Vector2(180f, 220f),
                new Vector2(214f, 255f),
                tooltipWidth: 150f,
                tooltipHeight: 70f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(122f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(148f).Within(0.01f));
            Assert.That(pos.x, Is.GreaterThanOrEqualTo(2f));
            Assert.That(pos.x + 150f, Is.LessThanOrEqualTo(478f));
        }

        [Test]
        public void HudSkillTooltipClampsWhenSlotIsNearTheLeftEdge()
        {
            Vector2 pos = ItemTooltipController.CalculateHudSkillTooltipPosition(
                new Vector2(0f, 220f),
                new Vector2(34f, 255f),
                tooltipWidth: 150f,
                tooltipHeight: 70f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(148f).Within(0.01f));
        }

        [Test]
        public void HudSkillTooltipSitsBelowWhenTheSlotIsAtTheTop()
        {
            Vector2 pos = ItemTooltipController.CalculateHudSkillTooltipPosition(
                new Vector2(180f, 8f),
                new Vector2(214f, 42f),
                tooltipWidth: 150f,
                tooltipHeight: 70f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(122f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(44f).Within(0.01f));
            Assert.That(pos.y, Is.GreaterThan(42f));
        }

        [Test]
        public void LinkedTextTooltipSitsAboveTheWordWhenThereIsRoom()
        {
            Vector2 pos = ItemTooltipController.CalculateLinkedTextTooltipPosition(
                textMin: new Vector2(200f, 80f),
                textMax: new Vector2(260f, 92f),
                tooltipWidth: 140f,
                tooltipHeight: 40f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(38f).Within(0.01f));
        }

        [Test]
        public void LinkedTextTooltipSitsBelowTheWordWhenTheTopDoesNotFit()
        {
            Vector2 pos = ItemTooltipController.CalculateLinkedTextTooltipPosition(
                textMin: new Vector2(200f, 8f),
                textMax: new Vector2(260f, 20f),
                tooltipWidth: 140f,
                tooltipHeight: 40f,
                screenWidth: 480f,
                screenHeight: 270f,
                gap: 2f,
                padding: 2f);

            Assert.That(pos.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(pos.y, Is.EqualTo(22f).Within(0.01f));
        }

        [Test]
        public void ClusterUnionIncludesTheGapBetweenItemAndSkillTooltips()
        {
            var item = new Rect(10f, 10f, 100f, 50f);
            var skill = new Rect(115f, 10f, 100f, 50f);
            Rect union = ItemTooltipController.EncapsulateRects(item, skill);
            union = ItemTooltipController.InflateRect(union, 8f);

            Assert.That(ItemTooltipController.ContainsInclusive(union, new Vector2(112f, 30f)), Is.True);
            Assert.That(ItemTooltipController.ContainsInclusive(union, new Vector2(50f, 80f)), Is.False);
        }

        [Test]
        public void SkillSlotRemembersAssignedSkillForHover()
        {
            var slotObject = new GameObject("SkillSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.transform.SetParent(slotObject.transform, false);

            UISkillSlot slot = slotObject.AddComponent<UISkillSlot>();
            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.SkillName = "Quake";
            skill.ManaCost = 10f;

            try
            {
                slot.Setup(skill, "Q");
                Assert.That(slot.CurrentSkill, Is.SameAs(skill));

                slot.Clear();
                Assert.That(slot.CurrentSkill, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(slotObject);
            }
        }
    }
}
