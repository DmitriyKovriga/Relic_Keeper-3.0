using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument _uiDoc;
    [Tooltip("Если задано, при закрытии окна инвентаря сбрасывается режим крафта орбой.")]
    [SerializeField] private WindowView _windowView;
    [Header("Crafting")]
    [SerializeField] private CraftingOrbSlotsConfigSO _orbSlotsConfig;
    
    private const int ROWS = 4;
    private const int COLUMNS = 10;
    private const float SLOT_SIZE = 24f;

    /// <summary>Размеры слотов экипировки/крафта в пикселях (должны совпадать с USS: slot-2x2, slot-2x3, slot-2x4).</summary>
    private static readonly (float w, float h)[] EquipmentSlotSizes =
    {
        (48f, 48f),  // Helmet 2x2
        (48f, 72f),  // Body 2x3
        (48f, 96f),  // MainHand 2x4
        (48f, 96f),  // OffHand 2x4
        (48f, 48f),  // Gloves 2x2
        (48f, 48f),  // Boots 2x2
    };
    private const float CraftSlotWidth = 48f;
    private const float CraftSlotHeight = 96f; 

    private VisualElement _root;
    private VisualElement _inventoryContainer;
    private VisualElement _itemsLayer; 
    private VisualElement _ghostIcon;

    private VisualElement _equipmentView;
    private VisualElement _craftView;
    private VisualElement _craftSlot;
    private VisualElement _orbSlotsRow;
    private int _currentTab; // 0 = Equipment, 1 = Craft
    
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    private List<(VisualElement slot, Label countLabel)> _orbSlots = new List<(VisualElement, Label)>();
    
    private bool _isDragging;
    private int _draggedSlotIndex = -1;

    private bool _applyOrbMode;
    private CraftingOrbSO _applyOrbOrb;
    private VisualElement _applyOrbSlotHighlight;
    private int _capturedPointerId = -1;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        if (_inventoryContainer == null) { Debug.LogError("InventoryGrid not found"); return; }
        _inventoryContainer.style.overflow = Overflow.Visible;

        CreateGhostIcon();
        GenerateBackpackGrid();
        SetupEquipmentSlots();
        LoadOrbSlotsConfig();
        SetupTabs();
        SetupCraftView();

        if (InventoryManager.Instance == null)
            _root.schedule.Execute(TrySubscribe).Every(100).Until(() => InventoryManager.Instance != null);
        else
            TrySubscribe();

        _root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        _root.RegisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);

        if (_windowView == null) _windowView = GetComponent<WindowView>();
        if (_windowView != null) _windowView.OnClosed += OnInventoryWindowClosed;
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (_windowView != null) _windowView.OnClosed -= OnInventoryWindowClosed;
        ExitApplyOrbMode();

        _root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        _root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        _root.UnregisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);
    }

    private void OnInventoryWindowClosed()
    {
        ExitApplyOrbMode();
    }

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

    private void TrySubscribe()
    {
        if (InventoryManager.Instance == null) return;
        InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        RefreshInventory();
    }
    
    private void GenerateBackpackGrid()
    {
        if (_inventoryContainer == null) return;
        _inventoryContainer.Clear();
        _backpackSlots.Clear();

        _itemsLayer = new VisualElement { name = "ItemsLayer" };
        _itemsLayer.style.position = Position.Absolute;
        _itemsLayer.StretchToParentSize();
        _itemsLayer.pickingMode = PickingMode.Ignore;
        _itemsLayer.style.overflow = Overflow.Visible;
        _inventoryContainer.Add(_itemsLayer);

        int slotIndex = 0;
        for (int r = 0; r < ROWS; r++)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("inventory-row");
            _inventoryContainer.Add(row);

            for (int c = 0; c < COLUMNS; c++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("slot");
                slot.userData = slotIndex;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                row.Add(slot);
                _backpackSlots.Add(slot);
                slotIndex++;
            }
        }
    }

    private void RefreshInventory()
    {
        if (_itemsLayer == null || InventoryManager.Instance == null) return;
        _itemsLayer.style.display = DisplayStyle.None;
        _itemsLayer.Clear();
        foreach (var slot in _equipmentSlots) 
        {
            var oldImg = slot.Q<Image>();
            if (oldImg != null) slot.Remove(oldImg);
        }
        var items = InventoryManager.Instance.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].Data != null)
            {
                var icon = CreateItemIcon(items[i]);
                icon.style.left = (i % COLUMNS) * SLOT_SIZE;
                icon.style.top = (i / COLUMNS) * SLOT_SIZE;
                _itemsLayer.Add(icon);
                icon.MarkDirtyRepaint();
            }
        }
        if (_currentTab == 0)
            DrawEquipmentIcons();
        else
            DrawCraftSlotIcon();
        RefreshOrbSlots();
        _itemsLayer.style.display = DisplayStyle.Flex;
        _itemsLayer.BringToFront(); 
        _root.MarkDirtyRepaint();
    }

    private void DrawEquipmentIcons()
    {
        var equipItems = InventoryManager.Instance.EquipmentItems;
        
        for (int i = 0; i < equipItems.Length; i++)
        {
            var item = equipItems[i];
            
            int targetID = InventoryManager.EQUIP_OFFSET + i;
            VisualElement slot = _equipmentSlots.Find(s => (int)s.userData == targetID);

            if (slot != null && item != null && item.Data != null)
            {
                var icon = CreateItemIcon(item);
                float iconW = item.Data.Width * SLOT_SIZE;
                float iconH = item.Data.Height * SLOT_SIZE;
                float slotW = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].w : 48f;
                float slotH = i < EquipmentSlotSizes.Length ? EquipmentSlotSizes[i].h : 48f;
                icon.style.left = (slotW - iconW) * 0.5f;
                icon.style.top = (slotH - iconH) * 0.5f;
                icon.style.right = StyleKeyword.Null;
                icon.style.bottom = StyleKeyword.Null;
                slot.Add(icon);
            }
        }
    }

    private void DrawCraftSlotIcon()
    {
        if (_craftSlot == null) return;
        var oldImg = _craftSlot.Q<Image>();
        if (oldImg != null) _craftSlot.Remove(oldImg);
        var item = InventoryManager.Instance.CraftingSlotItem;
        if (item != null && item.Data != null)
        {
            var icon = CreateItemIcon(item);
            float iconW = item.Data.Width * SLOT_SIZE;
            float iconH = item.Data.Height * SLOT_SIZE;
            icon.style.left = (CraftSlotWidth - iconW) * 0.5f;
            icon.style.top = (CraftSlotHeight - iconH) * 0.5f;
            icon.style.right = StyleKeyword.Null;
            icon.style.bottom = StyleKeyword.Null;
            _craftSlot.Add(icon);
        }
    }

    private void RefreshOrbSlots()
    {
        if (InventoryManager.Instance == null) return;
        for (int i = 0; i < _orbSlots.Count; i++)
        {
            var (slot, countLabel) = _orbSlots[i];
            var orb = _orbSlotsConfig != null ? _orbSlotsConfig.GetOrbInSlot(i) : null;
            int count = orb != null ? InventoryManager.Instance.GetOrbCount(orb.ID) : 0;
            slot.style.backgroundImage = (orb != null && orb.Icon != null) ? new StyleBackground(orb.Icon) : default;
            countLabel.text = count.ToString();
            countLabel.style.visibility = Visibility.Visible;
        }
    }

    private VisualElement CreateItemIcon(InventoryItem item)
    {
        Image icon = new Image();
        icon.sprite = item.Data.Icon;
        icon.style.width = item.Data.Width * SLOT_SIZE;
        icon.style.height = item.Data.Height * SLOT_SIZE;
        icon.style.position = Position.Absolute;
        icon.pickingMode = PickingMode.Ignore;
        if (item.Data.Icon != null) icon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        return icon;
    }

    private void CreateGhostIcon()
    {
        _ghostIcon = new VisualElement { name = "GhostIcon" };
        _ghostIcon.style.position = Position.Absolute;
        _ghostIcon.style.display = DisplayStyle.None; 
        _ghostIcon.pickingMode = PickingMode.Ignore; 
        _ghostIcon.style.opacity = 0.7f; 
        _root.Add(_ghostIcon);
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
                Debug.LogError($"[InventoryUI] Слот '{name}' не найден в UXML!");
            }
        }
    }

    private void LoadOrbSlotsConfig()
    {
        if (_orbSlotsConfig == null)
            _orbSlotsConfig = Resources.Load<CraftingOrbSlotsConfigSO>(ProjectPaths.ResourcesCraftingOrbSlotsConfig);
    }

    private Button _toggleModeButton;

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
        _toggleModeButton.tooltip = _currentTab == 0 ? "Крафт" : "Экипировка";
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
                    ? "Сферы: Tools → Crafting Orb Editor → Create config. Либо перетащите конфиг в поле Orb Slots Config у Inventory UI."
                    : "Сферы: в Crafting Orb Editor назначьте орбы в слоты и Save config.");
                hint.style.fontSize = 9;
                hint.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                _orbSlotsRow.Add(hint);
            }
            for (int i = 0; i < count; i++)
            {
                var orb = _orbSlotsConfig.GetOrbInSlot(i);
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
        TryEnterApplyOrbModeFromSlot(evt.currentTarget as VisualElement);
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
        _applyOrbOrb = orb;
        _applyOrbSlotHighlight = orbSlotElement;
        orbSlotElement.AddToClassList("orb-slot-applying");
        _ghostIcon.style.backgroundImage = orb.Icon != null ? new StyleBackground(orb.Icon) : default;
        _ghostIcon.style.width = 32;
        _ghostIcon.style.height = 32;
        _ghostIcon.style.display = DisplayStyle.Flex;
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

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging || InventoryManager.Instance == null) return;
        VisualElement hoveredSlot = evt.currentTarget as VisualElement;
        if (hoveredSlot == null || hoveredSlot.userData == null) return;

        InventoryItem item = InventoryManager.Instance.GetItemAt((int)hoveredSlot.userData, out int anchorIndex);
        
        if (item != null && item.Data != null && ItemTooltipController.Instance != null)
        {
            VisualElement anchorSlot = GetSlotVisual(anchorIndex);
            if (anchorSlot == null) anchorSlot = hoveredSlot;
            ItemTooltipController.Instance.ShowTooltip(item, anchorSlot);
        }
    }

    private VisualElement GetSlotVisual(int index)
    {
        if (index == InventoryManager.CRAFT_SLOT_INDEX)
            return _craftSlot;
        if (index >= InventoryManager.EQUIP_OFFSET)
            return _equipmentSlots.Find(s => (int)s.userData == index);
        if (index >= 0 && index < _backpackSlots.Count)
            return _backpackSlots[index];
        return null;
    }

    private void OnPointerOutSlot(PointerOutEvent evt)
    {
        if (ItemTooltipController.Instance != null)
            ItemTooltipController.Instance.HideTooltip();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isDragging)
            UpdateGhostPosition(evt.position);
        else if (_applyOrbMode)
            UpdateGhostPosition(evt.position);
    }
    
    private void OnSlotPointerDown(PointerDownEvent evt)
    {
        if (_applyOrbMode) return;
        if (evt.button != 0) return;

        VisualElement slot = evt.currentTarget as VisualElement;
        int idx = (int)slot.userData;
        InventoryItem item = InventoryManager.Instance.GetItemAt(idx, out int anchorIdx);
        if (item == null) return;

        _isDragging = true;
        _draggedSlotIndex = anchorIdx;
        _ghostIcon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        _ghostIcon.style.width = item.Data.Width * SLOT_SIZE;
        _ghostIcon.style.height = item.Data.Height * SLOT_SIZE;
        _ghostIcon.style.display = DisplayStyle.Flex;
        
        if (ItemTooltipController.Instance != null) ItemTooltipController.Instance.HideTooltip();

        UpdateGhostPosition(evt.position);
        _root.CapturePointer(evt.pointerId);
    }

    private void UpdateGhostPosition(Vector2 pos)
    {
        Vector2 localPos = _root.WorldToLocal(pos);
        _ghostIcon.style.left = localPos.x - (_ghostIcon.resolvedStyle.width / 2);
        _ghostIcon.style.top = localPos.y - (_ghostIcon.resolvedStyle.height / 2);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_applyOrbMode)
        {
            if (evt.button == 0)
            {
                bool applied = TryApplyOrbOnPointerUp(evt.position);
                if (!applied) ExitApplyOrbMode();
            }
            return;
        }

        if (!_isDragging) return;

        // 1. Где сейчас наш призрак?
        Vector2 ghostCenter = _ghostIcon.worldBound.center;
        int foundIndex = -1;

        if (_currentTab == 1 && _craftSlot != null && _craftSlot.worldBound.Contains(ghostCenter))
        {
            foundIndex = InventoryManager.CRAFT_SLOT_INDEX;
        }
        else if (_currentTab == 0)
        {
            foreach (var slot in _equipmentSlots)
            {
                if (slot.worldBound.Contains(ghostCenter))
                {
                    if (slot.userData != null) foundIndex = (int)slot.userData;
                    break;
                }
            }
        }

        if (foundIndex == -1)
        {
            int gridIndex = GetSmartTargetIndex(); 
            if (gridIndex != -1 && gridIndex < InventoryManager.EQUIP_OFFSET)
                foundIndex = gridIndex;
        }

        // --- ЗАВЕРШЕНИЕ ---
        _isDragging = false;
        _ghostIcon.style.display = DisplayStyle.None;
        _root.ReleasePointer(evt.pointerId);

        if (InventoryManager.Instance != null && foundIndex != -1)
        {
            InventoryManager.Instance.TryMoveOrSwap(_draggedSlotIndex, foundIndex);
        }

        _draggedSlotIndex = -1;
        RefreshInventory();
    }

    private int GetSmartTargetIndex()
    {
        Rect ghostWorld = _ghostIcon.worldBound;
        Vector2 localPos = _itemsLayer.WorldToLocal(new Vector2(ghostWorld.x, ghostWorld.y));

        int col = Mathf.RoundToInt(localPos.x / SLOT_SIZE);
        int row = Mathf.RoundToInt(localPos.y / SLOT_SIZE);

        if (col < 0 || col >= COLUMNS || row < 0 || row >= ROWS)
        {
            // Fallback: проверяем прямой луч под мышкой, если GhostIcon улетел странно
            Vector2 mousePos = _ghostIcon.worldBound.center;
            VisualElement picked = _root.panel.Pick(mousePos);
            VisualElement slot = FindParentSlot(picked);
            
            if (slot != null && slot.userData != null)
            {
                int idx = (int)slot.userData;
                if (idx >= InventoryManager.EQUIP_OFFSET) return idx;
            }
            return -1; 
        }

        return (row * COLUMNS) + col;
    }

    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot")) target = target.parent;
        return target;
    }
}