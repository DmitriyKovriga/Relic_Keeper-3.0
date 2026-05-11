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
            List<string> usedGroups = new List<string>();
            
            // РљР°РЅРґРёРґР°С‚С‹: Р°С„С„РёРєСЃС‹, С‡РµР№ С‚РёСЂ РґРѕРїСѓСЃРєР°РµС‚ РґР°РЅРЅС‹Р№ СѓСЂРѕРІРµРЅСЊ РїСЂРµРґРјРµС‚Р° (Р·Р°С…Р°СЂРґРєРѕР¶РµРЅРѕ РІ AffixTierHelper)
            var candidates = new List<ItemAffixSO>();
            foreach (var a in Affixes)
            {
                if (a != null && AffixTierHelper.IsTierAllowedForLevel(itemLevel, a.Tier) && IsRuntimeAllowed(a))
                    candidates.Add(a);
            }

            // РџС‹С‚Р°РµРјСЃСЏ РЅР°Р±СЂР°С‚СЊ РЅСѓР¶РЅРѕРµ РєРѕР»РёС‡РµСЃС‚РІРѕ
            for (int i = 0; i < count; i++)
            {
                if (candidates.Count == 0) break;

                // Р‘РµСЂРµРј СЃР»СѓС‡Р°Р№РЅС‹Р№
                int index = Random.Range(0, candidates.Count);
                ItemAffixSO picked = candidates[index];

                // РџСЂРѕРІРµСЂСЏРµРј РіСЂСѓРїРїСѓ (С‡С‚РѕР±С‹ РЅРµ Р±С‹Р»Рѕ 2 СЂР°Р·Р° Life)
                if (!usedGroups.Contains(picked.GroupID))
                {
                    result.Add(picked);
                    usedGroups.Add(picked.GroupID);
                }

                // РЈРґР°Р»СЏРµРј РёР· РєР°РЅРґРёРґР°С‚РѕРІ (С‡С‚РѕР±С‹ РЅРµ РІС‹С‚Р°С‰РёС‚СЊ СЌС‚РѕС‚ Р¶Рµ РѕР±СЉРµРєС‚ СЃРЅРѕРІР°)
                candidates.RemoveAt(index);
                
                // РћРїС‚РёРјРёР·Р°С†РёСЏ: РјРѕР¶РЅРѕ СЃСЂР°Р·Сѓ СѓРґР°Р»РёС‚СЊ РёР· РєР°РЅРґРёРґР°С‚РѕРІ РІСЃРµ Р°С„С„РёРєСЃС‹ СЌС‚РѕР№ Р¶Рµ РіСЂСѓРїРїС‹,
                // РЅРѕ РґР»СЏ РїСЂРѕСЃС‚РѕС‚С‹ РїРѕРєР° РѕСЃС‚Р°РІРёРј С‚Р°Рє.
            }

            return result;
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
