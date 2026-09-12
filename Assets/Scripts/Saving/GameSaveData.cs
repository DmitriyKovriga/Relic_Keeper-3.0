using System;
using System.Collections.Generic;
using Scripts.Dungeon;

namespace Scripts.Saving
{
    [Serializable]
    public class GameSaveData
    {
        /// <summary>Версия формата сейва. При загрузке старых сейвов применяются миграции.</summary>
        public int SaveVersion;

        /// <summary>ID активного персонажа (v2+).</summary>
        public string ActiveCharacterID;

        /// <summary>Данные по каждому персонажу в партии (v2+). Инвентарь и статы привязаны к персонажу.</summary>
        public List<CharacterSaveData> Characters = new List<CharacterSaveData>();

        /// <summary>Склад — общий для всех персонажей.</summary>
        public StashSaveData Stash;

        /// <summary>Золото — общее для всех персонажей.</summary>
        public int Gold;

        /// <summary>Ассортимент торговца и выкуп — общие для всех персонажей.</summary>
        public MarketSaveData Market;

        // --- Legacy (v1): оставляем для миграции ---
        public string CharacterClassID;
        public float CurrentHealth;
        public float CurrentMana;
        public int CurrentLevel;
        public float CurrentXP;
        public float RequiredXP;
        public int SkillPoints;
        public InventorySaveData Inventory;
        public List<string> AllocatedPassiveNodes = new List<string>();
        public List<DungeonUnlockRecord> DungeonUnlocks = new List<DungeonUnlockRecord>();

        public GameSaveData() { }
    }
}