using UnityEngine;
using System.Collections.Generic;
using System;
using Scripts.Stats;

namespace Scripts.Skills.PassiveTree
{
    public enum PassiveNodeType
    {
        Small,      // Обычный (маленький)
        Notable,    // Значимый (средний, с рамкой)
        Keystone,   // Ключевой (большой, меняет механику)
        Start       // Точка старта
    }

    [CreateAssetMenu(menuName = "RPG/Passive Tree/Skill Tree Definition")]
    public class PassiveSkillTreeSO : ScriptableObject
    {
        [Header("Graph Data")]
        public List<PassiveNodeDefinition> Nodes = new List<PassiveNodeDefinition>();

        // Быстрый поиск нода по ID (инициализируется в Runtime)
        private Dictionary<string, PassiveNodeDefinition> _lookup;

        public void InitLookup()
        {
            _lookup = new Dictionary<string, PassiveNodeDefinition>();
            foreach (var node in Nodes)
            {
                if (!string.IsNullOrEmpty(node.ID) && !_lookup.ContainsKey(node.ID))
                {
                    _lookup.Add(node.ID, node);
                }
            }
        }

        public PassiveNodeDefinition GetNode(string id)
        {
            if (_lookup == null) InitLookup();
            return _lookup.GetValueOrDefault(id);
        }
    }

    [Serializable]
    public class PassiveNodeDefinition
    {
        [Header("Identification")]
        public string ID; // GUID
        public Vector2 Position; // Координаты в окне редактора/игры
        public PassiveNodeType NodeType = PassiveNodeType.Small;

        [Header("Data Source")]
        // Вариант А: Использовать шаблон
        public PassiveNodeTemplateSO Template;
        
        // Вариант Б: Уникальные статы (переопределяют или дополняют шаблон)
        public List<SerializableStatModifier> UniqueModifiers;

        [Header("Graph Connections")]
        public List<string> ConnectionIDs = new List<string>(); // ID соседей
        
        // Хелпер для получения финального списка статов
        public List<SerializableStatModifier> GetFinalModifiers()
        {
            var result = new List<SerializableStatModifier>();
            
            // Сначала добавляем из шаблона
            if (Template != null && Template.Modifiers != null)
            {
                result.AddRange(Template.Modifiers);
            }
            
            // Потом уникальные
            if (UniqueModifiers != null)
            {
                result.AddRange(UniqueModifiers);
            }
            
            return result;
        }
        
        // Хелпер для имени
        public string GetDisplayName()
        {
            return Template != null ? Template.Name : "Unknown Node";
        }
        
        // Хелпер для иконки
        public Sprite GetIcon()
        {
             return Template != null ? Template.Icon : null;
        }
    }
}