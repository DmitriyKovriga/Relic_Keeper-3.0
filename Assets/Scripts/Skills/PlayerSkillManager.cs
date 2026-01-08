using UnityEngine;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

namespace Scripts.Skills
{
    public class PlayerSkillManager : MonoBehaviour
    {
        // Событие для UI: (Index слота, Данные скилла)
        public event System.Action<int, SkillDataSO> OnSkillSlotUpdated;

        // Храним активные скиллы. 
        // 0 = MainHand (LMB), 1 = OffHand (RMB), 2 = Gloves (Q), 3 = Boots (W) и т.д.
        private Dictionary<int, SkillDataSO> _equippedSkills = new Dictionary<int, SkillDataSO>();

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += HandleItemEquipped;
                InventoryManager.Instance.OnItemUnequipped += HandleItemUnequipped;
                
                // Если загрузились после InventoryManager (редкий кейс, но все же),
                // нужно проверить уже надетые вещи.
                RefreshAllSkills(); 
            }
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped -= HandleItemEquipped;
                InventoryManager.Instance.OnItemUnequipped -= HandleItemUnequipped;
            }
        }

        private void HandleItemEquipped(InventoryItem item)
        {
            if (item == null || item.GrantedSkills.Count == 0) return;

            // Определяем, в какой слот скиллов пойдет этот предмет
            // Это упрощенная логика. В будущем можно сделать маппинг в ScriptableObject.
            int skillSlotIndex = GetSkillSlotByItemSlot(item.Data.Slot);

            if (skillSlotIndex != -1)
            {
                // Берем первый скилл с предмета (пока так)
                var skill = item.GrantedSkills[0];
                EquipSkill(skillSlotIndex, skill);
            }
        }

        private void HandleItemUnequipped(InventoryItem item)
        {
            int skillSlotIndex = GetSkillSlotByItemSlot(item.Data.Slot);
            if (skillSlotIndex != -1)
            {
                UnequipSkill(skillSlotIndex);
            }
        }

        private void EquipSkill(int slotIndex, SkillDataSO skill)
        {
            _equippedSkills[slotIndex] = skill;
            OnSkillSlotUpdated?.Invoke(slotIndex, skill);
            Debug.Log($"[SkillManager] Equipped {skill.SkillName} to Slot {slotIndex}");
        }

        private void UnequipSkill(int slotIndex)
        {
            if (_equippedSkills.ContainsKey(slotIndex))
            {
                _equippedSkills.Remove(slotIndex);
                OnSkillSlotUpdated?.Invoke(slotIndex, null); // null = очистить слот
                Debug.Log($"[SkillManager] Unequipped Slot {slotIndex}");
            }
        }

        // Логика маппинга: Какая шмотка в какую кнопку бьет
        private int GetSkillSlotByItemSlot(EquipmentSlot itemSlot)
        {
            switch (itemSlot)
            {
                case EquipmentSlot.MainHand: return 0; // LMB
                case EquipmentSlot.OffHand: return 1;  // RMB
                case EquipmentSlot.Gloves: return 2;   // Клавиша Q (или FirstSkill в Input)
                case EquipmentSlot.Boots: return 3;    // Клавиша W
                case EquipmentSlot.Helmet: return 4;   // Клавиша E
                default: return -1;
            }
        }

        public void RefreshAllSkills()
        {
            // Пробегаемся по надетому шмоту InventoryManager
            // (Реализуем, если нужно принудительное обновление при загрузке)
        }
    }
}