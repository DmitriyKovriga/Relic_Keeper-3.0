using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(EnemyStats))]
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        public static event System.Action<float> OnEnemyKilled;

        [Header("Settings")]
        public bool DestroyOnDeath = true;

        private EnemyStats _stats;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;
        private EnemyBrain _brain;
        private MysticShieldController _mysticShield;
        private float _currentHealth;
        private float _maxHealth;
        private bool _isDead;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _isDead;

        public event System.Action<EnemyHealth> OnDeath;
        public event System.Action<float, float> OnHealthChanged;

        private void Awake()
        {
            _stats = GetComponent<EnemyStats>();
            _attack = GetComponent<EnemyAttackController>();
            _animation = GetComponent<EnemyAnimationBridge>();
            _brain = GetComponent<EnemyBrain>();
            _mysticShield = GetComponent<MysticShieldController>();
        }

        public void Initialize()
        {
            _stats = GetComponent<EnemyStats>();
            if (_stats == null)
                return;
            _maxHealth = _stats.GetValue(StatType.MaxHealth);
            if (_maxHealth <= 0f)
                _maxHealth = 1f;
            _currentHealth = _maxHealth;
            _isDead = false;
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        public void TakeDamage(DamageSnapshot damage)
        {
            if (_isDead) return;
            if (!EnsureReady())
                return;

            float armor = _stats.GetValue(StatType.Armor);
            float physDmg = damage.Physical;
            if (armor > 0 && physDmg > 0)
                physDmg = Mathf.Max(0, physDmg - (armor * 0.1f));

            float fireRes = Mathf.Clamp(_stats.GetValue(StatType.FireResist), -200, 75);
            float coldRes = Mathf.Clamp(_stats.GetValue(StatType.ColdResist), -200, 75);
            float lightRes = Mathf.Clamp(_stats.GetValue(StatType.LightningResist), -200, 75);

            float fireDmg = damage.Fire * (1f - (fireRes / 100f));
            float coldDmg = damage.Cold * (1f - (coldRes / 100f));
            float lightDmg = damage.Lightning * (1f - (lightRes / 100f));

            float finalDamage = physDmg + fireDmg + coldDmg + lightDmg;
            if (finalDamage < 0)
                finalDamage = 0;
            if (_mysticShield == null)
                MysticShieldController.TryResolve(transform, out _mysticShield);
            if (_mysticShield != null)
                finalDamage = _mysticShield.ApplyMitigation(finalDamage);

            _currentHealth -= finalDamage;
            TryPlayHitReaction(finalDamage);

            if (FloatingTextManager.Instance != null && finalDamage > 0f)
            {
                string damageType = ResolveDominantDamageType(physDmg, fireDmg, coldDmg, lightDmg);
                FloatingTextManager.Instance.Show(finalDamage, damage.IsCrit, damageType, transform.position);
            }

            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);

            if (_currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            _isDead = true;

            if (_stats != null)
            {
                float xp = _stats.ExperienceReward;
                if (xp > 0f)
                {
                    Vector3 soulPosition = transform.position;
                    var entity = GetComponent<EnemyEntity>();
                    var spriteRenderer = entity != null ? entity.VisualRenderer : GetComponentInChildren<SpriteRenderer>(true);
                    if (spriteRenderer != null)
                        soulPosition = spriteRenderer.bounds.center;

                    ExperienceSoulPickup.Spawn(xp, soulPosition, transform.parent);
                    OnEnemyKilled?.Invoke(xp);
                }
            }

            OnDeath?.Invoke(this);

            if (DestroyOnDeath)
            {
                var entity = GetComponent<EnemyEntity>();
                var spriteRenderer = entity != null ? entity.VisualRenderer : GetComponentInChildren<SpriteRenderer>(true);
                EnemyDeathEffectSpawner.Spawn(entity, spriteRenderer);
                Destroy(gameObject);
            }
        }

        public void Resurrect()
        {
            Initialize();
        }

        public void SyncMaxHealthFromStats()
        {
            if (_stats == null)
                _stats = GetComponent<EnemyStats>();

            if (_stats == null)
                return;

            float newMaxHealth = Mathf.Max(1f, _stats.GetValue(StatType.MaxHealth));
            bool changed = Mathf.Abs(newMaxHealth - _maxHealth) > 0.001f;
            _maxHealth = newMaxHealth;
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);

            if (changed)
                OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
        }

        private bool EnsureReady()
        {
            if (_stats == null)
                _stats = GetComponent<EnemyStats>();

            if (_attack == null)
                _attack = GetComponent<EnemyAttackController>();

            if (_animation == null)
                _animation = GetComponent<EnemyAnimationBridge>();

            if (_brain == null)
                _brain = GetComponent<EnemyBrain>();

            if (_mysticShield == null)
                MysticShieldController.TryResolve(transform, out _mysticShield);

            if (_stats == null)
                return false;

            if (_maxHealth <= 0f)
                Initialize();

            return _stats != null && _maxHealth > 0f;
        }

        private void TryPlayHitReaction(float finalDamage)
        {
            if (finalDamage <= 0f || _animation == null)
                return;

            if (_attack != null && _attack.IsBusy)
                return;

            if (_brain != null && _brain.IsInSpecialAction)
                return;

            _animation.TryPlayHitReaction();
        }

        private static string ResolveDominantDamageType(float physical, float fire, float cold, float lightning)
        {
            string damageType = "Physical";
            float maxDamage = physical;

            if (fire > maxDamage)
            {
                maxDamage = fire;
                damageType = "Fire";
            }

            if (cold > maxDamage)
            {
                maxDamage = cold;
                damageType = "Cold";
            }

            if (lightning > maxDamage)
            {
                damageType = "Lightning";
            }

            return damageType;
        }
    }
}
