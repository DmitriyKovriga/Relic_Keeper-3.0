using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Enemies;
using Scripts.Dungeon;
using Scripts.GameplayEvents;
using Scripts.Saving;
using Scripts.Stats;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace RelicKeeper.Tests.EditMode
{
    public class DeathProgressTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private CharacterPartyManager _party;
        private PlayerStats _player;
        private GameSaveData _save;

        [SetUp]
        public void SetUp()
        {
            _player = Keep("Player").AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_player, null);
            _player.GetStat(StatType.MaxHealth).BaseValue = 100;
            _player.Health.RestoreFull();
            _party = Keep("Party").AddComponent<CharacterPartyManager>();
            typeof(CharacterPartyManager).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_party, null);
            typeof(CharacterPartyManager).GetField("_playerStats", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_party, _player);
            _save = new GameSaveData { SaveVersion = 9, ActiveCharacterID = "first" };
            _save.Characters.Add(new CharacterSaveData("warrior", "first"));
            _save.Characters.Add(new CharacterSaveData("warrior", "second"));
            _party.LoadFromSave(_save, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_party != null)
                typeof(CharacterPartyManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_party, null);
            for (int i = _objects.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void LifetimeKills_AreCountedOnceAndBelongToCharacterInstance()
        {
            var enemy = Enemy();
            enemy.ApplyPureDamage(1000, _player);
            enemy.ApplyPureDamage(1000, _player);
            Assert.That(_party.GetCharacterData("first").MonstersKilled, Is.EqualTo(1));

            var written = new GameSaveData { SaveVersion = 9 };
            _party.WriteToSave(written);
            var loaded = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(written));
            loaded.ActiveCharacterID = "second";
            _party.LoadFromSave(loaded, null, null);
            Enemy().ApplyPureDamage(1000, _player);
            Assert.That(_party.GetCharacterData("first").MonstersKilled, Is.EqualTo(1));
            Assert.That(_party.GetCharacterData("second").MonstersKilled, Is.EqualTo(1));
        }

        [Test]
        public void NonPlayerKillsAndNonlethalDamage_DoNotCount()
        {
            Enemy().ApplyPureDamage(0.1f, _player);
            Enemy().ApplyPureDamage(1000, Keep("Other damage source"));
            Assert.That(_party.GetCharacterData("first").MonstersKilled, Is.Zero);
        }

        [Test]
        public void TrainingDummyDeaths_DoNotCount()
        {
            var enemy = Enemy();
            enemy.gameObject.AddComponent<DummyEvolution>();
            enemy.ApplyPureDamage(1000, _player);
            Assert.That(_party.GetCharacterData("first").MonstersKilled, Is.Zero);
        }

        [Test]
        public void DeathPreservesAccountRecordAndOtherCharactersKills()
        {
            _party.RecordDungeonStageReached(12);
            _party.RecordDungeonStageReached(3);
            _party.GetCharacterData("second").MonstersKilled = 6;
            Enemy().ApplyPureDamage(1000, _player);
            int deathKills = _party.GetCharacterData("first").MonstersKilled;
            _party.RemoveActiveCharacterAfterDeath();
            _party.RecordDungeonStageReached(99);

            var written = new GameSaveData();
            _party.WriteToSave(written);
            Assert.That(deathKills, Is.EqualTo(1));
            Assert.That(written.HighestDungeonStage, Is.EqualTo(12));
            Assert.That(written.Characters.Count, Is.EqualTo(1));
            Assert.That(written.Characters[0].MonstersKilled, Is.EqualTo(6));
            string newCharacter = _party.AddCharacterToParty("warrior");
            Assert.That(_party.GetCharacterData(newCharacter).MonstersKilled, Is.Zero);
        }

        [Test]
        public void AccountRecord_RoundTripsWithoutActiveCharacterAndCanBeResetForNewGame()
        {
            _party.RecordDungeonStageReached(23);
            _party.RemoveActiveCharacterAfterDeath();
            var written = new GameSaveData { SaveVersion = 9 };
            _party.WriteToSave(written);
            var loaded = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(written));
            _party.LoadFromSave(loaded, null, null);
            Assert.That(_party.HighestDungeonStage, Is.EqualTo(23));
            Assert.That(_party.HasActiveCharacter, Is.False);
            _party.ResetDungeonStageRecord();
            Assert.That(_party.HighestDungeonStage, Is.Zero);
        }

        [Test]
        public void OldSaveMigration_RetainsBestKnownProgressAndDefaultsKillsToZero()
        {
            var old = JsonUtility.FromJson<GameSaveData>("{\"SaveVersion\":8,\"Characters\":[{\"CharacterClassID\":\"warrior\"}],\"DungeonUnlocks\":[{\"DungeonId\":\"one\",\"HighestDisplayedRoom\":21},{\"DungeonId\":\"two\",\"HighestDisplayedRoom\":4}]}");
            var manager = Keep("Save manager").AddComponent<GameSaveManager>();
            typeof(GameSaveManager).GetMethod("MigrateSaveData", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(manager, new object[] { old });
            Assert.That(old.SaveVersion, Is.EqualTo(GameSaveManager.CurrentSaveVersion));
            Assert.That(old.HighestDungeonStage, Is.EqualTo(21));
            Assert.That(old.Characters[0].MonstersKilled, Is.Zero);
        }

        [TestCase("en", "Highest dungeon stage", "Monsters killed in this life")]
        [TestCase("ru", "Рекорд этапов подземелья", "Убито монстров за жизнь")]
        public void DeathSummary_HasEnglishAndRussianTableEntries(string locale, string stage, string kills)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(
                "Assets/Localization/LocalizationTables/MenuLabels_" + locale + ".asset");
            Assert.That(table.GetEntry(DeathScreenOverlay.HighestStageKey)?.LocalizedValue, Is.EqualTo(stage));
            Assert.That(table.GetEntry(DeathScreenOverlay.MonstersKilledKey)?.LocalizedValue, Is.EqualTo(kills));
        }

        private EnemyHealth Enemy()
        {
            var owner = Keep("Enemy");
            owner.AddComponent<EnemyStats>().Initialize(null, 1);
            var enemy = owner.AddComponent<EnemyHealth>();
            enemy.DestroyOnDeath = false;
            enemy.Initialize();
            return enemy;
        }

        private GameObject Keep(string name)
        {
            var owner = new GameObject(name);
            _objects.Add(owner);
            return owner;
        }
    }
}
