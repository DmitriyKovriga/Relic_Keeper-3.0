using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Нужно для удобства
using Scripts.Stats;

namespace Scripts.Skills.PassiveTree
{
    public class PassiveTreeManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] public PassiveSkillTreeSO _treeData;
        
        [Header("Dependencies")]
        [SerializeField] private PlayerStats _playerStats;

        public PassiveSkillTreeSO TreeData => _treeData;
        public PlayerStats PlayerStats => _playerStats;

        /// <summary>Режим предпросмотра (из таверны): скрыть Skill Points.</summary>
        public bool IsPreviewMode { get; set; }

        /// <summary>Сменить дерево (при смене персонажа). Очищает текущие ноды и устанавливает новое дерево.</summary>
        public void SetTreeData(PassiveSkillTreeSO newTree)
        {
            foreach (var id in new List<string>(_activeModifiers.Keys))
                RemoveNodeStats(id);
            _allocatedNodeIDs.Clear();
            _activeModifiers.Clear();
            _treeData = newTree;
            if (_treeData != null) _treeData.InitLookup();
            OnTreeUpdated?.Invoke();
        }

        public int SkillPoints
        {
            get
            {
                if (_playerStats == null || _playerStats.Leveling == null) return 0;
                return _playerStats.Leveling.SkillPoints;
            }
        }

        private HashSet<string> _allocatedNodeIDs = new HashSet<string>();
        
        // Храним тип стата вместе с модификатором, чтобы знать, откуда удалять
        private Dictionary<string, List<(StatType type, StatModifier mod)>> _activeModifiers = new Dictionary<string, List<(StatType, StatModifier)>>();

        public event System.Action OnTreeUpdated; 

        private void Awake()
        {
            if (_playerStats == null) _playerStats = GetComponent<PlayerStats>();
            if (_treeData != null) _treeData.InitLookup();
        }

        private void Start()
        {
            if (_allocatedNodeIDs.Count == 0 && _treeData != null)
            {
                AutoAllocateStartNodes();
            }
            EnsureStartNodesAllocated();
            OnTreeUpdated?.Invoke();
        }

        private void OnEnable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnLevelingInitialized += RefreshLevelingSubscription;
                RefreshLevelingSubscription();
            }
        }

        private void OnDisable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnLevelingInitialized -= RefreshLevelingSubscription;
                if (_playerStats.Leveling != null)
                    _playerStats.Leveling.OnSkillPointsChanged -= HandlePointsChanged;
            }
        }

        private void RefreshLevelingSubscription()
        {
            if (_playerStats.Leveling != null)
            {
                _playerStats.Leveling.OnSkillPointsChanged -= HandlePointsChanged;
                _playerStats.Leveling.OnSkillPointsChanged += HandlePointsChanged;
                HandlePointsChanged();
            }
        }

        private void HandlePointsChanged() => OnTreeUpdated?.Invoke();

        // --- ALLOCATION LOGIC ---

        public bool IsAllocated(string nodeID) => _allocatedNodeIDs.Contains(nodeID);

        public bool CanAllocate(string nodeID)
        {
            if (_allocatedNodeIDs.Contains(nodeID)) return false;
            if (SkillPoints <= 0) return false; 

            var nodeDef = _treeData.GetNode(nodeID);
            if (nodeDef == null) return false;

            foreach (var neighborID in nodeDef.ConnectionIDs)
            {
                if (_allocatedNodeIDs.Contains(neighborID)) return true;
            }
            return false;
        }

        public void AllocateNode(string nodeID)
        {
            if (!CanAllocate(nodeID)) return;

            if (_playerStats.Leveling.TrySpendPoint(1))
            {
                _allocatedNodeIDs.Add(nodeID);
                ApplyNodeStats(nodeID);
                OnTreeUpdated?.Invoke();
            }
        }

        // --- REFUND LOGIC (NEW) ---

        public bool CanRefund(string nodeID)
        {
            // 1. Нельзя откатить то, чего нет
            if (!IsAllocated(nodeID)) return false;

            var node = _treeData.GetNode(nodeID);
            if (node == null) return false;

            // 2. Нельзя откатить стартовый нод
            if (node.NodeType == PassiveNodeType.Start) return false;

            // 3. ПРОВЕРКА СВЯЗНОСТИ (Graph Connectivity Check)
            // Если мы удалим этот нод, не останутся ли другие ноды "висеть в воздухе"?
            
            // Создаем временный набор нодов БЕЗ того, который хотим удалить
            HashSet<string> potentialNodes = new HashSet<string>(_allocatedNodeIDs);
            potentialNodes.Remove(nodeID);

            // Ищем все стартовые ноды
            List<string> startNodes = new List<string>();
            foreach (var id in potentialNodes)
            {
                var n = _treeData.GetNode(id);
                if (n.NodeType == PassiveNodeType.Start) startNodes.Add(id);
            }

            // Запускаем поиск в ширину (BFS) от всех стартовых точек
            HashSet<string> visited = new HashSet<string>();
            Queue<string> queue = new Queue<string>();

            foreach(var start in startNodes)
            {
                queue.Enqueue(start);
                visited.Add(start);
            }

            while (queue.Count > 0)
            {
                string current = queue.Dequeue();
                var currentDef = _treeData.GetNode(current);

                foreach (var neighborID in currentDef.ConnectionIDs)
                {
                    // Идем только по купленным нодам, которые мы еще не посетили
                    if (potentialNodes.Contains(neighborID) && !visited.Contains(neighborID))
                    {
                        visited.Add(neighborID);
                        queue.Enqueue(neighborID);
                    }
                }
            }

            // Если количество посещенных нодов совпадает с количеством оставшихся нодов,
            // значит дерево осталось цельным.
            return visited.Count == potentialNodes.Count;
        }

        public void RefundNode(string nodeID)
        {
            if (!CanRefund(nodeID)) return;

            // 1. Убираем из списка
            _allocatedNodeIDs.Remove(nodeID);

            // 2. Снимаем статы
            RemoveNodeStats(nodeID);

            // 3. Возвращаем очко
            _playerStats.Leveling.RefundPoint(1);

            OnTreeUpdated?.Invoke();
        }

        // --- STATS MANAGEMENT ---

        private void AutoAllocateStartNodes()
        {
            foreach (var node in _treeData.Nodes)
            {
                if (node.NodeType == PassiveNodeType.Start)
                {
                    _allocatedNodeIDs.Add(node.ID);
                    ApplyNodeStats(node.ID);
                }
            }
        }

        private void EnsureStartNodesAllocated()
        {
            if (_treeData == null) return;
            foreach (var node in _treeData.Nodes)
            {
                if (node.NodeType == PassiveNodeType.Start && !_allocatedNodeIDs.Contains(node.ID))
                {
                    _allocatedNodeIDs.Add(node.ID);
                    ApplyNodeStats(node.ID);
                }
            }
        }

        private void ApplyNodeStats(string nodeID)
{
    if (_activeModifiers.ContainsKey(nodeID)) return;

    var nodeDef = _treeData.GetNode(nodeID);
    if (nodeDef == null) return;

    var modifiers = nodeDef.GetFinalModifiers();
    if (modifiers.Count == 0) return;

    var appliedMods = new List<(StatType, StatModifier)>();

    foreach (var modData in modifiers)
    {
        var runtimeMod = modData.ToStatModifier(this);
        
        // Добавляем модификатор (это пометит стат как "грязный", но не обновит UI)
        _playerStats.GetStat(modData.Stat).AddModifier(runtimeMod);
        
        appliedMods.Add((modData.Stat, runtimeMod));
    }

    _activeModifiers[nodeID] = appliedMods;

    // --- ДОБАВЛЕНО: Принудительно обновляем все системы и UI ---
    _playerStats.NotifyChanged();
}

        private void RemoveNodeStats(string nodeID)
{
    if (_activeModifiers.TryGetValue(nodeID, out var mods))
    {
        foreach (var (type, mod) in mods)
        {
            _playerStats.GetStat(type).RemoveModifier(mod);
        }
        _activeModifiers.Remove(nodeID);
        
        // --- ДОБАВЛЕНО: Принудительно обновляем все системы и UI ---
        _playerStats.NotifyChanged();
    }
}

        // --- SAVE / LOAD ---

        public List<string> GetSaveData() => new List<string>(_allocatedNodeIDs);

        public void LoadState(List<string> savedIDs)
        {
            // 1. Очищаем ТЕКУЩИЕ статы перед загрузкой новых
            // Это критично, если мы делаем LoadGame, не перезапуская игру
            foreach (var id in new List<string>(_activeModifiers.Keys))
            {
                RemoveNodeStats(id);
            }
            
            _allocatedNodeIDs.Clear();
            _activeModifiers.Clear();

            // 2. Накатываем новые
            foreach (var id in savedIDs)
            {
                _allocatedNodeIDs.Add(id);
                ApplyNodeStats(id);
            }
            
            EnsureStartNodesAllocated();
            OnTreeUpdated?.Invoke();
        }
    }
}