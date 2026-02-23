using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Items;

public partial class InventoryUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument _uiDoc;
    [Tooltip("Если задано, при закрытии окна инвентаря сбрасывается режим крафта орбой.")]
    [SerializeField] private WindowView _windowView;
    [Header("Crafting")]
    [SerializeField] private CraftingOrbSlotsConfigSO _orbSlotsConfig;
    
    private const int ROWS = 4;
    private const int COLUMNS = 10;
    [Tooltip("Размер одной ячейки рюкзака в px (родной размер). Иконки в инвентаре масштабируются под этот размер. Под 480×270: 24px влезает в экран.")]
    [SerializeField] [Min(16f)] private float _inventorySlotSize = 24f;
    [Tooltip("Размер одной ячейки на складе в px (уменьшенный, чтобы влезало больше). Иконки на складе и при перетаскивании со склада используют этот размер.")]
    [SerializeField] [Min(12f)] private float _stashSlotSize = 20f;
    private float InventorySlotSize => _inventorySlotSize;
    private float StashSlotSize => _stashSlotSize;
    /// <summary>Размер «клетки» для иконок экипировки/крафта (слот 2×2 = 48px → 24 на клетку).</summary>
    private const float EquipmentIconCellSize = 24f;

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

    /// <summary>userData для слотов склада = STASH_SLOT_OFFSET + slotIndex (0..STASH_SLOTS_PER_TAB-1).</summary>
    private const int STASH_SLOT_OFFSET = 500000;

    private VisualElement _root;
    /// <summary>Корень UIDocument инвентаря — тултипы должны использовать его для координат (тот же panel).</summary>
    public VisualElement RootVisualElement => _root;
    /// <summary>Окно инвентаря/склада внутри полноэкранного корня. Позиционируем именно его.</summary>
    private VisualElement _windowRoot;
    private VisualElement _inventoryContainer;
    private VisualElement _itemsLayer;
    private VisualElement _ghostIcon;
    /// <summary>Подсветка зона дропа: зелёный — можно, жёлтый — своп, красный — нельзя.</summary>
    private VisualElement _ghostHighlight;

    private VisualElement _stashPanel;
    private VisualElement _stashTabsRow;
    private VisualElement _stashGridContainer;
    private VisualElement _stashItemsLayer;
    private VisualElement _mainRow;
    private List<VisualElement> _stashSlots = new List<VisualElement>();
    private StashWindowController _stashWindowController;

    /// <summary>Склад открыт отдельно (бинт B). По умолчанию скрыт при открытии инвентаря по I.</summary>
    public bool IsStashVisible { get; private set; }

    private VisualElement _equipmentView;
    private VisualElement _craftView;
    private VisualElement _craftSlot;
    private VisualElement _orbSlotsRow;
    private int _currentTab; // 0 = Equipment, 1 = Craft
    
    private List<VisualElement> _backpackSlots = new List<VisualElement>();
    private List<VisualElement> _equipmentSlots = new List<VisualElement>();
    private List<(VisualElement slot, Label countLabel)> _orbSlots = new List<(VisualElement, Label)>();
    
    private bool _isDragging;
    /// <summary>Предмет «в руке» — не в контейнере, пока держим курсор (PoE-style).</summary>
    private InventoryItem _draggedItem;
    private int _draggedSourceAnchor = -1;
    private bool _draggedFromStash;
    private int _draggedStashTab = -1;
    private int _draggedStashAnchorSlot = -1;
    /// <summary>Смещение курсора от верх-левого угла иконки при захвате (grab offset), в локальных координатах root.</summary>
    private Vector2 _grabOffsetRootLocal;

    private bool _applyOrbMode;
    private CraftingOrbSO _applyOrbOrb;
    private VisualElement _applyOrbSlotHighlight;
    private bool _suppressNextApplyOrbPointerUp;
    private int _capturedPointerId = -1;
    private int _dragPointerId = -1;
    /// <summary>Блокировка повторного входа в OnPointerUp (частые дропы не должны дублировать предмет).</summary>
    private bool _dropInProgress;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _root = _uiDoc.rootVisualElement;
        _windowRoot = _root.Q<VisualElement>("WindowRoot");
        if (_windowRoot == null) _windowRoot = _root;
        
        _inventoryContainer = _root.Q<VisualElement>("InventoryGrid"); 
        if (_inventoryContainer == null) { Debug.LogError("InventoryGrid not found"); return; }
        _inventoryContainer.style.overflow = Overflow.Visible;

        _stashPanel = _root.Q<VisualElement>("StashPanel");
        _stashTabsRow = _root.Q<VisualElement>("StashTabsRow");
        _stashGridContainer = _root.Q<VisualElement>("StashGridContainer");
        _mainRow = _root.Q<VisualElement>("MainRow");
        SetStashPanelVisible(false);

        CreateGhostIcon();
        GenerateBackpackGrid();
        SetupEquipmentSlots();
        SetupStashPanel();
        LoadOrbSlotsConfig();
        SetupTabs();
        SetupCraftView();
        ApplyInventoryArtTheme();
        RegisterQuickTransferEndpoints();

        if (InventoryManager.Instance == null)
            _root.schedule.Execute(TrySubscribe).Every(100).Until(() => InventoryManager.Instance != null);
        else
            TrySubscribe();
        if (StashManager.Instance == null)
            _root.schedule.Execute(TrySubscribeStash).Every(100).Until(() => StashManager.Instance != null);
        else
            TrySubscribeStash();

        _root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.RegisterCallback<PointerOutEvent>(OnInventoryWindowPointerOut);
        _root.RegisterCallback<KeyDownEvent>(OnKeyDown);
        _root.RegisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);

        if (_windowView == null) _windowView = GetComponent<WindowView>();
        if (_windowView != null)
        {
            _windowView.OnClosed += OnInventoryWindowClosed;
            _windowView.OnOpened += OnInventoryWindowOpened;
        }
        _root.schedule.Execute(() => ApplyInventorySpacing(IsStashVisible)).ExecuteLater(3);
    }

    private void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        if (StashManager.Instance != null)
            StashManager.Instance.OnStashChanged -= RefreshStash;
        if (_windowView != null)
        {
            _windowView.OnClosed -= OnInventoryWindowClosed;
            _windowView.OnOpened -= OnInventoryWindowOpened;
        }
        CancelDragSession(restoreHeldItem: true);
        ExitApplyOrbMode();
        UnregisterQuickTransferEndpoints();

        _root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        _root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        _root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);
        _root.UnregisterCallback<PointerOutEvent>(OnInventoryWindowPointerOut);
        _root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        _root.UnregisterCallback<MouseDownEvent>(OnRootMouseDown, TrickleDown.TrickleDown);
    }

    private void OnInventoryWindowClosed()
    {
        ExitApplyOrbMode();
        SetStashPanelVisible(false);
        CancelDragSession(restoreHeldItem: true);
    }

    private void OnInventoryWindowOpened()
    {
        ApplyInventorySpacing(IsStashVisible);
    }

    /// <summary>Переключает видимость склада. Раскладка задаётся только в USS: класс stash-open на WindowRoot и MainRow.</summary>
    public void SetStashPanelVisible(bool visible)
    {
        IsStashVisible = visible;
        if (_stashPanel != null)
        {
            if (visible) _stashPanel.AddToClassList("visible");
            else _stashPanel.RemoveFromClassList("visible");
        }
        if (_mainRow != null)
        {
            if (visible)
            {
                _mainRow.AddToClassList("stash-open");
                _mainRow.RemoveFromClassList("inventory-solo");
            }
            else
            {
                _mainRow.RemoveFromClassList("stash-open");
                _mainRow.AddToClassList("inventory-solo");
            }
        }
        VisualElement target = _windowRoot != null ? _windowRoot : _root;
        if (target != null)
        {
            if (visible)
            {
                target.AddToClassList("stash-open");
                target.RemoveFromClassList("inventory-solo");
            }
            else
            {
                target.RemoveFromClassList("stash-open");
                target.AddToClassList("inventory-solo");
            }
        }
        ApplyInventorySpacing(visible);
    }

    /// <summary>Внутренние отступы: соло — 8px слева и справа у окна; дуо — 8px справа у контента. Задаём из кода в пикселях.</summary>
    private void ApplyInventorySpacing(bool stashOpen)
    {
        // Keep spacing fully class-driven in USS to avoid runtime inline style conflicts
        // between inventory-only and stash-open states.
        if (_windowRoot != null)
        {
            _windowRoot.style.paddingLeft = StyleKeyword.Null;
            _windowRoot.style.paddingRight = StyleKeyword.Null;
        }

        if (_mainRow != null)
        {
            _mainRow.style.marginLeft = StyleKeyword.Null;
            _mainRow.style.marginRight = StyleKeyword.Null;
        }
    }



    private void TrySubscribe()
    {
        if (InventoryManager.Instance == null) return;
        InventoryManager.Instance.OnInventoryChanged -= RefreshInventory;
        InventoryManager.Instance.OnInventoryChanged += RefreshInventory;
        RefreshInventory();
    }





    





    /// <param name="slotSizePx">Размер одной клетки в px (инвентарь, склад или экипировка).</param>
    /// <param name="receivePointerEvents">True для рюкзака/склада — иконка принимает hover/click, тултип только над иконкой.</param>


    /// <summary>Текущая позиция мыши в локальных координатах _root. Screen -> Panel (RuntimePanelUtils) -> _root. Не зависит от target события.</summary>

    /// <summary>Состояние подсветки под курсором: 0 = можно, 1 = своп, 2 = нельзя, -1 = не над сеткой.</summary>




























    /// <summary>Не скрываем тултип при уходе с слота — иначе мерцание (особенно на экипировке). Скрытие только при наведении на пустой слот (OnPointerOverSlot) или уходе с окна (OnInventoryWindowPointerOut).</summary>


    


    /// <summary>Позиция в пространстве панели (для хита дропа).</summary>

    /// <summary>pos — координаты в локальном пространстве _root. При перетаскивании предмета размер берём только из _draggedItem (layout ещё не обновился на первом кадре — из-за этого призрак появлялся справа-снизу).</summary>

    /// <summary>Позиция указателя в локальном пространстве root. ChangeCoordinatesTo даёт согласованные координаты с призраком (оба в root).</summary>


    /// <summary>
    /// Площадь пересечения двух прямоугольников (в одной системе координат).
    /// </summary>

    /// <summary>
    /// Якорь по максимальному пересечению: центр дропа + размер предмета задают виртуальный прямоугольник,
    /// перебираем все допустимые позиции WxH и выбираем ту, с которой площадь пересечения максимальна.
    /// </summary>

    /// <summary>
    /// Якорь в рюкзаке по максимальному пересечению виртуального прямоугольника (центр дропа + размер) с кандидатом.
    /// </summary>

}
