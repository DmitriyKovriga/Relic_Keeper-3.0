using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument _uiDoc;
    
    private const int ROWS = 4;
    private const int COLUMNS = 10;
    private const float SLOT_SIZE = 24f; 

    private VisualElement _root;
    private VisualElement _inventoryContainer;
    private VisualElement _itemsLayer; 
    private VisualElement _ghostIcon; 
    
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    
    private bool _isDragging;
    private int _draggedSlotIndex = -1;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        if (_inventoryContainer == null) return;

        CreateGhostIcon();
        CreateTooltip(); 
        GenerateBackpackGrid();
        SetupEquipmentSlots();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
            RefreshInventory();
        }

        _root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
            
        _root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void CreateTooltip()
    {
        _tooltip = new VisualElement { name = "ItemTooltip" };
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.95f));
        _tooltip.style.borderBottomColor = _tooltip.style.borderTopColor = 
            _tooltip.style.borderLeftColor = _tooltip.style.borderRightColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        _tooltip.style.borderBottomWidth = _tooltip.style.borderTopWidth = 
            _tooltip.style.borderLeftWidth = _tooltip.style.borderRightWidth = 1;
        _tooltip.style.paddingLeft = _tooltip.style.paddingRight = 8;
        _tooltip.style.paddingTop = _tooltip.style.paddingBottom = 8;
        _tooltip.style.display = DisplayStyle.None;
        _tooltip.pickingMode = PickingMode.Ignore;
        _tooltipLabel = new Label();
        _tooltipLabel.style.color = new StyleColor(Color.white);
        _tooltipLabel.style.fontSize = 12;
        _tooltip.Add(_tooltipLabel);
        _root.Add(_tooltip);
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
        string[] slotNames = { "Slot_MainHand", "Slot_OffHand", "Slot_Helmet", "Slot_Body", "Slot_Gloves", "Slot_Boots" };
        for (int i = 0; i < slotNames.Length; i++)
        {
            var slot = _root.Q<VisualElement>(slotNames[i]);
            if (slot != null)
            {
                slot.userData = InventoryManager.EQUIP_OFFSET + i;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                _equipmentSlots.Add(slot);
            }
        }
    }

    private void GenerateBackpackGrid()
    {
        _inventoryContainer.Clear();
        _backpackSlots.Clear();
        int slotIndex = 0;
        for (int r = 0; r < ROWS; r++)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
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
        _itemsLayer = new VisualElement { name = "ItemsLayer" };
        _itemsLayer.style.position = Position.Absolute;
        _itemsLayer.StretchToParentSize();
        _itemsLayer.pickingMode = PickingMode.Ignore;
        _inventoryContainer.Add(_itemsLayer);
    }

    private void RefreshInventory()
    {
        if (_itemsLayer == null || InventoryManager.Instance == null) return;
        _itemsLayer.Clear();
        _itemsLayer.BringToFront(); 

        var backpackItems = InventoryManager.Instance.Items;
        for (int i = 0; i < backpackItems.Length; i++)
        {
            if (backpackItems[i] != null && backpackItems[i].Data != null)
            {
                VisualElement icon = CreateItemIcon(backpackItems[i]);
                icon.style.left = (i % COLUMNS) * SLOT_SIZE;
                icon.style.top = (i / COLUMNS) * SLOT_SIZE;
                _itemsLayer.Add(icon);
            }
        }

        var equipItems = InventoryManager.Instance.EquipmentItems;
        for (int i = 0; i < _equipmentSlots.Count; i++)
        {
            var slot = _equipmentSlots[i];
            slot.Query<VisualElement>(name: "ItemIcon").ForEach(img => img.RemoveFromHierarchy());
            if (i < equipItems.Length && equipItems[i] != null && equipItems[i].Data != null)
            {
                VisualElement icon = CreateItemIcon(equipItems[i]);
                icon.style.left = 0; icon.style.top = 0;
                slot.Add(icon);
            }
        }
    }

    private VisualElement CreateItemIcon(InventoryItem item)
    {
        // ИСПОЛЬЗУЕМ VisualElement + BackgroundImage (как в Drag)
        VisualElement icon = new VisualElement { name = "ItemIcon" };
        icon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        
        // ВАЖНО: Подсказка движку отрисовывать это как динамический спрайт
        icon.usageHints = UsageHints.DynamicTransform | UsageHints.GroupTransform;
        
        icon.style.width = item.Data.Width * SLOT_SIZE;
        icon.style.height = item.Data.Height * SLOT_SIZE;
        icon.style.position = Position.Absolute;
        
        // Гарантируем видимость
        icon.style.display = DisplayStyle.Flex;
        icon.style.visibility = Visibility.Visible;
        icon.style.opacity = 1.0f;
        
        icon.pickingMode = PickingMode.Ignore;
        return icon;
    }

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging || InventoryManager.Instance == null) return;
        VisualElement slot = evt.currentTarget as VisualElement;
        if (slot?.userData == null) return;
        InventoryItem item = InventoryManager.Instance.GetItemAt((int)slot.userData, out _);
        if (item != null && item.Data != null)
        {
            _tooltipLabel.text = $"<b>{item.Data.ItemName}</b>\n\n";
            var lines = item.GetDescriptionLines();
            if (lines != null) foreach (var line in lines) _tooltipLabel.text += line + "\n";
            _tooltip.style.display = DisplayStyle.Flex;
            UpdateTooltipPosition(evt.position);
        }
    }

    private void OnPointerOutSlot(PointerOutEvent evt) => _tooltip.style.display = DisplayStyle.None;

    private void UpdateTooltipPosition(Vector2 pointerPosition)
    {
        Vector2 localPos = _root.WorldToLocal(pointerPosition);
        _tooltip.style.left = localPos.x + 15;
        _tooltip.style.top = localPos.y + 15;
    }

    private void OnSlotPointerDown(PointerDownEvent evt)
    {
        if (_isDragging || InventoryManager.Instance == null) return;
        VisualElement slot = evt.currentTarget as VisualElement;
        InventoryItem item = InventoryManager.Instance.GetItemAt((int)slot.userData, out int anchorIndex);
        if (item == null) return;

        _tooltip.style.display = DisplayStyle.None; 
        _isDragging = true;
        _draggedSlotIndex = anchorIndex;
        _ghostIcon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        _ghostIcon.style.width = item.Data.Width * SLOT_SIZE;
        _ghostIcon.style.height = item.Data.Height * SLOT_SIZE;
        _ghostIcon.style.display = DisplayStyle.Flex;
        UpdateGhostPosition(evt.position);
        _root.CapturePointer(evt.pointerId);
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isDragging) UpdateGhostPosition(evt.position);
        if (_tooltip.style.display == DisplayStyle.Flex) UpdateTooltipPosition(evt.position);
    }

    private void UpdateGhostPosition(Vector2 pointerPosition)
    {
        Vector2 localPos = _root.WorldToLocal(pointerPosition);
        _ghostIcon.style.left = localPos.x - (_ghostIcon.resolvedStyle.width / 2);
        _ghostIcon.style.top = localPos.y - (_ghostIcon.resolvedStyle.height / 2);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging || InventoryManager.Instance == null) return;
        _isDragging = false;
        _ghostIcon.style.display = DisplayStyle.None;
        _root.ReleasePointer(evt.pointerId);

        InventoryItem draggedItem = InventoryManager.Instance.GetItem(_draggedSlotIndex);
        if (draggedItem == null) { RefreshInventory(); return; }

        Vector2 mousePosV2 = (Vector2)evt.position;
        VisualElement target = FindParentSlot(_root.panel.Pick(mousePosV2));
        int targetIndex = (target?.userData != null) ? (int)target.userData : -1;

        if (targetIndex == -1)
        {
            Vector2 offset = new Vector2((draggedItem.Data.Width * SLOT_SIZE) / 2f, (draggedItem.Data.Height * SLOT_SIZE) / 2f);
            VisualElement smart = FindParentSlot(_root.panel.Pick(mousePosV2 - offset + new Vector2(SLOT_SIZE / 2f, SLOT_SIZE / 2f)));
            if (smart?.userData != null) targetIndex = (int)smart.userData;
        }

        if (targetIndex != -1 && targetIndex != _draggedSlotIndex)
            InventoryManager.Instance.TryMoveOrSwap(_draggedSlotIndex, targetIndex);
        
        _draggedSlotIndex = -1;
        RefreshInventory();
    }

    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot")) target = target.parent;
        return target;
    }
}