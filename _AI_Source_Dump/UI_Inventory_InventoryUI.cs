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

    // ... [Здесь методы GenerateBackpackGrid, RefreshInventory, DrawEquipmentIcons, CreateItemIcon - БЕЗ ИЗМЕНЕНИЙ] ...
    // Вставь их сюда из прошлого кода, они не менялись
    
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
            if (equipItems[i] == null || equipItems[i].Data == null) continue;
            int targetID = InventoryManager.EQUIP_OFFSET + i;
            VisualElement slot = _equipmentSlots.Find(s => (int)s.userData == targetID);
            if (slot != null)
            {
                var icon = CreateItemIcon(equipItems[i]);
                icon.style.left = 0;
                icon.style.top = 0;
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
        
        // ВАЖНО: Порядок должен строго соответствовать Enum EquipmentSlot!
        // 0: MainHand, 1: OffHand, 2: Helmet, 3: BodyArmor, 4: Gloves, 5: Boots
        string[] slotNames = { 
            "Slot_MainHand", 
            "Slot_OffHand", 
            "Slot_Helmet", 
            "Slot_Body",   // Убедись что в UXML он называется Slot_Body или Slot_BodyArmor
            "Slot_Gloves", 
            "Slot_Boots" 
        };

        for (int i = 0; i < slotNames.Length; i++)
        {
            var slot = _root.Q<VisualElement>(slotNames[i]);
            if (slot != null)
            {
                // userData = 100, 101, 102...
                slot.userData = InventoryManager.EQUIP_OFFSET + i;
                
                slot.RegisterCallback<PointerDownEvent>(OnSlotPointerDown);
                slot.RegisterCallback<PointerOverEvent>(OnPointerOverSlot);
                slot.RegisterCallback<PointerOutEvent>(OnPointerOutSlot);
                _equipmentSlots.Add(slot);
            }
            else
            {
                Debug.LogError($"[InventoryUI] Слот '{slotNames[i]}' не найден в UXML! Проверь имена.");
            }
        }
    }

    // --- ОБНОВЛЕННЫЕ ИВЕНТЫ ---

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging || InventoryManager.Instance == null) return;
        VisualElement hoveredSlot = evt.currentTarget as VisualElement;
        if (hoveredSlot == null || hoveredSlot.userData == null) return;

        // 1. Получаем предмет и индекс его ЯКОРЯ (верхний левый угол)
        InventoryItem item = InventoryManager.Instance.GetItemAt((int)hoveredSlot.userData, out int anchorIndex);
        
        if (item != null && item.Data != null && ItemTooltipController.Instance != null)
        {
            // 2. Находим визуальный элемент именно якорного слота
            VisualElement anchorSlot = GetSlotVisual(anchorIndex);
            
            // (На всякий случай фолбек, если не нашли)
            if (anchorSlot == null) anchorSlot = hoveredSlot;

            // 3. Передаем в тултип именно ЯКОРЬ
            ItemTooltipController.Instance.ShowTooltip(item, anchorSlot);
        }
    }

    private VisualElement GetSlotVisual(int index)
    {
        // Если это экипировка (100+)
        if (index >= InventoryManager.EQUIP_OFFSET)
        {
            return _equipmentSlots.Find(s => (int)s.userData == index);
        }
        
        // Если это рюкзак
        if (index >= 0 && index < _backpackSlots.Count)
        {
            return _backpackSlots[index];
        }
        
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
        // ВАЖНО: Мы убрали вызов MoveTooltip. Тултип теперь статичен.
    }

    // --- DRAG & DROP (Без изменений) ---
    
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

    Debug.Log($"[DEBUG] --- START DROP ---");
    
    // 1. Где сейчас наш призрак (иконка)?
    Rect ghostRect = _ghostIcon.worldBound;
    Vector2 ghostCenter = ghostRect.center;
    Debug.Log($"[DEBUG] Ghost Icon: Center={ghostCenter}, Rect={ghostRect}");

    // 2. Проверяем список слотов экипировки
    Debug.Log($"[DEBUG] Checking {_equipmentSlots.Count} equipment slots...");
    
    int foundIndex = -1;

    foreach (var slot in _equipmentSlots)
    {
        // Получаем данные слота
        Rect slotRect = slot.worldBound;
        string slotName = slot.name;
        int slotID = (slot.userData != null) ? (int)slot.userData : -999;

        // Проверяем попадание (содержит ли прямоугольник слота центр иконки)
        bool hit = slotRect.Contains(ghostCenter);
        
        // ЛОГ ПО КАЖДОМУ СЛОТУ
        Debug.Log($"[DEBUG] Slot '{slotName}' (ID: {slotID}): Rect={slotRect} | Contains GhostCenter? {hit}");

        if (hit)
        {
            foundIndex = slotID;
            Debug.Log($"[DEBUG] >>> HIT MATCH! Found slot: {slotName}");
            // Не делаем break, чтобы увидеть, нет ли перекрытий (вдруг два слота в одном месте)
        }
    }

    // Если не нашли в экипировке, проверяем сетку (кратко)
    if (foundIndex == -1)
    {
        Debug.Log("[DEBUG] Not in equipment. Checking Grid logic...");
        // Твоя логика сетки...
        int gridIndex = GetSmartTargetIndex(); 
        if (gridIndex != -1 && gridIndex < InventoryManager.EQUIP_OFFSET)
        {
            foundIndex = gridIndex;
            Debug.Log($"[DEBUG] Found in Grid: Index {foundIndex}");
        }
    }

    Debug.Log($"[DEBUG] Final Target Index: {foundIndex}");
    Debug.Log($"[DEBUG] --- END DROP ---");

    // --- СТАНДАРТНАЯ ЛОГИКА ЗАВЕРШЕНИЯ ---
    _isDragging = false;
    _ghostIcon.style.display = DisplayStyle.None;
    _root.ReleasePointer(evt.pointerId);

    if (InventoryManager.Instance != null && foundIndex != -1)
    {
        // Пытаемся переместить
        InventoryManager.Instance.TryMoveOrSwap(_draggedSlotIndex, foundIndex);
    }
    else
    {
         Debug.Log("[DEBUG] Drop cancelled: Invalid target.");
    }

    _draggedSlotIndex = -1;
    RefreshInventory();
}

    /// <summary>
    /// Вычисляет индекс слота, основываясь на положении GhostIcon.
    /// Это делает перетаскивание "магнитным" и интуитивным.
    /// </summary>
    private int GetSmartTargetIndex()
    {
        // Получаем мировые координаты призрака
        Rect ghostWorld = _ghostIcon.worldBound;
        
        // Переводим верхний левый угол призрака в локальные координаты СЛОЯ ПРЕДМЕТОВ
        // (ItemsLayer совпадает по координатам с сеткой)
        Vector2 localPos = _itemsLayer.WorldToLocal(new Vector2(ghostWorld.x, ghostWorld.y));

        // Рассчитываем примерную колонку и строку
        // Используем Mathf.RoundToInt для "примагничивания" к ближайшей ячейке
        int col = Mathf.RoundToInt(localPos.x / SLOT_SIZE);
        int row = Mathf.RoundToInt(localPos.y / SLOT_SIZE);

        // Проверяем границы сетки (чтобы не утащить за пределы)
        if (col < 0 || col >= COLUMNS || row < 0 || row >= ROWS)
        {
            // Возможно, игрок навел на экипировку?
            // Пробуем старый метод Raycast только для экипировки (так как она вне сетки)
            Vector2 mousePos = _ghostIcon.worldBound.center; // Берем центр предмета
            VisualElement picked = _root.panel.Pick(mousePos);
            VisualElement slot = FindParentSlot(picked);
            
            if (slot != null && slot.userData != null)
            {
                int idx = (int)slot.userData;
                // Если это слот экипировки
                if (idx >= InventoryManager.EQUIP_OFFSET) return idx;
            }
            
            return -1; // Мимо всего
        }

        // Превращаем Row/Col в линейный индекс
        return (row * COLUMNS) + col;
    }

    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot")) target = target.parent;
        return target;
    }
}