using NUnit.Framework;
using Scripts.Skills.Projectiles;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillProjectileHitboxTests
    {
        [Test]
        public void SquareSprite_UsesHalfTheLongestSide()
        {
            float radius = SkillProjectile.CalculateLocalRadius(2f, 2f, 1f, 1f, 1f, 1f);

            Assert.That(radius, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WideSprite_DoesNotUseFullAabbAsRadius()
        {
            float radius = SkillProjectile.CalculateLocalRadius(2f, 0.4f, 1f, 1f, 1f, 1f);

            Assert.That(radius, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(radius, Is.LessThan(2f));
        }

        [Test]
        public void AxisScales_EnlargeTheMatchingSide()
        {
            float radius = SkillProjectile.CalculateLocalRadius(1f, 1f, 1f, 1f, 2f, 1f);

            Assert.That(radius, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void MissingSprite_FallsBackToDefaultRadius()
        {
            float radius = SkillProjectile.CalculateLocalRadius(0f, 0f, 1f, 1f, 1f, 1f);

            Assert.That(radius, Is.EqualTo(0.18f).Within(0.0001f));
        }
    }
}
