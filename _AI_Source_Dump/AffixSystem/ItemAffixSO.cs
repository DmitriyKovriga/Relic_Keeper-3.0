using UnityEngine;
using Scripts.Stats; // Ссылка на StatModType и StatType

namespace Scripts.Items.Affixes
{
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix")]
    public class ItemAffixSO : ScriptableObject
    {
        // Группа исключения. Если на предмете уже есть аффикс с GroupID "Life",
        // то второй такой же выпасть не может.
        public string GroupID; 
        
        public int Tier; // 1 - слабый, 10 - сильный
        public int RequiredLevel; // С какого уровня монстров может падать

        public string TranslationKey;

        // Список статов, которые дает этот аффикс (обычно 1, но может быть гибрид)
        public AffixStatData[] Stats;

        [System.Serializable]
        public struct AffixStatData
        {
            public StatType Stat;
            public StatModType Type; // Flat или Percent
            public float MinValue;
            public float MaxValue;
        }
    }
}