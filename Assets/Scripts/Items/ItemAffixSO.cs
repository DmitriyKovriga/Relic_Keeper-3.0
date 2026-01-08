using UnityEngine;
using Scripts.Stats;
using Scripts.Items; // Для StatScope

namespace Scripts.Items.Affixes
{
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix")]
    public class ItemAffixSO : ScriptableObject
    {
        [HideInInspector] // Скрываем, чтобы случайно не сломать руками, он авто-генерируемый
        public string UniqueID;    

        public string GroupID; 
        public int Tier;
        public int RequiredLevel;
        public string TranslationKey;

        public AffixStatData[] Stats;

        [System.Serializable]
        public struct AffixStatData
        {
            public StatType Stat;
            public StatModType Type;
            
            // Важное поле: Local или Global. По умолчанию Global.
            public StatScope Scope; 
            
            public float MinValue;
            public float MaxValue;
        }
    }
}