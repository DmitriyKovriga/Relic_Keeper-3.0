using UnityEngine;
using System.Collections.Generic;

namespace Scripts.Items
{
    /// <summary>
    /// Назначение слотов сфер: какой CraftingOrbSO в каком слоте (индекс 0, 1, 2...).
    /// Редактируется в Crafting Orb Editor. Хранится в Resources/CraftingOrbs/.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Crafting/Crafting Orb Slots Config", fileName = "CraftingOrbSlotsConfig")]
    public class CraftingOrbSlotsConfigSO : ScriptableObject
    {
        [Tooltip("Фиксированное число слотов. Каждый элемент — сфера в этом слоте или пусто (null).")]
        public List<CraftingOrbSO> Slots = new List<CraftingOrbSO>();

        public int SlotCount => Slots != null ? Slots.Count : 0;

        public CraftingOrbSO GetOrbInSlot(int index)
        {
            if (Slots == null || index < 0 || index >= Slots.Count) return null;
            return Slots[index];
        }
    }
}
