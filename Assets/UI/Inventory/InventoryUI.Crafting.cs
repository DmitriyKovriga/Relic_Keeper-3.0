using Scripts.Inventory;
using Scripts.Items;
using UnityEngine;
using UnityEngine.UIElements;

public partial class InventoryUI
{
    private Button _toggleModeButton;

    private void OnRootPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode && evt.button == 0 && _capturedPointerId < 0)
        {
            _root.CapturePointer(evt.pointerId);
            _capturedPointerId = evt.pointerId;
        }
    }

    private void OnRootMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 1) return;
        VisualElement slot = evt.target as VisualElement;
        while (slot != null && !slot.ClassListContains("orb-slot")) slot = slot.parent;
        if (slot != null && TryEnterApplyOrbModeFromSlot(slot))
        {
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private void SetupEquipmentSlots()
    {
        _equipmentSlots.Clear();
        for (int i = 0; i < EquipmentSlotUxmlNames.Count; i++)
        {
            string name = EquipmentSlotUxmlNames.GetName((EquipmentSlot)i);
            if (string.IsNullOrEmpty(name)) continue;
            var slot = _root.Q<VisualElement>(name);
            if (slot != null)
            {
                slot.userData = InventoryManager.EQUIP_OFFSET + i;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                _equipmentSlots.Add(slot);
            }
            else
            {
                Debug.LogError($"[InventoryUI] Slot '{name}' was not found in UXML.");
            }
        }
    }

    private void LoadOrbSlotsConfig()
    {
        if (_orbSlotsConfig == null)
            _orbSlotsConfig = Resources.Load<CraftingOrbSlotsConfigSO>(ProjectPaths.ResourcesCraftingOrbSlotsConfig);
    }

    private void SetupTabs()
    {
        _equipmentView = _root.Q<VisualElement>("EquipmentView");
        _craftView = _root.Q<VisualElement>("CraftView");
        _toggleModeButton = _root.Q<Button>("ToggleModeButton");
        if (_equipmentView != null) _equipmentView.AddToClassList("hidden");
        if (_craftView != null) _craftView.RemoveFromClassList("visible");
        _currentTab = 0;
        if (_equipmentView != null) _equipmentView.RemoveFromClassList("hidden");

        if (_toggleModeButton != null)
        {
            _toggleModeButton.clicked += OnToggleModeClicked;
            UpdateToggleButtonLabel();
        }
    }

    private void OnToggleModeClicked()
    {
        SwitchTab(_currentTab == 0 ? 1 : 0);
    }

    private void UpdateToggleButtonLabel()
    {
        if (_toggleModeButton == null) return;
        _toggleModeButton.text = _currentTab == 0 ? "K" : "E";
        _toggleModeButton.tooltip = _currentTab == 0 ? "Craft" : "Equipment";
    }

    private void SwitchTab(int tab)
    {
        if (_currentTab == tab) return;
        _currentTab = tab;
        if (_equipmentView != null)
        {
            if (tab == 0) _equipmentView.RemoveFromClassList("hidden");
            else _equipmentView.AddToClassList("hidden");
        }
        if (_craftView != null)
        {
            if (tab == 1) _craftView.AddToClassList("visible");
            else _craftView.RemoveFromClassList("visible");
        }
        UpdateToggleButtonLabel();
        RefreshInventory();
    }

    private void SetupCraftView()
    {
        _craftSlot = _root.Q<VisualElement>("CraftSlot");
        _orbSlotsRow = _root.Q<VisualElement>("OrbSlotsRow");
        if (_craftSlot != null)
        {
            _craftSlot.userData = InventoryManager.CRAFT_SLOT_INDEX;
            _craftSlot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
            _craftSlot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
            _craftSlot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
        }
        if (_orbSlotsRow != null)
        {
            _orbSlotsRow.Clear();
            _orbSlots.Clear();
            int count = _orbSlotsConfig != null ? _orbSlotsConfig.SlotCount : 0;
            if (count == 0)
            {
                var hint = new Label(_orbSlotsConfig == null
                    ? "Orbs: create config in Crafting Orb Editor, then assign it to Inventory UI."
                    : "Orbs: assign orb assets to slots in Crafting Orb Editor and save config.");
                hint.style.fontSize = 9;
                hint.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _orbSlotsRow.Add(hint);
            }
            for (int i = 0; i < count; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("orb-slot");
                slot.userData = i;
                var countLabel = new Label { name = "OrbCount" };
                countLabel.AddToClassList("orb-count");
                slot.Add(countLabel);
                slot.RegisterCallback<PointerDownEvent>(OnOrbSlotPointerDown);
                slot.RegisterCallback<MouseDownEvent>(OnOrbSlotMouseDown);
                slot.RegisterCallback<PointerOverEvent>(OnOrbSlotPointerOver);
                slot.RegisterCallback<PointerOutEvent>(OnOrbSlotPointerOut);
                _orbSlotsRow.Add(slot);
                _orbSlots.Add((slot, countLabel));
            }
        }
    }

    private void OnOrbSlotPointerDown(PointerDownEvent evt)
    {
        if (evt.button != 1) return;
        if (TryEnterApplyOrbModeFromSlot(evt.currentTarget as VisualElement))
        {
            evt.StopPropagation();
        }
    }

    private void OnOrbSlotMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 1) return;
        if (TryEnterApplyOrbModeFromSlot(evt.currentTarget as VisualElement))
        {
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private bool TryEnterApplyOrbModeFromSlot(VisualElement slot)
    {
        if (_orbSlotsConfig == null || InventoryManager.Instance == null || slot?.userData == null) return false;
        int idx = (int)slot.userData;
        var orb = _orbSlotsConfig.GetOrbInSlot(idx);
        if (orb == null || string.IsNullOrEmpty(orb.ID)) return false;
        if (InventoryManager.Instance.GetOrbCount(orb.ID) <= 0) return false;
        EnterApplyOrbMode(orb, slot);
        return true;
    }

    private void OnOrbSlotPointerOver(PointerOverEvent evt)
    {
        if (_orbSlotsConfig == null) return;
        var slot = evt.currentTarget as VisualElement;
        if (slot?.userData == null) return;
        int idx = (int)slot.userData;
        var orb = _orbSlotsConfig.GetOrbInSlot(idx);
        if (orb != null && ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.ShowOrbTooltip(orb, slot);
    }

    private void OnOrbSlotPointerOut(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.HideTooltip();
    }

    private void EnterApplyOrbMode(CraftingOrbSO orb, VisualElement orbSlotElement)
    {
        _applyOrbMode = true;
        _suppressNextApplyOrbPointerUp = true;
        _applyOrbOrb = orb;
        _applyOrbSlotHighlight = orbSlotElement;
        orbSlotElement.AddToClassList("orb-slot-applying");
        _ghostIcon.style.backgroundImage = orb.Icon != null ? new StyleBackground(orb.Icon) : default;
        _ghostIcon.style.width = 32;
        _ghostIcon.style.height = 32;
        _ghostIcon.style.opacity = 0.85f;
        _ghostIcon.style.display = DisplayStyle.Flex;
        // Show the orb ghost immediately under the cursor, without waiting for the next move event.
        UpdateGhostPosition(GetPointerRootLocalFromScreen());
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();
    }

    private void ExitApplyOrbMode()
    {
        if (_capturedPointerId >= 0 && _root != null)
        {
            _root.ReleasePointer(_capturedPointerId);
            _capturedPointerId = -1;
        }
        _applyOrbMode = false;
        _suppressNextApplyOrbPointerUp = false;
        if (_applyOrbSlotHighlight != null)
        {
            _applyOrbSlotHighlight.RemoveFromClassList("orb-slot-applying");
            _applyOrbSlotHighlight = null;
        }
        _applyOrbOrb = null;
        _ghostIcon.style.display = DisplayStyle.None;
    }

    private bool TryApplyOrbOnPointerUp(Vector2 pointerPosition)
    {
        if (_applyOrbOrb == null || _craftSlot == null || InventoryManager.Instance == null) return false;
        if (!_craftSlot.worldBound.Contains(pointerPosition)) return false;

        var craftItem = InventoryManager.Instance.CraftingSlotItem;
        if (craftItem == null || !ItemGenerator.IsRare(craftItem)) return false;
        if (_applyOrbOrb.EffectId != CraftingOrbEffectId.RerollRare) return false;
        if (ItemGenerator.Instance == null) return false;

        if (!InventoryManager.Instance.ConsumeOrb(_applyOrbOrb.ID)) return false;
        ItemGenerator.Instance.RerollRare(craftItem);
        ExitApplyOrbMode();
        InventoryManager.Instance.TriggerUIUpdate();
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.RefreshCurrentItemTooltip();
        return true;
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape && _applyOrbMode)
        {
            ExitApplyOrbMode();
            evt.StopPropagation();
        }
    }
}
