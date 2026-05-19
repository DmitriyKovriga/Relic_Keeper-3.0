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

        private SkillHitbox _hitbox;
        private SkillDamageDealer _damage;
        private SkillVFX _vfx;
        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;

        private float _currentDuration;
        private float _currentAoe;
        private float _currentAps;
        private Coroutine _pipelineCoroutine;
        private bool _cancelRequested;

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

        public override void Cancel()
        {
            _cancelRequested = true;
            if (_pipelineCoroutine != null)
            {
                StopCoroutine(_pipelineCoroutine);
                _pipelineCoroutine = null;
            }

            Cleanup();
        }

        protected override void Execute()
        {
            _cancelRequested = false;
            if (_pipelineCoroutine != null)
                StopCoroutine(_pipelineCoroutine);

            _pipelineCoroutine = StartCoroutine(SkillPipeline());
        }

        private IEnumerator SkillPipeline()
        {
            _isCasting = true;

            try
            {
                CalculateSkillStats();
                yield return StartCoroutine(PhaseWindup());
                if (_cancelRequested)
                    yield break;

                PerformImpact();
                if (_cancelRequested)
                    yield break;

                yield return StartCoroutine(PhaseRecovery());
            }
            finally
            {
                Cleanup();
            }
        }

        private void CalculateSkillStats()
        {
            _currentAps = ResolveActionSpeed();

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
                if (_cancelRequested)
                    yield break;

                float globalProgress = timer / _currentDuration;
                float phaseProgress = windupDuration > 0f ? timer / windupDuration : 1f;

                if (!locked && globalProgress >= _lockTime)
                {
                    _moveCtrl.SetLock(true);
                    locked = true;
                }

                _animCtrl.LerpSlashWindup(phaseProgress);
                timer += Time.deltaTime;
                yield return null;
            }

            if (!locked)
                _moveCtrl.SetLock(true);
        }

        private void PerformImpact()
        {
            _animCtrl.SetWeaponVisible(false);
            _animCtrl.SnapToSlashImpact();

            float dir = _ownerStats.transform.localScale.x > 0f ? 1f : -1f;
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
                if (_cancelRequested)
                    yield break;

                float globalProgress = (impactTimeSeconds + timer) / _currentDuration;
                float phaseProgress = recoveryDuration > 0f ? timer / recoveryDuration : 1f;

                if (!unlocked && globalProgress >= _unlockTime)
                {
                    _moveCtrl.SetLock(false);
                    unlocked = true;
                }

                _animCtrl.LerpSlashRecovery(phaseProgress);
                timer += Time.deltaTime;
                yield return null;
            }
        }

        private void Cleanup()
        {
            _cancelRequested = false;
            _pipelineCoroutine = null;
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
