using System.Collections.Generic;
using Scripts.Items;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Items.Affixes
{
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix")]
    public class ItemAffixSO : ScriptableObject
    {
        [HideInInspector]
        public string UniqueID;

        public string GroupID;
        public int Tier;
        [Tooltip("Ключ локализации имени аффикса.")]
        public string NameKey;
        [Tooltip("Ключ локализации значения аффикса.")]
        public string TranslationKey;
        [Tooltip("Если включено — пакетная регенерация локалей не затрагивает этот аффикс.")]
        public bool LockAutoLocalization;

        [Tooltip("Теги аффикса для крафта и генерации.")]
        public List<string> TagIds = new List<string>();

        public AffixStatData[] Stats;

        [System.Serializable]
        public struct AffixStatData
        {
            public StatType Stat;
            public StatModType Type;
            public StatScope Scope;
            public float MinValue;
            public float MaxValue;
        }
    }
}
