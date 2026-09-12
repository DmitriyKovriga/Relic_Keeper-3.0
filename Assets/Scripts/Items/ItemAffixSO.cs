using System.Collections.Generic;
using System.Linq;
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
        [System.Serializable]
        public sealed class AffixTierData
        {
            [Range(1, 5)] public int Tier = 5;
            public AffixStatData[] Stats = System.Array.Empty<AffixStatData>();
        }

        [System.Serializable]
        public struct LegacyTierId
        {
            public string Id;
            [Range(1, 5)] public int Tier;
        }

        [HideInInspector]
        public string UniqueID;

        public string GroupID;
        [HideInInspector, Tooltip("Legacy single-tier data. Kept only so the one-shot migration can read old assets.")]
        public int Tier;
        [Tooltip("Ключ локализации имени аффикса.")]
        public string NameKey;
        [Tooltip("Ключ локализации значения аффикса.")]
        public string TranslationKey;
        [Tooltip("Если включено — пакетная регенерация локалей не затрагивает этот аффикс.")]
        public bool LockAutoLocalization;

        [Tooltip("Теги аффикса для крафта и генерации.")]
        public List<string> TagIds = new List<string>();

        [HideInInspector, Tooltip("Legacy single-tier data. Runtime uses Tiers after migration.")]
        public AffixStatData[] Stats;

        [Header("Tier Values")]
        public List<AffixTierData> Tiers = new List<AffixTierData>();

        [HideInInspector]
        public List<LegacyTierId> LegacyTierIds = new List<LegacyTierId>();

        public bool UsesEmbeddedTiers => Tiers != null && Tiers.Any(t => t != null && t.Stats != null && t.Stats.Length > 0);

        public IReadOnlyList<int> GetEligibleTiers(int itemLevel)
        {
            var result = new List<int>();
            if (UsesEmbeddedTiers)
            {
                foreach (AffixTierData tierData in Tiers)
                {
                    if (tierData != null && tierData.Stats != null && tierData.Stats.Length > 0 &&
                        AffixTierHelper.IsTierAllowedForLevel(itemLevel, tierData.Tier))
                        result.Add(tierData.Tier);
                }
                return result;
            }

            if (AffixTierHelper.IsTierAllowedForLevel(itemLevel, Tier))
                result.Add(Tier);
            return result;
        }

        public AffixStatData[] GetStatsForTier(int tier)
        {
            if (UsesEmbeddedTiers)
            {
                AffixTierData exact = Tiers.FirstOrDefault(entry => entry != null && entry.Tier == tier);
                if (exact != null)
                    return exact.Stats ?? System.Array.Empty<AffixStatData>();
                return System.Array.Empty<AffixStatData>();
            }

            return tier == Tier || tier <= 0 ? Stats ?? System.Array.Empty<AffixStatData>() : System.Array.Empty<AffixStatData>();
        }

        public int GetDefaultTier()
        {
            if (UsesEmbeddedTiers)
            {
                AffixTierData weakest = Tiers
                    .Where(entry => entry != null && entry.Stats != null && entry.Stats.Length > 0)
                    .OrderByDescending(entry => entry.Tier)
                    .FirstOrDefault();
                return weakest != null ? weakest.Tier : 5;
            }

            return Mathf.Clamp(Tier, 1, 5);
        }

        public string GetResolvedTranslationKey()
        {
            AffixStatData[] resolvedStats = GetStatsForTier(GetDefaultTier());
            if (resolvedStats == null || resolvedStats.Length == 0)
                return TranslationKey;

            var statData = resolvedStats[0];
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
