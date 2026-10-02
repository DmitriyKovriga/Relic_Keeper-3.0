using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
    private bool _frameQueued;
    private Vector2 _pendingFrameSize;
    private Vector2 _appliedFrameSize;
    private PassiveSkillTreeSO _lastBuiltTree;

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

        _lastBuiltTree = _treeManager.TreeData;
        _renderer.BuildGraph(_treeManager.TreeData);
        _appliedFrameSize = default;
        _contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        OnTreeUpdated();
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (_treeManager != null)
            _treeManager.OnTreeUpdated -= OnTreeUpdated;
        if (_contentViewport != null)
            _contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        _viewport?.Cleanup();
    }

    private void OnLocaleChanged(Locale locale)
    {
        _tooltip?.RefreshIfVisible();
        RefreshSkillPointsLabel();
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

        _pointsLabel = new Label();
        _pointsLabel.style.fontSize = 14;
        _pointsLabel.style.color = new Color(0.75f, 0.72f, 0.68f);
        _pointsLabel.pickingMode = PickingMode.Ignore;
        _overlayHeader.Add(_pointsLabel);
        RefreshSkillPointsLabel();
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
