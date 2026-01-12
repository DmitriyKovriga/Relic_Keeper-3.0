using UnityEngine;
using TMPro; // Если захочешь выводить уровень над головой

namespace Scripts.Enemies
{
    [RequireComponent(typeof(EnemyEntity))]
    [RequireComponent(typeof(EnemyHealth))]
    [RequireComponent(typeof(EnemyStats))]
    public class DummyEvolution : MonoBehaviour
    {
        [SerializeField] private int _maxLevel = 30;
        
        // Ссылка на данные, чтобы перезагружать их при левелапе
        [SerializeField] private EnemyDataSO _dummyData; 

        private EnemyEntity _entity;
        private EnemyHealth _health;
        private EnemyStats _stats;

        private void Awake()
        {
            _entity = GetComponent<EnemyEntity>();
            _health = GetComponent<EnemyHealth>();
            _stats = GetComponent<EnemyStats>();
        }

        private void Start()
        {
            // Запрещаем манекену умирать насовсем
            _health.DestroyOnDeath = false;
            
            // Подписываемся на смерть
            _health.OnDeath += HandleDeath;
        }

        private void OnDestroy()
        {
            _health.OnDeath -= HandleDeath;
        }

        private void HandleDeath(EnemyHealth hp)
        {
            int currentLevel = _stats.Level;

            if (currentLevel < _maxLevel)
            {
                int nextLevel = currentLevel + 1;
                Debug.Log($"<color=yellow>[Dummy] EVOLUTION! Level {currentLevel} -> {nextLevel}</color>");

                // 1. Пересчитываем статы через Entity (она внутри вызовет Stats.Initialize и Health.Initialize)
                if (_dummyData != null)
                {
                    _entity.Setup(_dummyData, nextLevel);
                }
                
                // 2. Дополнительно лечим (Setup уже вызывает Initialize в Health, но для надежности)
                _health.Resurrect();
            }
            else
            {
                Debug.Log("<color=red>[Dummy] MAX LEVEL REACHED. Destroying.</color>");
                // Разрешаем умереть окончательно
                _health.DestroyOnDeath = true; 
                Destroy(gameObject);
            }
        }
    }
}