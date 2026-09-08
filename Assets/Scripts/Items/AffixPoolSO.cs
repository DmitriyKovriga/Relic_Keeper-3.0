using UnityEngine;
using System.Collections.Generic;
using Scripts.Items; // Р”Р»СЏ Enum EquipmentSlot
using Scripts.Stats;

namespace Scripts.Items.Affixes
{
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix Pool")]
    public class AffixPoolSO : ScriptableObject
    {
        [Header("Config")]
        public EquipmentSlot Slot; // РќР° С‡С‚Рѕ СЌС‚Рѕ РїР°РґР°РµС‚ (Gloves)
        public ArmorDefenseType DefenseType; // РўРёРї Р·Р°С‰РёС‚С‹ (Armor)

        [Header("All Possible Affixes")]
        public List<ItemAffixSO> Affixes;

        // Р“Р»Р°РІРЅС‹Р№ РјРµС‚РѕРґ: Р”Р°Р№ РјРЅРµ N СЃР»СѓС‡Р°Р№РЅС‹С… СѓРЅРёРєР°Р»СЊРЅС‹С… Р°С„С„РёРєСЃРѕРІ
        public List<ItemAffixSO> GetRandomAffixes(int count, int itemLevel)
        {
            List<ItemAffixSO> result = new List<ItemAffixSO>();
            var candidates = BuildCandidates(itemLevel);

            for (int i = 0; i < count; i++)
            {
                if (candidates.Count == 0) break;

                int index = Random.Range(0, candidates.Count);
                ItemAffixSO picked = candidates[index];
                string pickedGroup = GetGroupKey(picked);
                result.Add(picked);
                candidates.RemoveAll(candidate => GetGroupKey(candidate) == pickedGroup);
            }

            return result;
        }

        public int GetAvailableAffixGroupCount(int itemLevel)
        {
            var groups = new HashSet<string>();
            foreach (ItemAffixSO affix in BuildCandidates(itemLevel))
                groups.Add(GetGroupKey(affix));
            return groups.Count;
        }

        private List<ItemAffixSO> BuildCandidates(int itemLevel)
        {
            var candidates = new List<ItemAffixSO>();
            if (Affixes == null)
                return candidates;

            foreach (var affix in Affixes)
            {
                if (affix != null && AffixTierHelper.IsTierAllowedForLevel(itemLevel, affix.Tier) && IsRuntimeAllowed(affix))
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

        private static bool IsRuntimeAllowed(ItemAffixSO affix)
        {
            if (affix == null || affix.Stats == null)
                return true;

            foreach (var stat in affix.Stats)
            {
                if (stat.Stat == StatType.AttackSpeed && stat.Type == StatModType.Flat)
                    return false;
            }

            return true;
        }
    }
}
