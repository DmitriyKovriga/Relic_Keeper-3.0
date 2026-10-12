using System.Linq;
using NUnit.Framework;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class GroundShakeVisualTests
    {
        private const string Root = "Skills/2HWeapon/Hammer/Adventurer/GroundShake/";

        [Test]
        public void GroundShakeRecipe_HasNoVisibleGroundClearance()
        {
            var recipe = Resources.Load<SkillRecipeSO>(Root + "Recipe_Ground_Shake_Skill");
            Assert.That(recipe, Is.Not.Null);
            var step = recipe.Steps.Single(entry => entry.GetObject<GameObject>("ProjectilePrefab") != null);
            Assert.That(step.GetFloat("GroundYOffset", 0.06f), Is.Zero);
        }

        [Test]
        public void TallerDebrisFrames_PreserveOriginalProjectileRadius()
        {
            var frames = Resources.LoadAll<Sprite>(Root + "GroundShakeRefined-Sheet");
            Assert.That(frames.Length, Is.EqualTo(8));
            float originalRadius = SkillProjectile.CalculateLocalRadius(31f / 24, 16f / 24, 1, 1, 1, 1);
            foreach (var frame in frames)
            {
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(24));
                float radius = SkillProjectile.CalculateLocalRadius(frame.bounds.size.x, frame.bounds.size.y, 1, 1, 1, 1);
                Assert.That(radius, Is.EqualTo(originalRadius).Within(0.0001f), frame.name);
            }
        }
    }
}
