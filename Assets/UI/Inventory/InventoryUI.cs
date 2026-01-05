using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items; // Для EquipmentSlot enum

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
    private List<VisualElement> _equipmentSlots = new List<VisualElement>(); // Список слотов экипировки
    
    private bool _isDragging;
    private int _draggedSlotIndex = -1;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        if (_inventoryContainer == null) return;

        CreateGhostIcon();
        GenerateBackpackGrid();
        SetupEquipmentSlots(); // <-- НОВЫЙ МЕТОД

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
            RefreshInventory();
        }
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
    }

    // --- НОВОЕ: Настройка слотов экипировки ---
    private void SetupEquipmentSlots()
    {
        _equipmentSlots.Clear();
        
        // Ищем слоты по именам, которые ты задал в UXML
        // Порядок важен и должен совпадать с Enum: Head, Body, Main, Off, Gloves, Boots
        string[] slotNames = { 
            "Slot_MainHand", // Index 0
            "Slot_OffHand",  // Index 1
            "Slot_Helmet",   // Index 2
            "Slot_Body",     // Index 3 (В UXML он Slot_Body, а в Enum - BodyArmor, это норм, главное позиция в массиве)
            "Slot_Gloves",   // Index 4
            "Slot_Boots"     // Index 5
        };

        for (int i = 0; i < slotNames.Length; i++)
        {
            var slot = _root.Q<VisualElement>(slotNames[i]);
            if (slot != null)
            {
                slot.userData = InventoryManager.EQUIP_OFFSET + i;
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                _equipmentSlots.Add(slot);
            }
            else
            {
                Debug.LogError($"InventoryUI: Equipment slot '{slotNames[i]}' not found in UXML!");
            }
        }
    }
    // ---------------------------------------------

    private void CreateGhostIcon()
    {
        _ghostIcon = new VisualElement();
        _ghostIcon.style.position = Position.Absolute;
        _ghostIcon.style.width = SLOT_SIZE; 
        _ghostIcon.style.height = SLOT_SIZE;
        _ghostIcon.style.display = DisplayStyle.None; 
        _ghostIcon.pickingMode = PickingMode.Ignore; 
        _ghostIcon.style.opacity = 0.7f; 
        _root.Add(_ghostIcon);
    }

    private void GenerateBackpackGrid()
    {
        _inventoryContainer.Clear();
        _backpackSlots.Clear();

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
                slot.AddToClassList("slot-1x1");
                slot.userData = slotIndex; 
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                
                row.Add(slot); 
                _backpackSlots.Add(slot); 
                slotIndex++;
            }
        }

        // Layer for Backpack Items
        _itemsLayer = new VisualElement();
        _itemsLayer.style.position = Position.Absolute;
        
        // --- ИСПРАВЛЕНИЕ ЗДЕСЬ ---
        _itemsLayer.style.top = 0;
        _itemsLayer.style.left = 0;
        _itemsLayer.style.right = 0;
        _itemsLayer.style.bottom = 0;
        // -------------------------

        _itemsLayer.pickingMode = PickingMode.Ignore;
        _inventoryContainer.Add(_itemsLayer);
        
        _root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
    }

    private void RefreshInventory()
    {
        // 1. Рисуем РЮКЗАК (в _itemsLayer)
        _itemsLayer.Clear();
        var backpackItems = InventoryManager.Instance.Items;

        for (int i = 0; i < backpackItems.Length; i++)
        {
            if (backpackItems[i] != null && backpackItems[i].Data != null)
            {
                InventoryItem item = backpackItems[i];
                Image icon = CreateItemIcon(item);

                // Позиция в сетке
                int row = i / COLUMNS;
                int col = i % COLUMNS;
                icon.style.left = col * SLOT_SIZE;
                icon.style.top = row * SLOT_SIZE;

                _itemsLayer.Add(icon);
            }
        }

        // 2. Рисуем ЭКИПИРОВКУ (прямо внутри слотов)
        // Для экипировки мы не используем отдельный слой, так как слоты разбросаны.
        // Мы кладем иконку прямо в VisualElement слота.
        var equipItems = InventoryManager.Instance.EquipmentItems;
        
        for (int i = 0; i < _equipmentSlots.Count; i++)
        {
            var slot = _equipmentSlots[i];
            slot.Clear(); // Чистим слот

            // Восстанавливаем Label (опционально, если хочешь видеть текст "Helm" когда пусто)
             // ... можно пропустить для простоты, или добавить Label обратно если item == null

            if (i < equipItems.Length && equipItems[i] != null && equipItems[i].Data != null)
            {
                InventoryItem item = equipItems[i];
                Image icon = CreateItemIcon(item);
                
                // В слотах экипировки позиция 0,0, иконка растягивается или центрируется
                // Но у нас предметы большие (2x4).
                // В экипировке мы хотим, чтобы иконка вписалась в слот или отображалась целиком?
                // Обычно в слоте экипировки иконка отображается "как есть" (большая), 
                // просто слот должен быть достаточно большим.
                
                icon.style.position = Position.Absolute;
                icon.style.left = 0; 
                icon.style.top = 0;
                
                // Размеры иконки. В экипировке предмет тоже должен иметь свой размер?
                // Или быть сжат?
                // Давай пока оставим реальный размер. Твои слоты в UXML (2x4 для оружия) уже правильного размера.
                icon.style.width = item.Data.Width * SLOT_SIZE;
                icon.style.height = item.Data.Height * SLOT_SIZE;

                slot.Add(icon);
            }
            else
            {
                // Если пусто - можно добавить заглушку-текст
                Label lbl = new Label(GetSlotName(i));
                lbl.style.fontSize = 8;
                lbl.style.color = new StyleColor(new Color(1,1,1,0.2f));
                slot.Add(lbl);
            }
        }
    }

    private Image CreateItemIcon(InventoryItem item)
    {
        Image icon = new Image();
        icon.sprite = item.Data.Icon;
        icon.pickingMode = PickingMode.Ignore;
        icon.style.width = item.Data.Width * SLOT_SIZE;
        icon.style.height = item.Data.Height * SLOT_SIZE;
        icon.style.position = Position.Absolute;
        return icon;
    }

    private string GetSlotName(int index)
    {
        switch(index) {
            case 0: return "Main"; // MainHand
            case 1: return "Off";  // OffHand
            case 2: return "Head"; // Helmet
            case 3: return "Body"; // BodyArmor
            case 4: return "Hand"; // Gloves
            case 5: return "Feet"; // Boots
            default: return "";
        }
    }

    // --- DRAG AND DROP ---

    private void OnSlotPointerDown(PointerDownEvent evt)
    {
        if (_isDragging) return;

        VisualElement slot = evt.currentTarget as VisualElement;
        int clickedSlotIndex = (int)slot.userData; // Это может быть 0-39 ИЛИ 100-105

        // GetItemAt теперь сам разберется, рюкзак это или экипировка
        InventoryItem item = InventoryManager.Instance.GetItemAt(clickedSlotIndex, out int anchorIndex);

        if (item == null) return;

        _isDragging = true;
        _draggedSlotIndex = anchorIndex; // Запоминаем ID (0-39 или 100+)

        // Настройка призрака
        _ghostIcon.style.backgroundImage = new StyleBackground(item.Data.Icon);
        float w = item.Data.Width * SLOT_SIZE;
        float h = item.Data.Height * SLOT_SIZE;
        _ghostIcon.style.width = w; 
        _ghostIcon.style.height = h;

        // Позиция
        Vector2 localPos = _root.WorldToLocal(evt.position);
        _ghostIcon.style.left = localPos.x - (w / 2);
        _ghostIcon.style.top = localPos.y - (h / 2);
        _ghostIcon.style.display = DisplayStyle.Flex;

        _root.CapturePointer(evt.pointerId);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging) return;

        _isDragging = false;
        _ghostIcon.style.display = DisplayStyle.None;
        _root.ReleasePointer(evt.pointerId);
        
        InventoryItem draggedItem = InventoryManager.Instance.GetItem(_draggedSlotIndex);
        if (draggedItem == null) { RefreshInventory(); return; }

        // --- НОВАЯ ЛОГИКА ПРИЦЕЛИВАНИЯ ---
        
        // 1. Сначала проверяем, что находится ПРЯМО ПОД МЫШКОЙ (Приоритет для Экипировки)
        Vector2 mousePos = evt.position;
        VisualElement targetUnderMouse = _root.panel.Pick(mousePos);
        VisualElement slotUnderMouse = FindParentSlot(targetUnderMouse);

        int targetIndex = -1;

        // Если под мышкой слот ЭКИПИРОВКИ (>= 100), используем его
        if (slotUnderMouse != null && slotUnderMouse.userData != null)
        {
            int idx = (int)slotUnderMouse.userData;
            if (idx >= InventoryManager.EQUIP_OFFSET)
            {
                targetIndex = idx;
            }
        }

        // 2. Если под мышкой НЕ экипировка, используем "Умную Точку" (для Рюкзака)
        if (targetIndex == -1)
        {
            float itemW = draggedItem.Data.Width * SLOT_SIZE;
            float itemH = draggedItem.Data.Height * SLOT_SIZE;
            
            // Смещение к левому верхнему углу
            Vector2 topLeft = mousePos - new Vector2(itemW / 2, itemH / 2);
            // Чуть-чуть внутрь первой клетки
            Vector2 smartPoint = topLeft + new Vector2(SLOT_SIZE / 2, SLOT_SIZE / 2);

            VisualElement targetSmart = _root.panel.Pick(smartPoint);
            VisualElement slotSmart = FindParentSlot(targetSmart);

            if (slotSmart != null && slotSmart.userData != null)
            {
                targetIndex = (int)slotSmart.userData;
            }
        }

        // --- ИТОГ: Пробуем переместить ---
        if (targetIndex != -1 && targetIndex != _draggedSlotIndex)
        {
            bool success = InventoryManager.Instance.TryMoveOrSwap(_draggedSlotIndex, targetIndex);
            if (!success) Debug.Log("Действие невозможно");
        }
        
        _draggedSlotIndex = -1;
        RefreshInventory();
    }
    
    // Вспомогательный метод поиска слота
    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot"))
        {
            target = target.parent;
        }
        return target;
    }
    
    // PointerMove без изменений
    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging) return;
        Vector2 localPos = _root.WorldToLocal(evt.position);
        float w = _ghostIcon.resolvedStyle.width;
        float h = _ghostIcon.resolvedStyle.height;
        _ghostIcon.style.left = localPos.x - (w / 2);
        _ghostIcon.style.top = localPos.y - (h / 2);
    }
}