using UnityEngine;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;

namespace Scripts.Skills
{
    public class PlayerSkillManager : MonoBehaviour
    {
        public event System.Action<int, SkillDataSO> OnSkillSlotUpdated;

        private Dictionary<int, SkillBehaviour> _activeSkills = new Dictionary<int, SkillBehaviour>();

        [SerializeField] private Transform _skillContainer;

        private PlayerStats _playerStats;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();

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
                InventoryManager.Instance.OnInventoryChanged += RefreshAllSkills;
                InventoryManager.Instance.OnItemEquipped += HandleEquipmentChanged;
                InventoryManager.Instance.OnItemUnequipped += HandleEquipmentChanged;
                RefreshAllSkills();
            }
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshAllSkills;
                InventoryManager.Instance.OnItemEquipped -= HandleEquipmentChanged;
                InventoryManager.Instance.OnItemUnequipped -= HandleEquipmentChanged;
            }
        }

        public void RefreshAllSkills()
        {
            var keys = new List<int>(_activeSkills.Keys);
            foreach (var key in keys)
                UnequipSkill(key);

            if (InventoryManager.Instance == null)
                return;

            var equipment = InventoryManager.Instance.EquipmentItems;
            for (int i = 0; i < equipment.Length; i++)
            {
                var item = equipment[i];
                if (item != null && item.Data != null)
                    EquipSkillsForItem(item, i);
            }
        }

        public void UseSkill(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                skillBehaviour.TryCast();
        }

        private void HandleEquipmentChanged(InventoryItem _)
        {
            RefreshAllSkills();
        }

        private void EquipSkillsForItem(InventoryItem item, int equippedSlotIndex)
        {
            if (item == null || item.GrantedSkills.Count == 0)
                return;

            if (item.Data is WeaponItemSO weapon)
            {
                if (equippedSlotIndex == (int)EquipmentSlot.OffHand)
                {
                    EquipSkill(1, item.GrantedSkills[0]);
                    return;
                }

                if (equippedSlotIndex == (int)EquipmentSlot.MainHand)
                {
                    EquipSkill(0, item.GrantedSkills[0]);

                    if (weapon.IsTwoHanded && item.GrantedSkills.Count > 1)
                        EquipSkill(1, item.GrantedSkills[1]);

                    return;
                }
            }

            int skillSlotIndex = GetSkillSlotByItemSlot((EquipmentSlot)equippedSlotIndex);
            if (skillSlotIndex != -1)
                EquipSkill(skillSlotIndex, item.GrantedSkills[0]);
        }

        private void EquipSkill(int slotIndex, SkillDataSO skillData)
        {
            UnequipSkill(slotIndex);

            if (skillData != null && skillData.SkillPrefab != null)
            {
                GameObject skillObj = Instantiate(skillData.SkillPrefab, _skillContainer);
                skillObj.name = $"Skill_{skillData.SkillName}_{slotIndex}";

                SkillBehaviour behaviour = skillObj.GetComponent<SkillBehaviour>();
                if (behaviour != null)
                {
                    behaviour.Initialize(_playerStats, skillData);
                    _activeSkills[slotIndex] = behaviour;
                }
                else
                {
                    Debug.LogError($"[PlayerSkillManager] Skill prefab '{skillData.SkillName}' does not contain SkillBehaviour.");
                    Destroy(skillObj);
                }
            }

            OnSkillSlotUpdated?.Invoke(slotIndex, skillData);
        }

        private void UnequipSkill(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var activeSkill))
            {
                if (activeSkill != null)
                    Destroy(activeSkill.gameObject);

                _activeSkills.Remove(slotIndex);
            }

            OnSkillSlotUpdated?.Invoke(slotIndex, null);
        }

        private static readonly int[] _equipmentSlotToSkillSlot = { 4, -1, 0, 1, 2, 3 };

        private int GetSkillSlotByItemSlot(EquipmentSlot itemSlot)
        {
            int i = (int)itemSlot;
            if (i < 0 || i >= _equipmentSlotToSkillSlot.Length) return -1;
            return _equipmentSlotToSkillSlot[i];
        }
    }
}
