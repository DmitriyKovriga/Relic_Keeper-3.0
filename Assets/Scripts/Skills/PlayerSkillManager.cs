using System.Collections.Generic;
using UnityEngine;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Skills.Modules;
using Scripts.Skills.Projectiles;

namespace Scripts.Skills
{
    public class PlayerSkillManager : MonoBehaviour
    {
        private const int SkillSlotCount = 5;
        private const int MainHandSkillSlot = 0;
        private const int OffHandSkillSlot = 1;
        private const int FirstUtilitySkillSlot = 2;

        public event System.Action<int, SkillDataSO> OnSkillSlotUpdated;

        private readonly Dictionary<int, SkillBehaviour> _activeSkills = new();

        [SerializeField] private Transform _skillContainer;

        private PlayerStats _playerStats;
        private bool _suppressSkillUsage;

        private void Awake()
        {
            _playerStats = GetComponent<PlayerStats>();

            if (_skillContainer == null)
            {
                GameObject container = new("ActiveSkillsContainer");
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
            SkillProjectile.DespawnAllForOwner(_playerStats);

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshAllSkills;
                InventoryManager.Instance.OnItemEquipped -= HandleEquipmentChanged;
                InventoryManager.Instance.OnItemUnequipped -= HandleEquipmentChanged;
            }
        }

        public void SetSkillUsageSuppressed(bool suppressed)
        {
            _suppressSkillUsage = suppressed;
        }

        public void CancelAllSkills()
        {
            foreach (var pair in _activeSkills)
            {
                if (pair.Value != null)
                    pair.Value.Cancel();
            }
        }

        public void RefreshAllSkills()
        {
            SkillProjectile.DespawnAllForOwner(_playerStats);

            for (int i = 0; i < SkillSlotCount; i++)
                UnequipSkill(i);

            if (InventoryManager.Instance == null)
                return;

            var equipment = InventoryManager.Instance.EquipmentItems;
            EquipHandSkills(equipment);
            EquipUtilitySkills(equipment);
        }

        public void UseSkill(int slotIndex)
        {
            if (_suppressSkillUsage)
                return;

            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                skillBehaviour.TryCast();
        }

        public float GetSkillCooldownNormalized(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                return skillBehaviour.CooldownNormalized;

            return 0f;
        }

        public float GetSkillCooldownRemaining(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                return skillBehaviour.CooldownRemaining;

            return 0f;
        }

        public SkillDataSO GetSkillData(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                return skillBehaviour.Data;

            return null;
        }

        public bool SlotHasCooldownSkill(int slotIndex)
        {
            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                return skillBehaviour.CooldownDuration > 0f;

            return false;
        }

        private void HandleEquipmentChanged(InventoryItem _)
        {
            RefreshAllSkills();
        }

        private void EquipHandSkills(InventoryItem[] equipment)
        {
            if (equipment == null)
                return;

            InventoryItem mainHandItem = GetEquipmentItem(equipment, EquipmentSlot.MainHand);
            InventoryItem offHandItem = GetEquipmentItem(equipment, EquipmentSlot.OffHand);

            if (mainHandItem != null && mainHandItem.GrantedSkills.Count > 0)
            {
                EquipSkill(MainHandSkillSlot, mainHandItem.GrantedSkills[0]);

                if (mainHandItem.Data is WeaponItemSO { IsTwoHanded: true } && mainHandItem.GrantedSkills.Count > 1)
                    EquipSkill(OffHandSkillSlot, mainHandItem.GrantedSkills[1]);
            }

            if (offHandItem != null && offHandItem.GrantedSkills.Count > 0)
                EquipSkill(OffHandSkillSlot, offHandItem.GrantedSkills[0]);
        }

        private void EquipUtilitySkills(InventoryItem[] equipment)
        {
            if (equipment == null)
                return;

            int nextSlot = FirstUtilitySkillSlot;
            EquipmentSlot[] priority =
            {
                EquipmentSlot.BodyArmor,
                EquipmentSlot.Gloves,
                EquipmentSlot.Boots,
                EquipmentSlot.Helmet
            };

            for (int i = 0; i < priority.Length; i++)
            {
                InventoryItem item = GetEquipmentItem(equipment, priority[i]);
                if (item == null || item.GrantedSkills.Count == 0)
                    continue;

                if (nextSlot >= SkillSlotCount)
                {
                    Debug.LogWarning($"[PlayerSkillManager] No free HUD skill slot for '{item.Data?.ItemName ?? item.Data?.name ?? "Unknown item"}' skill '{item.GrantedSkills[0]?.SkillName ?? "Unknown skill"}'. Add more skill slots or remove another equipment skill.");
                    continue;
                }

                EquipSkill(nextSlot, item.GrantedSkills[0]);
                nextSlot++;
            }
        }

        private static InventoryItem GetEquipmentItem(InventoryItem[] equipment, EquipmentSlot slot)
        {
            int index = (int)slot;
            if (equipment == null || index < 0 || index >= equipment.Length)
                return null;

            InventoryItem item = equipment[index];
            return item != null && item.Data != null ? item : null;
        }

        private void EquipSkill(int slotIndex, SkillDataSO skillData)
        {
            UnequipSkill(slotIndex);

            if (skillData != null)
            {
                GameObject skillObj = null;
                SkillBehaviour behaviour = null;

                if (skillData.SkillPrefab != null)
                {
                    skillObj = Instantiate(skillData.SkillPrefab, _skillContainer);
                    skillObj.name = $"Skill_{skillData.SkillName}_{slotIndex}";
                    behaviour = skillObj.GetComponent<SkillBehaviour>();
                }

                if (behaviour == null && skillData.Recipe != null)
                {
                    if (skillObj != null)
                    {
                        Debug.LogWarning($"[PlayerSkillManager] Skill prefab '{skillData.SkillName}' does not contain SkillBehaviour. Falling back to runtime StepRunner because Recipe is assigned.");
                        Destroy(skillObj);
                    }

                    skillObj = CreateRuntimeRecipeSkillObject(slotIndex, skillData);
                    behaviour = skillObj.GetComponent<SkillBehaviour>();
                }

                if (behaviour != null)
                {
                    behaviour.Initialize(_playerStats, skillData);
                    _activeSkills[slotIndex] = behaviour;
                }
                else if (skillObj != null)
                {
                    Debug.LogError($"[PlayerSkillManager] Skill prefab '{skillData.SkillName}' does not contain SkillBehaviour.");
                    Destroy(skillObj);
                }
            }

            OnSkillSlotUpdated?.Invoke(slotIndex, skillData);
        }

        private GameObject CreateRuntimeRecipeSkillObject(int slotIndex, SkillDataSO skillData)
        {
            var skillObj = new GameObject($"Skill_{skillData.SkillName}_{slotIndex}_Runtime");
            skillObj.transform.SetParent(_skillContainer, false);

            skillObj.AddComponent<SkillVFX>();
            skillObj.AddComponent<SkillMovementControl>();
            skillObj.AddComponent<SkillHandAnimation>();
            skillObj.AddComponent<SkillStepRunner>();

            return skillObj;
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
    }
}
