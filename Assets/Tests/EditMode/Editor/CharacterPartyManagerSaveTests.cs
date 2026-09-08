using NUnit.Framework;
using Scripts.Saving;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class CharacterPartyManagerSaveTests
    {
        private GameObject _owner;
        private CharacterPartyManager _manager;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("CharacterPartyManagerSaveTests");
            _manager = _owner.AddComponent<CharacterPartyManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_owner != null)
                Object.DestroyImmediate(_owner);
        }

        [Test]
        public void NullCharacterId_IsHandledAsMissingCharacter()
        {
            Assert.That(_manager.HasCharacter(null), Is.False);
            Assert.That(_manager.GetCharacterData(null), Is.Null);
        }

        [Test]
        public void LoadFromSave_WithNoUsableCharacters_LeavesPartyEmptyWithoutThrowing()
        {
            var save = new GameSaveData
            {
                SaveVersion = GameSaveManager.CurrentSaveVersion,
                ActiveCharacterID = null
            };
            save.Characters.Add(new CharacterSaveData
            {
                CharacterInstanceID = "broken-character",
                CharacterClassID = null
            });

            Assert.DoesNotThrow(() => _manager.LoadFromSave(save, null, null));
            Assert.That(_manager.ActiveCharacterID, Is.Null);
            Assert.That(_manager.GetCharacterData(_manager.ActiveCharacterID), Is.Null);
        }

        [Test]
        public void LoadFromSave_WhenRequestedCharacterIsInvalid_SelectsImportedCharacter()
        {
            var validCharacter = new CharacterSaveData("warrior", "valid-character");
            var save = new GameSaveData
            {
                SaveVersion = GameSaveManager.CurrentSaveVersion,
                ActiveCharacterID = "broken-character"
            };
            save.Characters.Add(new CharacterSaveData
            {
                CharacterInstanceID = "broken-character",
                CharacterClassID = null
            });
            save.Characters.Add(validCharacter);

            _manager.LoadFromSave(save, null, null);

            Assert.That(_manager.ActiveCharacterID, Is.EqualTo("valid-character"));
            Assert.That(_manager.GetCharacterData("valid-character"), Is.SameAs(validCharacter));
        }
    }
}
