// ==========================================
// FILENAME: Assets/Scripts/Skills/PassiveTree/PassiveTreeManager.cs
// ==========================================
using UnityEngine;
using System.Collections.Generic;
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

        public int SkillPoints
        {
            get
            {
                if (_playerStats == null || _playerStats.Leveling == null) return 0;
                return _playerStats.Leveling.SkillPoints;
            }
        }

        private HashSet<string> _allocatedNodeIDs = new HashSet<string>();
        private Dictionary<string, List<StatModifier>> _activeModifiers = new Dictionary<string, List<StatModifier>>();

        public event System.Action OnTreeUpdated; 

        private void Awake()
        {
            if (_playerStats == null) _playerStats = GetComponent<PlayerStats>();
            if (_treeData != null) _treeData.InitLookup();
        }

        private void Start()
{
    // Если список пуст (новая игра), ищем старт
    if (_allocatedNodeIDs.Count == 0 && _treeData != null)
    {
        AutoAllocateStartNodes();
    }
    
    // --- ДОБАВЛЕНО: Защита "на всякий случай" ---
    // Пробегаемся и проверяем, что все стартовые ноды точно добавлены
    EnsureStartNodesAllocated();
    
    OnTreeUpdated?.Invoke();
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

        private void OnEnable()
        {
            if (_playerStats != null)
            {
                // 1. Слушаем, если PlayerStats решит заменить объект Leveling
                _playerStats.OnLevelingInitialized += RefreshLevelingSubscription;
                
                // 2. И сразу подписываемся на текущий
                RefreshLevelingSubscription();
            }
        }

        private void OnDisable()
        {
            if (_playerStats != null)
            {
                _playerStats.OnLevelingInitialized -= RefreshLevelingSubscription;
                
                // Отписываемся от левелинга
                if (_playerStats.Leveling != null)
                {
                    _playerStats.Leveling.OnSkillPointsChanged -= HandlePointsChanged;
                }
            }
        }

        // --- ВАЖНЫЙ МЕТОД ---
        // Вызывается при старте и каждый раз, когда загружается игра/сбрасываются статы
        private void RefreshLevelingSubscription()
        {
            if (_playerStats.Leveling != null)
            {
                // Сначала отписываемся (на всякий случай, чтобы не дублировать)
                _playerStats.Leveling.OnSkillPointsChanged -= HandlePointsChanged;
                // Подписываемся заново к НОВОМУ объекту
                _playerStats.Leveling.OnSkillPointsChanged += HandlePointsChanged;
                
                // Обновляем UI прямо сейчас
                HandlePointsChanged();
            }
        }

        private void HandlePointsChanged()
        {
            OnTreeUpdated?.Invoke();
        }
        
        // ... (Остальной код без изменений: IsAllocated, CanAllocate, AllocateNode, AutoAllocateStartNodes, ApplyNodeStats, GetSaveData, LoadState) ...
        
        public bool IsAllocated(string nodeID) => _allocatedNodeIDs.Contains(nodeID);

        public bool CanAllocate(string nodeID)
        {
            if (_allocatedNodeIDs.Contains(nodeID)) return false;
            if (SkillPoints <= 0) return false; 
            var nodeDef = _treeData.GetNode(nodeID);
            if (nodeDef == null) return false;
            foreach (var neighborID in nodeDef.ConnectionIDs)
                if (_allocatedNodeIDs.Contains(neighborID)) return true;
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
                Debug.Log($"[PassiveTree] Node {nodeID} allocated!");
            }
        }

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
            OnTreeUpdated?.Invoke();
        }

        private void ApplyNodeStats(string nodeID)
        {
            var nodeDef = _treeData.GetNode(nodeID);
            if (nodeDef == null) return;
            var modifiers = nodeDef.GetFinalModifiers();
            if (modifiers.Count == 0) return;
            var appliedMods = new List<StatModifier>();
            foreach (var modData in modifiers)
            {
                var runtimeMod = modData.ToStatModifier(this);
                _playerStats.GetStat(modData.Stat).AddModifier(runtimeMod);
                appliedMods.Add(runtimeMod);
            }
            _activeModifiers[nodeID] = appliedMods;
        }

        public List<string> GetSaveData() => new List<string>(_allocatedNodeIDs);

        public void LoadState(List<string> savedIDs)
{
    _allocatedNodeIDs.Clear();
    _activeModifiers.Clear();

    foreach (var id in savedIDs)
    {
        _allocatedNodeIDs.Add(id);
        ApplyNodeStats(id);
    }
    
    // Гарантируем старт даже после загрузки
    EnsureStartNodesAllocated();

    OnTreeUpdated?.Invoke();
}
    }
}