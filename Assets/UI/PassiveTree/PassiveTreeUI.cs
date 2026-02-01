// ==========================================
// FILENAME: Assets/UI/PassiveTree/PassiveTreeUI.cs
// ==========================================
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;
using System.Collections.Generic;
using System.Text;
using Scripts.Stats;

public class PassiveTreeUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UIDocument _uiDoc;
    [SerializeField] private PassiveTreeManager _treeManager;

    [Header("Visual Settings")]
    [SerializeField] private float _nodeSizeSmall = 40f;
    [SerializeField] private float _nodeSizeNotable = 60f;
    [SerializeField] private float _nodeSizeKeystone = 80f;
    [SerializeField] private float _lineThickness = 4f;

    [Header("Interaction Settings")]
    [SerializeField] private float _zoomSpeed = 0.1f;
    [SerializeField] private float _minZoom = 0.3f;
    [SerializeField] private float _maxZoom = 2.0f;

    // --- ЦВЕТА (НАСТРОЙКА ПОД POE СТИЛЬ) ---
    // Купленный (Allocated)
    private readonly Color _colAllocatedFill = new Color(0.8f, 0.6f, 0.1f); // Золотой фон
    private readonly Color _colAllocatedBorder = new Color(1f, 0.8f, 0.2f); // Ярко-золотая рамка
    
    // Доступный (Available)
    private readonly Color _colAvailableFill = new Color(0.15f, 0.15f, 0.15f); // Темный фон (как у закрытого)
    private readonly Color _colAvailableBorder = new Color(0.5f, 0.5f, 0.5f);  // Серая рамка
    private readonly Color _colAvailableHighlight = new Color(1f, 1f, 1f, 0.3f); // БЕЛОЕ СВЕЧЕНИЕ (полупрозрачное)

    // Закрытый (Locked)
    private readonly Color _colLockedFill = new Color(0.1f, 0.1f, 0.1f);   // Черный фон
    private readonly Color _colLockedBorder = new Color(0.2f, 0.2f, 0.2f); // Темно-серая рамка

    // Линии
    private readonly Color _colLineAllocated = new Color(1f, 0.8f, 0.2f, 0.8f); // Золотая (прокачано)
    private readonly Color _colLinePath = new Color(0.7f, 0.7f, 0.7f, 0.5f);    // Тускло-белая (путь доступен)
    private readonly Color _colLineLocked = new Color(0.15f, 0.15f, 0.15f, 0.5f); // Темная (недоступно)

    // UI Elements
    private VisualElement _root;
    private VisualElement _treeContainer;
    private VisualElement _contentViewport;
    private VisualElement _windowRoot;
    private Label _pointsLabel;

    // Tooltip
    private VisualElement _tooltipBox;
    private Label _tooltipTitle;
    private Label _tooltipDesc;
    private Label _tooltipStats;

    // State
    private bool _isDragging;
    private Vector2 _dragStartPos;
    private Vector2 _containerStartPos;
    private float _currentZoom = 1.0f;

    private Dictionary<string, VisualElement> _nodeVisuals = new Dictionary<string, VisualElement>();
    private List<(string id1, string id2, VisualElement line)> _connections = new List<(string, string, VisualElement)>();

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null) return;

        _root = _uiDoc.rootVisualElement;

        BuildBaseUIStructure();
        CreateTooltipElement();

        if (_treeManager != null)
        {
            _treeManager.OnTreeUpdated += RefreshVisuals;
            GenerateTree();
            
            _root.schedule.Execute(() => 
            {
                CenterOnStartNode();
                RefreshVisuals();
            }).ExecuteLater(50);
        }

        _contentViewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
        _contentViewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        _contentViewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
        _contentViewport.RegisterCallback<PointerLeaveEvent>(OnPointerUp);
        _contentViewport.RegisterCallback<WheelEvent>(OnWheel);
    }

    private void OnDisable()
    {
        if (_treeManager != null)
            _treeManager.OnTreeUpdated -= RefreshVisuals;
            
        if (_contentViewport != null)
        {
            _contentViewport.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            _contentViewport.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _contentViewport.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            _contentViewport.UnregisterCallback<PointerLeaveEvent>(OnPointerUp);
            _contentViewport.UnregisterCallback<WheelEvent>(OnWheel);
        }
    }

    private void BuildBaseUIStructure()
    {
        var documentRoot = _root;
        documentRoot.Clear();

        _windowRoot = new VisualElement { name = "WindowRoot" };
        _windowRoot.style.flexGrow = 1;
        _windowRoot.style.alignItems = Align.Stretch;
        _windowRoot.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
        documentRoot.Add(_windowRoot);

        _pointsLabel = new Label("Points: 0");
        _pointsLabel.style.fontSize = 20;
        _pointsLabel.style.color = Color.white;
        _pointsLabel.style.alignSelf = Align.Center;
        _pointsLabel.style.marginTop = 10;
        _pointsLabel.style.flexShrink = 0;
        _windowRoot.Add(_pointsLabel);

        _contentViewport = new VisualElement { name = "Viewport" };
        _contentViewport.style.flexGrow = 1;
        _contentViewport.style.overflow = Overflow.Hidden;
        _windowRoot.Add(_contentViewport);

        _treeContainer = new VisualElement { name = "TreeContainer" };
        _treeContainer.style.position = Position.Absolute;
        _treeContainer.style.width = 0;
        _treeContainer.style.height = 0;
        _treeContainer.transform.scale = Vector3.one * _currentZoom;
        _contentViewport.Add(_treeContainer);
    }

    private void CreateTooltipElement()
    {
        _tooltipBox = new VisualElement();
        _tooltipBox.style.position = Position.Absolute;
        _tooltipBox.style.backgroundColor = new StyleColor(new Color(0.05f, 0.05f, 0.05f, 0.95f));
        _tooltipBox.style.borderTopWidth = 1; _tooltipBox.style.borderBottomWidth = 1;
        _tooltipBox.style.borderLeftWidth = 1; _tooltipBox.style.borderRightWidth = 1;
        _tooltipBox.style.borderTopColor = Color.gray; _tooltipBox.style.borderBottomColor = Color.gray;
        _tooltipBox.style.borderLeftColor = Color.gray; _tooltipBox.style.borderRightColor = Color.gray;
        _tooltipBox.style.paddingTop = 5; _tooltipBox.style.paddingBottom = 5;
        _tooltipBox.style.paddingLeft = 5; _tooltipBox.style.paddingRight = 5;
        _tooltipBox.style.display = DisplayStyle.None; 
        _tooltipBox.pickingMode = PickingMode.Ignore; 
        _tooltipBox.style.maxWidth = 250; 

        _tooltipTitle = new Label("Title");
        _tooltipTitle.style.fontSize = 14;
        _tooltipTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        _tooltipTitle.style.color = new StyleColor(new Color(1f, 0.8f, 0.4f)); 
        _tooltipTitle.style.marginBottom = 5;
        _tooltipTitle.style.whiteSpace = WhiteSpace.Normal;
        _tooltipBox.Add(_tooltipTitle);

        _tooltipDesc = new Label("Description");
        _tooltipDesc.style.fontSize = 12;
        _tooltipDesc.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        _tooltipDesc.style.marginBottom = 5;
        _tooltipDesc.style.whiteSpace = WhiteSpace.Normal;
        _tooltipBox.Add(_tooltipDesc);

        _tooltipStats = new Label("");
        _tooltipStats.style.fontSize = 12;
        _tooltipStats.style.color = new StyleColor(new Color(0.5f, 0.7f, 1f));
        _tooltipStats.style.whiteSpace = WhiteSpace.Normal;
        _tooltipBox.Add(_tooltipStats);

        _windowRoot.Add(_tooltipBox);
    }

    private void GenerateTree()
    {
        _treeContainer.Clear();
        _nodeVisuals.Clear();
        _connections.Clear();

        var treeData = _treeManager.TreeData; 
        if (treeData == null) return;

        HashSet<string> processedConnections = new HashSet<string>();

        foreach (var node in treeData.Nodes)
        {
            foreach (var neighborID in node.ConnectionIDs)
            {
                var neighbor = treeData.GetNode(neighborID);
                if (neighbor == null) continue;

                string key = string.Compare(node.ID, neighborID) < 0 
                    ? $"{node.ID}-{neighborID}" : $"{neighborID}-{node.ID}";

                if (!processedConnections.Contains(key))
                {
                    CreateConnectionVisual(node.Position, neighbor.Position, node.ID, neighborID);
                    processedConnections.Add(key);
                }
            }
        }

        foreach (var node in treeData.Nodes)
        {
            CreateNodeVisual(node);
        }
    }

    private void CreateNodeVisual(PassiveNodeDefinition nodeDef)
    {
        float size = _nodeSizeSmall;
        if (nodeDef.NodeType == PassiveNodeType.Notable) size = _nodeSizeNotable;
        if (nodeDef.NodeType == PassiveNodeType.Keystone) size = _nodeSizeKeystone;
        if (nodeDef.NodeType == PassiveNodeType.Start) size = _nodeSizeNotable; // Старт большой

        // 1. Контейнер нода (прозрачный, держит позицию)
        var nodeRoot = new VisualElement();
        nodeRoot.style.position = Position.Absolute;
        nodeRoot.style.width = size;
        nodeRoot.style.height = size;
        nodeRoot.style.left = nodeDef.Position.x - (size / 2f);
        nodeRoot.style.top = nodeDef.Position.y - (size / 2f);

        // 2. Подсветка (Highlight) - лежит "под" кружком
        var highlight = new VisualElement { name = "Highlight" };
        highlight.style.position = Position.Absolute;
        // Делаем подсветку чуть больше самого нода
        float glowSize = size * 1.4f; 
        highlight.style.width = glowSize;
        highlight.style.height = glowSize;
        highlight.style.left = (size - glowSize) / 2f;
        highlight.style.top = (size - glowSize) / 2f;
        highlight.style.backgroundColor = new StyleColor(_colAvailableHighlight);
        highlight.style.borderTopLeftRadius = glowSize / 2f;
        highlight.style.borderTopRightRadius = glowSize / 2f;
        highlight.style.borderBottomLeftRadius = glowSize / 2f;
        highlight.style.borderBottomRightRadius = glowSize / 2f;
        // Размытие краев (fake glow через радиус, в UI Toolkit сложнее, пока просто круг)
        highlight.style.display = DisplayStyle.None; // Скрыт по умолчанию
        nodeRoot.Add(highlight);

        // 3. Сам круг нода (Visual)
        var circle = new VisualElement { name = "Circle" };
        circle.style.flexGrow = 1;
        circle.style.borderTopLeftRadius = size / 2f;
        circle.style.borderTopRightRadius = size / 2f;
        circle.style.borderBottomLeftRadius = size / 2f;
        circle.style.borderBottomRightRadius = size / 2f;
        
        circle.style.borderTopWidth = 2; circle.style.borderBottomWidth = 2;
        circle.style.borderLeftWidth = 2; circle.style.borderRightWidth = 2;
        
        // Иконка
        var icon = nodeDef.GetIcon();
        if (icon != null)
        {
            circle.style.backgroundImage = new StyleBackground(icon);
        }
        
        nodeRoot.Add(circle);
        
        // События
        nodeRoot.RegisterCallback<ClickEvent>(evt => OnNodeClicked(nodeDef.ID));
        nodeRoot.RegisterCallback<MouseEnterEvent>(evt => OnNodeMouseEnter(nodeDef, nodeRoot));
        nodeRoot.RegisterCallback<MouseLeaveEvent>(evt => OnNodeMouseLeave());

        _treeContainer.Add(nodeRoot);
        _nodeVisuals.Add(nodeDef.ID, nodeRoot);
    }
    
    private void CreateConnectionVisual(Vector2 posA, Vector2 posB, string id1, string id2)
    {
        var line = new VisualElement();
        Vector2 diff = posB - posA;
        float distance = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        line.style.position = Position.Absolute;
        line.style.width = distance;
        line.style.height = _lineThickness;
        line.style.backgroundColor = _colLineLocked;
        
        line.style.left = posA.x;
        line.style.top = posA.y - (_lineThickness / 2f);
        line.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(50));
        line.style.rotate = new Rotate(angle);
        line.pickingMode = PickingMode.Ignore;

        _treeContainer.Add(line);
        _connections.Add((id1, id2, line));
    }
    
    private void RefreshVisuals()
    {
        if (_treeManager == null || _treeManager.PlayerStats == null) return;
        
        _pointsLabel.text = $"Skill Points: {_treeManager.SkillPoints}";

        // --- 1. ОБНОВЛЯЕМ НОДЫ ---
        foreach (var kvp in _nodeVisuals)
        {
            string id = kvp.Key;
            VisualElement nodeRoot = kvp.Value;
            VisualElement circle = nodeRoot.Q("Circle");
            VisualElement highlight = nodeRoot.Q("Highlight");
            
            bool allocated = _treeManager.IsAllocated(id);
            bool canAllocate = !allocated && _treeManager.CanAllocate(id);
            
            // Проверка на Start Node: он всегда выглядит как купленный
            // Мы не можем легко проверить тип нода здесь без поиска в TreeData,
            // но _treeManager.IsAllocated(id) уже должен возвращать true для старта благодаря правке в Manager.

            if (allocated)
            {
                // КУПЛЕН: Золотой, без подсветки
                circle.style.borderTopColor = _colAllocatedBorder; circle.style.borderBottomColor = _colAllocatedBorder;
                circle.style.borderLeftColor = _colAllocatedBorder; circle.style.borderRightColor = _colAllocatedBorder;
                circle.style.backgroundColor = new StyleColor(_colAllocatedFill);
                highlight.style.display = DisplayStyle.None;
            }
            else if (canAllocate)
            {
                // ДОСТУПЕН: Темный, серая рамка, БЕЛАЯ ПОДСВЕТКА СНИЗУ
                circle.style.borderTopColor = _colAvailableBorder; circle.style.borderBottomColor = _colAvailableBorder;
                circle.style.borderLeftColor = _colAvailableBorder; circle.style.borderRightColor = _colAvailableBorder;
                circle.style.backgroundColor = new StyleColor(_colAvailableFill);
                
                highlight.style.display = DisplayStyle.Flex; // <-- ВКЛЮЧАЕМ ПОДСВЕТКУ
            }
            else
            {
                // ЗАКРЫТ: Темный, темная рамка, без подсветки
                circle.style.borderTopColor = _colLockedBorder; circle.style.borderBottomColor = _colLockedBorder;
                circle.style.borderLeftColor = _colLockedBorder; circle.style.borderRightColor = _colLockedBorder;
                circle.style.backgroundColor = new StyleColor(_colLockedFill);
                highlight.style.display = DisplayStyle.None;
            }
        }

        // --- 2. ОБНОВЛЯЕМ ЛИНИИ ---
        foreach (var conn in _connections)
        {
            bool isNode1Allocated = _treeManager.IsAllocated(conn.id1);
            bool isNode2Allocated = _treeManager.IsAllocated(conn.id2);

            bool isNode1Available = !isNode1Allocated && _treeManager.CanAllocate(conn.id1);
            bool isNode2Available = !isNode2Allocated && _treeManager.CanAllocate(conn.id2);

            if (isNode1Allocated && isNode2Allocated)
            {
                // Оба куплены -> Золотая линия
                conn.line.style.backgroundColor = _colLineAllocated;
            }
            else if ((isNode1Allocated && isNode2Available) || (isNode2Allocated && isNode1Available))
            {
                // Путь от купленного к доступному -> Тускло-белая линия
                conn.line.style.backgroundColor = _colLinePath;
            }
            else
            {
                // Недоступно -> Темная линия
                conn.line.style.backgroundColor = _colLineLocked;
            }
        }
    }
    
    // ... (Методы OnNodeClicked, OnNodeMouseEnter, OnNodeMouseLeave, CenterOnStartNode, OnWheel, OnPointerDown/Move/Up без изменений) ...
    // Скопируй их из предыдущего ответа, они там правильные.
    
    // --- ПОВТОРЯЮ МЕТОДЫ ВВОДА ДЛЯ УДОБСТВА КОПИРОВАНИЯ ---
    private void OnNodeClicked(string id)
    {
        _treeManager.AllocateNode(id);
    }

    private void OnNodeMouseEnter(PassiveNodeDefinition node, VisualElement visual)
    {
        if (_tooltipBox == null) return;
        _tooltipTitle.text = node.GetDisplayName();
        
        string desc = node.Template != null ? node.Template.Description : "";
        _tooltipDesc.text = desc;
        _tooltipDesc.style.display = string.IsNullOrEmpty(desc) ? DisplayStyle.None : DisplayStyle.Flex;

        StringBuilder sb = new StringBuilder();
        var mods = node.GetFinalModifiers();
        foreach (var mod in mods)
        {
            string sign = mod.Type == StatModType.Flat ? "+" : "";
            string end = mod.Type != StatModType.Flat ? "%" : "";
            sb.AppendLine($"{mod.Stat}: {sign}{mod.Value}{end}");
        }
        _tooltipStats.text = sb.ToString();

        Vector2 nodeWorldPos = visual.worldBound.center;
        Vector2 localPos = _windowRoot.WorldToLocal(nodeWorldPos);
        _tooltipBox.style.left = localPos.x + 20; 
        _tooltipBox.style.top = localPos.y - 20;
        _tooltipBox.style.display = DisplayStyle.Flex;
    }

    private void OnNodeMouseLeave()
    {
        if (_tooltipBox != null) _tooltipBox.style.display = DisplayStyle.None;
    }

    private void CenterOnStartNode()
    {
        if (_treeManager?.TreeData?.Nodes == null) return;
        var startNode = _treeManager.TreeData.Nodes.Find(n => n.NodeType == PassiveNodeType.Start);
        if (startNode != null)
        {
            float viewWidth = _contentViewport.resolvedStyle.width;
            float viewHeight = _contentViewport.resolvedStyle.height;
            _treeContainer.style.left = -startNode.Position.x * _currentZoom + viewWidth / 2f;
            _treeContainer.style.top = -startNode.Position.y * _currentZoom + viewHeight / 2f;
        }
    }

    private void OnWheel(WheelEvent evt)
    {
        if (_treeContainer == null) return;
        float zoomDelta = -evt.delta.y > 0 ? 1 + _zoomSpeed : 1 - _zoomSpeed;
        float oldZoom = _currentZoom;
        _currentZoom = Mathf.Clamp(_currentZoom * zoomDelta, _minZoom, _maxZoom);
        if (Mathf.Approximately(oldZoom, _currentZoom)) return;
        Vector2 mousePosInViewport = evt.localMousePosition;
        Vector2 oldContainerPos = new Vector2(_treeContainer.resolvedStyle.left, _treeContainer.resolvedStyle.top);
        Vector2 mousePosInContainer = (mousePosInViewport - oldContainerPos) / oldZoom;
        _treeContainer.transform.scale = Vector3.one * _currentZoom;
        Vector2 newContainerPos = mousePosInViewport - (mousePosInContainer * _currentZoom);
        _treeContainer.style.left = newContainerPos.x;
        _treeContainer.style.top = newContainerPos.y;
        evt.StopPropagation();
    }
    
    private void OnPointerDown(PointerDownEvent evt)
    {
        if (evt.button == 0 || evt.button == 2)
        {
            _isDragging = true;
            _dragStartPos = evt.position;
            _containerStartPos = new Vector2(_treeContainer.resolvedStyle.left, _treeContainer.resolvedStyle.top);
            _contentViewport.CapturePointer(evt.pointerId);
        }
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (_isDragging)
        {
            Vector2 delta = (Vector2)evt.position - _dragStartPos;
            _treeContainer.style.left = _containerStartPos.x + delta.x;
            _treeContainer.style.top = _containerStartPos.y + delta.y;
        }
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (_isDragging) { _isDragging = false; _contentViewport.ReleasePointer(evt.pointerId); }
    }
    
    private void OnPointerUp(PointerLeaveEvent evt)
    {
        if (_isDragging) { _isDragging = false; _contentViewport.ReleasePointer(evt.pointerId); }
    }
}