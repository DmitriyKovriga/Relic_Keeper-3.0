using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills;
using Scripts.Items.Affixes;

namespace Scripts.Items
{
    public abstract class EquipmentItemSO : ScriptableObject
    {
        [Header("Core Info")]
        public string ID;
        public string ItemName;
        public Sprite Icon;
        
        [Header("Size (In Slots)")]
        [Min(1)] public int Width = 1;  // РЁРёСЂРёРЅР° РІ РєР»РµС‚РєР°С…
        [Min(1)] public int Height = 1; // Р’С‹СЃРѕС‚Р° РІ РєР»РµС‚РєР°С…

        [Header("Equip Settings")]
        public EquipmentSlot Slot;

        [Header("Drop Configuration")]
        public int DropLevel = 1;

        [Header("Implicit / Fixed Mods")]
        public List<ItemStatModifier> ImplicitModifiers = new List<ItemStatModifier>();

        [Header("Affix Configuration")]
        [Tooltip("Explicit affix pool used when this item rolls random affixes. If empty, generated items based on this asset receive no random affixes.")]
        public AffixPoolSO AffixPool;

        [Header("Skill Configuration")]
        [Tooltip("РџСѓР» СЃРєРёР»Р»РѕРІ, РєРѕС‚РѕСЂС‹Рµ РјРѕРіСѓС‚ РІС‹РїР°СЃС‚СЊ РЅР° СЌС‚РѕРј РїСЂРµРґРјРµС‚Рµ")]
        public SkillPoolSO SkillPool;
        
        [Tooltip("РЎРєРѕР»СЊРєРѕ СЃРєРёР»Р»РѕРІ СЂРѕР»Р»РёС‚СЊ? (РћР±С‹С‡РЅРѕ 1, РґР»СЏ РґРІСѓСЂСѓС‡РµРє РјР± 2)")]
        public int SkillCount = 1;

        [System.Serializable]
        public class ItemStatModifier
        {
            public StatType Stat;
            public float Value;
            public StatModType Type = StatModType.Flat;
            public StatScope Scope = StatScope.Global; 
        }
    }
}
