using NUnit.Framework;
using System;
using System.Reflection;
using Scripts.Skills;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using UnityEngine;
using Scripts.Visuals;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class HitStopTests
    {
        [TestCase(-10, 0)]
        [TestCase(0, 0)]
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
        public void ZeroFrameRecipeDisablesHitStopForAllHitsOfThatCast()
        {
            var gate = new SkillHitStopGate(0);
            Assert.That(gate.TryTrigger(), Is.False);
            Assert.That(gate.TryTrigger(), Is.False);
        }

        [TestCase(0, 0f)] [TestCase(1, 1f / 60)] [TestCase(3, .05f)]
        [TestCase(4, 4f / 60)] [TestCase(5, 5f / 60)]
        public void LegacyRecipeFramesHaveAStableReferenceDuration(int frames, float seconds)
        {
            Assert.That(HitStopService.DurationSeconds(frames), Is.EqualTo(seconds).Within(.000001f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void DisplayRateChangesOnlyTheFinalFrameQuantization(int fps)
        {
            var budget = new HitStopBudget();
            Assert.That(budget.TrySchedule(0, 3, .25f, out double end), Is.True);
            int samples = 0; while (samples / (double)fps < end) samples++;
            double observed = samples / (double)fps;
            Assert.That(observed, Is.InRange(.05 - .000001, .05 + 1.0 / fps + .000001));
        }

        [Test]
        public void IndependentCastsCannotExtendTheStopOrAvoidItsSharedQuietInterval()
        {
            var budget = new HitStopBudget();
            Assert.That(budget.TrySchedule(0, 3, .25f, out double end), Is.True);
            for (int target = 0; target < 1000; target++) Assert.That(budget.TrySchedule(.01, 5, .25f, out _), Is.False);
            Assert.That(end, Is.EqualTo(.05).Within(.000001));
            Assert.That(budget.TrySchedule(.1, 5, .25f, out _), Is.False, "The visible stop ended, but the shared interval remains.");
            Assert.That(budget.TrySchedule(.25, 3, .25f, out _), Is.True);
        }

        [Test]
        public void BurstBudgetIsBoundedAndDoesNotAllocatePerRequest()
        {
            var budget = new HitStopBudget(); budget.TrySchedule(0, 3, .25f, out _); budget.Clear();
            long before = GC.GetAllocatedBytesForCurrentThread(); int accepted = 0;
            for (int i = 0; i < 10000; i++) if (budget.TrySchedule(i / 10000.0, 3, .25f, out _)) accepted++;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(accepted, Is.EqualTo(4)); Assert.That(allocated, Is.Zero);
            budget.Clear(); Assert.That(budget.TrySchedule(0, 5, 0, out double end), Is.True);
            Assert.That(budget.TrySchedule(end + .02, 5, 0, out _), Is.False, "Unsafe Inspector intervals still leave a quiet gap.");
        }

        private static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);

        private static void WithRunner(Action<Component, CombatPoseProfile> test)
        {
            float previous = Time.timeScale; Component runner = null;
            var profile = Object.Instantiate(CombatPoseProfile.LoadDefault());
            try
            {
                GamePauseService.ResumeAll(); Time.timeScale = 1;
                runner = (Component)typeof(HitStopService).GetMethod("EnsureRunner", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                test(runner, profile);
            }
            finally
            {
                GamePauseService.ResumeAll(); if (runner != null) Object.DestroyImmediate(runner.gameObject);
                Object.DestroyImmediate(profile); Time.timeScale = previous;
            }
        }

        [Test]
        public void MenuPauseDuringHitStopRestoresSlowMotionOnlyAfterEveryPauseOwnerCloses()
        {
            WithRunner((runner, profile) =>
            {
                Time.timeScale = .35f; Assert.That(Invoke(runner, "Request", 3, 0d, profile), Is.True);
                var menu = GamePauseService.Acquire(GamePauseReason.PauseMenu);
                var inventory = GamePauseService.Acquire(GamePauseReason.Inventory);
                Assert.That(HitStopService.IsActive, Is.False);
                Assert.That(Invoke(runner, "Request", 5, .1d, profile), Is.False);
                menu.Dispose(); Invoke(runner, "Sample", 2d);
                Assert.That(Time.timeScale, Is.Zero); inventory.Dispose();
                Assert.That(Time.timeScale, Is.EqualTo(.35f).Within(.00001f));
            });
        }

        [Test]
        public void AnActiveStopNeverExtendsAndRestoresItsOwnPreviousScale()
        {
            WithRunner((runner, profile) =>
            {
                Time.timeScale = .5f; Assert.That(Invoke(runner, "Request", 3, 0d, profile), Is.True);
                for (int i = 0; i < 100; i++) Assert.That(Invoke(runner, "Request", 5, .04d, profile), Is.False);
                Invoke(runner, "Sample", .049d); Assert.That(Time.timeScale, Is.Zero);
                Invoke(runner, "Sample", .051d); Assert.That(Time.timeScale, Is.EqualTo(.5f));
            });
        }

        [Test]
        public void ANewClockOwnerIsNeverOverwrittenByAnExpiringHitStop()
        {
            WithRunner((runner, profile) =>
            {
                Invoke(runner, "Request", 3, 0d, profile); Time.timeScale = .2f;
                Invoke(runner, "Sample", 1d); Assert.That(Time.timeScale, Is.EqualTo(.2f));
                Time.timeScale = 0; Assert.That(Invoke(runner, "Request", 3, 2d, profile), Is.False);
                Invoke(runner, "Sample", 3d); Assert.That(Time.timeScale, Is.Zero);
            });
        }

        [TestCase(false)] [TestCase(true)]
        public void DisableAndDestroyCleanupCallbacksReleaseOwnedTime(bool destroy)
        {
            WithRunner((runner, profile) =>
            {
                Time.timeScale = .6f; Invoke(runner, "Request", 5, 0d, profile);
                // EditMode does not drive lifecycle for this runtime-only behaviour.
                Invoke(runner, destroy ? "OnDestroy" : "OnDisable");
                if (destroy) Object.DestroyImmediate(runner.gameObject); else ((Behaviour)runner).enabled = false;
                Assert.That(Time.timeScale, Is.EqualTo(.6f)); Assert.That(HitStopService.IsActive, Is.False);
                if (!destroy)
                {
                    Assert.That(Invoke(runner, "Request", 3, 1d, profile), Is.False, "A disabled runner has no update to end a new freeze.");
                    Assert.That(Time.timeScale, Is.EqualTo(.6f));
                }
            });
        }

        [Test]
        public void ProfileSwitchCanDisableAndReleaseHitStopWithoutAnotherTimer()
        {
            WithRunner((runner, profile) =>
            {
                profile.EnableHitStop = false;
                Assert.That(Invoke(runner, "Request", 3, 0d, profile), Is.False); Assert.That(Time.timeScale, Is.EqualTo(1));
                profile.EnableHitStop = true; Assert.That(Invoke(runner, "Request", 3, 0d, profile), Is.True);
                profile.EnableHitStop = false; Invoke(runner, "Sample", .001d);
                Assert.That(Time.timeScale, Is.EqualTo(1));
            });
        }

        [Test]
        public void ExistingRecipes_HaveValidConfiguredHitStopFrames()
        {
            SkillRecipeSO[] recipes = Resources.LoadAll<SkillRecipeSO>("Skills");
            Assert.That(recipes, Is.Not.Empty);
            foreach (SkillRecipeSO recipe in recipes)
                Assert.That(recipe.HitStopFrames, Is.InRange(0, 5), recipe.name);

            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Axe/LeftButton/Recipe_Cleave").HitStopFrames, Is.EqualTo(3));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/1HWeapon/Dagger/VenomStrike/Recipe_Venom_Strike_Skill").HitStopFrames, Is.EqualTo(5));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Staff/Fire/Adventurers/Recipe_Fire_Ball_Skill").HitStopFrames, Is.EqualTo(1));
            Assert.That(Resources.Load<SkillRecipeSO>("Skills/2HWeapon/Staff/Cold/Recipe_Icecle_Shot_Skill").HitStopFrames, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        public void FrameRateLimiter_EnablesVSyncWithSixtyFpsFallback(int initialVSync)
        {
            int previousTarget = Application.targetFrameRate;
            int previousVSync = QualitySettings.vSyncCount;
            try
            {
                QualitySettings.vSyncCount = initialVSync;
                FrameRateLimiter.Apply();
                Assert.That(Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            }
            finally
            {
                Application.targetFrameRate = previousTarget;
                QualitySettings.vSyncCount = previousVSync;
            }
        }
    }
}
