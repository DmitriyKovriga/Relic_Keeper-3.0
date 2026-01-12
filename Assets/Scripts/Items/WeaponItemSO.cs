using UnityEngine;
using Scripts.Skills;

namespace Scripts.Items
{
    [CreateAssetMenu(menuName = "RPG/Inventory/Weapon Item")]
    public class WeaponItemSO : EquipmentItemSO
    {
        [Header("Weapon Config")]
        public bool IsTwoHanded;

        [Header("Visuals")]
        [Tooltip("Спрайт, который отображается в руке персонажа")]
        public Sprite InHandSprite; 

        [Header("Base Offense Stats")]
        public float MinPhysicalDamage;
        public float MaxPhysicalDamage;

        [Space]
        public float MinFireDamage;
        public float MaxFireDamage;

        public float MinColdDamage;
        public float MaxColdDamage;

        public float MinLightningDamage;
        public float MaxLightningDamage;
        // AI ADDED END

        [Tooltip("Атак в секунду (APS)")]
        public float AttacksPerSecond = 1.0f;
        
        // ОБНОВЛЕНО: Используем 5.0 для 5%
        [Tooltip("Базовый шанс крита в процентах (5 = 5%). Локальные моды будут умножать это число.")]
        public float BaseCritChance = 5f; 
    

        [Header("2H Special Skills")]
        [Tooltip("Только для двуручного оружия: Пул скиллов для правой кнопки мыши (Secondary Attack)")]
        public SkillPoolSO SecondarySkillPool;
    }
}