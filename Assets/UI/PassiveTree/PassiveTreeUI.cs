using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using Scripts.UI;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI;

public class PassiveTreeUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private PassiveTreeManager _treeManager;
    
    [Header("Configuration")]
    [Tooltip("Создай ассет PassiveTreeThemeSO и перетащи сюда")]
    [SerializeField] private PassiveTreeThemeSO _theme;

    // Sub-systems
    private PassiveTreeViewport _viewport;
    private PassiveTreeRenderer _renderer;
    private PassiveTreeTooltip _tooltip;

    // Visual Elements
    private VisualElement _windowRoot;
    private VisualElement _treeContainer;
    private VisualElement _contentViewport;
    private VisualElement _overlayHeader;
    private Label _pointsLabel;
    private Label _searchHint;
    private TextField _searchField;
    private WindowView _windowView;
    private bool _waitingForLocalizationInit;
    private bool _frameQueued;
    private Vector2 _pendingFrameSize;
    private Vector2 _appliedFrameSize;
    private PassiveSkillTreeSO _lastBuiltTree;
    private bool _searchHeldPlayerMap;

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null || _treeManager == null || _theme == null)
        {
            Debug.LogError("[PassiveTreeUI] Missing dependencies or theme!");
            return;
        }

        BuildUI();
        InitializeSubsystems();

        _treeManager.OnTreeUpdated += OnTreeUpdated;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        _windowView = GetComponent<WindowView>()
            ?? GetComponentInParent<WindowView>()
            ?? GetComponentInChildren<WindowView>(true);
        if (_windowView != null)
            _windowView.OnOpened += OnTreeWindowOpened;
        RefreshSearchHint();
        if (LocalizationSettings.SelectedLocale == null)
            RefreshSearchHintWhenReady();

        _lastBuiltTree = _treeManager.TreeData;
        _renderer.BuildGraph(_treeManager.TreeData);
        _appliedFrameSize = default;
        _contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        OnTreeUpdated();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (_windowView != null)
            _windowView.OnOpened -= OnTreeWindowOpened;
        UnsubscribeLocalizationInit();
        if (_treeManager != null)
            _treeManager.OnTreeUpdated -= OnTreeUpdated;
        if (_contentViewport != null)
            _contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        _viewport?.Cleanup();
        ReleasePlayerInputForSearch();
    }

    private void OnLocaleChanged(Locale locale)
    {
        _tooltip?.RefreshIfVisible();
        RefreshSkillPointsLabel();
        RefreshSearchHint();
        _renderer?.InvalidateSearchText();
    }

    private void RefreshSearchHint()
    {
        if (_searchHint == null)
            return;

        _searchHint.text = RuntimeLocalization.Resolve("passive.search", "Search", "Поиск");
    }

    private void RefreshSearchHintWhenReady()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            RefreshSearchHint();
            return;
        }

        AsyncOperationHandle<LocalizationSettings> init = LocalizationSettings.InitializationOperation;
        if (init.IsDone || _waitingForLocalizationInit)
            return;

        _waitingForLocalizationInit = true;
        init.Completed += OnLocalizationInitialized;
    }

    private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> handle)
    {
        handle.Completed -= OnLocalizationInitialized;
        _waitingForLocalizationInit = false;
        if (!isActiveAndEnabled)
            return;

        RefreshSearchHint();
    }

    private void UnsubscribeLocalizationInit()
    {
        if (!_waitingForLocalizationInit)
            return;

        _waitingForLocalizationInit = false;
        LocalizationSettings.InitializationOperation.Completed -= OnLocalizationInitialized;
    }

    private void OnViewportGeometryChanged(GeometryChangedEvent evt)
    {
        if (_viewport != null && _viewport.UserNavigated)
            return;

        Vector2 size = evt.newRect.size;
        if (size.x < 100f || size.y < 100f)
            return;
        if (Mathf.Abs(_appliedFrameSize.x - size.x) < 1f && Mathf.Abs(_appliedFrameSize.y - size.y) < 1f)
            return;

        _pendingFrameSize = size;
        if (_frameQueued)
            return;

        _frameQueued = true;
        _contentViewport.schedule.Execute(ApplyPendingFrame);
    }

    private void ApplyPendingFrame()
    {
        _frameQueued = false;
        if (_viewport != null && _viewport.UserNavigated)
            return;
        if (!FrameAll(_pendingFrameSize))
            return;

        _appliedFrameSize = _pendingFrameSize;
    }

    private void BuildUI()
    {
        var root = _uiDoc.rootVisualElement;
        root.Clear();
        UIFontApplier.ApplyToRoot(root);

        // 1. Window Root — на весь экран, фон в стиле Path of Exile (тёмный, непрозрачный)
        _windowRoot = new VisualElement { name = "WindowRoot" };
        _windowRoot.style.flexGrow = 1;
        _windowRoot.style.position = Position.Absolute;
        _windowRoot.style.left = 0; _windowRoot.style.right = 0;
        _windowRoot.style.top = 0; _windowRoot.style.bottom = 0;
        _windowRoot.style.backgroundColor = new StyleColor(new Color(0.09f, 0.07f, 0.06f, 1f)); // PoE-подобный тёмно-коричневый, без прозрачности
        root.Add(_windowRoot);

        // 2. Viewport на всё пространство (дерево на полный экран)
        _contentViewport = new VisualElement { name = "Viewport" };
        _contentViewport.style.position = Position.Absolute;
        _contentViewport.style.left = 0; _contentViewport.style.right = 0;
        _contentViewport.style.top = 0; _contentViewport.style.bottom = 0;
        _contentViewport.style.overflow = Overflow.Hidden;
        _windowRoot.Add(_contentViewport);

        // 3. Tree Container
        _treeContainer = new VisualElement { name = "TreeContainer" };
        _treeContainer.style.position = Position.Absolute;
        _treeContainer.transform.scale = Vector3.one;
        _contentViewport.Add(_treeContainer);

        // 4. Оверлей: только Skill Points по центру вверху, компактно
        _overlayHeader = new VisualElement { name = "OverlayHeader" };
        _overlayHeader.style.position = Position.Absolute;
        _overlayHeader.style.left = 0; _overlayHeader.style.right = 0;
        _overlayHeader.style.top = 0;
        _overlayHeader.style.height = 32;
        _overlayHeader.style.flexDirection = FlexDirection.Row;
        _overlayHeader.style.justifyContent = Justify.Center;
        _overlayHeader.style.alignItems = Align.Center;
        _overlayHeader.pickingMode = PickingMode.Ignore;
        _windowRoot.Add(_overlayHeader);

        var search = new TextField { name = "PassiveTreeSearch" };
        _searchField = search;
        search.focusable = false;
        search.style.position = Position.Absolute;
        search.style.left = 4;
        search.style.top = 8;
        search.style.width = 108;
        search.style.minWidth = 108;
        search.style.maxWidth = 108;
        search.style.height = 14;
        search.style.minHeight = 14;
        search.style.maxHeight = 14;
        search.style.flexGrow = 0;
        search.style.flexShrink = 0;
        search.style.fontSize = 8;
        search.style.marginTop = 0;
        search.style.marginBottom = 0;
        search.style.paddingTop = 0;
        search.style.paddingBottom = 0;
        search.style.paddingLeft = 3;
        search.style.paddingRight = 3;
        search.style.backgroundColor = new StyleColor(new Color(0.08f, 0.07f, 0.05f, 0.94f));
        search.style.borderTopWidth = 1;
        search.style.borderBottomWidth = 1;
        search.style.borderLeftWidth = 1;
        search.style.borderRightWidth = 1;
        search.style.borderTopColor = new Color(0.55f, 0.44f, 0.22f, 1f);
        search.style.borderBottomColor = new Color(0.55f, 0.44f, 0.22f, 1f);
        search.style.borderLeftColor = new Color(0.55f, 0.44f, 0.22f, 1f);
        search.style.borderRightColor = new Color(0.55f, 0.44f, 0.22f, 1f);
        search.style.color = new Color(0.93f, 0.86f, 0.68f, 1f);
        search.pickingMode = PickingMode.Position;
        search.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            StyleSearchField(search);
            search.schedule.Execute(() => StyleSearchField(search));
        });
        search.RegisterCallback<PointerDownEvent>(_ => ArmSearchField());
        search.RegisterCallback<FocusInEvent>(_ => HoldPlayerInputForSearch());
        search.RegisterCallback<FocusOutEvent>(_ => ReleasePlayerInputForSearch());
        search.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                search.Blur();
                evt.StopPropagation();
            }
        });
        _searchHint = new Label
        {
            name = "PassiveTreeSearchHint",
            pickingMode = PickingMode.Ignore
        };
        _searchHint.style.position = Position.Absolute;
        _searchHint.style.left = 3;
        _searchHint.style.right = 2;
        _searchHint.style.top = 0;
        _searchHint.style.bottom = 0;
        _searchHint.style.minHeight = 0;
        _searchHint.style.marginTop = 0;
        _searchHint.style.marginRight = 0;
        _searchHint.style.marginBottom = 0;
        _searchHint.style.marginLeft = 0;
        _searchHint.style.paddingTop = 0;
        _searchHint.style.paddingRight = 0;
        _searchHint.style.paddingBottom = 0;
        _searchHint.style.paddingLeft = 0;
        _searchHint.style.fontSize = 8;
        _searchHint.style.color = new Color(0.7f, 0.62f, 0.45f, 0.75f);
        _searchHint.style.unityTextAlign = TextAnchor.MiddleLeft;
        RefreshSearchHint();
        search.RegisterValueChangedCallback(evt =>
        {
            _renderer?.SetSearch(evt.newValue);
            _searchHint.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
        });
        search.Add(_searchHint);
        _overlayHeader.Add(search);

        _pointsLabel = new Label();
        _pointsLabel.style.fontSize = 14;
        _pointsLabel.style.color = new Color(0.75f, 0.72f, 0.68f);
        _pointsLabel.pickingMode = PickingMode.Ignore;
        _overlayHeader.Add(_pointsLabel);
        RefreshSkillPointsLabel();
    }

    private static void StyleSearchField(TextField field)
    {
        var ink = new Color(0.93f, 0.86f, 0.68f, 1f);
        var paper = new Color(0.08f, 0.07f, 0.05f, 1f);
        var gold = new Color(0.55f, 0.44f, 0.22f, 1f);
        PaintSearchSurface(field, paper, ink, gold, 1);
        field.Query().ForEach(element =>
        {
            if (element == field
                || element.name == "PassiveTreeSearchHint"
                || element.ClassListContains("unity-base-field__label"))
                return;
            PaintSearchSurface(element, paper, ink, gold, 0);
            element.style.paddingTop = 0;
            element.style.paddingRight = 0;
            element.style.paddingBottom = 0;
            element.style.paddingLeft = 0;
            element.focusable = false;
        });
        field.focusable = false;
    }

    private static void PaintSearchSurface(VisualElement element, Color paper, Color ink, Color gold, int border)
    {
        element.style.backgroundImage = new StyleBackground(StyleKeyword.None);
        element.style.backgroundColor = paper;
        element.style.unityBackgroundImageTintColor = paper;
        element.style.color = ink;
        element.style.fontSize = 8;
        element.style.marginTop = 0;
        element.style.marginRight = 0;
        element.style.marginBottom = 0;
        element.style.marginLeft = 0;
        element.style.paddingTop = 1;
        element.style.paddingRight = 2;
        element.style.paddingBottom = 0;
        element.style.paddingLeft = 2;
        element.style.borderTopWidth = border;
        element.style.borderRightWidth = border;
        element.style.borderBottomWidth = border;
        element.style.borderLeftWidth = border;
        element.style.borderTopColor = gold;
        element.style.borderRightColor = gold;
        element.style.borderBottomColor = gold;
        element.style.borderLeftColor = gold;
        element.style.unityTextAlign = TextAnchor.MiddleLeft;
    }

    private void OnTreeWindowOpened()
    {
        DeactivateSearch();
    }

    private void ArmSearchField()
    {
        SetSearchFocusable(true);
        _searchField?.Focus();
    }

    private void DeactivateSearch()
    {
        if (_searchField == null)
            return;

        SetSearchFocusable(false);
        _searchField.SetValueWithoutNotify(string.Empty);
        if (_searchHint != null)
            _searchHint.style.display = DisplayStyle.Flex;
        _renderer?.SetSearch(string.Empty);
        _searchField.Blur();
    }

    private void SetSearchFocusable(bool focusable)
    {
        if (_searchField == null)
            return;

        _searchField.focusable = focusable;
        _searchField.Query().ForEach(element => element.focusable = focusable);
    }

    private void HoldPlayerInputForSearch()
    {
        UiTypingGate.IsTyping = true;
        var map = InputManager.InputActions?.Player.Get();
        if (map == null || !map.enabled || _searchHeldPlayerMap)
            return;

        _searchHeldPlayerMap = true;
        map.Disable();
    }

    private void ReleasePlayerInputForSearch()
    {
        UiTypingGate.IsTyping = false;
        if (!_searchHeldPlayerMap)
            return;

        _searchHeldPlayerMap = false;
        InputManager.InputActions?.Player.Get()?.Enable();
    }

    private void InitializeSubsystems()
{
    _tooltip = new PassiveTreeTooltip(_windowRoot);
    
    // Передаем новый колбэк OnNodeRightClick (последний аргумент)
    _renderer = new PassiveTreeRenderer(_treeContainer, _theme, _tooltip, OnNodeClick, OnNodeRightClick);
    
    _viewport = new PassiveTreeViewport(_contentViewport, _treeContainer);
}

    private void OnTreeUpdated()
    {
        if (_overlayHeader != null)
            _overlayHeader.style.display = _treeManager.IsPreviewMode ? DisplayStyle.None : DisplayStyle.Flex;
        if (_pointsLabel != null && !_treeManager.IsPreviewMode)
            RefreshSkillPointsLabel();
        if (_treeManager.TreeData != _lastBuiltTree)
        {
            _lastBuiltTree = _treeManager.TreeData;
            _renderer.BuildGraph(_treeManager.TreeData);
            _appliedFrameSize = default;
            _viewport?.ResetNavigation();
        }
        _renderer.UpdateVisuals(_treeManager);
    }

    private void RefreshSkillPointsLabel()
    {
        if (_pointsLabel == null)
            return;

        string label = RuntimeLocalization.Resolve("passive.skillPoints", "Skill Points", "Очки навыков");
        int points = _treeManager != null ? _treeManager.SkillPoints : 0;
        _pointsLabel.text = $"{label}: {points}";
    }

    // Существующий метод (ЛКМ)
private void OnNodeClick(string id)
{
    _treeManager.AllocateNode(id);
}

private void OnNodeRightClick(string id)
{
    _treeManager.RefundNode(id);
}

    /// <summary>
    /// Подогнать вид так, чтобы всё дерево было в кадре (как Frame All в редакторе).
    /// </summary>
    private bool FrameAll(Vector2 viewportSize)
    {
        if (_treeManager?.TreeData == null || _viewport == null)
            return false;
        var bounds = _treeManager.TreeData.GetTreeContentBounds(80f);
        if (bounds.width <= 0f || bounds.height <= 0f)
            return false;
        return _viewport.FrameContentRect(bounds, viewportSize, 40f);
    }
}
