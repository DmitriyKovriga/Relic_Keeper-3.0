using System.Collections.Generic;
using UnityEngine;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Skills.Modules;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.Projectiles;

namespace Scripts.Skills
{
    public class PlayerSkillManager : MonoBehaviour, IPassiveSkillCooldowns
    {
        private const int SkillSlotCount = 6;
        private const int MainHandSkillSlot = 0;
        private const int OffHandSkillSlot = 1;
        private const int HelmetSkillSlot = 2;
        private const int BodyArmorSkillSlot = 3;
        private const int GlovesSkillSlot = 4;
        private const int BootsSkillSlot = 5;

        public event System.Action<int, SkillDataSO> OnSkillSlotUpdated;
        public event System.Action OnSkillsCanceled;

        private readonly Dictionary<int, SkillBehaviour> _activeSkills = new();
        private readonly Dictionary<int, (string Item, SkillDataSO Skill, int Index)> _skillKeys = new();
        private readonly Dictionary<(string Item, SkillDataSO Skill, int Index), SkillBehaviour.CooldownState> _storedCooldowns = new();

        [SerializeField] private Transform _skillContainer;

        private PlayerStats _playerStats;
        private bool _suppressSkillUsage;

        public bool IsAnySkillCasting
        {
            get
            {
                foreach (var pair in _activeSkills)
                {
                    if (pair.Value != null && pair.Value.IsCasting)
                        return true;
                }

                return false;
            }
        }

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
                InventoryManager.Instance.OnItemEquipped += HandleEquipmentChanged;
                InventoryManager.Instance.OnItemUnequipped += HandleEquipmentChanged;
                InventoryManager.Instance.OnWeaponPresetChanged += RefreshAllSkills;
                RefreshAllSkills();
            }

            if (CharacterPartyManager.Instance != null)
                CharacterPartyManager.Instance.OnActiveCharacterChanged += HandleActiveCharacterChanged;
        }

        private void OnDestroy()
        {
            // During owner/scene teardown the pool may also be going away. Destroy
            // projectiles directly so they are never reparented into a dying pool.
            SkillProjectile.DespawnAllForOwner(_playerStats, false);

            if (CharacterPartyManager.Instance != null)
                CharacterPartyManager.Instance.OnActiveCharacterChanged -= HandleActiveCharacterChanged;

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnItemEquipped -= HandleEquipmentChanged;
                InventoryManager.Instance.OnItemUnequipped -= HandleEquipmentChanged;
                InventoryManager.Instance.OnWeaponPresetChanged -= RefreshAllSkills;
            }
        }

        public void SetSkillUsageSuppressed(bool suppressed)
        {
            _suppressSkillUsage = suppressed;
        }

        public void CancelAllSkills()
        {
            CancelAllSkillsExcept(null);
            OnSkillsCanceled?.Invoke();
        }

        public void CancelAllSkillsExcept(SkillBehaviour except)
        {
            foreach (var pair in _activeSkills)
            {
                if (pair.Value != null && pair.Value != except)
                    pair.Value.Cancel();
            }
        }

        public void RefreshAllSkills()
        {
            // Launched projectiles have their own lifetime. Only replace skills whose
            // source changed, keeping unrelated skills and their running cooldowns intact.
            var expired = new List<(string Item, SkillDataSO Skill, int Index)>();
            foreach (var pair in _storedCooldowns)
                if (!pair.Value.IsRecoveringAt(Time.time)) expired.Add(pair.Key);
            foreach (var key in expired) _storedCooldowns.Remove(key);

            var desired = new (InventoryItem Item, int Index)[SkillSlotCount];
            var equipment = InventoryManager.Instance != null ? InventoryManager.Instance.EquipmentItems : null;
            GetHandSkills(equipment, desired);
            desired[HelmetSkillSlot] = (GetEquipmentItem(equipment, EquipmentSlot.Helmet), 0);
            desired[BodyArmorSkillSlot] = (GetEquipmentItem(equipment, EquipmentSlot.BodyArmor), 0);
            desired[GlovesSkillSlot] = (GetEquipmentItem(equipment, EquipmentSlot.Gloves), 0);
            desired[BootsSkillSlot] = (GetEquipmentItem(equipment, EquipmentSlot.Boots), 0);
            // Capture both hands before equipping either one: moving a weapon between
            // hands must carry its own cooldown rather than borrowing the other hand's.
            for (int i = 0; i < SkillSlotCount; i++)
            {
                var source = desired[i];
                var data = source.Item != null && source.Item.GrantedSkills.Count > source.Index
                    ? source.Item.GrantedSkills[source.Index] : null;
                if (data == null || !_skillKeys.TryGetValue(i, out var key)
                    || key != (source.Item.InstanceID, data, source.Index))
                    UnequipSkill(i);
            }
            for (int i = 0; i < SkillSlotCount; i++)
            {
                var source = desired[i];
                var data = source.Item != null && source.Item.GrantedSkills.Count > source.Index
                    ? source.Item.GrantedSkills[source.Index] : null;
                if (data == null)
                    UnequipSkill(i);
                else
                    EquipSkill(i, data, source.Item.InstanceID, source.Index);
            }
        }

        public void UseSkill(int slotIndex)
        {
            if (_suppressSkillUsage || GamePauseService.IsPaused)
                return;

            foreach (var active in _activeSkills.Values)
                if (active != null && active.IsCasting && active.Data?.Recipe?.Steps != null
                    && active.Data.Recipe.Steps.Exists(step => step?.StepDefinition?.Id == "FallingAnvil"))
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

        private void HandleActiveCharacterChanged(string _)
        {
            // Character changes still tear down the previous character's casts/projectiles.
            CancelAllSkills();
            _playerStats.GetComponent<PlayerLightningRodController>()?.End();
            _playerStats.GetComponent<PlayerFrostWallController>()?.End();
            _playerStats.GetComponent<PlayerPrismaticCycleController>()?.ResetCycles();
            SkillProjectile.DespawnAllForOwner(_playerStats);
            for (int i = 0; i < SkillSlotCount; i++)
                UnequipSkill(i);
            RefreshAllSkills();
        }

        private static void GetHandSkills(InventoryItem[] equipment, (InventoryItem Item, int Index)[] desired)
        {
            if (equipment == null)
                return;

            InventoryItem mainHandItem = GetEquipmentItem(equipment, EquipmentSlot.MainHand);
            InventoryItem offHandItem = GetEquipmentItem(equipment, EquipmentSlot.OffHand);

            if (mainHandItem != null && mainHandItem.GrantedSkills.Count > 0)
            {
                desired[MainHandSkillSlot] = (mainHandItem, 0);

                if (mainHandItem.Data is WeaponItemSO { IsTwoHanded: true } && mainHandItem.GrantedSkills.Count > 1)
                    desired[OffHandSkillSlot] = (mainHandItem, 1);
            }

            if (offHandItem != null && offHandItem.GrantedSkills.Count > 0)
                desired[OffHandSkillSlot] = (offHandItem, 0);
        }

        public void ReduceSkillCooldown(int slotIndex, float seconds)
        {
            if (seconds <= 0f)
                return;

            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                skillBehaviour.ReduceCooldownRemaining(seconds);
        }

        /// <summary>Reduces the remaining cooldown of one random skill that is currently recovering.</summary>
        public bool ReduceRandomSkillCooldown(float seconds, System.Func<int, int> pickIndex)
        {
            if (seconds <= 0f)
                return false;

            var recovering = new List<SkillBehaviour>(_activeSkills.Count);
            foreach (var pair in _activeSkills)
            {
                if (pair.Value != null && pair.Value.CooldownRemaining > 0f)
                    recovering.Add(pair.Value);
            }

            if (recovering.Count == 0)
                return false;

            int index = pickIndex != null ? pickIndex(recovering.Count) : Random.Range(0, recovering.Count);
            recovering[Mathf.Clamp(index, 0, recovering.Count - 1)].ReduceCooldownRemaining(seconds);
            return true;
        }

        public void AddSkillCooldown(int slotIndex, float seconds)
        {
            if (seconds <= 0f)
                return;

            if (_activeSkills.TryGetValue(slotIndex, out var skillBehaviour) && skillBehaviour != null)
                skillBehaviour.AddCooldownRemaining(seconds);
        }

        public void ReduceCooldownsExcept(int excludedSlotIndex, float seconds)
        {
            if (seconds <= 0f)
                return;

            foreach (var pair in _activeSkills)
            {
                if (pair.Key == excludedSlotIndex || pair.Value == null)
                    continue;

                pair.Value.ReduceCooldownRemaining(seconds);
            }
        }

        public void AddCooldownsExcept(int excludedSlotIndex, float seconds)
        {
            if (seconds <= 0f)
                return;

            foreach (var pair in _activeSkills)
            {
                if (pair.Key == excludedSlotIndex || pair.Value == null)
                    continue;

                pair.Value.AddCooldownRemaining(seconds);
            }
        }

        public void ReduceAllCooldowns(float seconds)
        {
            if (seconds <= 0f)
                return;

            foreach (var pair in _activeSkills)
            {
                if (pair.Value != null)
                    pair.Value.ReduceCooldownRemaining(seconds);
            }
        }

        public void AddAllCooldowns(float seconds)
        {
            if (seconds <= 0f)
                return;

            foreach (var pair in _activeSkills)
            {
                if (pair.Value != null)
                    pair.Value.AddCooldownRemaining(seconds);
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

        private void EquipSkill(int slotIndex, SkillDataSO skillData, string itemId, int skillIndex)
        {
            var key = (itemId, skillData, skillIndex);
            if (_activeSkills.TryGetValue(slotIndex, out var existing) && existing != null
                && _skillKeys.TryGetValue(slotIndex, out var existingKey) && existingKey == key)
                return;

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
                    behaviour.SetRuntimeSlot(this, slotIndex);
                    if (_storedCooldowns.TryGetValue(key, out var cooldown))
                    {
                        behaviour.RestoreCooldownState(cooldown);
                        _storedCooldowns.Remove(key);
                    }
                    _activeSkills[slotIndex] = behaviour;
                    _skillKeys[slotIndex] = key;
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
                {
                    if (_skillKeys.TryGetValue(slotIndex, out var key))
                    {
                        var cooldown = activeSkill.CaptureCooldownState();
                        if (cooldown.IsRecoveringAt(Time.time)) _storedCooldowns[key] = cooldown;
                        else _storedCooldowns.Remove(key);
                    }
                    activeSkill.Cancel();
                    if (Application.isPlaying) Destroy(activeSkill.gameObject);
                    else DestroyImmediate(activeSkill.gameObject);
                }

                _activeSkills.Remove(slotIndex);
                _skillKeys.Remove(slotIndex);
            }

            OnSkillSlotUpdated?.Invoke(slotIndex, null);
        }
    }
}
