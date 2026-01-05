using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

public class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument _uiDoc;
    
    // Настройки сетки
    private const int ROWS = 4;
    private const int COLUMNS = 10;
    private const float SLOT_SIZE = 24f; 

    // Основные элементы
    private VisualElement _root;
    private VisualElement _inventoryContainer;
    private VisualElement _itemsLayer; 
    private VisualElement _ghostIcon; 
    
    // Тултип
    private VisualElement _tooltip;
    private Label _tooltipLabel;
    
    // Списки слотов
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    
    // Drag & Drop
    private bool _isDragging;
    private int _draggedSlotIndex = -1;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        
        // 1. Ищем контейнер в UXML. Убедись, что в UXML он называется "InventoryGrid"
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        
        if (_inventoryContainer == null)
        {
            Debug.LogError("[InventoryUI] Элемент 'InventoryGrid' не найден в UXML! Сетка не будет построена.");
            return;
        }

        _inventoryContainer.style.overflow = Overflow.Visible;

        // 2. Инициализация UI компонентов
        CreateGhostIcon();
        CreateTooltip(); 
        GenerateBackpackGrid();
        SetupEquipmentSlots();

        // 3. Безопасная подписка на Менеджера (ждем его инициализации)
        if (InventoryManager.Instance == null)
        {
            // Проверяем каждые 100мс, появился ли менеджер
            _root.schedule.Execute(TrySubscribe).Every(100).Until(() => InventoryManager.Instance != null);
        }
        else
        {
            TrySubscribe();
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

    private void TrySubscribe()
    {
        if (InventoryManager.Instance == null) return;
        
        // Отписываемся во избежание дублей
        InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        
        // Первичная отрисовка
        RefreshInventory();
    }

    private void GenerateBackpackGrid()
    {
        if (_inventoryContainer == null) return;

        _inventoryContainer.Clear();
        _backpackSlots.Clear();

        // Создаем слой для иконок предметов (поверх слотов)
        _itemsLayer = new VisualElement { name = "ItemsLayer" };
        _itemsLayer.style.position = Position.Absolute;
        _itemsLayer.StretchToParentSize();
        _itemsLayer.pickingMode = PickingMode.Ignore;
        _itemsLayer.style.overflow = Overflow.Visible; // Важно для Unity 6
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

        // --- FIX VISIBILITY: Скрываем и показываем слой, чтобы форсировать пересчет Layout ---
        _itemsLayer.style.display = DisplayStyle.None;
        _itemsLayer.Clear();

        // Очистка экипировки
        foreach (var slot in _equipmentSlots) 
        {
            var oldImg = slot.Q<Image>();
            if (oldImg != null) slot.Remove(oldImg);
        }

        // Отрисовка рюкзака
        var items = InventoryManager.Instance.Items;
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null && items[i].Data != null)
            {
                var icon = CreateItemIcon(items[i]);
                icon.style.left = (i % COLUMNS) * SLOT_SIZE;
                icon.style.top = (i / COLUMNS) * SLOT_SIZE;
                _itemsLayer.Add(icon);
            }
        }

        DrawEquipmentIcons();

        // --- ВКЛЮЧАЕМ ОБРАТНО ---
        _itemsLayer.style.display = DisplayStyle.Flex;
        _itemsLayer.BringToFront(); 
        
        // Финальный пинок движку
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
        // Для надежности дублируем через background
        if (item.Data.Icon != null)
            icon.style.backgroundImage = new StyleBackground(item.Data.Icon);
            
        return icon;
    }

    // --- Вспомогательные методы инициализации ---

    private void CreateTooltip()
    {
        _tooltip = new VisualElement { name = "ItemTooltip" };
        _tooltip.style.position = Position.Absolute;
        _tooltip.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.95f));
        _tooltip.style.borderBottomWidth = 1; _tooltip.style.borderTopWidth = 1;
        _tooltip.style.borderLeftWidth = 1; _tooltip.style.borderRightWidth = 1;
        _tooltip.style.borderTopColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
        _tooltip.style.paddingLeft = 8; _tooltip.style.paddingRight = 8;
        _tooltip.style.paddingTop = 8; _tooltip.style.paddingBottom = 8;
        _tooltip.style.display = DisplayStyle.None;
        _tooltip.pickingMode = PickingMode.Ignore;
        
        _tooltipLabel = new Label();
        _tooltipLabel.style.color = Color.white;
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

    // --- Обработка ввода (Drag & Drop) ---

    private void OnPointerOverSlot(PointerOverEvent evt)
    {
        if (_isDragging || InventoryManager.Instance == null) return;
        VisualElement slot = evt.currentTarget as VisualElement;
        if (slot == null || slot.userData == null) return;

        InventoryItem item = InventoryManager.Instance.GetItemAt((int)slot.userData, out _);
        if (item != null && item.Data != null)
        {
            _tooltipLabel.text = item.Data.ItemName; 
            _tooltip.style.display = DisplayStyle.Flex;
            UpdateTooltipPosition(evt.position);
        }
    }

    private void OnPointerOutSlot(PointerOutEvent evt) => _tooltip.style.display = DisplayStyle.None;

    private void UpdateTooltipPosition(Vector2 pos)
    {
        Vector2 localPos = _root.WorldToLocal(pos);
        _tooltip.style.left = localPos.x + 15;
        _tooltip.style.top = localPos.y + 15;
    }

    private void OnSlotPointerDown(PointerDownEvent evt)
    {
        VisualElement slot = evt.currentTarget as VisualElement;
        if (slot == null || slot.userData == null) return;
        
        int idx = (int)slot.userData;
        if (InventoryManager.Instance == null) return;
        
        InventoryItem item = InventoryManager.Instance.GetItemAt(idx, out int anchorIdx);

        if (item == null) return;

        _isDragging = true;
        _draggedSlotIndex = anchorIdx;
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

    private void UpdateGhostPosition(Vector2 pos)
    {
        Vector2 localPos = _root.WorldToLocal(pos);
        _ghostIcon.style.left = localPos.x - (_ghostIcon.resolvedStyle.width / 2);
        _ghostIcon.style.top = localPos.y - (_ghostIcon.resolvedStyle.height / 2);
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _ghostIcon.style.display = DisplayStyle.None;
        _root.ReleasePointer(evt.pointerId);

        if (InventoryManager.Instance == null) return;

        VisualElement target = FindParentSlot(_root.panel.Pick(evt.position));
        if (target != null && target.userData != null)
        {
            InventoryManager.Instance.TryMoveOrSwap(_draggedSlotIndex, (int)target.userData);
        }
        
        _draggedSlotIndex = -1;
        RefreshInventory();
    }

    private VisualElement FindParentSlot(VisualElement target)
    {
        while (target != null && !target.ClassListContains("slot")) target = target.parent;
        return target;
    }
}