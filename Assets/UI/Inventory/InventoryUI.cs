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
    
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    
    private bool _isDragging;
    private int _draggedSlotIndex = -1;

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

        if (InventoryManager.Instance == null)
            _root.schedule.Execute(TrySubscribe).Every(100).Until(() => InventoryManager.Instance != null);
        else
            TrySubscribe();

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
        DrawEquipmentIcons();
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
            
            // Ищем UI слот
            int targetID = InventoryManager.EQUIP_OFFSET + i;
            VisualElement slot = _equipmentSlots.Find(s => (int)s.userData == targetID);

            if (slot != null && item != null && item.Data != null)
            {
                var icon = CreateItemIcon(item);
                
                // Сброс позиций (на случай если Absolute сломал верстку)
                icon.style.left = 0; 
                icon.style.top = 0;
                icon.style.right = StyleKeyword.Null;
                icon.style.bottom = StyleKeyword.Null;
                
                slot.Add(icon);
            }
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
        
        // 0: Head, 1: Body, 2: Main, 3: Off, 4: Gloves, 5: Boots
        string[] slotNames = { 
            "Slot_Helmet",      
            "Slot_Body",        
            "Slot_MainHand",    
            "Slot_OffHand",     
            "Slot_Gloves",      
            "Slot_Boots"        
        };

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
            else
            {
                Debug.LogError($"[InventoryUI] Слот '{slotNames[i]}' не найден в UXML!");
            }
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
        {
            UpdateGhostPosition(evt.position);
        }
    }
    
    private void OnSlotPointerDown(PointerDownEvent evt)
    {
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
        if (!_isDragging) return;

        // 1. Где сейчас наш призрак?
        Vector2 ghostCenter = _ghostIcon.worldBound.center;
        int foundIndex = -1;

        // 2. Проверяем список слотов экипировки
        foreach (var slot in _equipmentSlots)
        {
            if (slot.worldBound.Contains(ghostCenter))
            {
                if (slot.userData != null) foundIndex = (int)slot.userData;
                break;
            }
        }

        // Если не нашли в экипировке, проверяем сетку
        if (foundIndex == -1)
        {
            int gridIndex = GetSmartTargetIndex(); 
            if (gridIndex != -1 && gridIndex < InventoryManager.EQUIP_OFFSET)
            {
                foundIndex = gridIndex;
            }
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