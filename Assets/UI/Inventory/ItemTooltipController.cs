using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Stats;
using Scripts.Skills;
using Scripts.Combat;
using Scripts.StatusEffects;
using Scripts.Items.World;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

public enum ItemTooltipPriceMode
{
    None,
    Buy,
    Sell,
    Buyback
}

public class ItemTooltipController : MonoBehaviour
{
    public static ItemTooltipController Instance { get; private set; }
    public bool IsConsumingTooltipLockInput => _pin.IsVisible && IsTooltipLockHeld();

    [Header("UI Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private Font _customFont;

    [Header("Layout Settings (Pixel Perfect)")]
    [SerializeField] private float _tooltipWidth = 150f; // Чуть уже (было 160)
    [SerializeField] private float _gap = 5f; 
    [SerializeField] private float _screenPadding = 4f;
    private const float HudSkillTooltipGap = 2f;
    private const float HudSkillTooltipPadding = 2f;
    private const float HudDpsBreakdownWidth = 88f;
    private const float BuffTooltipWidth = 140f;
    private const int SkillDescriptionFontSize = 6;
    private const int SkillBuffNameFontSize = 7;
    private const int SkillDescriptionLineMinHeight = 8; 
    private const int LockHintFontSize = 6;
    private const string TooltipLockActionName = "TooltipLock";
    private const string TooltipLockHintKey = "tooltip.lockHint";
    
    [SerializeField, Tooltip("Задержка в миллисекундах перед скрытием тултипа (увеличена против мерцания при наведении на экипировку)")]
    private long _hideDelayMs = 180;
    
    private const float SLOT_SIZE = 24f;
    private const float PinBadgeSize = 7f; 

    // --- Localization Tables ---
    private const string TABLE_MENU = "MenuLabels";
    private const string TABLE_AFFIXES = "AffixesLabels";
    private const string TABLE_ITEMS = "ItemsLabels";
    private const string TABLE_SKILLS = "SkillsLabels";

    // --- UI Elements ---
    private VisualElement _root;
    
    // 1. Основной (Item)
    private VisualElement _itemTooltipBox;
    private Label _headerLabel;
    private VisualElement _headerDivider; 
    private VisualElement _statsContainer;

    // 2. Вторичный (Skill)
    private VisualElement _skillTooltipBox;

    // --- State ---
    private InventoryItem _currentTargetItem;
    private ItemTooltipPriceMode _currentPriceMode;
    private CraftingOrbSO _currentTargetOrb;
    private VisualElement _targetAnchorSlot;
    private VisualElement _worldAnchor;
    private WorldDroppedItem _worldTargetItem;
    private VisualElement _hudDpsBreakdownBox;
    private VisualElement _buffTooltipBox;
    private VisualElement _hudDpsHoverRow;
    private VisualElement _hudHitHoverRow;
    private VisualElement _hudBreakdownAnchorRow;
    private VisualElement _buffTooltipAnchor;
    private StatusEffectSO _hoveredBuff;
    private readonly List<VisualElement> _buffNameHoverTargets = new List<VisualElement>();
    private bool _hudBreakdownShowPerHit;
    private RectTransform _hudSkillRect;
    private SkillDataSO _currentHudSkill;
    private object _hudSkillSource;
    private int _hudSkillSlotIndex = -1;
    private SkillDpsPreview _hudDpsPreview;
    private readonly TooltipPinPolicy _pin = new TooltipPinPolicy();
    private VisualElement _itemPinBadge;
    private VisualElement _skillPinBadge;
    private VisualElement _orbPinBadge;
    private Label _itemLockHint;
    private Label _skillLockHint;
    private Label _orbLockHint;
    private InputAction _tooltipLockAction;
    private InputAction _tooltipLockReader;
    private float _pinAnimElapsed = -1f;
    private int _worldOwnerFrame = -1;

    // --- Orb Tooltip ---
    private VisualElement _orbTooltipBox;
    private Label _orbTitleLabel;
    private Label _orbDescLabel;
    private StatsDatabaseSO _statsDb;

    // --- Colors ---
    private readonly Color _colBg = new Color(0.05f, 0.05f, 0.05f, 0.98f); 
    private readonly Color _colSkillBg = new Color(0.05f, 0.1f, 0.15f, 0.98f);
    
    private readonly Color _colNormalText = new Color(0.9f, 0.9f, 0.9f);
    private readonly Color _colModifiedText = new Color(0.5f, 0.6f, 1f);
    
    private readonly Color _colTitleRare = ItemRarity.RareTitle;

    private readonly Color _colImplicit = new Color(0.6f, 0.8f, 1f);
    private readonly Color _colAffix = new Color(0.5f, 0.5f, 1f);
    private readonly Color _colGoldText = new Color(0.93f, 0.78f, 0.28f);
    
    private readonly Color _colFireText = new Color(1f, 0.5f, 0.5f);
    private readonly Color _colColdText = new Color(0.5f, 0.6f, 1f);
    private readonly Color _colLightningText = new Color(1f, 1f, 0.5f);

    private readonly Color _colSkillType = new Color(0.6f, 0.6f, 0.6f);
    private readonly Color _colBuffName = new Color(0.91f, 0.77f, 0.36f);
    private readonly Color _colInspect = new Color(0.95f, 0.82f, 0.35f);
    private bool _inspectDetailsVisible; 

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        _statsDb = Resources.Load<StatsDatabaseSO>(ProjectPaths.ResourcesStatsDatabase);
        // Тултип должен жить в том же UIDocument, что и инвентарь — иначе WorldToLocal даёт неверные координаты (другая панель).
        var inv = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
        if (inv != null && inv.RootVisualElement != null)
            _root = inv.RootVisualElement;
        else if (_uiDoc != null)
            _root = _uiDoc.rootVisualElement;
        UIFontApplier.ApplyToRoot(_root);
        if (_root != null)
            _root.schedule.Execute(RebuildTooltipStructure).ExecuteLater(50);

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        InputRebindSaver.RebindsChanged += OnTooltipLockBindingChanged;
        ResolveTooltipLockAction();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        InputRebindSaver.RebindsChanged -= OnTooltipLockBindingChanged;
        DisposeTooltipLockReader();
        HideTooltipImmediate();
    }

    private void LateUpdate()
    {
        TickTooltipPin();
        UpdateItemInspectOverlay();
        UpdateHudDpsBreakdownHover();
        UpdateBuffTooltipHover();
    }

    private void TickTooltipPin()
    {
        if (!_pin.IsVisible)
            return;

        bool overOwner = IsPointerOverCurrentOwner();
        bool overTooltip = IsPointerOverTooltipCluster();
        bool clickOutside = Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame
            && !overTooltip;

        bool hidden = _pin.Tick(IsTooltipLockHeld(), overOwner, overTooltip, clickOutside);
        if (hidden)
        {
            HideTooltipImmediate();
            return;
        }

        if (_pin.JustPinned)
            _pinAnimElapsed = 0f;

        RefreshPinVisual();
        SetTooltipClusterPicking(_pin.IsPinned);
    }

    private void ResolveTooltipLockAction()
    {
        DisposeTooltipLockReader();

        InputActionAsset asset = InputManager.InputActions?.asset;
        _tooltipLockAction = asset != null ? asset.FindAction(TooltipLockActionName, false) : null;
        if (_tooltipLockAction != null)
        {
            int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(_tooltipLockAction);
            if (bindingIndex >= 0)
            {
                string effectivePath = _tooltipLockAction.bindings[bindingIndex].effectivePath;
                if (!string.IsNullOrWhiteSpace(effectivePath))
                {
                    // Inventory and modal windows may disable the Player action map. The tooltip
                    // still needs to read its configured binding while UI is active, so use a
                    // lightweight independent reader built from the same (possibly rebound) path.
                    _tooltipLockReader = new InputAction(
                        "TooltipLockUIReader",
                        InputActionType.Button,
                        effectivePath);
                    _tooltipLockReader.Enable();
                }
            }
        }

        RefreshLockHintText();
    }

    private void DisposeTooltipLockReader()
    {
        if (_tooltipLockReader == null)
            return;

        _tooltipLockReader.Disable();
        _tooltipLockReader.Dispose();
        _tooltipLockReader = null;
    }

    private void OnTooltipLockBindingChanged()
    {
        ResolveTooltipLockAction();
    }

    private bool IsTooltipLockHeld()
    {
        if (_tooltipLockReader == null)
            return false;

        // Poll the resolved buttons as well as the action phase. Direct polling keeps the lock
        // responsive even when another map was disabled during the same frame as the UI opened.
        foreach (InputControl control in _tooltipLockReader.controls)
        {
            if (control is ButtonControl button && button.isPressed)
                return true;
        }

        return _tooltipLockReader.IsPressed();
    }

    private bool IsPointerOverCurrentOwner()
    {
        if (_currentHudSkill != null && _hudSkillRect != null)
        {
            if (Mouse.current == null)
                return false;
            Canvas canvas = _hudSkillRect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _hudSkillRect, Mouse.current.position.ReadValue(), camera);
        }

        if (_worldTargetItem != null)
            return _worldOwnerFrame >= 0 && Time.frameCount - _worldOwnerFrame <= 1;

        return IsPointerOverElement(_targetAnchorSlot);
    }

    private bool IsPointerOverTooltipCluster()
    {
        if (_root == null || _root.panel == null || Mouse.current == null)
            return false;

        Vector2 panelPos = GetMousePanelPos();
        if (IsPanelPointOver(_itemTooltipBox, panelPos)
            || IsPanelPointOver(_skillTooltipBox, panelPos)
            || IsPanelPointOver(_orbTooltipBox, panelPos)
            || IsPanelPointOver(_hudDpsBreakdownBox, panelPos)
            || IsPanelPointOver(_buffTooltipBox, panelPos))
            return true;

        return IsPickedTooltipElement(panelPos);
    }

    private static Rect GetTooltipPanelRect(VisualElement element)
    {
        Rect bound = element.worldBound;
        if (bound.width >= 1f && bound.height >= 1f)
            return bound;

        float w = element.resolvedStyle.width;
        float h = element.resolvedStyle.height;
        if (float.IsNaN(w) || w < 1f || float.IsNaN(h) || h < 1f)
            return bound;

        Vector2 min = element.LocalToWorld(Vector2.zero);
        Vector2 max = element.LocalToWorld(new Vector2(w, h));
        return Rect.MinMaxRect(
            Mathf.Min(min.x, max.x),
            Mathf.Min(min.y, max.y),
            Mathf.Max(min.x, max.x),
            Mathf.Max(min.y, max.y));
    }

    public static bool ContainsInclusive(Rect rect, Vector2 point)
    {
        return point.x >= rect.xMin && point.x <= rect.xMax
            && point.y >= rect.yMin && point.y <= rect.yMax;
    }

    private static bool IsDisplayedTooltip(VisualElement element)
    {
        return element != null
            && element.panel != null
            && element.style.display == DisplayStyle.Flex;
    }

    private bool IsPickedTooltipElement(Vector2 panelPos)
    {
        if (_root == null || _root.panel == null)
            return false;

        VisualElement picked = _root.panel.Pick(panelPos);
        while (picked != null)
        {
            if (picked == _itemTooltipBox
                || picked == _skillTooltipBox
                || picked == _orbTooltipBox
                || picked == _hudDpsBreakdownBox
                || picked == _buffTooltipBox)
                return true;
            picked = picked.parent;
        }

        return false;
    }

    public static Rect EncapsulateRects(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    public static Rect InflateRect(Rect rect, float amount)
    {
        rect.xMin -= amount;
        rect.yMin -= amount;
        rect.xMax += amount;
        rect.yMax += amount;
        return rect;
    }

    private void SetTooltipClusterPicking(bool pickable)
    {
        PickingMode mode = pickable ? PickingMode.Position : PickingMode.Ignore;
        if (_itemTooltipBox != null) _itemTooltipBox.pickingMode = mode;
        if (_skillTooltipBox != null) _skillTooltipBox.pickingMode = mode;
        if (_orbTooltipBox != null) _orbTooltipBox.pickingMode = mode;
        if (_hudDpsBreakdownBox != null) _hudDpsBreakdownBox.pickingMode = mode;
        if (_buffTooltipBox != null) _buffTooltipBox.pickingMode = mode;
    }

    private VisualElement CreatePinLockBadge(string name)
    {
        var badge = new VisualElement { name = name };
        badge.pickingMode = PickingMode.Ignore;
        badge.style.position = Position.Absolute;
        badge.style.width = PinBadgeSize;
        badge.style.height = 8f;
        badge.style.display = DisplayStyle.None;

        AddPinRect(badge, "PinBowTop", 1, 0, 4, 1);
        AddPinRect(badge, "PinBowLeft", 1, 1, 1, 2);
        AddPinRect(badge, "PinBowRight", 4, 1, 1, 2);

        var body = AddPinRect(badge, "PinBody", 0, 3, 6, 5);
        body.style.backgroundColor = new StyleColor(new Color(0.12f, 0.1f, 0.06f, 1f));
        body.style.borderTopWidth = 1;
        body.style.borderBottomWidth = 1;
        body.style.borderLeftWidth = 1;
        body.style.borderRightWidth = 1;

        AddPinRect(badge, "PinFill", 1, 6, 4, 0);
        return badge;
    }

    private static VisualElement AddPinRect(VisualElement parent, string name, float left, float top, float width, float height)
    {
        var el = new VisualElement { name = name };
        el.pickingMode = PickingMode.Ignore;
        el.style.position = Position.Absolute;
        el.style.left = left;
        el.style.top = top;
        el.style.width = width;
        el.style.height = height;
        parent.Add(el);
        return el;
    }

    private void RefreshPinVisual()
    {
        RefreshLockHintVisibility();
        if (!_pin.IsPinned)
        {
            HidePinBadges();
            return;
        }

        if (_pin.JustPinned)
            _pinAnimElapsed = 0f;
        else if (_pinAnimElapsed >= 0f)
        {
            _pinAnimElapsed += Time.unscaledDeltaTime;
            if (_pinAnimElapsed >= 0.18f)
                _pinAnimElapsed = -1f;
        }

        int pulseFrame = _pinAnimElapsed < 0f ? 2 : (_pinAnimElapsed < 0.09f ? 0 : 1);
        HidePinBadges();
        if (IsDisplayedTooltip(_itemTooltipBox))
            PlacePinBadge(_itemPinBadge, _itemTooltipBox, pulseFrame);
        else if (IsDisplayedTooltip(_skillTooltipBox))
            PlacePinBadge(_skillPinBadge, _skillTooltipBox, pulseFrame);
        else if (IsDisplayedTooltip(_orbTooltipBox))
            PlacePinBadge(_orbPinBadge, _orbTooltipBox, pulseFrame);
    }

    private void PlacePinBadge(VisualElement badge, VisualElement host, int pulseFrame)
    {
        if (badge == null)
            return;

        if (!IsDisplayedTooltip(host) || _root == null)
        {
            badge.style.display = DisplayStyle.None;
            return;
        }

        if (badge.parent != _root)
            _root.Add(badge);

        Rect bound = host.worldBound;
        Vector2 local = _root.WorldToLocal(new Vector2(bound.xMin, bound.yMin));
        badge.style.left = Mathf.Round(local.x + 2f);
        badge.style.top = Mathf.Round(local.y + 2f);
        badge.style.display = DisplayStyle.Flex;
        badge.BringToFront();
        ApplyPinLockedVisual(badge, pulseFrame);
    }

    private void HidePinBadges()
    {
        if (_itemPinBadge != null) _itemPinBadge.style.display = DisplayStyle.None;
        if (_skillPinBadge != null) _skillPinBadge.style.display = DisplayStyle.None;
        if (_orbPinBadge != null) _orbPinBadge.style.display = DisplayStyle.None;
    }

    private void ApplyPinLockedVisual(VisualElement badge, int pulseFrame)
    {
        var fill = badge.Q<VisualElement>("PinFill");
        var body = badge.Q<VisualElement>("PinBody");
        if (fill == null || body == null)
            return;

        Color outline = pulseFrame == 0 ? new Color(0.98f, 0.93f, 0.72f) : _colBuffName;
        Color fillColor = outline;

        SetPinPartColor(badge, "PinBowTop", outline);
        SetPinPartColor(badge, "PinBowLeft", outline);
        SetPinPartColor(badge, "PinBowRight", outline);
        body.style.borderTopColor = outline;
        body.style.borderBottomColor = outline;
        body.style.borderLeftColor = outline;
        body.style.borderRightColor = outline;

        const int fillHeight = 3;
        fill.style.height = fillHeight;
        fill.style.top = 7 - fillHeight;
        fill.style.backgroundColor = new StyleColor(fillColor);
    }

    private static void SetPinPartColor(VisualElement badge, string name, Color color)
    {
        var part = badge.Q<VisualElement>(name);
        if (part != null)
            part.style.backgroundColor = new StyleColor(color);
    }

    private Label CreateLockHintLabel(string name)
    {
        var label = CreateLabel(string.Empty, LockHintFontSize, FontStyle.Normal, TextAnchor.MiddleCenter);
        label.name = name;
        label.pickingMode = PickingMode.Ignore;
        label.style.width = Length.Percent(100);
        label.style.height = 7f;
        label.style.minHeight = 7f;
        label.style.marginTop = 1f;
        label.style.marginBottom = 0f;
        label.style.paddingTop = 0f;
        label.style.paddingBottom = 0f;
        label.style.color = new StyleColor(new Color(0.78f, 0.72f, 0.58f, 0.34f));
        label.style.display = DisplayStyle.None;
        return label;
    }

    private void RefreshLockHintVisibility()
    {
        bool showHint = _pin.IsVisible && !_pin.IsPinned;
        bool itemIsPrimary = showHint && IsDisplayedTooltip(_itemTooltipBox);
        bool skillIsPrimary = showHint && !itemIsPrimary && IsDisplayedTooltip(_skillTooltipBox);
        bool orbIsPrimary = showHint && !itemIsPrimary && !skillIsPrimary && IsDisplayedTooltip(_orbTooltipBox);

        if (_itemLockHint != null)
            _itemLockHint.style.display = itemIsPrimary ? DisplayStyle.Flex : DisplayStyle.None;
        if (_skillLockHint != null)
            _skillLockHint.style.display = skillIsPrimary ? DisplayStyle.Flex : DisplayStyle.None;
        if (_orbLockHint != null)
            _orbLockHint.style.display = orbIsPrimary ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshLockHintText()
    {
        string binding = GetTooltipLockBindingLabel();
        bool russian = (LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en")
            .StartsWith("ru", System.StringComparison.OrdinalIgnoreCase);
        SetLockHintText(binding, russian ? "Закрепить" : "Lock");

        AsyncOperationHandle<string> operation = LocalizationSettings.StringDatabase
            .GetLocalizedStringAsync(TABLE_MENU, TooltipLockHintKey);
        operation.Completed += handle =>
        {
            if (handle.Status != AsyncOperationStatus.Succeeded
                || string.IsNullOrWhiteSpace(handle.Result)
                || handle.Result.Contains("No translation found"))
                return;

            SetLockHintText(GetTooltipLockBindingLabel(), handle.Result);
        };
    }

    private void SetLockHintText(string binding, string actionText)
    {
        string text = $"[{binding}] {actionText}";
        if (_itemLockHint != null) _itemLockHint.text = text;
        if (_skillLockHint != null) _skillLockHint.text = text;
        if (_orbLockHint != null) _orbLockHint.text = text;
    }

    private string GetTooltipLockBindingLabel()
    {
        if (_tooltipLockAction == null)
            return "LShift";

        int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(_tooltipLockAction);
        if (bindingIndex < 0)
            return "Unbound";

        string display = _tooltipLockAction.GetBindingDisplayString(
            bindingIndex,
            InputBinding.DisplayStringOptions.DontIncludeInteractions);
        if (string.IsNullOrWhiteSpace(display))
            return "Unbound";

        return display
            .Replace("Left Shift", "LShift")
            .Replace("Right Shift", "RShift")
            .Replace("Control", "Ctrl");
    }

    private void OnLocaleChanged(UnityEngine.Localization.Locale locale)
    {
        RefreshLockHintText();
        if (_currentTargetItem != null && _itemTooltipBox.style.display == DisplayStyle.Flex)
        {
            FillItemData(_currentTargetItem);
            FillSkillData(_currentTargetItem);
            _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
        }
        else if (_currentTargetOrb != null && _orbTooltipBox != null && _orbTooltipBox.style.display == DisplayStyle.Flex)
        {
            string nameKey = string.IsNullOrEmpty(_currentTargetOrb.NameKey) ? $"crafting_relic.{_currentTargetOrb.ID}.name" : _currentTargetOrb.NameKey;
            string descKey = string.IsNullOrEmpty(_currentTargetOrb.DescriptionKey) ? $"crafting_relic.{_currentTargetOrb.ID}.description" : _currentTargetOrb.DescriptionKey;
            LocalizeLabel(_orbTitleLabel, TABLE_MENU, nameKey, _currentTargetOrb.name);
            LocalizeLabel(_orbDescLabel, TABLE_MENU, descKey, "");
            _root.schedule.Execute(RecalculateOrbPosition).ExecuteLater(1);
        }
        else if (_currentHudSkill != null && _skillTooltipBox != null && _skillTooltipBox.style.display == DisplayStyle.Flex)
        {
            FillHudSkillTooltip(_currentHudSkill);
            _root.schedule.Execute(RecalculateHudSkillPosition).ExecuteLater(1);
        }
    }

    private void RebuildTooltipStructure()
    {
        // Тултип в том же корне, что и инвентарь — иначе позиция считается в другой панели и "ничего не меняется".
        var inv = UnityEngine.Object.FindObjectOfType<InventoryUI>(true);
        if (inv != null && inv.RootVisualElement != null)
            _root = inv.RootVisualElement;
        else if (_root == null && _uiDoc != null)
            _root = _uiDoc.rootVisualElement;
        if (_root == null) return;

        var old1 = _root.Q<VisualElement>("GlobalItemTooltip");
        if (old1 != null) _root.Remove(old1);
        var old2 = _root.Q<VisualElement>("GlobalSkillTooltip");
        if (old2 != null) _root.Remove(old2);
        var oldOrb = _root.Q<VisualElement>("GlobalOrbTooltip");
        if (oldOrb != null) _root.Remove(oldOrb);
        var oldWorldAnchor = _root.Q<VisualElement>("WorldItemTooltipAnchor");
        if (oldWorldAnchor != null) _root.Remove(oldWorldAnchor);
        var oldHudAnchor = _root.Q<VisualElement>("HudSkillTooltipAnchor");
        if (oldHudAnchor != null) _root.Remove(oldHudAnchor);
        var oldDpsBreakdown = _root.Q<VisualElement>("GlobalHudSkillDpsBreakdown");
        if (oldDpsBreakdown != null) _root.Remove(oldDpsBreakdown);
        var oldBuffTooltip = _root.Q<VisualElement>("GlobalSkillBuffTooltip");
        if (oldBuffTooltip != null) _root.Remove(oldBuffTooltip);
        var oldPinBadge = _root.Q<VisualElement>("TooltipPinBadge");
        if (oldPinBadge != null) _root.Remove(oldPinBadge);
        var oldItemPin = _root.Q<VisualElement>("TooltipPinBadgeItem");
        if (oldItemPin != null) _root.Remove(oldItemPin);
        var oldSkillPin = _root.Q<VisualElement>("TooltipPinBadgeSkill");
        if (oldSkillPin != null) _root.Remove(oldSkillPin);
        var oldOrbPin = _root.Q<VisualElement>("TooltipPinBadgeOrb");
        if (oldOrbPin != null) _root.Remove(oldOrbPin);

        _worldAnchor = new VisualElement { name = "WorldItemTooltipAnchor" };
        _worldAnchor.style.position = Position.Absolute;
        _worldAnchor.style.width = 18f;
        _worldAnchor.style.height = 18f;
        _worldAnchor.style.visibility = Visibility.Hidden;
        _worldAnchor.pickingMode = PickingMode.Ignore;
        _root.Add(_worldAnchor);

        // --- 1. Item Tooltip ---
        _itemTooltipBox = CreateContainer("GlobalItemTooltip", _colBg);
        _headerLabel = CreateLabel("", 8, FontStyle.Bold, TextAnchor.MiddleCenter);
        _statsContainer = new VisualElement { style = { width = Length.Percent(100) } };
        
        _itemTooltipBox.Add(_headerLabel);
        _itemTooltipBox.Add(CreateDivider());
        _itemTooltipBox.Add(_statsContainer);
        _itemLockHint = CreateLockHintLabel("ItemTooltipLockHint");
        _itemTooltipBox.Add(_itemLockHint);
        _root.Add(_itemTooltipBox);

        // --- 2. Skill Tooltip ---
        _skillTooltipBox = CreateContainer("GlobalSkillTooltip", _colSkillBg);
        _skillTooltipBox.style.alignItems = Align.Stretch;
        _skillTooltipBox.style.borderTopColor = new Color(0, 0.5f, 0.5f); 
        _skillTooltipBox.style.borderBottomColor = new Color(0, 0.5f, 0.5f);
        _skillTooltipBox.style.borderLeftColor = new Color(0, 0.5f, 0.5f); 
        _skillTooltipBox.style.borderRightColor = new Color(0, 0.5f, 0.5f);
        _skillLockHint = CreateLockHintLabel("SkillTooltipLockHint");
        
        _root.Add(_skillTooltipBox);

        _hudDpsBreakdownBox = CreateContainer("GlobalHudSkillDpsBreakdown", _colSkillBg);
        _hudDpsBreakdownBox.style.width = HudDpsBreakdownWidth;
        _hudDpsBreakdownBox.style.borderTopColor = new Color(0, 0.5f, 0.5f);
        _hudDpsBreakdownBox.style.borderBottomColor = new Color(0, 0.5f, 0.5f);
        _hudDpsBreakdownBox.style.borderLeftColor = new Color(0, 0.5f, 0.5f);
        _hudDpsBreakdownBox.style.borderRightColor = new Color(0, 0.5f, 0.5f);
        _hudDpsBreakdownBox.style.alignItems = Align.Stretch;
        _root.Add(_hudDpsBreakdownBox);

        _buffTooltipBox = CreateContainer("GlobalSkillBuffTooltip", _colSkillBg);
        _buffTooltipBox.style.width = BuffTooltipWidth;
        _buffTooltipBox.style.borderTopColor = new Color(0.57f, 0.48f, 0.23f);
        _buffTooltipBox.style.borderBottomColor = new Color(0.57f, 0.48f, 0.23f);
        _buffTooltipBox.style.borderLeftColor = new Color(0.57f, 0.48f, 0.23f);
        _buffTooltipBox.style.borderRightColor = new Color(0.57f, 0.48f, 0.23f);
        _buffTooltipBox.style.alignItems = Align.Stretch;
        _root.Add(_buffTooltipBox);

        _itemPinBadge = CreatePinLockBadge("TooltipPinBadgeItem");
        _skillPinBadge = CreatePinLockBadge("TooltipPinBadgeSkill");
        _orbPinBadge = CreatePinLockBadge("TooltipPinBadgeOrb");
        _root.Add(_itemPinBadge);
        _root.Add(_skillPinBadge);
        _root.Add(_orbPinBadge);

        // --- 3. Orb Tooltip (crafting orbs) ---
        _orbTooltipBox = CreateContainer("GlobalOrbTooltip", _colSkillBg);
        _orbTooltipBox.style.borderTopColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderBottomColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderLeftColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTooltipBox.style.borderRightColor = new Color(0.4f, 0.35f, 0.2f);
        _orbTitleLabel = CreateLabel("", 9, FontStyle.Bold, TextAnchor.MiddleCenter);
        _orbTitleLabel.style.color = new StyleColor(_colTitleRare);
        _orbDescLabel = CreateLabel("", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        _orbLockHint = CreateLockHintLabel("OrbTooltipLockHint");
        _orbTooltipBox.Add(_orbTitleLabel);
        _orbTooltipBox.Add(CreateDivider());
        _orbTooltipBox.Add(_orbDescLabel);
        _orbTooltipBox.Add(_orbLockHint);
        _root.Add(_orbTooltipBox);

        _itemTooltipBox.RegisterCallback<GeometryChangedEvent>(OnItemTooltipGeometryChanged);
        RefreshLockHintText();
    }

    private void OnItemTooltipGeometryChanged(GeometryChangedEvent evt)
    {
        if (_itemTooltipBox == null || _itemTooltipBox.style.display != DisplayStyle.Flex || _currentTargetItem == null)
            return;
        if (Mathf.Approximately(evt.oldRect.width, evt.newRect.width)
            && Mathf.Approximately(evt.oldRect.height, evt.newRect.height))
            return;
        RecalculatePosition();
    }

    private VisualElement CreateContainer(string name, Color bg)
    {
        var el = new VisualElement { name = name };
        el.style.position = Position.Absolute;
        el.style.width = _tooltipWidth;
        el.style.backgroundColor = new StyleColor(bg);
        
        // C# совместимые бордеры
        el.style.borderTopWidth = 1; el.style.borderBottomWidth = 1;
        el.style.borderLeftWidth = 1; el.style.borderRightWidth = 1;
        
        // --- ИСПРАВЛЕНИЕ 1: УМЕНЬШИЛ ОТСТУПЫ (БЫЛО 4) ---
        el.style.paddingTop = 2; el.style.paddingBottom = 2;
        el.style.paddingLeft = 3; el.style.paddingRight = 3;
        
        el.style.visibility = Visibility.Hidden; 
        el.style.display = DisplayStyle.None;
        el.style.overflow = Overflow.Visible;
        el.pickingMode = PickingMode.Ignore; 
        
        var resolvedFont = _customFont != null
            ? _customFont
            : UIFontResolver.ResolveUIToolkitFont(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        if (resolvedFont != null)
            el.style.unityFontDefinition = FontDefinition.FromFont(resolvedFont);
        
        el.style.fontSize = 8;
        el.style.alignItems = Align.Center; 
        return el;
    }

    private Label CreateLabel(string txt, int size, FontStyle style, TextAnchor align)
    {
        var lbl = new Label(txt);
        lbl.style.fontSize = size;
        lbl.style.unityFontStyleAndWeight = style;
        lbl.style.unityTextAlign = align;
        lbl.style.whiteSpace = WhiteSpace.Normal;
        lbl.style.color = new StyleColor(_colNormalText);
        // Уменьшил внутренние отступы лейблов
        lbl.style.paddingTop = 0; lbl.style.paddingBottom = 1;
        return lbl;
    }

    private VisualElement CreateDivider()
    {
        var d = new VisualElement();
        d.style.height = 1;
        d.style.width = Length.Percent(100);
        d.style.marginTop = 2; d.style.marginBottom = 2;
        d.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.5f));
        return d;
    }

    // --- Public API ---

    public void ShowOrbTooltip(CraftingOrbSO orb, VisualElement anchorSlot)
    {
        if (_orbTooltipBox == null || orb == null) return;
        if (_currentTargetOrb == orb
            && _targetAnchorSlot == anchorSlot
            && _orbTooltipBox.style.display == DisplayStyle.Flex)
            return;
        if (_pin.IsPinned)
            return;

        _pin.Show();
        _currentTargetItem = null;
        _currentTargetOrb = orb;
        _targetAnchorSlot = anchorSlot;
        _worldTargetItem = null;
        ClearHudSkillTarget();

        if (_itemTooltipBox != null) { _itemTooltipBox.style.display = DisplayStyle.None; }
        if (_skillTooltipBox != null) { _skillTooltipBox.style.display = DisplayStyle.None; }

        string nameKey = string.IsNullOrEmpty(orb.NameKey) ? $"crafting_relic.{orb.ID}.name" : orb.NameKey;
        string descKey = string.IsNullOrEmpty(orb.DescriptionKey) ? $"crafting_relic.{orb.ID}.description" : orb.DescriptionKey;
        _orbTitleLabel.text = orb.name;
        _orbDescLabel.text = "";

        LocalizeLabel(_orbTitleLabel, TABLE_MENU, nameKey, orb.name);
        LocalizeLabel(_orbDescLabel, TABLE_MENU, descKey, "");

        _orbTooltipBox.style.display = DisplayStyle.Flex;
        _orbTooltipBox.style.visibility = Visibility.Hidden;
        _orbTooltipBox.MarkDirtyRepaint();
        _root.schedule.Execute(RecalculateOrbPosition).ExecuteLater(1);
    }

    public void ShowTooltip(InventoryItem item, VisualElement anchorSlot)
    {
        ShowTooltip(item, anchorSlot, ItemTooltipPriceMode.None);
    }

    public void ShowTooltip(InventoryItem item, VisualElement anchorSlot, ItemTooltipPriceMode priceMode)
    {
        _currentPriceMode = priceMode;
        ShowTooltipInternal(item, anchorSlot, null);
    }

    public void ShowWorldTooltip(WorldDroppedItem droppedItem)
    {
        if (droppedItem == null || droppedItem.Item?.Data == null)
            return;
        if (ShouldHideWorldItemTooltip())
            return;

        if (_itemTooltipBox == null || _worldAnchor == null)
            RebuildTooltipStructure();
        if (!UpdateWorldAnchorPosition(droppedItem.TooltipWorldPosition))
            return;

        _currentPriceMode = ItemTooltipPriceMode.None;
        ShowTooltipInternal(droppedItem.Item, _worldAnchor, droppedItem);
        if (_worldTargetItem == droppedItem)
            RecalculatePosition();
    }

    public void HideWorldTooltip(WorldDroppedItem droppedItem)
    {
        if (_worldTargetItem == droppedItem)
            HideTooltip();
    }

    public void HideWorldTooltip()
    {
        if (_worldTargetItem != null)
            HideTooltip();
    }

    public void ShowHudSkillTooltip(SkillDataSO skill, RectTransform slotRect, object source)
    {
        if (skill == null || slotRect == null)
        {
            HideHudSkillTooltip(source);
            return;
        }

        if (_skillTooltipBox == null)
            RebuildTooltipStructure();
        if (_skillTooltipBox == null || _root == null || _root.panel == null)
            return;

        if (IsShowingHudSkillTooltip(source) && _currentHudSkill == skill)
        {
            _hudSkillRect = slotRect;
            RecalculateHudSkillPosition();
            return;
        }

        if (_pin.IsPinned)
            return;

        _pin.Show();
        _currentTargetItem = null;
        _currentTargetOrb = null;
        _targetAnchorSlot = null;
        _worldTargetItem = null;
        _currentHudSkill = skill;
        _hudSkillRect = slotRect;
        _hudSkillSource = source;
        _hudSkillSlotIndex = source is UISkillSlot hudSlot ? hudSlot.SlotIndex : -1;

        if (_itemTooltipBox != null)
            _itemTooltipBox.style.display = DisplayStyle.None;
        if (_orbTooltipBox != null)
            _orbTooltipBox.style.display = DisplayStyle.None;

        FillHudSkillTooltip(skill);
        _skillTooltipBox.style.display = DisplayStyle.Flex;
        _skillTooltipBox.style.visibility = Visibility.Hidden;
        _skillTooltipBox.MarkDirtyRepaint();
        RecalculateHudSkillPosition();
        _root.schedule.Execute(RecalculateHudSkillPosition).ExecuteLater(1);
    }

    public void HideHudSkillTooltip(object source)
    {
        if (_currentHudSkill == null)
            return;
        if (source != null && _hudSkillSource != null && !ReferenceEquals(_hudSkillSource, source))
            return;
        HideTooltip();
    }

    public bool IsShowingHudSkillTooltip(object source)
    {
        return _currentHudSkill != null
            && (source == null || ReferenceEquals(_hudSkillSource, source))
            && _skillTooltipBox != null
            && _skillTooltipBox.style.display == DisplayStyle.Flex;
    }

    public bool IsPointerOverHudSkillUi()
    {
        if (_currentHudSkill == null || _root == null || _root.panel == null || Mouse.current == null)
            return false;

        Vector2 panelPos = GetMousePanelPos();
        return IsPanelPointOver(_skillTooltipBox, panelPos)
            || IsPanelPointOver(_hudDpsBreakdownBox, panelPos)
            || IsPanelPointOver(_buffTooltipBox, panelPos);
    }

    private static bool IsPanelPointOver(VisualElement element, Vector2 panelPos)
    {
        return IsDisplayedTooltip(element)
            && ContainsInclusive(GetTooltipPanelRect(element), panelPos);
    }

    private bool IsPointerOverElement(VisualElement element)
    {
        if (element == null || element.panel == null || Mouse.current == null)
            return false;

        Vector2 panelPos = MouseToUiToolkitPanel(element.panel);
        if (element.worldBound.Contains(panelPos))
            return true;

        var picked = element.panel.Pick(panelPos);
        return picked != null && (picked == element || element.Contains(picked));
    }

    private Vector2 GetMousePanelPos()
    {
        if (_root == null || _root.panel == null || Mouse.current == null)
            return Vector2.negativeInfinity;
        return MouseToUiToolkitPanel(_root.panel);
    }

    private static Vector2 MouseToUiToolkitPanel(IPanel panel)
    {
        Vector2 screen = Mouse.current.position.ReadValue();
        screen.y = Screen.height - screen.y;
        return RuntimePanelUtils.ScreenToPanel(panel, screen);
    }

    private void UpdateHudDpsBreakdownHover()
    {
        VisualElement anchor = null;
        bool perHit = false;
        if (_currentHudSkill != null && _hudDpsPreview.HasHitDamage)
        {
            if (IsPointerOverElement(_hudHitHoverRow))
            {
                anchor = _hudHitHoverRow;
                perHit = true;
            }
            else if (IsPointerOverElement(_hudDpsHoverRow))
            {
                anchor = _hudDpsHoverRow;
            }
        }

        bool visible = _hudDpsBreakdownBox != null && _hudDpsBreakdownBox.style.display == DisplayStyle.Flex;
        if (anchor == null)
        {
            if (visible)
                HideHudDpsBreakdown();
            return;
        }

        if (!visible || _hudBreakdownShowPerHit != perHit || _hudBreakdownAnchorRow != anchor)
            ShowHudDpsBreakdown(perHit, anchor);
        else
            RecalculateHudDpsBreakdownPosition();
    }

    private static bool ShouldHideWorldItemTooltip()
    {
        if (WorldItemInspection.IsCombatTooltipBlocked)
            return true;

        var windowManager = Object.FindFirstObjectByType<WindowManager>();
        return windowManager != null && windowManager.HasOpenWindow;
    }

    private void ShowTooltipInternal(InventoryItem item, VisualElement anchorSlot, WorldDroppedItem worldTargetItem)
    {
        if (_itemTooltipBox == null || item == null || item.Data == null) return;
        if (_pin.IsPinned && (_currentTargetItem != item || _targetAnchorSlot != anchorSlot))
            return;

        _currentTargetOrb = null;
        if (_orbTooltipBox != null) _orbTooltipBox.style.display = DisplayStyle.None;

        if (_currentTargetItem == item && _targetAnchorSlot == anchorSlot && _itemTooltipBox.style.display == DisplayStyle.Flex)
        {
            _worldTargetItem = worldTargetItem;
            if (worldTargetItem != null)
                _worldOwnerFrame = Time.frameCount;
            return;
        }

        _pin.Show();
        _currentTargetItem = item;
        _targetAnchorSlot = anchorSlot;
        _worldTargetItem = worldTargetItem;
        ClearHudSkillTarget();

        FillItemData(item);
        FillSkillData(item);
        if (worldTargetItem != null)
            _worldOwnerFrame = Time.frameCount;

        _itemTooltipBox.style.display = DisplayStyle.Flex;
        _itemTooltipBox.style.visibility = Visibility.Hidden;

        bool hasSkill = _skillTooltipBox.userData != null; // userData "true" если есть скиллы
        if (hasSkill)
        {
            _skillTooltipBox.style.display = DisplayStyle.Flex;
            _skillTooltipBox.style.visibility = Visibility.Hidden;
        }
        else
        {
            _skillTooltipBox.style.display = DisplayStyle.None;
        }

        _itemTooltipBox.MarkDirtyRepaint();
        RecalculatePosition();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(50);
    }

    /// <summary>
    /// Refreshes the item tooltip content and position if it is currently showing (e.g. after orb reroll).
    /// </summary>
    public void RefreshCurrentItemTooltip()
    {
        if (_currentTargetItem == null || _itemTooltipBox == null || _itemTooltipBox.style.display != DisplayStyle.Flex)
            return;
        FillItemData(_currentTargetItem);
        FillSkillData(_currentTargetItem);
        _itemTooltipBox.MarkDirtyRepaint();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
    }

    /// <summary>
    /// Crafting changes the contents of the same slot without a new pointer-over event.
    /// The old tooltip (including its pin) belongs to the pre-craft presentation.
    /// </summary>
    public void ShowCraftedItemTooltip(InventoryItem item, VisualElement anchorSlot, ItemTooltipPriceMode priceMode)
    {
        HideTooltipImmediate();
        if (item != null && anchorSlot != null)
            ShowTooltip(item, anchorSlot, priceMode);
    }

    public void HideTooltip()
    {
        if (_pin.IsPinned)
            return;
        HideTooltipImmediate();
    }

    public void HideTooltipImmediate()
    {
        _pin.Hide();
        _pinAnimElapsed = -1f;
        HidePinBadges();
        _inspectDetailsVisible = false;

        if (_itemTooltipBox != null)
        {
            _itemTooltipBox.style.display = DisplayStyle.None;
            _itemTooltipBox.style.visibility = Visibility.Hidden;
        }
        if (_skillTooltipBox != null)
        {
            _skillTooltipBox.style.display = DisplayStyle.None;
            _skillTooltipBox.style.visibility = Visibility.Hidden;
        }
        if (_orbTooltipBox != null)
        {
            _orbTooltipBox.style.display = DisplayStyle.None;
            _orbTooltipBox.style.visibility = Visibility.Hidden;
        }
        HideHudDpsBreakdown();
        HideBuffTooltip();
        SetTooltipClusterPicking(false);
        _currentTargetItem = null;
        _currentTargetOrb = null;
        _targetAnchorSlot = null;
        _worldTargetItem = null;
        _worldOwnerFrame = -1;
        ClearHudSkillTarget();
    }

    /// <summary>
    /// Hides tooltip if its anchor element was rebuilt/removed or referenced item no longer exists in inventory/stash.
    /// Call after inventory/stash redraws.
    /// </summary>
    public void ValidateCurrentTarget()
    {
        bool anyVisible =
            (_itemTooltipBox != null && _itemTooltipBox.style.display == DisplayStyle.Flex) ||
            (_skillTooltipBox != null && _skillTooltipBox.style.display == DisplayStyle.Flex) ||
            (_orbTooltipBox != null && _orbTooltipBox.style.display == DisplayStyle.Flex);

        if (!anyVisible) return;
        if (_currentHudSkill != null)
        {
            if (_hudSkillRect == null)
                HideTooltipImmediate();
            return;
        }
        if (_worldTargetItem != null)
        {
            if (!_worldTargetItem.CanInteract())
                HideTooltipImmediate();
            return;
        }
        if (_targetAnchorSlot == null || _targetAnchorSlot.panel == null)
        {
            HideTooltipImmediate();
            return;
        }

        if (_currentTargetItem != null && !IsItemStillPresent(_currentTargetItem))
            HideTooltipImmediate();
    }

    private bool UpdateWorldAnchorPosition(Vector3 worldPosition)
    {
        if (_root == null || _root.panel == null || _worldAnchor == null || Camera.main == null)
            return false;

        Vector3 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
        if (screenPoint.z < 0f)
            return false;

        Vector2 rootPoint = ScreenToTooltipRootLocal(screenPoint);
        _worldAnchor.style.left = rootPoint.x - 9f;
        _worldAnchor.style.top = rootPoint.y - 9f;
        return true;
    }

    private static bool IsItemStillPresent(InventoryItem target)
    {
        if (target == null) return false;

        var inv = InventoryManager.Instance;
        if (inv != null)
        {
            for (int i = 0; i < inv.BackpackSlotCount; i++)
            {
                if (ReferenceEquals(inv.GetItemAt(i, out _), target))
                    return true;
            }

            for (int i = 0; i < 6; i++)
            {
                if (ReferenceEquals(inv.GetItem(InventoryManager.EQUIP_OFFSET + i), target))
                    return true;
            }

            if (ReferenceEquals(inv.GetItem(InventoryManager.CRAFT_SLOT_INDEX), target))
                return true;
        }

        var stash = StashManager.Instance;
        if (stash != null)
        {
            for (int tab = 0; tab < stash.TabCount; tab++)
            {
                for (int i = 0; i < StashManager.STASH_SLOTS_PER_TAB; i++)
                {
                    if (ReferenceEquals(stash.GetItem(tab, i), target))
                        return true;
                }
            }
        }

        return false;
    }

    // --- Positioning Logic ---

    private void RecalculatePosition()
    {
        if (_targetAnchorSlot == null || _currentTargetItem == null) return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        // Якорь = слот или иконка; границы предмета берём по worldBound якоря (для иконки это весь предмет)
        Rect r = _targetAnchorSlot.worldBound;
        Vector2 pMin = _root.WorldToLocal(r.min);
        Vector2 pMax = _root.WorldToLocal(r.max);
        float itemLeft = pMin.x;
        float itemRight = pMax.x;
        float itemCenterY = (pMin.y + pMax.y) * 0.5f;

        // Размеры тултипов
        float itemW = _itemTooltipBox.resolvedStyle.width;
        if (float.IsNaN(itemW) || itemW < 10) itemW = _tooltipWidth;
        float itemH = _itemTooltipBox.resolvedStyle.height;
        if (float.IsNaN(itemH) || itemH < 10) itemH = 100f;

        bool hasSkill = _skillTooltipBox.style.display == DisplayStyle.Flex;
        float skillW = hasSkill ? _skillTooltipBox.resolvedStyle.width : 0;
        if (hasSkill && (float.IsNaN(skillW) || skillW < 10)) skillW = _tooltipWidth;
        float skillH = hasSkill ? _skillTooltipBox.resolvedStyle.height : 0;
        if (hasSkill && (float.IsNaN(skillH) || skillH < 10)) skillH = 100f;

        float maxHeight = Mathf.Max(itemH, skillH);
        float finalItemX;
        float finalSkillX;
        float y;
        bool isEquipmentSlot = _targetAnchorSlot.userData is int sid && sid >= 100;

        if (isEquipmentSlot)
        {
            // Экипировка: тултип слева от предмета
            y = pMin.y;
            finalItemX = itemLeft - itemW - _gap;
            finalSkillX = hasSkill ? (finalItemX - _gap - skillW) : 0;
        }
        else
        {
            // Рюкзак/склад: сначала пробуем сверху (центр по горизонтали), иначе справа/слева (центр по вертикали)
            float itemTop = pMin.y;
            float itemCenterX = (itemLeft + itemRight) * 0.5f;
            float totalW = itemW + (hasSkill ? (_gap + skillW) : 0);
            float yAbove = itemTop - maxHeight - _gap;

            if (yAbove >= _screenPadding)
            {
                // Место сверху есть — тултип над предметом, центрирован по горизонтали
                y = yAbove;
                finalItemX = Mathf.Clamp(itemCenterX - itemW * 0.5f, _screenPadding, screenW - itemW - _screenPadding);
                finalSkillX = hasSkill ? Mathf.Clamp(finalItemX + itemW + _gap, _screenPadding, screenW - skillW - _screenPadding) : 0;
                if (hasSkill && finalSkillX + skillW > screenW - _screenPadding)
                    finalSkillX = Mathf.Clamp(finalItemX - _gap - skillW, _screenPadding, screenW - skillW - _screenPadding);
            }
            else
            {
                // Сверху не влезает — справа или слева, центрирован по вертикали
                y = itemCenterY - maxHeight * 0.5f;
                if (itemRight + totalW + _screenPadding <= screenW)
                {
                    finalItemX = itemRight + _gap;
                    finalSkillX = hasSkill ? (finalItemX + itemW + _gap) : 0;
                }
                else if (itemLeft - totalW - _screenPadding >= 0)
                {
                    finalItemX = itemLeft - itemW - _gap;
                    finalSkillX = hasSkill ? (finalItemX - _gap - skillW) : 0;
                }
                else
                {
                    finalItemX = itemRight + _gap;
                    finalSkillX = hasSkill ? Mathf.Clamp(finalItemX - _gap - skillW, _screenPadding, screenW - skillW - _screenPadding) : 0;
                }
            }
        }

        // Ограничение по вертикали
        if (y + maxHeight > screenH - _screenPadding)
            y = screenH - maxHeight - _screenPadding;
        if (y < _screenPadding)
            y = _screenPadding;
        finalItemX = Mathf.Clamp(finalItemX, _screenPadding, screenW - itemW - _screenPadding);

        // Скилл-тултип строго слева или справа: не перекрывать ни тултип предмета, ни иконку предмета (itemLeft..itemRight)
        if (hasSkill)
        {
            float zoneLeft = Mathf.Min(finalItemX, itemLeft);
            float zoneRight = Mathf.Max(finalItemX + itemW, itemRight);
            float skillRightX = Mathf.Max(finalItemX + itemW + _gap, itemRight + _gap);
            float skillLeftX = Mathf.Min(finalItemX - _gap - skillW, itemLeft - _gap - skillW);
            bool fitsRight = skillRightX + skillW <= screenW - _screenPadding;
            bool fitsLeft = skillLeftX >= _screenPadding;
            if (fitsRight)
                finalSkillX = skillRightX;
            else if (fitsLeft)
                finalSkillX = skillLeftX;
            else
            {
                float spaceRight = screenW - _screenPadding - skillRightX;
                float spaceLeft = skillLeftX - _screenPadding;
                if (spaceRight >= spaceLeft)
                    finalSkillX = Mathf.Clamp(skillRightX, skillRightX, screenW - skillW - _screenPadding);
                else
                    finalSkillX = Mathf.Clamp(skillLeftX, _screenPadding, skillLeftX);
            }
            finalSkillX = Mathf.Clamp(finalSkillX, _screenPadding, screenW - skillW - _screenPadding);
            // Жёстко: не заходить в зону предмета и тултипа (даже после clamp)
            if (finalSkillX + skillW > zoneLeft - _gap && finalSkillX < zoneRight + _gap)
            {
                if (finalSkillX >= zoneLeft)
                    finalSkillX = zoneRight + _gap;
                else
                    finalSkillX = zoneLeft - _gap - skillW;
                finalSkillX = Mathf.Clamp(finalSkillX, _screenPadding, screenW - skillW - _screenPadding);
            }
        }

        _itemTooltipBox.style.left = finalItemX;
        _itemTooltipBox.style.top = y;
        _itemTooltipBox.style.visibility = Visibility.Visible;

        if (hasSkill)
        {
            _skillTooltipBox.style.left = finalSkillX;
            _skillTooltipBox.style.top = y;
            _skillTooltipBox.style.visibility = Visibility.Visible;
        }
    }

    private void RecalculateOrbPosition()
    {
        if (_targetAnchorSlot == null || _currentTargetOrb == null || _orbTooltipBox == null) return;
        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;
        Rect r = _targetAnchorSlot.worldBound;
        Vector2 slotPos = _root.WorldToLocal(r.position);
        float orbW = _orbTooltipBox.resolvedStyle.width;
        if (float.IsNaN(orbW) || orbW < 10) orbW = _tooltipWidth;
        float orbH = _orbTooltipBox.resolvedStyle.height;
        if (float.IsNaN(orbH) || orbH < 10) orbH = 40f;
        float slotW = 32f;
        float x = slotPos.x + slotW + _gap;
        if (x + orbW + _screenPadding > screenW) x = slotPos.x - orbW - _gap;
        if (x < _screenPadding) x = _screenPadding;
        float y = slotPos.y;
        if (y + orbH > screenH - _screenPadding) y = screenH - orbH - _screenPadding;
        if (y < _screenPadding) y = _screenPadding;
        _orbTooltipBox.style.left = x;
        _orbTooltipBox.style.top = y;
        _orbTooltipBox.style.visibility = Visibility.Visible;
    }

    private void RecalculateHudSkillPosition()
    {
        if (_currentHudSkill == null || _skillTooltipBox == null || _root == null)
            return;
        if (_hudSkillRect == null || !TryGetHudSkillSlotRectInRoot(_hudSkillRect, out Vector2 pMin, out Vector2 pMax))
            return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;

        float skillW = _skillTooltipBox.resolvedStyle.width;
        if (float.IsNaN(skillW) || skillW < 10) skillW = _tooltipWidth;
        float skillH = _skillTooltipBox.resolvedStyle.height;
        if (float.IsNaN(skillH) || skillH < 10) skillH = 80f;

        Vector2 pos = CalculateHudSkillTooltipPosition(
            pMin,
            pMax,
            skillW,
            skillH,
            screenW,
            screenH,
            HudSkillTooltipGap,
            HudSkillTooltipPadding);
        _skillTooltipBox.style.left = pos.x;
        _skillTooltipBox.style.top = pos.y;
        _skillTooltipBox.style.visibility = Visibility.Visible;
        RecalculateHudDpsBreakdownPosition();
    }

    public static Vector2 CalculateHudSkillTooltipPosition(
        Vector2 slotMin,
        Vector2 slotMax,
        float tooltipWidth,
        float tooltipHeight,
        float screenWidth,
        float screenHeight,
        float gap,
        float padding)
    {
        float slotLeft = slotMin.x;
        float slotRight = slotMax.x;
        float slotTop = slotMin.y;
        float slotBottom = slotMax.y;
        float centerX = (slotLeft + slotRight) * 0.5f;
        float x = Mathf.Clamp(centerX - tooltipWidth * 0.5f, padding, Mathf.Max(padding, screenWidth - tooltipWidth - padding));
        float yAbove = slotTop - tooltipHeight - gap;
        float y = yAbove >= padding ? yAbove : slotBottom + gap;
        if (y + tooltipHeight > screenHeight - padding)
            y = Mathf.Max(padding, screenHeight - tooltipHeight - padding);
        if (y < padding)
            y = padding;
        return new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    public static Vector2 CalculateHudNestedTooltipPosition(
        Vector2 anchorMin,
        Vector2 anchorMax,
        float tooltipWidth,
        float tooltipHeight,
        float screenWidth,
        float screenHeight,
        float gap,
        float padding)
    {
        float x = anchorMax.x + gap;
        if (x + tooltipWidth + padding > screenWidth)
            x = anchorMin.x - tooltipWidth - gap;
        x = Mathf.Clamp(x, padding, Mathf.Max(padding, screenWidth - tooltipWidth - padding));
        float y = anchorMin.y;
        if (y + tooltipHeight > screenHeight - padding)
            y = Mathf.Max(padding, screenHeight - tooltipHeight - padding);
        if (y < padding)
            y = padding;
        return new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    /// <summary>
    /// Places a nested tooltip on the linked word: above if possible, otherwise below, otherwise beside.
    /// </summary>
    public static Vector2 CalculateLinkedTextTooltipPosition(
        Vector2 textMin,
        Vector2 textMax,
        float tooltipWidth,
        float tooltipHeight,
        float screenWidth,
        float screenHeight,
        float gap,
        float padding)
    {
        float x = textMin.x;
        float y = textMin.y - tooltipHeight - gap;
        if (y < padding)
        {
            y = textMax.y + gap;
            if (y + tooltipHeight > screenHeight - padding)
            {
                y = textMin.y;
                x = textMax.x + gap;
                if (x + tooltipWidth + padding > screenWidth)
                    x = textMin.x - tooltipWidth - gap;
            }
        }

        x = Mathf.Clamp(x, padding, Mathf.Max(padding, screenWidth - tooltipWidth - padding));
        y = Mathf.Clamp(y, padding, Mathf.Max(padding, screenHeight - tooltipHeight - padding));
        return new Vector2(Mathf.Round(x), Mathf.Round(y));
    }

    private bool TryGetHudSkillSlotRectInRoot(RectTransform slotRect, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        if (_root == null || _root.panel == null || slotRect == null)
            return false;

        Canvas canvas = slotRect.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        Vector3[] corners = new Vector3[4];
        slotRect.GetWorldCorners(corners);
        Vector2 topLeft = ScreenToTooltipRootLocal(RectTransformUtility.WorldToScreenPoint(camera, corners[1]));
        Vector2 bottomRight = ScreenToTooltipRootLocal(RectTransformUtility.WorldToScreenPoint(camera, corners[3]));

        float left = Mathf.Min(topLeft.x, bottomRight.x);
        float top = Mathf.Min(topLeft.y, bottomRight.y);
        float right = Mathf.Max(topLeft.x, bottomRight.x);
        float bottom = Mathf.Max(topLeft.y, bottomRight.y);
        min = new Vector2(left, top);
        max = new Vector2(right, bottom);
        return right - left >= 1f && bottom - top >= 1f;
    }

    private Vector2 ScreenToTooltipRootLocal(Vector2 screenPoint)
    {
        Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
            _root.panel,
            new Vector2(screenPoint.x, Screen.height - screenPoint.y));
        return _root.WorldToLocal(panelPoint);
    }

    private void ClearHudSkillTarget()
    {
        _currentHudSkill = null;
        _hudSkillRect = null;
        _hudSkillSource = null;
        _hudSkillSlotIndex = -1;
        _hudDpsHoverRow = null;
        _hudHitHoverRow = null;
        _hudBreakdownAnchorRow = null;
        _hudDpsPreview = default;
        HideHudDpsBreakdown();
        HideBuffTooltip();
        _buffNameHoverTargets.Clear();
    }

    // --- Fill Data Logic (SKILLS) - ТВОЙ КОД ---

    private void FillHudSkillTooltip(SkillDataSO skill)
    {
        _skillTooltipBox.Clear();
        bool hasSkill = skill != null;
        _skillTooltipBox.userData = hasSkill ? "true" : null;
        if (!hasSkill)
            return;

        AddSkillTooltipBlock(skill, typeKey: null, typeFallback: null, addDivider: false, includeDps: true);
        _skillTooltipBox.Add(_skillLockHint);
    }

    private void FillSkillData(InventoryItem item)
    {
        _skillTooltipBox.Clear(); 
        bool hasSkill = item.GrantedSkills != null && item.GrantedSkills.Count > 0;
        _skillTooltipBox.userData = hasSkill ? "true" : null; // Маркер для ShowTooltip

        if (hasSkill)
        {
            for (int i = 0; i < item.GrantedSkills.Count; i++)
            {
                var skill = item.GrantedSkills[i];
                if (skill == null) continue;

                string slotKey = "skill_type_granted";
                if (item.Data is WeaponItemSO weapon)
                {
                    if (weapon.IsTwoHanded) slotKey = (i == 0) ? "skill_type_mainhand" : "skill_type_offhand";
                    else slotKey = "skill_type_weapon";
                }

                AddSkillTooltipBlock(skill, slotKey, i == 0 ? "Primary Action" : "Secondary Action", i > 0);
            }

            _skillTooltipBox.Add(_skillLockHint);
        }
    }

    private void AddSkillTooltipBlock(SkillDataSO skill, string typeKey, string typeFallback, bool addDivider, bool includeDps = false)
    {
        if (skill == null)
            return;

        if (addDivider)
        {
            var div = CreateDivider();
            div.style.marginTop = 4;
            div.style.marginBottom = 4;
            _skillTooltipBox.Add(div);
        }

        if (!string.IsNullOrEmpty(typeKey))
        {
            var typeLabel = CreateLabel("", 7, FontStyle.Italic, TextAnchor.UpperLeft);
            typeLabel.style.color = new StyleColor(_colSkillType);
            typeLabel.style.width = Length.Percent(100);
            _skillTooltipBox.Add(typeLabel);
            LocalizeLabel(typeLabel, TABLE_MENU, typeKey, typeFallback ?? "");
        }

        var nameLabel = CreateLabel("", 8, FontStyle.Bold, TextAnchor.MiddleCenter);
        nameLabel.style.color = new StyleColor(Color.cyan);
        nameLabel.style.marginTop = 2;
        nameLabel.style.width = Length.Percent(100);
        nameLabel.style.alignSelf = Align.Center;
        _skillTooltipBox.Add(nameLabel);
        LocalizeLabel(nameLabel, TABLE_SKILLS, GetSkillNameKey(skill), skill.SkillName);

        if (skill.Icon != null)
        {
            var icon = new Image();
            icon.sprite = skill.Icon;
            icon.style.width = 24;
            icon.style.height = 24;
            icon.style.alignSelf = Align.Center;
            icon.style.marginTop = 2;
            _skillTooltipBox.Add(icon);
        }

        if (includeDps)
            AddHudSkillDpsBlock(skill);

        FillSkillDescription(_skillTooltipBox, skill);
    }

    private void AddHudSkillDpsBlock(SkillDataSO skill)
    {
        HideHudDpsBreakdown();
        _hudDpsHoverRow = null;
        _hudHitHoverRow = null;
        _hudBreakdownAnchorRow = null;

        PlayerStats player = Object.FindFirstObjectByType<PlayerStats>();
        if (player == null)
            return;

        SkillDpsPreview preview = SkillDpsPreview.Build(skill, player, _hudSkillSlotIndex);
        _hudDpsPreview = preview;
        if (!preview.HasContent)
            return;

        var block = new VisualElement { name = "HudSkillDpsBlock" };
        block.pickingMode = PickingMode.Ignore;
        block.style.alignSelf = Align.Stretch;
        block.style.width = Length.Percent(100);
        block.style.marginTop = 2;
        block.style.marginBottom = 0;
        block.style.minHeight = 0;

        if (preview.HasHitDamage)
        {
            _hudHitHoverRow = CreateHudDpsRow(
                "skills.hit_damage",
                "Урон за удар",
                SkillDpsPreview.FormatAmount(preview.Hit.TotalPerHit),
                _colNormalText,
                hoverable: true);
            block.Add(_hudHitHoverRow);

            _hudDpsHoverRow = CreateHudDpsRow(
                "skills.dps",
                "Урон в сек",
                SkillDpsPreview.FormatAmount(preview.Hit.TotalDps),
                _colNormalText,
                hoverable: true);
            block.Add(_hudDpsHoverRow);
        }

        for (int i = 0; i < preview.Dots.Length; i++)
        {
            SkillDotDpsPreview dot = preview.Dots[i];
            GetDotDamageLabel(dot.Type, out string damageKey, out string damageFallback);
            GetDotChanceLabel(dot.Type, out string chanceKey, out string chanceFallback);
            block.Add(CreateHudDpsRow(
                damageKey,
                damageFallback,
                $"{SkillDpsPreview.FormatAmount(dot.TickDps)}/с",
                _colNormalText,
                hoverable: false));
            block.Add(CreateHudDpsRow(
                chanceKey,
                chanceFallback,
                SkillDpsPreview.FormatChance(dot.ChancePercent),
                _colNormalText,
                hoverable: false));
        }

        _skillTooltipBox.Add(block);
    }

    private VisualElement CreateHudDpsRow(string key, string fallback, string value, Color color, bool hoverable)
    {
        var row = new VisualElement { name = hoverable ? "HudSkillTotalDpsRow" : "HudSkillDpsRow" };
        row.pickingMode = hoverable ? PickingMode.Position : PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.width = Length.Percent(100);
        row.style.height = 8;
        row.style.marginTop = 1;
        row.style.paddingTop = 0;
        row.style.paddingBottom = 0;
        row.style.minHeight = 0;

        var nameLabel = CreateCompactDpsLabel("", TextAnchor.MiddleLeft);
        nameLabel.style.flexGrow = 1;
        nameLabel.style.flexShrink = 1;
        nameLabel.style.color = new StyleColor(color);
        LocalizeLabel(nameLabel, TABLE_SKILLS, key, fallback);

        var valueLabel = CreateCompactDpsLabel(value, TextAnchor.MiddleRight);
        valueLabel.style.flexGrow = 0;
        valueLabel.style.marginLeft = 4;
        valueLabel.style.color = new StyleColor(color);

        row.Add(nameLabel);
        row.Add(valueLabel);
        return row;
    }

    private Label CreateCompactDpsLabel(string text, TextAnchor align)
    {
        var lbl = CreateLabel(text, 7, FontStyle.Normal, align);
        lbl.pickingMode = PickingMode.Ignore;
        lbl.style.paddingTop = 0;
        lbl.style.paddingBottom = 0;
        lbl.style.paddingLeft = 0;
        lbl.style.paddingRight = 0;
        lbl.style.marginTop = 0;
        lbl.style.marginBottom = 0;
        lbl.style.minHeight = 0;
        lbl.style.height = 8;
        lbl.style.whiteSpace = WhiteSpace.NoWrap;
        lbl.style.overflow = Overflow.Hidden;
        return lbl;
    }

    private void ShowHudDpsBreakdown(bool perHit, VisualElement anchorRow)
    {
        if (_hudDpsBreakdownBox == null || !_hudDpsPreview.HasHitDamage || anchorRow == null)
            return;

        _hudBreakdownShowPerHit = perHit;
        _hudBreakdownAnchorRow = anchorRow;
        _hudDpsBreakdownBox.Clear();
        SkillHitDpsPreview hit = _hudDpsPreview.Hit;
        AddHudDpsBreakdownChannel("skills.damage_physical", "Физ.", perHit ? hit.PhysicalPerHit : hit.PhysicalDps, _colNormalText);
        AddHudDpsBreakdownChannel("skills.damage_fire", "Огонь", perHit ? hit.FirePerHit : hit.FireDps, _colFireText);
        AddHudDpsBreakdownChannel("skills.damage_cold", "Холод", perHit ? hit.ColdPerHit : hit.ColdDps, _colColdText);
        AddHudDpsBreakdownChannel("skills.damage_lightning", "Молния", perHit ? hit.LightningPerHit : hit.LightningDps, _colLightningText);

        if (_hudDpsBreakdownBox.childCount == 0)
            return;

        _hudDpsBreakdownBox.style.display = DisplayStyle.Flex;
        _hudDpsBreakdownBox.style.visibility = Visibility.Hidden;
        RecalculateHudDpsBreakdownPosition();
        if (_root != null)
            _root.schedule.Execute(RecalculateHudDpsBreakdownPosition).ExecuteLater(1);
    }

    private void AddHudDpsBreakdownChannel(string key, string fallback, float amount, Color color)
    {
        if (amount <= 0.049f)
            return;

        AddHudDpsBreakdownRow(key, fallback, amount, color);
    }

    private void AddHudDpsBreakdownRow(string key, string fallback, float dps, Color color)
    {
        var row = new VisualElement();
        row.pickingMode = PickingMode.Ignore;
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.alignItems = Align.Center;
        row.style.width = Length.Percent(100);
        row.style.height = 8;
        row.style.marginTop = 1;
        row.style.minHeight = 0;

        var nameLabel = CreateCompactDpsLabel("", TextAnchor.MiddleLeft);
        nameLabel.style.flexGrow = 1;
        nameLabel.style.color = new StyleColor(color);
        LocalizeLabel(nameLabel, TABLE_SKILLS, key, fallback);

        var valueLabel = CreateCompactDpsLabel(SkillDpsPreview.FormatAmount(dps), TextAnchor.MiddleRight);
        valueLabel.style.marginLeft = 4;
        valueLabel.style.color = new StyleColor(color);

        row.Add(nameLabel);
        row.Add(valueLabel);
        _hudDpsBreakdownBox.Add(row);
    }

    private void HideHudDpsBreakdown()
    {
        if (_hudDpsBreakdownBox == null)
            return;
        _hudDpsBreakdownBox.style.display = DisplayStyle.None;
        _hudDpsBreakdownBox.style.visibility = Visibility.Hidden;
    }

    private void RecalculateHudDpsBreakdownPosition()
    {
        if (_hudDpsBreakdownBox == null || _root == null || _hudBreakdownAnchorRow == null)
            return;
        if (_hudDpsBreakdownBox.style.display != DisplayStyle.Flex)
            return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;
        Rect row = _hudBreakdownAnchorRow.worldBound;
        Vector2 min = _root.WorldToLocal(row.min);
        Vector2 max = _root.WorldToLocal(row.max);

        float boxW = _hudDpsBreakdownBox.resolvedStyle.width;
        if (float.IsNaN(boxW) || boxW < 10f)
            boxW = HudDpsBreakdownWidth;
        float boxH = _hudDpsBreakdownBox.resolvedStyle.height;
        if (float.IsNaN(boxH) || boxH < 8f)
            boxH = 8f + _hudDpsBreakdownBox.childCount * 9f;

        Vector2 pos = CalculateHudNestedTooltipPosition(
            min,
            max,
            boxW,
            boxH,
            screenW,
            screenH,
            HudSkillTooltipGap,
            HudSkillTooltipPadding);
        _hudDpsBreakdownBox.style.left = pos.x;
        _hudDpsBreakdownBox.style.top = pos.y;
        _hudDpsBreakdownBox.style.visibility = Visibility.Visible;
    }

    private static void GetDotDamageLabel(AilmentType type, out string key, out string fallback)
    {
        switch (type)
        {
            case AilmentType.Bleed:
                key = "skills.dot_damage_bleed";
                fallback = "Урон от кровотечения";
                return;
            case AilmentType.Poison:
                key = "skills.dot_damage_poison";
                fallback = "Урон от яда";
                return;
            case AilmentType.Ignite:
                key = "skills.dot_damage_ignite";
                fallback = "Урон от поджига";
                return;
            default:
                key = "skills.dot";
                fallback = "Постепенный урон";
                return;
        }
    }

    private static void GetDotChanceLabel(AilmentType type, out string key, out string fallback)
    {
        switch (type)
        {
            case AilmentType.Bleed:
                key = "skills.dot_chance_bleed";
                fallback = "Шанс кровотечения";
                return;
            case AilmentType.Poison:
                key = "skills.dot_chance_poison";
                fallback = "Шанс яда";
                return;
            case AilmentType.Ignite:
                key = "skills.dot_chance_ignite";
                fallback = "Шанс поджига";
                return;
            default:
                key = "skills.dot_chance";
                fallback = "Шанс наложить";
                return;
        }
    }

    private void FillSkillDescription(VisualElement parent, SkillDataSO skill)
    {
        HideBuffTooltip();
        _buffNameHoverTargets.Clear();
        if (parent == null || skill == null)
            return;

        int skillSlotIndex = _hudSkillSlotIndex;
        float effectiveCooldown = skill.Cooldown;
        if (skillSlotIndex >= 0)
        {
            PlayerStats player = Object.FindFirstObjectByType<PlayerStats>();
            effectiveCooldown = SkillCooldownRecovery.Resolve(skill.Cooldown, player, skillSlotIndex);
        }

        string localeCode = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
        System.Func<StatType, string> statResolver = stat =>
        {
            string localized = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE_MENU, $"stats.{stat}");
            return string.IsNullOrWhiteSpace(localized) ? SkillDescriptionGenerator.Humanize(stat.ToString()) : localized;
        };

        string legacyDescription = null;
        if (skill.DescriptionMode != SkillDescriptionMode.Automatic)
        {
            legacyDescription = LocalizationSettings.StringDatabase.GetLocalizedString(
                TABLE_SKILLS,
                GetSkillDescriptionKey(skill));
            if (string.IsNullOrWhiteSpace(legacyDescription))
                legacyDescription = skill.Description;
        }

        List<SkillDescriptionLine> lines;
        if (skill.DescriptionMode == SkillDescriptionMode.LegacyOnly)
        {
            lines = new List<SkillDescriptionLine>();
            if (!string.IsNullOrWhiteSpace(legacyDescription))
                lines.Add(SkillDescriptionLine.Plain(legacyDescription));
        }
        else
        {
            lines = SkillDescriptionGenerator.BuildAutomaticLines(skill, localeCode, statResolver);
            if (skill.DescriptionMode == SkillDescriptionMode.AutomaticWithLegacy
                && !string.IsNullOrWhiteSpace(legacyDescription))
            {
                lines.Add(SkillDescriptionLine.Plain(string.Empty));
                lines.Add(SkillDescriptionLine.Plain(legacyDescription));
            }
        }

        var body = new VisualElement { name = "SkillDescBody" };
        body.pickingMode = PickingMode.Ignore;
        body.style.width = Length.Percent(100);
        body.style.alignItems = Align.Stretch;
        body.style.marginTop = 3;
        body.style.minHeight = 0;

        for (int i = 0; i < lines.Count; i++)
            body.Add(CreateSkillDescriptionLine(lines[i], i == 0));

        if (effectiveCooldown > 0)
        {
            string cooldownLabel = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE_SKILLS, "skills.cooldown");
            if (string.IsNullOrWhiteSpace(cooldownLabel) || cooldownLabel.IndexOf("translation found", System.StringComparison.OrdinalIgnoreCase) >= 0)
                cooldownLabel = localeCode.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase) ? "Перезарядка" : "Cooldown";
            body.Add(CreateSkillMetaLabel($"{cooldownLabel}: {effectiveCooldown:0.##}s", first: lines.Count > 0));
        }

        if (skill.ManaCost > 0)
        {
            string manaLabel = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE_SKILLS, "skills.manaCost");
            if (string.IsNullOrWhiteSpace(manaLabel) || manaLabel.IndexOf("translation found", System.StringComparison.OrdinalIgnoreCase) >= 0)
                manaLabel = localeCode.StartsWith("ru", System.StringComparison.OrdinalIgnoreCase) ? "Расход маны" : "Mana Cost";
            body.Add(CreateSkillMetaLabel($"{manaLabel}: {skill.ManaCost:0.##}", first: lines.Count == 0 && effectiveCooldown <= 0));
        }

        parent.Add(body);
        if (_root != null)
        {
            if (_currentHudSkill != null)
                _root.schedule.Execute(RecalculateHudSkillPosition).ExecuteLater(1);
            else
                _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
        }
    }

    private VisualElement CreateSkillDescriptionLine(SkillDescriptionLine line, bool first)
    {
        if (!line.HasLink)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                var spacer = new VisualElement();
                spacer.pickingMode = PickingMode.Ignore;
                spacer.style.height = 2;
                spacer.style.width = Length.Percent(100);
                return spacer;
            }

            return CreateSkillRichLabel(line.Text, SkillDescriptionFontSize, FontStyle.Normal, _colNormalText, first);
        }

        var block = new VisualElement { name = "SkillBuffLine" };
        block.pickingMode = PickingMode.Ignore;
        block.style.flexDirection = FlexDirection.Column;
        block.style.alignItems = Align.Stretch;
        block.style.width = Length.Percent(100);
        block.style.marginTop = first ? 0 : 2;
        block.style.minHeight = 0;

        string prefix = line.Prefix?.Trim();
        if (!string.IsNullOrEmpty(prefix))
            block.Add(CreateSkillRichLabel(prefix, SkillDescriptionFontSize, FontStyle.Normal, _colNormalText, first: true));

        var nameHit = CreateBuffNameHit(line.LinkedName, line.LinkedEffect);
        _buffNameHoverTargets.Add(nameHit);
        block.Add(nameHit);

        string suffix = line.Suffix?.Trim();
        if (!string.IsNullOrEmpty(suffix))
            block.Add(CreateSkillRichLabel(suffix, SkillDescriptionFontSize, FontStyle.Normal, _colNormalText, first: false));

        return block;
    }

    private VisualElement CreateBuffNameHit(string text, StatusEffectSO effect)
    {
        var hit = new VisualElement { name = "BuffNameHit" };
        hit.pickingMode = PickingMode.Position;
        hit.userData = effect;
        hit.style.flexDirection = FlexDirection.Row;
        hit.style.alignItems = Align.Center;
        hit.style.flexGrow = 0;
        hit.style.flexShrink = 1;
        hit.style.width = Length.Percent(100);
        hit.style.height = StyleKeyword.Auto;
        hit.style.minHeight = SkillDescriptionLineMinHeight;
        hit.style.marginTop = 1;
        hit.style.marginBottom = 0;
        hit.style.paddingLeft = 0;
        hit.style.paddingRight = 0;
        hit.style.paddingTop = 1;
        hit.style.paddingBottom = 1;

        var label = CreateSkillInlineLabel(text, _colBuffName, FontStyle.Bold, hoverable: false);
        label.pickingMode = PickingMode.Ignore;
        label.style.fontSize = SkillBuffNameFontSize;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexGrow = 1;
        label.style.flexShrink = 1;
        label.style.width = Length.Percent(100);
        label.style.minHeight = SkillDescriptionLineMinHeight;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        hit.Add(label);
        return hit;
    }

    private Label CreateSkillRichLabel(string text, int size, FontStyle style, Color fallbackColor, bool first)
    {
        var label = CreateLabel(SkillDescriptionHighlight.Colorize(text), size, style, TextAnchor.UpperLeft);
        label.enableRichText = true;
        label.pickingMode = PickingMode.Ignore;
        label.style.color = new StyleColor(fallbackColor);
        label.style.marginTop = first ? 0 : 2;
        label.style.marginBottom = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.minHeight = 0;
        label.style.width = Length.Percent(100);
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private Label CreateSkillInlineLabel(string text, Color color, FontStyle style, bool hoverable)
    {
        var label = CreateLabel(text, SkillDescriptionFontSize, style, TextAnchor.UpperLeft);
        label.pickingMode = hoverable ? PickingMode.Position : PickingMode.Ignore;
        label.style.color = new StyleColor(color);
        label.style.marginTop = 0;
        label.style.marginBottom = 0;
        label.style.paddingTop = 0;
        label.style.paddingBottom = 0;
        label.style.paddingLeft = 0;
        label.style.paddingRight = 0;
        label.style.minHeight = 0;
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private Label CreateSkillMetaLabel(string text, bool first)
    {
        var label = CreateLabel(SkillDescriptionHighlight.Colorize(text), 7, FontStyle.Normal, TextAnchor.UpperLeft);
        label.enableRichText = true;
        label.pickingMode = PickingMode.Ignore;
        label.style.color = new StyleColor(new Color(0.67f, 0.67f, 0.67f));
        label.style.marginTop = first ? 4 : 1;
        label.style.minHeight = 0;
        label.style.width = Length.Percent(100);
        return label;
    }

    private void UpdateBuffTooltipHover()
    {
        if (_skillTooltipBox == null || _skillTooltipBox.style.display != DisplayStyle.Flex)
        {
            HideBuffTooltip();
            return;
        }

        VisualElement anchor = null;
        StatusEffectSO effect = null;
        for (int i = 0; i < _buffNameHoverTargets.Count; i++)
        {
            VisualElement target = _buffNameHoverTargets[i];
            if (!IsPointerOverElement(target))
                continue;
            anchor = target;
            effect = target.userData as StatusEffectSO;
            break;
        }

        if (effect == null && _hoveredBuff != null && IsPanelPointOver(_buffTooltipBox, GetMousePanelPos()))
        {
            effect = _hoveredBuff;
            anchor = _buffTooltipAnchor;
        }

        if (effect == null || anchor == null)
        {
            HideBuffTooltip();
            return;
        }

        if (_hoveredBuff != effect || _buffTooltipAnchor != anchor
            || _buffTooltipBox == null || _buffTooltipBox.style.display != DisplayStyle.Flex)
            ShowBuffTooltip(effect, anchor);
        else
            RecalculateBuffTooltipPosition();
    }

    private void ShowBuffTooltip(StatusEffectSO effect, VisualElement anchor)
    {
        if (_buffTooltipBox == null || effect == null || anchor == null)
            return;

        _hoveredBuff = effect;
        _buffTooltipAnchor = anchor;
        _buffTooltipBox.Clear();

        bool ru = (LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en")
            .StartsWith("ru", System.StringComparison.OrdinalIgnoreCase);
        string localeCode = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";

        var title = CreateLabel(effect.GetDisplayName(ru), 8, FontStyle.Bold, TextAnchor.UpperLeft);
        title.pickingMode = PickingMode.Ignore;
        title.style.color = new StyleColor(_colBuffName);
        title.style.width = Length.Percent(100);
        title.style.minHeight = 0;
        title.style.marginTop = 0;
        title.style.paddingBottom = 0;
        _buffTooltipBox.Add(title);

        string body = SkillDescriptionGenerator.BuildStatusEffectTooltip(
            effect,
            localeCode,
            stat =>
            {
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(TABLE_MENU, $"stats.{stat}");
                return string.IsNullOrWhiteSpace(localized) ? SkillDescriptionGenerator.Humanize(stat.ToString()) : localized;
            });

        if (!string.IsNullOrWhiteSpace(body))
        {
            var desc = CreateLabel(SkillDescriptionHighlight.Colorize(body), 6, FontStyle.Normal, TextAnchor.UpperLeft);
            desc.enableRichText = true;
            desc.pickingMode = PickingMode.Ignore;
            desc.style.color = new StyleColor(_colNormalText);
            desc.style.width = Length.Percent(100);
            desc.style.minHeight = 0;
            desc.style.marginTop = 2;
            desc.style.whiteSpace = WhiteSpace.Normal;
            _buffTooltipBox.Add(desc);
        }

        _buffTooltipBox.style.display = DisplayStyle.Flex;
        _buffTooltipBox.style.visibility = Visibility.Hidden;
        RecalculateBuffTooltipPosition();
        if (_root != null)
            _root.schedule.Execute(RecalculateBuffTooltipPosition).ExecuteLater(1);
    }

    private void HideBuffTooltip()
    {
        _hoveredBuff = null;
        _buffTooltipAnchor = null;
        if (_buffTooltipBox == null)
            return;
        _buffTooltipBox.style.display = DisplayStyle.None;
        _buffTooltipBox.style.visibility = Visibility.Hidden;
    }

    private void RecalculateBuffTooltipPosition()
    {
        if (_buffTooltipBox == null || _root == null || _buffTooltipAnchor == null)
            return;
        if (_buffTooltipBox.style.display != DisplayStyle.Flex)
            return;

        float screenW = _root.resolvedStyle.width;
        float screenH = _root.resolvedStyle.height;
        VisualElement textAnchor = _buffTooltipAnchor.childCount > 0
            ? _buffTooltipAnchor[0]
            : _buffTooltipAnchor;
        Rect row = textAnchor.worldBound;
        Vector2 min = _root.WorldToLocal(row.min);
        Vector2 max = _root.WorldToLocal(row.max);

        float boxW = _buffTooltipBox.resolvedStyle.width;
        if (float.IsNaN(boxW) || boxW < 10f)
            boxW = BuffTooltipWidth;
        float boxH = _buffTooltipBox.resolvedStyle.height;
        if (float.IsNaN(boxH) || boxH < 8f)
            boxH = 36f;

        Vector2 pos = CalculateLinkedTextTooltipPosition(
            min,
            max,
            boxW,
            boxH,
            screenW,
            screenH,
            HudSkillTooltipGap,
            HudSkillTooltipPadding);
        _buffTooltipBox.style.left = pos.x;
        _buffTooltipBox.style.top = pos.y;
        _buffTooltipBox.style.visibility = Visibility.Visible;
    }

    private static string GetSkillNameKey(SkillDataSO skill)
    {
        if (skill == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(skill.NameKey)
            ? skill.NameKey
            : $"skills.{skill.ID}";
    }

    private static string GetSkillDescriptionKey(SkillDataSO skill)
    {
        if (skill == null)
            return string.Empty;

        return !string.IsNullOrWhiteSpace(skill.DescriptionKey)
            ? skill.DescriptionKey
            : $"skills.{skill.ID}.description";
    }

    // --- Fill Data Logic (ITEMS) ---

    private void FillItemData(InventoryItem item)
    {
        _inspectDetailsVisible = IsInspectModifierHeld();
        LocalizeLabel(_headerLabel, TABLE_ITEMS, $"items.{item.Data.ID}", item.Data.ItemName);
        _headerLabel.style.color = new StyleColor(ItemRarity.GetTooltipTitleColor(item));
        
        Color borderCol = ItemRarity.GetTooltipBorderColor(item);
        _itemTooltipBox.style.borderTopColor = borderCol; _itemTooltipBox.style.borderBottomColor = borderCol;
        _itemTooltipBox.style.borderLeftColor = borderCol; _itemTooltipBox.style.borderRightColor = borderCol;

        _statsContainer.Clear();

        if (ItemTooltipInspect.TryGetWeaponHandedness(item.Data, out bool isTwoHanded))
        {
            string handKey = ItemTooltipInspect.GetWeaponHandKey(isTwoHanded);
            string handFallback = ItemTooltipInspect.GetWeaponHandFallback(isTwoHanded);
            CreateAsyncLabel(handKey, n =>
                IsMissingTranslationResult(n) || n == handKey ? handFallback : n, _colSkillType);
        }

        if (item.Data is WeaponItemSO weapon && !weapon.IsDefensiveOffHand)
        {
            AddRow(StatType.DamagePhysical, item, weapon.MinPhysicalDamage, weapon.MaxPhysicalDamage);
            AddRow(StatType.DamageFire, item, weapon.MinFireDamage, weapon.MaxFireDamage, _colFireText);
            AddRow(StatType.DamageCold, item, weapon.MinColdDamage, weapon.MaxColdDamage, _colColdText);
            AddRow(StatType.DamageLightning, item, weapon.MinLightningDamage, weapon.MaxLightningDamage, _colLightningText);
            
            AddSimpleRow(StatType.AttackSpeed, item, weapon.AttacksPerSecond, "{0:F2}");
            AddSimpleRow(StatType.CritChance, item, weapon.BaseCritChance, "{0}%");
        }
        else if (item.Data is ArmorItemSO armor)
        {
            if (armor.BaseArmor > 0) AddSimpleRow(StatType.Armor, item, armor.BaseArmor);
            if (armor.BaseEvasion > 0) AddSimpleRow(StatType.Evasion, item, armor.BaseEvasion);
            if (armor.BaseMysticShield > 0) AddSimpleRow(StatType.MaxMysticShield, item, armor.BaseMysticShield);
        }

        if (_statsContainer.childCount > 0) AddDivToContainer();

        if (item.Data.ImplicitModifiers != null)
        {
            foreach(var mod in item.Data.ImplicitModifiers)
                AddModRow(mod.Stat, mod.Value, mod.Type, _colImplicit);
        }

        if (item.Data.ImplicitModifiers != null && item.Data.ImplicitModifiers.Count > 0 && ItemRarity.GetAffixCount(item) > 0)
            AddDivToContainer();

        if (item.Affixes != null)
        {
            foreach(var aff in item.Affixes)
            {
                if(aff.Modifiers.Count == 0) continue;
                string key = aff.Data.GetResolvedTranslationKey();
                var modifier = aff.Modifiers[0];
                modifier.GetRolledRange(out float minVal, out float maxVal);
                if (string.IsNullOrEmpty(key)) key = $"stats.{modifier.Type}";
                AddAffixRow(key, aff, minVal, maxVal, modifier.HasRange, _colAffix);
            }
        }

        if (_inspectDetailsVisible)
        {
            if (_statsContainer.childCount > 0)
            {
                VisualElement last = _statsContainer[_statsContainer.childCount - 1];
                if (last.name != "TooltipDivider")
                    AddDivToContainer();
            }
            int itemLevel = item.ResolvedItemLevel;
            CreateAsyncLabel(
                ItemTooltipInspect.ItemLevelKey,
                n => ItemTooltipInspect.FormatItemLevel(
                    itemLevel,
                    IsMissingTranslationResult(n) || n == ItemTooltipInspect.ItemLevelKey
                        ? ItemTooltipInspect.ItemLevelFallback
                        : n),
                _colInspect);
        }

        AppendPriceRow(item);
    }

    private void AppendPriceRow(InventoryItem item)
    {
        if (_currentPriceMode == ItemTooltipPriceMode.None || item == null)
            return;

        int price = _currentPriceMode == ItemTooltipPriceMode.Buy
            ? Scripts.Economy.ItemPriceCalculator.GetVendorPrice(item)
            : Scripts.Economy.ItemPriceCalculator.GetSellPrice(item);
        string key = _currentPriceMode == ItemTooltipPriceMode.Sell
            ? "market.price.sell"
            : "market.price.buy";
        string fallbackPrefix = _currentPriceMode == ItemTooltipPriceMode.Sell ? "Sell" : "Price";
        AddDivToContainer();
        CreateAsyncLabel(key, n =>
        {
            string prefix = string.IsNullOrEmpty(n) || n.IndexOf("translation found", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? fallbackPrefix
                : n;
            return $"{prefix}: {price}";
        }, _colGoldText);
    }

    // --- Helpers (ТВОЙ КОД) ---

    private void LocalizeLabel(Label label, string table, string key, string fallback)
    {
        label.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(table, key);
        op.Completed += (h) => 
        {
            if (label == null) return;
            if (h.Status == AsyncOperationStatus.Succeeded && !IsMissingTranslationResult(h.Result))
                label.text = h.Result;
        };
    }

    private static bool IsMissingTranslationResult(string result)
    {
        return string.IsNullOrEmpty(result) || (result != null && result.Contains("No translation found"));
    }

    private void AddRow(StatType type, InventoryItem item, float min, float max, Color? c = null)
    {
        float fMin = item.GetCalculatedStat(type, min);
        float fMax = item.GetCalculatedStatUpperBound(type, max);
        if (fMax <= 0) return;
        bool mod = Mathf.Abs(fMax - max) > 0.01f;
        CreateAsyncLabel(type.ToString(), (n) => $"{n}: {Mathf.Round(fMin)}-{Mathf.Round(fMax)}", c ?? (mod ? _colModifiedText : _colNormalText));
    }

    private void AddSimpleRow(StatType type, InventoryItem item, float baseVal, string fmt = "{0}")
    {
        float f = item.GetCalculatedStat(type, baseVal);
        CreateAsyncLabel(type.ToString(), (n) => $"{n}: {string.Format(fmt, f)}", Mathf.Abs(f - baseVal) > 0.01f ? _colModifiedText : _colNormalText);
    }

    private void AddModRow(StatType type, float val, StatModType mt, Color c)
    {
        CreateAsyncLabel(
            $"stats.{type}",
            n => StatPresentation.FormatModifierLine(
                _statsDb,
                type,
                n,
                val,
                mt,
                StatPresentation.ModifierLineStyle.StatThenValue),
            c);
    }

    private void AddAffixRow(string key, AffixInstance affix, float minVal, float maxVal, bool hasRange, Color c)
    {
        bool inspect = _inspectDetailsVisible;
        string tierText = inspect ? ItemTooltipInspect.FormatTierLabel(affix) : null;
        var lbl = CreateLabel("...", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        lbl.style.color = new StyleColor(c);

        if (string.IsNullOrEmpty(tierText))
        {
            _statsContainer.Add(lbl);
        }
        else
        {
            lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
            lbl.style.flexGrow = 1;
            lbl.style.flexShrink = 1;
            lbl.style.minWidth = 0;

            var tier = CreateLabel(tierText, 7, FontStyle.Normal, TextAnchor.MiddleRight);
            tier.style.color = new StyleColor(_colInspect);
            tier.style.flexShrink = 0;
            tier.style.marginLeft = 2;
            tier.style.minWidth = 14;
            tier.style.whiteSpace = WhiteSpace.NoWrap;
            tier.pickingMode = PickingMode.Ignore;

            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.NoWrap;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.FlexStart;
            row.style.width = Length.Percent(100);
            row.Add(lbl);
            row.Add(tier);
            _statsContainer.Add(row);
        }

        object[] args = hasRange ? new object[] { minVal, maxVal } : new object[] { minVal };
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_AFFIXES, key, args);
        op.Completed += (h) =>
        {
            if (lbl == null) return;
            string text = h.Result;
            if (inspect)
            {
                text = ItemTooltipInspect.ReplaceRolledValuesWithRanges(text, affix);
                ScheduleItemTooltipRelayout();
            }
            lbl.text = text;
        };
    }

    private void UpdateItemInspectOverlay()
    {
        if (_itemTooltipBox == null || _itemTooltipBox.style.display != DisplayStyle.Flex || _currentTargetItem == null)
            return;

        bool inspect = IsInspectModifierHeld();
        if (inspect == _inspectDetailsVisible)
            return;

        FillItemData(_currentTargetItem);
        ScheduleItemTooltipRelayout();
    }

    private void ScheduleItemTooltipRelayout()
    {
        if (_itemTooltipBox == null || _root == null)
            return;

        _itemTooltipBox.MarkDirtyRepaint();
        RecalculatePosition();
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(1);
        _root.schedule.Execute(RecalculatePosition).ExecuteLater(50);
    }

    private static bool IsInspectModifierHeld()
    {
        return Keyboard.current != null && Keyboard.current.altKey.isPressed;
    }

    private void CreateAsyncLabel(string key, System.Func<string, string> fmt, Color c)
    {
        var lbl = CreateLabel("...", 8, FontStyle.Normal, TextAnchor.MiddleCenter);
        lbl.style.color = new StyleColor(c);
        _statsContainer.Add(lbl);
        var k = key.Contains(".") ? key : $"stats.{key}";
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TABLE_MENU, k);
        op.Completed += (h) =>
        {
            if (lbl == null) return;
            lbl.text = fmt(h.Status == AsyncOperationStatus.Succeeded ? h.Result : key);
            if (_inspectDetailsVisible)
                ScheduleItemTooltipRelayout();
        };
    }

    private void AddDivToContainer()
    {
        var d = new VisualElement { name = "TooltipDivider" };
        d.style.height = 1;
        d.style.width = Length.Percent(100);
        d.style.marginTop = 2; d.style.marginBottom = 2;
        d.style.backgroundColor = new StyleColor(new Color(0.4f, 0.4f, 0.4f));
        _statsContainer.Add(d);
    }
}
