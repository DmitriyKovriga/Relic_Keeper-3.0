// ==========================================
// FILENAME: Assets/UI/PassiveTree/PassiveTreeUI.cs
// ==========================================
using UnityEngine;
using UnityEngine.UIElements;
using Scripts.Skills.PassiveTree;
using System.Collections.Generic;
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

    // --- NEW: Zoom Settings ---
    [Header("Interaction Settings")]
    [SerializeField] private float _zoomSpeed = 0.1f;
    [SerializeField] private float _minZoom = 0.3f;
    [SerializeField] private float _maxZoom = 2.0f;

    private readonly Color _colAllocated = new Color(1f, 0.8f, 0.2f);
    private readonly Color _colAvailable = new Color(0.3f, 0.8f, 0.3f);
    private readonly Color _colLocked = new Color(0.3f, 0.3f, 0.3f);
    private readonly Color _colLineActive = new Color(1f, 0.8f, 0.2f, 0.8f);
    private readonly Color _colLineInactive = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    private VisualElement _root;
    private VisualElement _treeContainer;
    private VisualElement _contentViewport;
    private Label _pointsLabel;

    private bool _isDragging;
    private Vector2 _dragStartPos;
    private Vector2 _containerStartPos;
    
    // --- NEW: Zoom State ---
    private float _currentZoom = 1.0f;

    private Dictionary<string, VisualElement> _nodeVisuals = new Dictionary<string, VisualElement>();
    private List<(string id1, string id2, VisualElement line)> _connections = new List<(string, string, VisualElement)>();

    private void OnEnable()
    {
        if (_uiDoc == null) _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null) return;

        _root = _uiDoc.rootVisualElement;

        BuildBaseUIStructure();

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
        
        // --- NEW: Register Wheel Event for Zoom ---
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
            // --- NEW: Unregister Wheel Event ---
            _contentViewport.UnregisterCallback<WheelEvent>(OnWheel);
        }
    }
    
    private void BuildBaseUIStructure()
{
    // Получаем корневой элемент самого UIDocument
    var documentRoot = _root;
    documentRoot.Clear(); // Очищаем на случай повторной генерации

    // --- ГЛАВНОЕ ИЗМЕНЕНИЕ: Создаем контейнер-обертку ---
    // Именно этот элемент будет искать и контролировать WindowView.cs
    var windowRoot = new VisualElement { name = "WindowRoot" };
    
    // Стилизуем его, чтобы он занимал все доступное пространство и был контейнером
    windowRoot.style.flexGrow = 1;
    windowRoot.style.alignItems = Align.Stretch; // Растягиваем дочерние элементы
    windowRoot.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f, 0.95f));
    
    // Добавляем нашу обертку в корень документа
    documentRoot.Add(windowRoot);
    
    // --- Теперь все остальные элементы добавляем ВНУТРЬ windowRoot ---

    // Лейбл очков
    _pointsLabel = new Label("Points: 0");
    _pointsLabel.style.fontSize = 20;
    _pointsLabel.style.color = Color.white;
    _pointsLabel.style.alignSelf = Align.Center;
    _pointsLabel.style.marginTop = 10;
    _pointsLabel.style.flexShrink = 0; // Запрещаем сжиматься
    windowRoot.Add(_pointsLabel);

    // Viewport (маска)
    _contentViewport = new VisualElement { name = "Viewport" };
    _contentViewport.style.flexGrow = 1; // Занимает все оставшееся место
    _contentViewport.style.overflow = Overflow.Hidden; // Обрезаем все что вылезает
    windowRoot.Add(_contentViewport);

    // Container (то, что двигается)
    _treeContainer = new VisualElement { name = "TreeContainer" };
    _treeContainer.style.position = Position.Absolute;
    _treeContainer.style.width = 0; // Размер не важен, элементы абсолютные
    _treeContainer.style.height = 0;
    _treeContainer.transform.scale = Vector3.one * _currentZoom;
    _contentViewport.Add(_treeContainer);
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
        if (nodeDef.NodeType == PassiveNodeType.Start) size = _nodeSizeNotable;

        var el = new VisualElement();
        el.style.position = Position.Absolute;
        el.style.width = size;
        el.style.height = size;
        el.style.left = nodeDef.Position.x - (size / 2f);
        el.style.top = nodeDef.Position.y - (size / 2f);
        
        el.style.borderTopLeftRadius = size / 2f;
        el.style.borderTopRightRadius = size / 2f;
        el.style.borderBottomLeftRadius = size / 2f;
        el.style.borderBottomRightRadius = size / 2f;

        el.style.borderTopWidth = 2; el.style.borderBottomWidth = 2;
        el.style.borderLeftWidth = 2; el.style.borderRightWidth = 2;
        el.style.borderTopColor = Color.black; el.style.borderBottomColor = Color.black;
        el.style.borderLeftColor = Color.black; el.style.borderRightColor = Color.black;

        var icon = nodeDef.GetIcon();
        if (icon != null)
        {
            el.style.backgroundImage = new StyleBackground(icon);
            el.style.backgroundColor = new StyleColor(Color.white); 
        }
        else
        {
            el.style.backgroundColor = new StyleColor(new Color(0.7f, 0.7f, 0.7f)); 
        }
        
        el.RegisterCallback<ClickEvent>(evt => OnNodeClicked(nodeDef.ID));

        _treeContainer.Add(el);
        _nodeVisuals.Add(nodeDef.ID, el);
    }
    
    // --- FIX №3: Reworked Line Math ---
    private void CreateConnectionVisual(Vector2 posA, Vector2 posB, string id1, string id2)
    {
        var line = new VisualElement();
        
        Vector2 diff = posB - posA;
        float distance = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        line.style.position = Position.Absolute;
        line.style.width = distance;
        line.style.height = _lineThickness;
        line.style.backgroundColor = _colLineInactive;
        
        // Позиционируем НАЧАЛО линии в центре первого нода
        line.style.left = posA.x;
        line.style.top = posA.y - (_lineThickness / 2f);

        // Устанавливаем точку вращения в НАЧАЛО линии (0% по X, 50% по Y)
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

        foreach (var kvp in _nodeVisuals)
        {
            string id = kvp.Key;
            VisualElement el = kvp.Value;
            
            bool allocated = _treeManager.IsAllocated(id);
            bool canAllocate = !allocated && _treeManager.CanAllocate(id);

            if (allocated)
            {
                el.style.borderTopColor = _colAllocated; el.style.borderBottomColor = _colAllocated;
                el.style.borderLeftColor = _colAllocated; el.style.borderRightColor = _colAllocated;
                el.style.backgroundColor = new StyleColor(new Color(0.8f, 0.6f, 0.1f));
            }
            else if (canAllocate)
            {
                el.style.borderTopColor = _colAvailable; el.style.borderBottomColor = _colAvailable;
                el.style.borderLeftColor = _colAvailable; el.style.borderRightColor = _colAvailable;
                el.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            }
            else
            {
                el.style.borderTopColor = _colLocked; el.style.borderBottomColor = _colLocked;
                el.style.borderLeftColor = _colLocked; el.style.borderRightColor = _colLocked;
                el.style.backgroundColor = new StyleColor(new Color(0.1f, 0.1f, 0.1f));
            }
        }

        foreach (var conn in _connections)
        {
            bool active = _treeManager.IsAllocated(conn.id1) && _treeManager.IsAllocated(conn.id2);
            
            if (active)
                conn.line.style.backgroundColor = _colLineActive;
            else
            {
                bool oneActive = _treeManager.IsAllocated(conn.id1) || _treeManager.IsAllocated(conn.id2);
                 conn.line.style.backgroundColor = oneActive ? new Color(0.5f, 0.5f, 0.5f) : _colLineInactive;
            }
        }
    }
    
    private void OnNodeClicked(string id)
    {
        _treeManager.AllocateNode(id);
    }

    private void CenterOnStartNode()
    {
        if (_treeManager?.TreeData?.Nodes == null) return;
        
        var startNode = _treeManager.TreeData.Nodes.Find(n => n.NodeType == PassiveNodeType.Start);
        if (startNode != null)
        {
            float viewWidth = _contentViewport.resolvedStyle.width;
            float viewHeight = _contentViewport.resolvedStyle.height;
            
            // Смещаем контейнер так, чтобы позиция нода оказалась в центре вьюпорта
            _treeContainer.style.left = -startNode.Position.x * _currentZoom + viewWidth / 2f;
            _treeContainer.style.top = -startNode.Position.y * _currentZoom + viewHeight / 2f;
        }
    }

    // --- FIX №2: NEW Zoom Logic ---
    private void OnWheel(WheelEvent evt)
    {
        if (_treeContainer == null) return;

        // 1. Рассчитываем новый зум
        float zoomDelta = -evt.delta.y > 0 ? 1 + _zoomSpeed : 1 - _zoomSpeed;
        float oldZoom = _currentZoom;
        _currentZoom = Mathf.Clamp(_currentZoom * zoomDelta, _minZoom, _maxZoom);
        
        if (Mathf.Approximately(oldZoom, _currentZoom)) return;

        // 2. Определяем позицию мыши относительно контейнера
        Vector2 mousePosInViewport = evt.localMousePosition;
        Vector2 oldContainerPos = new Vector2(_treeContainer.resolvedStyle.left, _treeContainer.resolvedStyle.top);
        Vector2 mousePosInContainer = (mousePosInViewport - oldContainerPos) / oldZoom;

        // 3. Применяем новый масштаб
        _treeContainer.transform.scale = Vector3.one * _currentZoom;
        
        // 4. Корректируем позицию контейнера, чтобы точка под мышкой осталась на месте
        Vector2 newContainerPos = mousePosInViewport - (mousePosInContainer * _currentZoom);
        _treeContainer.style.left = newContainerPos.x;
        _treeContainer.style.top = newContainerPos.y;
        
        // Предотвращаем стандартное поведение скролла
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
        if (_isDragging)
        {
            _isDragging = false;
            _contentViewport.ReleasePointer(evt.pointerId);
        }
    }
    
    private void OnPointerUp(PointerLeaveEvent evt)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _contentViewport.ReleasePointer(evt.pointerId);
        }
    }
}