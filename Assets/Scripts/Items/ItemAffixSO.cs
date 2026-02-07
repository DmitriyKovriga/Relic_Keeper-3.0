using UnityEngine;
using System.Collections.Generic;
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
        public int Tier; // 1 = самый сильный, 5 = самый слабый. Уровень выпадения захардкожен в AffixTierHelper.
        [Tooltip("Ключ локализации названия аффикса (для детального тултипа)")]
        public string NameKey;
        [Tooltip("Ключ для отображения значения аффикса (affix_type_stat)")]
        public string TranslationKey;

        [Tooltip("Теги для крафта/генерации. Часть заполняется автоматически из категорий статов.")]
        public List<string> TagIds = new List<string>();

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