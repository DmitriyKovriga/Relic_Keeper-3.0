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
        [Range(0f, 1f)] [SerializeField] private float _lockTime = 0.1f;
        [Range(0f, 1f)] [SerializeField] private float _impactTime = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float _unlockTime = 0.4f;

        // Модули
        private SkillHitbox _hitbox;
        private SkillDamageDealer _damage;
        private SkillVFX _vfx;
        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;

        // Кэшированные данные для текущего каста
        private float _currentDuration;
        private float _currentAoe;
        private float _currentAps;

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
            StartCoroutine(SkillPipeline());
        }

        // --- ГЛАВНЫЙ ОРКЕСТРАТОР (Теперь он чистый) ---
        private IEnumerator SkillPipeline()
        {
            _isCasting = true;

            // 1. Снапшот статов (запоминаем параметры на начало удара)
            CalculateSkillStats();

            // 2. Фаза Замаха (от 0 до ImpactTime)
            // Внутри происходит блокировка движения
            yield return StartCoroutine(PhaseWindup());

            // 3. Фаза Удара (Моментальное событие)
            PerformImpact();

            // 4. Фаза Возврата (от ImpactTime до 1.0)
            // Внутри происходит разблокировка движения
            yield return StartCoroutine(PhaseRecovery());

            // 5. Завершение
            Cleanup();
        }

        // --- ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ФАЗ ---

        private void CalculateSkillStats()
        {
            _currentAps = _ownerStats.GetValue(StatType.AttackSpeed);
            if (_currentAps <= 0) _currentAps = 1f;
            
            _currentDuration = 1f / _currentAps;
            _currentAoe = 1f + (_ownerStats.GetValue(StatType.AreaOfEffect) / 100f);
        }

        private IEnumerator PhaseWindup()
        {
            float windupDuration = _currentDuration * _impactTime;
            float timer = 0f;
            bool locked = false;

            while (timer < windupDuration)
            {
                // Нормализованное время ОТНОСИТЕЛЬНО ВСЕГО СКИЛЛА (0.0 -> ImpactTime)
                float globalProgress = timer / _currentDuration;
                
                // Нормализованное время ОТНОСИТЕЛЬНО ФАЗЫ ЗАМАХА (0.0 -> 1.0)
                // Нужно для Lerp анимации
                float phaseProgress = timer / windupDuration;

                // Логика блокировки
                if (!locked && globalProgress >= _lockTime)
                {
                    _moveCtrl.SetLock(true);
                    locked = true;
                }

                // Анимация
                _animCtrl.LerpSlashWindup(phaseProgress);

                timer += Time.deltaTime;
                yield return null;
            }
            
            // Гарантируем, что блокировка сработала, даже если лагануло
            if (!locked) _moveCtrl.SetLock(true);
        }

        private void PerformImpact()
        {
            // Визуал
            _animCtrl.SetWeaponVisible(false);
            _animCtrl.SnapToSlashImpact();

            // Логика (VFX + Урон)
            float dir = _ownerStats.transform.localScale.x > 0 ? 1 : -1;
            
            _vfx.Play(_ownerStats.transform, dir, _currentAoe, _currentAps);
            
            var targets = _hitbox.GetTargets(_ownerStats.transform.position, dir, _currentAoe);
            _damage.DealDamage(targets);
        }

        private IEnumerator PhaseRecovery()
        {
            float impactTimeSeconds = _currentDuration * _impactTime;
            float recoveryDuration = _currentDuration - impactTimeSeconds;
            
            float timer = 0f;
            bool unlocked = false;

            while (timer < recoveryDuration)
            {
                // Время относительно всего скилла
                float globalProgress = (impactTimeSeconds + timer) / _currentDuration;
                
                // Время относительно фазы возврата (0.0 -> 1.0)
                float phaseProgress = timer / recoveryDuration;

                // Логика разблокировки
                if (!unlocked && globalProgress >= _unlockTime)
                {
                    _moveCtrl.SetLock(false);
                    // Можно вернуть оружие визуально, если игрок побежал
                    // _animCtrl.SetWeaponVisible(true); // Опционально
                    unlocked = true;
                }

                // Анимация
                _animCtrl.LerpSlashRecovery(phaseProgress);

                timer += Time.deltaTime;
                yield return null;
            }
        }

        private void Cleanup()
        {
            _animCtrl.ForceReset();
            _moveCtrl.SetLock(false);
            _isCasting = false;
        }

        private void OnDisable()
        {
            Cleanup();
        }
    }
}