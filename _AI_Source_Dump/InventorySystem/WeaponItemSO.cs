using UnityEngine;

namespace Scripts.Items
{
    [CreateAssetMenu(menuName = "RPG/Inventory/Weapon Item")]
    public class WeaponItemSO : EquipmentItemSO
    {
        [Header("Weapon Config")]
        public bool IsTwoHanded;

        [Header("Base Offense Stats")]
        public float MinPhysicalDamage;
        public float MaxPhysicalDamage;

        [Tooltip("Атак в секунду (APS)")]
        public float AttacksPerSecond = 1.0f;
        
        // ОБНОВЛЕНО: Используем 5.0 для 5%
        [Tooltip("Базовый шанс крита в процентах (5 = 5%). Локальные моды будут умножать это число.")]
        public float BaseCritChance = 5f; 
    }
}