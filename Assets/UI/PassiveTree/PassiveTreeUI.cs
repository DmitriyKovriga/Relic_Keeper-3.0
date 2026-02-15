using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI; // Наш новый namespace

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
    private bool _frameAllScheduled;
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

        _lastBuiltTree = _treeManager.TreeData;
        _renderer.BuildGraph(_treeManager.TreeData);
        _frameAllScheduled = false;
        _contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        OnTreeUpdated();
    }

    private void OnDisable()
    {
        if (_treeManager != null)
            _treeManager.OnTreeUpdated -= OnTreeUpdated;
        if (_contentViewport != null)
            _contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
        _viewport?.Cleanup();
    }

    private void OnViewportGeometryChanged(GeometryChangedEvent evt)
    {
        if (_frameAllScheduled) return;
        float w = _contentViewport.resolvedStyle.width;
        float h = _contentViewport.resolvedStyle.height;
        if (float.IsNaN(w) || float.IsNaN(h) || w < 100f || h < 100f) return;
        _frameAllScheduled = true;
        FrameAll();
    }

    private void BuildUI()
    {
        var root = _uiDoc.rootVisualElement;
        root.Clear();

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

        _pointsLabel = new Label("Skill Points: 0");
        _pointsLabel.style.fontSize = 14;
        _pointsLabel.style.color = new Color(0.75f, 0.72f, 0.68f);
        _pointsLabel.pickingMode = PickingMode.Ignore;
        _overlayHeader.Add(_pointsLabel);
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
            _pointsLabel.text = $"Skill Points: {_treeManager.SkillPoints}";
        if (_treeManager.TreeData != _lastBuiltTree)
        {
            _lastBuiltTree = _treeManager.TreeData;
            _renderer.BuildGraph(_treeManager.TreeData);
            _frameAllScheduled = false;
        }
        _renderer.UpdateVisuals(_treeManager);
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
    private void FrameAll()
    {
        if (_treeManager?.TreeData == null) return;
        var bounds = _treeManager.TreeData.GetTreeContentBounds(80f);
        if (bounds.width > 0 && bounds.height > 0)
            _viewport.FrameContentRect(bounds, 40f);
    }
}