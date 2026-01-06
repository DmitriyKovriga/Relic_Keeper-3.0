using UnityEngine;

namespace Scripts.Items
{
    [CreateAssetMenu(menuName = "RPG/Inventory/Armor Item")]
    public class ArmorItemSO : EquipmentItemSO
    {
        [Header("Defense Archetype")]
        public ArmorDefenseType DefenseType;

        [Header("Base Defense Stats")]
        [Tooltip("Базовая броня (увеличивается локальными аффиксами).")]
        public float BaseArmor;
        
        [Tooltip("Базовое уклонение.")]
        public float BaseEvasion;
        
        [Tooltip("Базовые баблы (Bubbles).")]
        public float BaseBubbles; 
    }
}