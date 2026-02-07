using System;
using System.Collections.Generic;

namespace Scripts.Saving
{
    [Serializable]
    public class GameSaveData
    {
        /// <summary>Версия формата сейва. При загрузке старых сейвов применяются миграции.</summary>
        public int SaveVersion;

        public string CharacterClassID;
        public float CurrentHealth;
        public float CurrentMana;

        public int CurrentLevel;
        public float CurrentXP;
        public float RequiredXP;
        public int SkillPoints;

        public InventorySaveData Inventory;

        public StashSaveData Stash;

        public List<string> AllocatedPassiveNodes = new List<string>();

        public GameSaveData() { }
    }
}