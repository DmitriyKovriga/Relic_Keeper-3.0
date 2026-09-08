using UnityEngine;
using System.Collections.Generic;
using Scripts.Items; // Р”Р»СЏ Enum EquipmentSlot
using Scripts.Stats;

namespace Scripts.Items.Affixes
{
    public readonly struct AffixRollSelection
    {
        public ItemAffixSO Affix { get; }
        public int Tier { get; }

        public AffixRollSelection(ItemAffixSO affix, int tier)
        {
            Affix = affix;
            Tier = tier;
        }
    }

    [CreateAssetMenu(menuName = "RPG/Affixes/Affix Pool")]
    public class AffixPoolSO : ScriptableObject
    {
        [Header("Config")]
        public EquipmentSlot Slot; // РќР° С‡С‚Рѕ СЌС‚Рѕ РїР°РґР°РµС‚ (Gloves)
        public ArmorDefenseType DefenseType; // РўРёРї Р·Р°С‰РёС‚С‹ (Armor)

        [Header("All Possible Affixes")]
        public List<ItemAffixSO> Affixes;

        // Р“Р»Р°РІРЅС‹Р№ РјРµС‚РѕРґ: Р”Р°Р№ РјРЅРµ N СЃР»СѓС‡Р°Р№РЅС‹С… СѓРЅРёРєР°Р»СЊРЅС‹С… Р°С„С„РёРєСЃРѕРІ
        public List<AffixRollSelection> GetRandomAffixes(int count, int itemLevel)
        {
            var result = new List<AffixRollSelection>();
            var candidates = BuildCandidates(itemLevel);

            for (int i = 0; i < count; i++)
            {
                if (candidates.Count == 0) break;

                int index = Random.Range(0, candidates.Count);
                ItemAffixSO picked = candidates[index];
                List<int> eligibleTiers = GetRuntimeAllowedTiers(picked, itemLevel);
                if (eligibleTiers.Count > 0)
                    result.Add(new AffixRollSelection(picked, eligibleTiers[Random.Range(0, eligibleTiers.Count)]));
                candidates.RemoveAt(index);
            }

            return result;
        }

        public int GetAvailableAffixGroupCount(int itemLevel)
        {
            return BuildCandidates(itemLevel).Count;
        }

        private List<ItemAffixSO> BuildCandidates(int itemLevel)
        {
            var candidates = new List<ItemAffixSO>();
            if (Affixes == null)
                return candidates;

            var seenGroups = new HashSet<string>();
            foreach (var affix in Affixes)
            {
                if (affix != null && seenGroups.Add(GetGroupKey(affix)) && GetRuntimeAllowedTiers(affix, itemLevel).Count > 0)
                    candidates.Add(affix);
            }

            return candidates;
        }

        private static string GetGroupKey(ItemAffixSO affix)
        {
            if (!string.IsNullOrWhiteSpace(affix.GroupID))
                return affix.GroupID.Trim();
            if (!string.IsNullOrWhiteSpace(affix.UniqueID))
                return affix.UniqueID.Trim();
            return affix.name;
        }

        private static List<int> GetRuntimeAllowedTiers(ItemAffixSO affix, int itemLevel)
        {
            var result = new List<int>();
            if (affix == null)
                return result;

            foreach (int tier in affix.GetEligibleTiers(itemLevel))
            {
                ItemAffixSO.AffixStatData[] stats = affix.GetStatsForTier(tier);
                bool allowed = true;
                for (int i = 0; i < stats.Length; i++)
                {
                    if (stats[i].Stat == StatType.AttackSpeed && stats[i].Type == StatModType.Flat)
                    {
                        allowed = false;
                        break;
                    }
                }

                if (allowed)
                    result.Add(tier);
            }

            return result;
        }
    }
}
