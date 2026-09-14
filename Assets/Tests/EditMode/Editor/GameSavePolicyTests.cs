using NUnit.Framework;

namespace RelicKeeper.Tests.EditMode
{
    public class GameSavePolicyTests
    {
        [TestCase(GameSaveReason.LegacyAutomatic)]
        [TestCase(GameSaveReason.LocationTransition)]
        [TestCase(GameSaveReason.CharacterDeath)]
        [TestCase(GameSaveReason.TavernPartyChanged)]
        [TestCase(GameSaveReason.StarterGearGranted)]
        public void AutomaticSave_IsBlockedWhenEditorAutosaveIsDisabled(GameSaveReason reason)
        {
            Assert.That(GameSavePolicy.IsAllowed(reason, autoSaveEnabled: false), Is.False);
        }

        [TestCase(GameSaveReason.LegacyAutomatic)]
        [TestCase(GameSaveReason.LocationTransition)]
        [TestCase(GameSaveReason.CharacterDeath)]
        [TestCase(GameSaveReason.TavernPartyChanged)]
        [TestCase(GameSaveReason.StarterGearGranted)]
        public void AutomaticSave_IsAllowedWhenAutosaveIsEnabled(GameSaveReason reason)
        {
            Assert.That(GameSavePolicy.IsAllowed(reason, autoSaveEnabled: true), Is.True);
        }

        [Test]
        public void ManualSave_IsAlwaysAllowed()
        {
            Assert.That(GameSavePolicy.IsAllowed(GameSaveReason.Manual, autoSaveEnabled: false), Is.True);
        }
    }
}
