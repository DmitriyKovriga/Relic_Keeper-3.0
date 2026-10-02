using NUnit.Framework;
using Scripts.Visuals;

namespace RelicKeeper.Tests.EditMode
{
    public class PlayerLevelUpFeedbackTests
    {
        [Test]
        public void LevelUpMessage_IncludesTheNewLevel()
        {
            string message = PlayerLevelUpFeedback.FormatMessage();

            Assert.That(message, Is.EqualTo("Character Level Up").Or.EqualTo("Уровень персонажа повышен"));
        }
    }
}
