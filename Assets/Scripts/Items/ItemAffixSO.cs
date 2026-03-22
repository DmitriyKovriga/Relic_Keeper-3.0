using System.Collections.Generic;
using Scripts.Items;
using Scripts.Stats;
using UnityEngine;

namespace Scripts.Items.Affixes
{
    public enum AffixValueMode
    {
        Single = 0,
        [InspectorName("Single")]
        SingleLegacy = 1,
        Range = 2
    }

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

        public string GetResolvedTranslationKey()
        {
            if (Stats == null || Stats.Length == 0)
                return TranslationKey;

            var statData = Stats[0];
            var kind = StatPresentation.FromStatModType(statData.Type);
            string preferredKey = BuildAutoTranslationKey(statData.Stat, kind, statData.GetEffectiveValueMode());

            if (string.IsNullOrEmpty(TranslationKey) || IsAutoTranslationKey(TranslationKey, statData.Stat, kind))
                return preferredKey;

            return TranslationKey;
        }

        private static string BuildAutoTranslationKey(StatType stat, StatAffixModifierKind kind, AffixValueMode valueMode)
        {
            string key = $"affix_{StatPresentation.GetModifierKindId(kind)}_{stat.ToString().ToLowerInvariant()}";
            if (kind == StatAffixModifierKind.Flat && valueMode == AffixValueMode.Range)
                key += "_range";
            return key;
        }

        private static bool IsAutoTranslationKey(string key, StatType stat, StatAffixModifierKind kind)
        {
            return key == BuildAutoTranslationKey(stat, kind, AffixValueMode.Single) ||
                   key == BuildAutoTranslationKey(stat, kind, AffixValueMode.Range);
        }

        [System.Serializable]
        public struct AffixStatData
        {
            public StatType Stat;
            public StatModType Type;
            public StatScope Scope;
            public AffixValueMode ValueMode;
            public float MinValue;
            public float MaxValue;
            public float RangeMinValue;
            public float RangeMaxValue;

            public AffixValueMode GetEffectiveValueMode()
            {
                return ValueMode == AffixValueMode.SingleLegacy ? AffixValueMode.Single : ValueMode;
            }

            public bool UsesRangeRoll()
            {
                return GetEffectiveValueMode() == AffixValueMode.Range;
            }

            public float GetPrimaryRollMin()
            {
                return MinValue;
            }

            public float GetPrimaryRollMax()
            {
                return MaxValue;
            }

            public float GetSecondaryRollMin()
            {
                return Mathf.Approximately(RangeMinValue, 0f) && Mathf.Approximately(RangeMaxValue, 0f)
                    ? MinValue
                    : RangeMinValue;
            }

            public float GetSecondaryRollMax()
            {
                return Mathf.Approximately(RangeMinValue, 0f) && Mathf.Approximately(RangeMaxValue, 0f)
                    ? MaxValue
                    : RangeMaxValue;
            }
        }
    }
}
