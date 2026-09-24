using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class HitStopTests
    {
        [TestCase(-10, 1)]
        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(3, 3)]
        [TestCase(5, 5)]
        [TestCase(20, 5)]
        public void FrameCount_IsClampedToRecipeRange(int input, int expected)
        {
            Assert.That(HitStopService.ClampFrames(input), Is.EqualTo(expected));
        }

        [Test]
        public void SharedCastGate_TriggersOnlyOnceAcrossProjectileClones()
        {
            var gate = new SkillHitStopGate(3);
            var launch = new SkillProjectileLaunchData { HitStopGate = gate };
            SkillProjectileLaunchData cloneA = launch.Clone();
            SkillProjectileLaunchData cloneB = launch.Clone();

            Assert.That(cloneA.HitStopGate, Is.SameAs(cloneB.HitStopGate));
            Assert.That(cloneA.HitStopGate.TryTrigger(), Is.True);
            Assert.That(cloneB.HitStopGate.TryTrigger(), Is.False);
            Assert.That(gate.IsConsumed, Is.True);
        }

        [Test]
        public void ExistingRecipes_HaveValidConfiguredHitStopFrames()
        {
            SkillRecipeSO[] recipes = Resources.LoadAll<SkillRecipeSO>("Skills");
            Assert.That(recipes, Is.Not.Empty);
            foreach (SkillRecipeSO recipe in recipes)
                Assert.That(recipe.HitStopFrames, Is.InRange(1, 5), recipe.name);

            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Axe/LeftButton/Recipe_Cleave").HitStopFrames, Is.EqualTo(3));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/1HWeapon/Dagger/VenomStrike/Recipe_Venom_Strike_Skill").HitStopFrames, Is.EqualTo(3));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Staff/Fire/Adventurers/Recipe_Fire_Ball_Skill").HitStopFrames, Is.EqualTo(1));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Staff/Cold/Recipe_Icecle_Shot_Skill").HitStopFrames, Is.EqualTo(1));
        }

        [Test]
        public void FrameRateLimiter_UsesSixtyFpsAndDisablesVSyncOverride()
        {
            int previousTarget = Application.targetFrameRate;
            int previousVSync = QualitySettings.vSyncCount;
            try
            {
                FrameRateLimiter.Apply();
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0));
            }
            finally
            {
                Application.targetFrameRate = previousTarget;
                QualitySettings.vSyncCount = previousVSync;
            }
        }
    }
}
