using UnityEngine;
using System.Collections;
using Scripts.Stats;
using Scripts.Skills.Modules;

namespace Scripts.Skills
{
    [RequireComponent(typeof(SkillHitbox))]
    [RequireComponent(typeof(SkillDamageDealer))]
    [RequireComponent(typeof(SkillVFX))]
    [RequireComponent(typeof(SkillMovementControl))]
    [RequireComponent(typeof(SkillHandAnimation))]
    public class CleaveSkill : SkillBehaviour
    {
        [Header("Timeline Config (0.0 - 1.0)")]
        [Tooltip("Блокируем движение (чуть-чуть дали проскользить в начале)")]
        [Range(0f, 1f)] [SerializeField] private float _lockTime = 0.1f;
        
        [Tooltip("Момент нанесения урона и вспышки VFX")]
        [Range(0f, 1f)] [SerializeField] private float _impactTime = 0.35f;
        
        [Tooltip("Момент, когда уже МОЖНО БЕЖАТЬ (Animation Cancel). Ставим сразу после удара.")]
        [Range(0f, 1f)] [SerializeField] private float _unlockTime = 0.4f; 

        // Модули
        private SkillHitbox _hitbox;
        private SkillDamageDealer _damage;
        private SkillVFX _vfx;
        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;

        private void Awake()
        {
            _hitbox = GetComponent<SkillHitbox>();
            _damage = GetComponent<SkillDamageDealer>();
            _vfx = GetComponent<SkillVFX>();
            _moveCtrl = GetComponent<SkillMovementControl>();
            _animCtrl = GetComponent<SkillHandAnimation>();
        }

        public override void Initialize(PlayerStats stats, SkillDataSO data)
        {
            base.Initialize(stats, data);
            _damage.Initialize(stats);
            _moveCtrl.Initialize(stats);
            _animCtrl.Initialize(stats);
        }

        protected override void Execute()
        {
            StartCoroutine(SkillRoutine());
        }

        private IEnumerator SkillRoutine()
        {
            _isCasting = true;

            float aps = _ownerStats.GetValue(StatType.AttackSpeed);
            if (aps <= 0) aps = 1f;
            
            float duration = 1f / aps;
            float aoeMod = 1f + (_ownerStats.GetValue(StatType.AreaOfEffect) / 100f);
            
            float timer = 0f;
            
            bool locked = false;
            bool impacted = false;
            bool unlocked = false;

            while (timer < duration)
            {
                float progress = timer / duration;

                // 1. LOCK
                if (!locked && progress >= _lockTime)
                {
                    _moveCtrl.SetLock(true);
                    locked = true;
                }

                // 2. IMPACT (Удар)
                if (!impacted && progress >= _impactTime)
                {
                    // Прячем оружие (теперь роль визуала играет VFX)
                    _animCtrl.SetWeaponVisible(false); 
                    _animCtrl.SnapToImpact();        

                    float dir = _ownerStats.transform.localScale.x > 0 ? 1 : -1;
                    
                    _vfx.Play(_ownerStats.transform, dir, aoeMod, aps);
                    
                    var targets = _hitbox.GetTargets(_ownerStats.transform.position, dir, aoeMod);
                    _damage.DealDamage(targets);

                    impacted = true;
                }

                // 3. UNLOCK (Разрешаем бежать!)
                // Обрати внимание: мы НЕ включаем здесь отображение оружия.
                // Игрок бежит, рука опускается невидимой, VFX доигрывает.
                if (!unlocked && progress >= _unlockTime)
                {
                    _moveCtrl.SetLock(false);
                    unlocked = true;
                }

                // --- Анимация руки ---
                // Даже если мы уже бежим (Unlock сработал), рука продолжает опускаться (Recovery).
                // Это не мешает движению, но сохраняет плавность для следующего удара.
                if (progress < _impactTime)
                {
                    float t = progress / _impactTime;
                    _animCtrl.LerpWindup(t);
                }
                else
                {
                    float t = (progress - _impactTime) / (1f - _impactTime);
                    _animCtrl.LerpRecovery(t);
                }

                timer += Time.deltaTime;
                yield return null;
            }

            // --- ФИНАЛ ---
            // Анимация закончилась полностью.
            
            _animCtrl.ForceReset();          // Возвращаем руку в 0 и ВКЛЮЧАЕМ спрайт оружия
            _moveCtrl.SetLock(false);        // На всякий случай разблокируем (если UnlockTime был > 1)
            
            _isCasting = false;              // Разрешаем следующий удар
        }
    }
}