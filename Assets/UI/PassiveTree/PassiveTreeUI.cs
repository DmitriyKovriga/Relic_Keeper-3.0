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
    private Label _pointsLabel;

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

        // Построение и центровка
        _renderer.BuildGraph(_treeManager.TreeData);
        
        _uiDoc.rootVisualElement.schedule.Execute(() =>
        {
            CenterOnStart();
            OnTreeUpdated(); // Первый рефреш цветов
        }).ExecuteLater(50);
    }

    private void OnDisable()
    {
        if (_treeManager != null)
            _treeManager.OnTreeUpdated -= OnTreeUpdated;
        
        _viewport?.Cleanup();
    }

    private void BuildUI()
    {
        var root = _uiDoc.rootVisualElement;
        root.Clear();

        // 1. Window Root
        _windowRoot = new VisualElement { name = "WindowRoot" };
        _windowRoot.style.flexGrow = 1;
        _windowRoot.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
        root.Add(_windowRoot);

        // 2. Points Label
        _pointsLabel = new Label("Points: 0");
        _pointsLabel.style.fontSize = 20;
        _pointsLabel.style.color = Color.white;
        _pointsLabel.style.alignSelf = Align.Center;
        _pointsLabel.style.marginTop = 10;
        _windowRoot.Add(_pointsLabel);

        // 3. Viewport (Mask)
        _contentViewport = new VisualElement { name = "Viewport" };
        _contentViewport.style.flexGrow = 1;
        _contentViewport.style.overflow = Overflow.Hidden;
        _windowRoot.Add(_contentViewport);

        // 4. Tree Container (Content)
        _treeContainer = new VisualElement { name = "TreeContainer" };
        _treeContainer.style.position = Position.Absolute;
        _treeContainer.transform.scale = Vector3.one; // Initial scale
        _contentViewport.Add(_treeContainer);
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
        _pointsLabel.text = $"Skill Points: {_treeManager.SkillPoints}";
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

    private void CenterOnStart()
    {
        if (_treeManager.TreeData == null) return;
        var startNode = _treeManager.TreeData.Nodes.Find(n => n.NodeType == PassiveNodeType.Start);
        
        if (startNode != null)
        {
            _viewport.CenterOnPosition(startNode.Position);
        }
    }
}