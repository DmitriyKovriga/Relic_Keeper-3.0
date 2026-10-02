using System;
using Scripts.Items;
using Scripts.Skills;
using UnityEngine;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public const int WeaponPresetCount = 2;

        public int ActiveWeaponPreset { get; private set; }

        private InventoryItem _storedMainHand;
        private InventoryItem _storedOffHand;

        public event Action OnWeaponPresetChanged;

        public void SetActiveWeaponPreset(int index)
        {
            int next = Mathf.Clamp(index, 0, WeaponPresetCount - 1);
            if (next == ActiveWeaponPreset)
                return;

            SwapWeaponPreset();
        }

        public bool SwapWeaponPreset()
        {
            var skills = FindFirstObjectByType<PlayerSkillManager>();
            if (skills != null && skills.IsAnySkillCasting)
                return false;

            int main = (int)EquipmentSlot.MainHand;
            int off = (int)EquipmentSlot.OffHand;
            InventoryItem activeMain = EquipmentItems[main];
            InventoryItem activeOff = EquipmentItems[off];
            InventoryItem nextMain = _storedMainHand;
            InventoryItem nextOff = _storedOffHand;

            if (activeMain != null)
                OnItemUnequipped?.Invoke(activeMain);
            if (activeOff != null)
                OnItemUnequipped?.Invoke(activeOff);

            EquipmentItems[main] = nextMain;
            EquipmentItems[off] = nextOff;
            _storedMainHand = activeMain;
            _storedOffHand = activeOff;
            ActiveWeaponPreset = 1 - ActiveWeaponPreset;

            if (nextMain != null)
                OnItemEquipped?.Invoke(nextMain);
            if (nextOff != null)
                OnItemEquipped?.Invoke(nextOff);

            OnWeaponPresetChanged?.Invoke();
            OnInventoryChanged?.Invoke();
            return true;
        }

        private void ClearStoredWeaponPreset()
        {
            _storedMainHand = null;
            _storedOffHand = null;
            ActiveWeaponPreset = 0;
        }

        private void Update()
        {
            var actions = InputManager.InputActions;
            if (actions == null)
                return;

            var swap = actions.asset.FindActionMap("Player", false)?.FindAction("WeaponSwap", false);
            if (swap != null && swap.WasPressedThisFrame())
                SwapWeaponPreset();
        }
    }
}
