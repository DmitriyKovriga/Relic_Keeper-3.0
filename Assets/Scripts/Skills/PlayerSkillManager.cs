using UnityEngine;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats; // Нужен для получения PlayerStats

namespace Scripts.Skills
{
    public class PlayerSkillManager : MonoBehaviour
    {
        // Событие для UI (Иконки)
        public event System.Action<int, SkillDataSO> OnSkillSlotUpdated;

        // Словарь АКТИВНЫХ инстансов (скриптов на сцене), а не просто данных
        private Dictionary<int, SkillBehaviour> _activeSkills = new Dictionary<int, SkillBehaviour>();

        // Контейнер, куда будут складываться спавнящиеся скиллы (чтобы не мусорить в иерархии)
        [SerializeField] private Transform _skillContainer;
        
        private PlayerStats _playerStats;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();
            
            // Если контейнера нет, создаем его под игроком
            if (_skillContainer == null)
            {
                GameObject container = new GameObject("ActiveSkillsContainer");
                container.transform.SetParent(transform);
                container.transform.localPosition = Vector3.zero;
                _skillContainer = container.transform;
            }
        }

        private void Start()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped += HandleItemEquipped;
                InventoryManager.Instance.OnItemUnequipped += HandleItemUnequipped;
                
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

        public void RefreshAllSkills()
        {
            // Очищаем всё перед рефрешем (на всякий случай)
            var keys = new List<int>(_activeSkills.Keys);
            foreach (var key in keys) UnequipSkill(key);

            var equipment = InventoryManager.Instance.EquipmentItems;
            for (int i = 0; i < equipment.Length; i++)
            {
                var item = equipment[i];
                if (item != null && item.Data != null)
                {
                    HandleItemEquipped(item);
                }
            }
        }

        // --- МЕТОД ИСПОЛЬЗОВАНИЯ СКИЛЛА (Вызывается из InputSystem) ---
        public void UseSkill(int slotIndex)
{
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour))
            {
                if (skillBehaviour != null)
                {
                    skillBehaviour.TryCast();
                }
            }
}

        // --- ЭКИПИРОВКА ---

        private void HandleItemEquipped(InventoryItem item)
        {
            if (item == null || item.GrantedSkills.Count == 0) return;

            // 1. ОРУЖИЕ
            if (item.Data is WeaponItemSO weapon)
            {
                if (weapon.Slot == EquipmentSlot.OffHand)
                {
                    if (item.GrantedSkills.Count > 0) 
                        EquipSkill(1, item.GrantedSkills[0]);
                    return;
                }

                if (weapon.Slot == EquipmentSlot.MainHand)
                {
                    if (item.GrantedSkills.Count > 0) 
                        EquipSkill(0, item.GrantedSkills[0]);

                    if (weapon.IsTwoHanded && item.GrantedSkills.Count > 1)
                    {
                        EquipSkill(1, item.GrantedSkills[1]);
                    }
                    return;
                }
            }

            // 2. БРОНЯ
            int skillSlotIndex = GetSkillSlotByItemSlot(item.Data.Slot);
            if (skillSlotIndex != -1)
            {
                var skill = item.GrantedSkills[0];
                EquipSkill(skillSlotIndex, skill);
            }
        }

        private void HandleItemUnequipped(InventoryItem item)
        {
            if (item == null || item.Data == null) return;

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

            int skillSlotIndex = GetSkillSlotByItemSlot(item.Data.Slot);
            if (skillSlotIndex != -1)
            {
                UnequipSkill(skillSlotIndex);
            }
        }

        // --- ГЛАВНЫЕ МЕТОДЫ УПРАВЛЕНИЯ ПРЕФАБАМИ ---

        private void EquipSkill(int slotIndex, SkillDataSO skillData)
        {
            // 1. Сначала снимаем старый, если был
            UnequipSkill(slotIndex);

            // 2. Если есть префаб -> Инстанцируем
            if (skillData != null && skillData.SkillPrefab != null)
            {
                // Спавним префаб (он сразу становится дочерним к Container)
                GameObject skillObj = Instantiate(skillData.SkillPrefab, _skillContainer);
                skillObj.name = $"Skill_{skillData.SkillName}_{slotIndex}"; // Для удобства в иерархии

                // Получаем компонент логики
                SkillBehaviour behaviour = skillObj.GetComponent<SkillBehaviour>();
                
                if (behaviour != null)
                {
                    // Инициализируем (передаем статы игрока)
                    behaviour.Initialize(_playerStats, skillData);
                    
                    // Сохраняем в словарь активных
                    _activeSkills[slotIndex] = behaviour;
                }
                else
                {
                    Debug.LogError($"[PlayerSkillManager] В префабе скилла '{skillData.SkillName}' нет компонента SkillBehaviour (или наследника)!");
                    Destroy(skillObj); // Удаляем мусор
                }
            }

            // 3. Обновляем UI (Иконку)
            OnSkillSlotUpdated?.Invoke(slotIndex, skillData);
        }

        private void UnequipSkill(int slotIndex)
        {
            // Если в слоте что-то есть -> Удаляем объект со сцены
            if (_activeSkills.TryGetValue(slotIndex, out var activeSkill))
            {
                if (activeSkill != null)
                {
                    Destroy(activeSkill.gameObject);
                }
                _activeSkills.Remove(slotIndex);
            }

            // Очищаем UI (передаем null)
            OnSkillSlotUpdated?.Invoke(slotIndex, null);
        }

        private int GetSkillSlotByItemSlot(EquipmentSlot itemSlot)
        {
            switch (itemSlot)
            {
                case EquipmentSlot.MainHand: return 0;
                case EquipmentSlot.OffHand: return 1;
                case EquipmentSlot.Gloves: return 2;
                case EquipmentSlot.Boots: return 3; 
                case EquipmentSlot.Helmet: return 4;
                default: return -1;
            }
        }
    }
}