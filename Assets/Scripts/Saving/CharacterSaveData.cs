using System;
using System.Collections.Generic;

namespace Scripts.Saving
{
    [Serializable]
    public class CharacterSaveData
    {
        public string CharacterClassID;
        public float CurrentHealth;
        public float CurrentMana;
        public int CurrentLevel;
        public float CurrentXP;
        public float RequiredXP;
        public int SkillPoints;
        public InventorySaveData Inventory = new InventorySaveData();
        public List<string> AllocatedPassiveNodes = new List<string>();

        public CharacterSaveData() { }

        public CharacterSaveData(string characterId)
        {
            CharacterClassID = characterId;
            CurrentHealth = 0;
            CurrentMana = 0;
            CurrentLevel = 1;
            CurrentXP = 0;
            RequiredXP = 100;
            SkillPoints = 0;
        }
    }
}
