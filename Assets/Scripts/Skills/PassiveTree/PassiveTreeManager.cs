using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Scripts.Stats;

namespace Scripts.Skills.PassiveTree
{
    public class PassiveTreeManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PassiveSkillTreeSO _treeData;
        
        [Header("Dependencies")]
        [SerializeField] private PlayerStats _playerStats;

        // Храним ID купленных нодов
        private HashSet<string> _allocatedNodeIDs = new HashSet<string>();
        
        // Список модификаторов, наложенных деревом (чтобы можно было их снять при респеке)
        // Ключ: NodeID, Значение: Список примененных модификаторов
        private Dictionary<string, List<StatModifier>> _activeModifiers = new Dictionary<string, List<StatModifier>>();

        public event System.Action OnTreeUpdated; // UI будет слушать это

        private void Start()
        {
            if (_playerStats == null) _playerStats = GetComponent<PlayerStats>();
            
            // Инициализация словаря в SO для быстрого поиска
            if (_treeData != null) _treeData.InitLookup();

            // Если это новая игра, ищем стартовые ноды
            // В будущем здесь будет загрузка из сейва
            if (_allocatedNodeIDs.Count == 0 && _treeData != null)
            {
                AutoAllocateStartNodes();
            }
        }

        // --- PUBLIC API ---

        public bool IsAllocated(string nodeID)
        {
            return _allocatedNodeIDs.Contains(nodeID);
        }

        public bool CanAllocate(string nodeID)
        {
            // 1. Уже куплен?
            if (_allocatedNodeIDs.Contains(nodeID)) return false;

            // 2. Хватает очков?
            if (_playerStats.Leveling.SkillPoints <= 0) return false;

            // 3. Соединен ли с уже купленным?
            // Ищем нод в базе
            var nodeDef = _treeData.GetNode(nodeID);
            if (nodeDef == null) return false;

            // Проходимся по соседям этого нода. Если хотя бы один сосед куплен -> можно брать.
            foreach (var neighborID in nodeDef.ConnectionIDs)
            {
                if (_allocatedNodeIDs.Contains(neighborID)) return true;
            }

            return false;
        }

        public void AllocateNode(string nodeID)
        {
            if (!CanAllocate(nodeID)) return;

            // Тратим очко
            if (_playerStats.Leveling.TrySpendPoint(1))
            {
                // Добавляем в список купленных
                _allocatedNodeIDs.Add(nodeID);

                // Применяем статы
                ApplyNodeStats(nodeID);

                OnTreeUpdated?.Invoke();
                Debug.Log($"[PassiveTree] Node {nodeID} allocated!");
            }
        }

        // --- INTERNAL LOGIC ---

        private void AutoAllocateStartNodes()
        {
            foreach (var node in _treeData.Nodes)
            {
                if (node.NodeType == PassiveNodeType.Start)
                {
                    // Стартовые ноды даются бесплатно и без проверки связей
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
                // Создаем рантайм модификатор. Источник (Source) = этот скрипт или сам объект NodeDefinition
                var runtimeMod = modData.ToStatModifier(this);
                
                // Накладываем на игрока
                _playerStats.GetStat(modData.Stat).AddModifier(runtimeMod);
                
                appliedMods.Add(runtimeMod);
            }

            // Запоминаем, чтобы потом можно было удалить (при респеке)
            _activeModifiers[nodeID] = appliedMods;
            
            // Форсируем обновление статов игрока
            // (В PlayerStats уже есть событие OnAnyStatChanged, оно сработает само при добавлении мода)
        }
        
        // Метод для сохранения (вызовем в GameSaveManager)
        public List<string> GetSaveData()
        {
            return new List<string>(_allocatedNodeIDs);
        }

        // Метод для загрузки
        public void LoadState(List<string> savedIDs)
        {
            _allocatedNodeIDs.Clear();
            _activeModifiers.Clear(); // Очистить старые, если были (надо бы удалить их из статов тоже, но при загрузке статы и так чистые)
            
            // Важно: При загрузке мы не тратим очки и не проверяем связи, просто восстанавливаем
            foreach (var id in savedIDs)
            {
                _allocatedNodeIDs.Add(id);
                ApplyNodeStats(id);
            }
            OnTreeUpdated?.Invoke();
        }
    }
}