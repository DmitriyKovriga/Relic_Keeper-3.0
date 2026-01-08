using UnityEngine;
using System.Collections.Generic;
using Scripts.Stats;
using Scripts.Skills;

namespace Scripts.Items
{
    public abstract class EquipmentItemSO : ScriptableObject
    {
        [Header("Core Info")]
        public string ID;
        public string ItemName;
        public Sprite Icon;
        
        [Header("Size (In Slots)")]
        [Min(1)] public int Width = 1;  // Ширина в клетках
        [Min(1)] public int Height = 1; // Высота в клетках

        [Header("Equip Settings")]
        public EquipmentSlot Slot;

        [Header("Drop Configuration")]
        public int DropLevel = 1;

        [Header("Implicit / Fixed Mods")]
        public List<ItemStatModifier> ImplicitModifiers = new List<ItemStatModifier>();

        [Header("Skill Configuration")]
        [Tooltip("Пул скиллов, которые могут выпасть на этом предмете")]
        public SkillPoolSO SkillPool;
        
        [Tooltip("Сколько скиллов роллить? (Обычно 1, для двуручек мб 2)")]
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