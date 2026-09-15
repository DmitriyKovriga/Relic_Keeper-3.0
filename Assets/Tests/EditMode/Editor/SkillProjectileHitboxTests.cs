using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Skills.Projectiles;
using UnityEngine;

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

            Assert.That(radius, Is.EqualTo(SkillProjectile.MinPlayableWorldRadius).Within(0.0001f));
        }

        [Test]
        public void TinyVfxSprite_UsesMinimumPlayableRadius()
        {
            float radius = SkillProjectile.CalculateLocalRadius(0.333f, 0.292f, 1f, 0.3f, 1f, 1f);

            Assert.That(radius, Is.EqualTo(SkillProjectile.MinPlayableWorldRadius).Within(0.0001f));
            Assert.That(radius, Is.GreaterThan(0.1f));
        }

        [Test]
        public void KnightPhysicsBox_KeepsTheOriginalStandingHeight()
        {
            EnemyPhysicsFit.CalculatePhysicsBox(new Vector2(2f, 2f), out Vector2 size, out _);

            Assert.That(size.y, Is.EqualTo(1.25f).Within(0.0001f));
        }

        [Test]
        public void KnightHurtbox_GrowsUpFromTheSameFeet()
        {
            EnemyPhysicsFit.CalculatePhysicsBox(new Vector2(2f, 2f), out Vector2 physicsSize, out Vector2 physicsOffset);
            EnemyPhysicsFit.CalculateHurtbox(new Vector2(2f, 2f), out Vector2 hurtSize, out Vector2 hurtOffset);

            float physicsBottom = physicsOffset.y - physicsSize.y * 0.5f;
            float hurtBottom = hurtOffset.y - hurtSize.y * 0.5f;
            Assert.That(hurtSize.y, Is.GreaterThan(physicsSize.y));
            Assert.That(hurtBottom, Is.EqualTo(physicsBottom).Within(0.0001f));
        }

        [Test]
        public void KnightHurtbox_IsReachedByFireballFromOneTileAbove()
        {
            float knightTop = EnemyPhysicsFit.HurtboxWorldTopAfterGroundSnap(new Vector2(2f, 2f));
            const float playerFeetY = 1f;
            const float playerColliderHalfHeight = 0.46f;
            const float fireballOffsetY = 0.25f;
            float fireballY = playerFeetY + playerColliderHalfHeight + fireballOffsetY;
            float fireballRadius = SkillProjectile.MinPlayableWorldRadius;

            Assert.That(fireballY - fireballRadius, Is.LessThan(knightTop));
        }
    }
}
