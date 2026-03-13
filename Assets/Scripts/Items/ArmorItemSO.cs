using UnityEngine;
using UnityEngine.Serialization;

namespace Scripts.Items
{
    [CreateAssetMenu(menuName = "RPG/Inventory/Armor Item")]
    public class ArmorItemSO : EquipmentItemSO
    {
        [Header("Defense Archetype")]
        public ArmorDefenseType DefenseType;

        [Header("Base Defense Stats")]
        [Tooltip("Base armor value.")]
        public float BaseArmor;

        [Tooltip("Base evasion value.")]
        public float BaseEvasion;

        [FormerlySerializedAs("BaseBubbles")]
        [Tooltip("Base Mystic Shield value.")]
        public float BaseMysticShield;
    }
}
