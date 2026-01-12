using UnityEngine;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyEntity : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private EnemyDataSO _defaultData;
        
        [Tooltip("Уровень врага (1-30+). Влияет на ХП и Урон.")]
        [SerializeField] [Range(1, 100)] private int _level = 1;

        private EnemyStats _stats;
        private EnemyHealth _health;
        private bool _isInitialized = false;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _health = GetComponent<EnemyHealth>();
        }

        private void Start()
        {
            // Если мы просто кинули префаб на сцену и не вызывали Setup из кода
            if (!_isInitialized && _defaultData != null)
            {
                Setup(_defaultData, _level);
            }
        }

        /// <summary>
        /// Главный метод инициализации.
        /// </summary>
        /// <param name="data">База данных врага</param>
        /// <param name="levelOverride">Если > 0, переписывает уровень из инспектора</param>
        public void Setup(EnemyDataSO data, int levelOverride = -1)
        {
            if (data == null) return;

            if (levelOverride > 0) _level = levelOverride;

            // 1. Инициализируем статы с учетом уровня
            _stats.Initialize(data, _level);

            // 2. Инициализируем здоровье (оно подтянет уже увеличенный MaxHealth из статов)
            _health.Initialize();
            
            // 3. Визуал и имя
            name = $"[{_level}] {data.DisplayName}";
            
            _isInitialized = true;
        }

        // Метод для изменения уровня на лету (например для тестов)
        [ContextMenu("Refresh Stats")]
        public void Refresh()
        {
            if (_defaultData != null) Setup(_defaultData, _level);
        }
    }
}