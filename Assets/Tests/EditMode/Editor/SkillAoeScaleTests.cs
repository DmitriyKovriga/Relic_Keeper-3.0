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

    public class SkillHitboxFitTests
    {
        [Test]
        public void FrontBox_KeepsFarEdgeAndCoversOwner()
        {
            Vector2 center = new Vector2(1.2f, 0f);
            Vector2 size = new Vector2(1.6f, 1f);
            float farEdge = center.x + size.x * 0.5f;

            SkillHitboxFit.PullTowardOwner(Vector2.zero, 1f, ref center, ref size);

            Assert.That(center.x + size.x * 0.5f, Is.EqualTo(farEdge).Within(0.0001f));
            Assert.That(center.x - size.x * 0.5f, Is.EqualTo(-SkillHitboxFit.OwnerOverlap).Within(0.0001f));
        }

        [Test]
        public void BoxAlreadyOverOwner_DoesNotChange()
        {
            Vector2 center = new Vector2(0.4f, 0f);
            Vector2 size = new Vector2(2f, 1f);
            Vector2 originalCenter = center;
            Vector2 originalSize = size;

            SkillHitboxFit.PullTowardOwner(Vector2.zero, 1f, ref center, ref size);

            Assert.That(center, Is.EqualTo(originalCenter));
            Assert.That(size, Is.EqualTo(originalSize));
        }

        [Test]
        public void FacingLeft_PullsTowardOwner()
        {
            Vector2 center = new Vector2(-1.2f, 0f);
            Vector2 size = new Vector2(1.6f, 1f);
            float farEdge = center.x - size.x * 0.5f;

            SkillHitboxFit.PullTowardOwner(Vector2.zero, -1f, ref center, ref size);

            Assert.That(center.x - size.x * 0.5f, Is.EqualTo(farEdge).Within(0.0001f));
            Assert.That(center.x + size.x * 0.5f, Is.EqualTo(SkillHitboxFit.OwnerOverlap).Within(0.0001f));
        }
    }
}
