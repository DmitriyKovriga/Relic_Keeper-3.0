using NUnit.Framework;
using Scripts.Skills;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillAoeScaleTests
    {
        [Test]
        public void NeutralAoe_KeepsUnitScale()
        {
            Assert.That(SkillAoeScale.FromAoe(1f), Is.EqualTo(new Vector2(1f, 1f)));
        }

        [Test]
        public void ExtraAoe_AppliesHalfOnVertical()
        {
            Vector2 scale = SkillAoeScale.FromAoe(1.5f);

            Assert.That(scale.x, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(scale.y, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void DoubleAoe_ScalesVerticalByHalfContribution()
        {
            Vector2 scale = SkillAoeScale.FromAoe(2f);

            Assert.That(scale.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(scale.y, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void AxisMultipliers_ApplyAfterAoe()
        {
            Vector2 scale = SkillAoeScale.FromAoe(2f, 0.5f, 2f);

            Assert.That(scale.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(scale.y, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void ScaleSize_UsesAnisotropicAoe()
        {
            Vector2 size = SkillAoeScale.ScaleSize(new Vector2(2f, 1f), 2f);

            Assert.That(size.x, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(size.y, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void MissingAxisMultiplier_DefaultsToOne()
        {
            Assert.That(SkillAoeScale.AxisMultiplierOrDefault(0f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(SkillAoeScale.AxisMultiplierOrDefault(0.75f), Is.EqualTo(0.75f).Within(0.0001f));
        }
    }
}
