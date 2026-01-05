using UnityEngine;
using System.Collections.Generic;
using Scripts.Items; // Для Enum EquipmentSlot

namespace Scripts.Items.Affixes
{
    [CreateAssetMenu(menuName = "RPG/Affixes/Affix Pool")]
    public class AffixPoolSO : ScriptableObject
    {
        [Header("Config")]
        public EquipmentSlot Slot; // На что это падает (Gloves)
        public ArmorDefenseType DefenseType; // Тип защиты (Armor)

        [Header("All Possible Affixes")]
        public List<ItemAffixSO> Affixes;

        // Главный метод: Дай мне N случайных уникальных аффиксов
        public List<ItemAffixSO> GetRandomAffixes(int count, int itemLevel)
        {
            List<ItemAffixSO> result = new List<ItemAffixSO>();
            List<string> usedGroups = new List<string>();
            
            // Создаем копию списка кандидатов, подходящих по уровню
            var candidates = new List<ItemAffixSO>();
            foreach(var a in Affixes)
            {
                if (a.RequiredLevel <= itemLevel) candidates.Add(a);
            }

            // Пытаемся набрать нужное количество
            for (int i = 0; i < count; i++)
            {
                if (candidates.Count == 0) break;

                // Берем случайный
                int index = Random.Range(0, candidates.Count);
                ItemAffixSO picked = candidates[index];

                // Проверяем группу (чтобы не было 2 раза Life)
                if (!usedGroups.Contains(picked.GroupID))
                {
                    result.Add(picked);
                    usedGroups.Add(picked.GroupID);
                }

                // Удаляем из кандидатов (чтобы не вытащить этот же объект снова)
                candidates.RemoveAt(index);
                
                // Оптимизация: можно сразу удалить из кандидатов все аффиксы этой же группы,
                // но для простоты пока оставим так.
            }

            return result;
        }
    }
}