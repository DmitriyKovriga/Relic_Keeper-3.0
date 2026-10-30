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
        public void NoActiveCharacterSaveCanResolveMissingClassWithoutResettingTheParty()
        {
            var database = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
            try
            {
                var save = new GameSaveData
                {
                    SaveVersion = GameSaveManager.CurrentSaveVersion,
                    ActiveCharacterID = "",
                    HighestDungeonStage = 17
                };
                save.Characters.Add(new CharacterSaveData("mage", "hostel-character"));
                _manager.LoadFromSave(save, database, null);
                Assert.That(_manager.HasActiveCharacter, Is.False);
                string classId = _manager.GetCharacterData(_manager.ActiveCharacterID)?.CharacterClassID;
                Assert.That(database.GetCharacterByID(classId), Is.Null,
                    "GameSaveManager performs this lookup before opening required recruitment.");
                Assert.That(_manager.HostelCharacterIDs, Does.Contain("hostel-character"));
                Assert.That(_manager.HighestDungeonStage, Is.EqualTo(17));
            }
            finally { Object.DestroyImmediate(database); }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("unknown-character")]
        public void DatabaseMissingIdIsHandledAsMissingCharacter(string id)
        {
            var database = ScriptableObject.CreateInstance<CharacterDatabaseSO>();
            try
            {
                Assert.That(database.GetCharacterByID(id), Is.Null);
                Assert.That(database.ContainsCharacter(id), Is.False);
            }
            finally { Object.DestroyImmediate(database); }
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

        [Test]
        public void LoadVersionFourSave_WithNoActiveCharacter_PreservesRequiredSelectionState()
        {
            var hostelCharacter = new CharacterSaveData("mage", "hostel-character");
            var save = new GameSaveData
            {
                SaveVersion = GameSaveManager.CurrentSaveVersion,
                ActiveCharacterID = null
            };
            save.Characters.Add(hostelCharacter);

            _manager.LoadFromSave(save, null, null);

            Assert.That(_manager.HasActiveCharacter, Is.False);
            Assert.That(_manager.HostelCharacterIDs, Does.Contain("hostel-character"));
        }

        [Test]
        public void RemoveActiveCharacterAfterDeath_RemovesOnlyDeadCharacterAndWritesNoActiveId()
        {
            var deadCharacter = new CharacterSaveData("warrior", "dead-character");
            var hostelCharacter = new CharacterSaveData("mage", "hostel-character");
            var source = new GameSaveData
            {
                SaveVersion = GameSaveManager.CurrentSaveVersion,
                ActiveCharacterID = deadCharacter.CharacterInstanceID
            };
            source.Characters.Add(deadCharacter);
            source.Characters.Add(hostelCharacter);
            _manager.LoadFromSave(source, null, null);

            Assert.That(_manager.RemoveActiveCharacterAfterDeath(), Is.True);

            var written = new GameSaveData();
            _manager.WriteToSave(written);
            Assert.That(written.ActiveCharacterID, Is.Null);
            Assert.That(written.Characters, Has.Count.EqualTo(1));
            Assert.That(written.Characters[0].CharacterInstanceID, Is.EqualTo("hostel-character"));
        }
    }
}
