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

    /// <summary>
    /// Режим размещения нода: свободно на canvas или на орбите кластера.
    /// </summary>
    public enum NodePlacementMode
    {
        Free,       // Position используется напрямую
        OnOrbit     // Позиция вычисляется из ClusterID + OrbitIndex + OrbitAngle
    }

    /// <summary>
    /// Орбита в кластере — окружность с заданным радиусом.
    /// Ноды на орбите размещаются по углу (OrbitAngle в градусах).
    /// </summary>
    [Serializable]
    public class PassiveOrbitDefinition
    {
        public float Radius = 80f;
        
        [Header("Arc Settings (Optional)")]
        [Tooltip("Если true, орбита - это дуга (ракушка), не полный круг")]
        public bool IsPartialArc = false;
        [Tooltip("Начальный угол дуги (0-360)")] 
        [Range(0f, 360f)]
        public float ArcStartAngle = 0f;
        [Tooltip("Конечный угол дуги (0-360)")]
        [Range(0f, 360f)]
        public float ArcEndAngle = 360f;
    }

    /// <summary>
    /// Кластер нодов — "планета с орбитами". Центр + список орбит разного радиуса.
    /// Соединения между нодами на одной орбите рисуются дугой по окружности.
    /// </summary>
    [Serializable]
    public class PassiveClusterDefinition
    {
        public string ID;
        public string Name = "Cluster";
        public Vector2 Center;
        public List<PassiveOrbitDefinition> Orbits = new List<PassiveOrbitDefinition>();
        
        [Header("Visual Settings")]
        [Tooltip("Цвет кластера в редакторе")]
        public Color EditorColor = new Color(0.5f, 0.5f, 1f, 0.3f);
        
        [Header("Road Connections")]
        [Tooltip("ID других кластеров, к которым идут дороги")]
        public List<string> RoadConnections = new List<string>();
    }

    [CreateAssetMenu(menuName = "RPG/Passive Tree/Skill Tree Definition")]
    public class PassiveSkillTreeSO : ScriptableObject
    {
        [Header("Graph Data")]
        public List<PassiveNodeDefinition> Nodes = new List<PassiveNodeDefinition>();

        [Header("Clusters (Orbit Groups)")]
        public List<PassiveClusterDefinition> Clusters = new List<PassiveClusterDefinition>();

        [Header("Editor Settings")]
        [Tooltip("Шаг сетки в пикселях. 0 = сетка отключена.")]
        public float GridSize = 20f;
        [Tooltip("Привязывать ноды к сетке при перемещении.")]
        public bool SnapToGrid = true;

        // Быстрый поиск нода по ID (инициализируется в Runtime)
        private Dictionary<string, PassiveNodeDefinition> _lookup;
        private Dictionary<string, PassiveClusterDefinition> _clusterLookup;

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

            _clusterLookup = new Dictionary<string, PassiveClusterDefinition>();
            if (Clusters == null) Clusters = new List<PassiveClusterDefinition>();
            foreach (var cluster in Clusters)
            {
                if (!string.IsNullOrEmpty(cluster.ID) && !_clusterLookup.ContainsKey(cluster.ID))
                {
                    _clusterLookup.Add(cluster.ID, cluster);
                }
            }
        }

        public PassiveNodeDefinition GetNode(string id)
        {
            if (_lookup == null) InitLookup();
            return _lookup.GetValueOrDefault(id);
        }

        public PassiveClusterDefinition GetCluster(string id)
        {
            if (_clusterLookup == null) InitLookup();
            return _clusterLookup?.GetValueOrDefault(id);
        }

        /// <summary>
        /// Проверяет, находятся ли оба нода на одной орбите одного кластера.
        /// Используется для отрисовки дуги вместо прямой линии.
        /// </summary>
        public bool AreNodesOnSameOrbit(string nodeIdA, string nodeIdB, out string clusterId, out int orbitIndex)
        {
            clusterId = null;
            orbitIndex = -1;

            var nodeA = GetNode(nodeIdA);
            var nodeB = GetNode(nodeIdB);
            if (nodeA == null || nodeB == null) return false;
            if (nodeA.PlacementMode != NodePlacementMode.OnOrbit || nodeB.PlacementMode != NodePlacementMode.OnOrbit)
                return false;
            if (nodeA.ClusterID != nodeB.ClusterID) return false;
            if (nodeA.OrbitIndex != nodeB.OrbitIndex) return false;

            clusterId = nodeA.ClusterID;
            orbitIndex = nodeA.OrbitIndex;
            return true;
        }
    }

    [Serializable]
    public class PassiveNodeDefinition
    {
        [Header("Identification")]
        public string ID; // GUID
        public PassiveNodeType NodeType = PassiveNodeType.Small;

        [Header("Placement")]
        [Tooltip("Free = позиция вручную. OnOrbit = позиция на окружности кластера.")]
        public NodePlacementMode PlacementMode = NodePlacementMode.Free;

        [Tooltip("Используется когда PlacementMode = Free")]
        public Vector2 Position;

        [Tooltip("Используется когда PlacementMode = OnOrbit")]
        public string ClusterID;
        [Tooltip("Индекс орбиты в кластере (0 = внутренняя)")]
        public int OrbitIndex;
        [Tooltip("Угол на орбите в градусах. 0 = вправо, 90 = вниз")]
        [Range(0f, 360f)]
        public float OrbitAngle;

        [Header("Data Source")]
        // Вариант А: Использовать шаблон
        public PassiveNodeTemplateSO Template;
        
        // Вариант Б: Уникальные статы (переопределяют или дополняют шаблон)
        public List<SerializableStatModifier> UniqueModifiers;

        [Header("Graph Connections")]
        public List<string> ConnectionIDs = new List<string>(); // ID соседей

        /// <summary>
        /// Возвращает мировую позицию нода (для отрисовки и расчёта связей).
        /// </summary>
        public Vector2 GetWorldPosition(PassiveSkillTreeSO tree)
        {
            if (tree == null) return Position;

            if (PlacementMode == NodePlacementMode.Free)
                return Position;

            var cluster = tree.GetCluster(ClusterID);
            if (cluster == null) return Position;

            if (OrbitIndex < 0 || OrbitIndex >= cluster.Orbits.Count)
                return Position;

            var orbit = cluster.Orbits[OrbitIndex];
            float rad = OrbitAngle * Mathf.Deg2Rad;
            // 0° = вправо, 90° = вниз (Unity UI: Y вниз)
            return cluster.Center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbit.Radius;
        }
        
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