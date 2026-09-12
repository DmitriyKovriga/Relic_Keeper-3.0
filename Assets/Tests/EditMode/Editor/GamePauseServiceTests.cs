using NUnit.Framework;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class GamePauseServiceTests
    {
        [SetUp]
        public void SetUp()
        {
            GamePauseService.ResumeAll();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            GamePauseService.ResumeAll();
            Time.timeScale = 1f;
        }

        [Test]
        public void ReleasingOneOfSeveralReasons_KeepsGamePaused()
        {
            var menuPause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
            var inventoryPause = GamePauseService.Acquire(GamePauseReason.Inventory);

            menuPause.Dispose();

            Assert.That(GamePauseService.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            inventoryPause.Dispose();

            Assert.That(GamePauseService.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void LastReason_RestoresPreviousTimeScale()
        {
            Time.timeScale = 0.35f;
            var pause = GamePauseService.Acquire(GamePauseReason.Inventory);

            Assert.That(Time.timeScale, Is.Zero);

            pause.Dispose();

            Assert.That(Time.timeScale, Is.EqualTo(0.35f).Within(0.001f));
        }

        [Test]
        public void ResumeAll_InvalidatesOldHandles()
        {
            var oldPause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
            GamePauseService.ResumeAll();
            var currentPause = GamePauseService.Acquire(GamePauseReason.Inventory);

            oldPause.Dispose();

            Assert.That(GamePauseService.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            currentPause.Dispose();
        }
    }
}
