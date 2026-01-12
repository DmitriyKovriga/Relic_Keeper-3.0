using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static event System.Action<float> OnEnemyKilled;

        // --- AI ADDED: Флаг, разрешающий удаление объекта ---
        [Header("Settings")]
        public bool DestroyOnDeath = true; 

        private EnemyStats _stats;
        private float _currentHealth;
        private float _maxHealth;
        private bool _isDead;

        public event System.Action<EnemyHealth> OnDeath;
        public event System.Action<float, float> OnHealthChanged;

        public void Initialize()
        {
            _stats = GetComponent<EnemyStats>();
            _maxHealth = _stats.GetValue(StatType.MaxHealth);
            _currentHealth = _maxHealth;
            _isDead = false;
            
            // Обновляем UI сразу при инициализации
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(DamageSnapshot damage)
        {
            if (_isDead) return;

            // ... (ВЕСЬ КОД РАСЧЕТА УРОНА БЕЗ ИЗМЕНЕНИЙ) ...
            // Armor vs Physical
            float armor = _stats.GetValue(StatType.Armor);
            float physDmg = damage.Physical;
            if (armor > 0 && physDmg > 0) physDmg = Mathf.Max(0, physDmg - (armor * 0.1f)); 

            float fireRes = Mathf.Clamp(_stats.GetValue(StatType.FireResist), -200, 75);
            float coldRes = Mathf.Clamp(_stats.GetValue(StatType.ColdResist), -200, 75);
            float lightRes = Mathf.Clamp(_stats.GetValue(StatType.LightningResist), -200, 75);

            float fireDmg = damage.Fire * (1f - (fireRes / 100f));
            float coldDmg = damage.Cold * (1f - (coldRes / 100f));
            float lightDmg = damage.Lightning * (1f - (lightRes / 100f));

            float finalDamage = physDmg + fireDmg + coldDmg + lightDmg;
            if (finalDamage < 0) finalDamage = 0;
            // ... (КОНЕЦ РАСЧЕТА) ...

            _currentHealth -= finalDamage;
            
            if (FloatingTextManager.Instance != null)
                FloatingTextManager.Instance.Show(damage, transform.position);

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            // Debug.Log удалил, чтобы не спамил

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            _isDead = true;
            
            if (_stats != null)
            {
                float xp = _stats.ExperienceReward;
                if (xp > 0) OnEnemyKilled?.Invoke(xp);
            }

            // Уведомляем подписчиков (DummyEvolution подпишется сюда)
            OnDeath?.Invoke(this);

            // --- AI MODIFIED: Удаляем только если разрешено ---
            if (DestroyOnDeath)
            {
                Destroy(gameObject); 
            }
        }
        
        // Метод для воскрешения (вызывается извне)
        public void Resurrect()
        {
            // Initialize сбросит ХП до максимума и флаг _isDead
            Initialize();
        }
    }
}