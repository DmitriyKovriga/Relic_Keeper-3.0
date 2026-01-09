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
            // LOG 1: Проверяем, вызвался ли метод вообще
            Debug.Log($"[SkillManager] Item Equipped: {item.Data.ItemName}");

             if (item == null || item.GrantedSkills.Count == 0) 
            {
                // LOG 2: Если скиллов нет
                Debug.LogWarning($"[SkillManager] У предмета {item.Data.ItemName} НЕТ скиллов в списке GrantedSkills!");
                return;
            }

            // 1. Логика для ОРУЖИЯ (MainHand / OffHand / TwoHanded)
            if (item.Data is WeaponItemSO weapon)
            {
                // Если оружие в левой руке (щит/меч), просто ставим в слот OffHand (1)
                if (weapon.Slot == EquipmentSlot.OffHand)
                {
                    if (item.GrantedSkills.Count > 0) 
                        EquipSkill(1, item.GrantedSkills[0]);
                    return;
                }

                // Если оружие в правой руке (MainHand)
                if (weapon.Slot == EquipmentSlot.MainHand)
                {
                    // Первый скилл всегда идет в MainHand (0)
                    if (item.GrantedSkills.Count > 0) 
                        EquipSkill(0, item.GrantedSkills[0]);

                    // Если это ДВУРУЧНОЕ оружие, оно может дать второй скилл в слот OffHand (1)
                    if (weapon.IsTwoHanded && item.GrantedSkills.Count > 1)
                    {
                        EquipSkill(1, item.GrantedSkills[1]);
                    }
                    return;
                }
            }

            // 2. Логика для БРОНИ (Gloves, Boots, Helm)
            int skillSlotIndex = GetSkillSlotByItemSlot(item.Data.Slot);
            Debug.Log($"[SkillManager] Item Slot: {item.Data.Slot}, Mapped to Skill Slot Index: {skillSlotIndex}");
            if (skillSlotIndex != -1)
            {
                var skill = item.GrantedSkills[0];
                EquipSkill(skillSlotIndex, skill);
            }
        }

        private void HandleItemUnequipped(InventoryItem item)
        {
            // --- FIX START: Защита от null ---
            // Если предмет пустой или у него потерялась ссылка на ScriptableObject, 
            // мы не можем определить, какой слот очищать. Просто выходим.
            if (item == null || item.Data == null) return;
            // --- FIX END ---

            // 1. ОРУЖИЕ
            if (item.Data is WeaponItemSO weapon)
            {
                if (weapon.Slot == EquipmentSlot.OffHand)
                {
                    UnequipSkill(1);
                    return;
                }
                if (weapon.Slot == EquipmentSlot.MainHand)
                {
                    UnequipSkill(0);
                    if (weapon.IsTwoHanded) UnequipSkill(1);
                    return;
                }
            }

            // 2. БРОНЯ
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