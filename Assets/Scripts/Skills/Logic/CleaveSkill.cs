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

        // РњРѕРґСѓР»Рё
        private SkillHitbox _hitbox;
        private SkillDamageDealer _damage;
        private SkillVFX _vfx;
        private SkillMovementControl _moveCtrl;
        private SkillHandAnimation _animCtrl;

        // РљСЌС€РёСЂРѕРІР°РЅРЅС‹Рµ РґР°РЅРЅС‹Рµ РґР»СЏ С‚РµРєСѓС‰РµРіРѕ РєР°СЃС‚Р°
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

        // --- Р“Р›РђР’РќР«Р™ РћР РљР•РЎРўР РђРўРћР  (РўРµРїРµСЂСЊ РѕРЅ С‡РёСЃС‚С‹Р№) ---
        private IEnumerator SkillPipeline()
        {
            _isCasting = true;

            try
            {
                CalculateSkillStats();
                yield return StartCoroutine(PhaseWindup());
                PerformImpact();
                yield return StartCoroutine(PhaseRecovery());
            }
            finally
            {
                Cleanup();
            }
        }

        // --- Р’РЎРџРћРњРћР“РђРўР•Р›Р¬РќР«Р• РњР•РўРћР”Р« Р¤РђР— ---

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
                // РќРѕСЂРјР°Р»РёР·РѕРІР°РЅРЅРѕРµ РІСЂРµРјСЏ РћРўРќРћРЎРРўР•Р›Р¬РќРћ Р’РЎР•Р“Рћ РЎРљРР›Р›Рђ (0.0 -> ImpactTime)
                float globalProgress = timer / _currentDuration;
                
                // РќРѕСЂРјР°Р»РёР·РѕРІР°РЅРЅРѕРµ РІСЂРµРјСЏ РћРўРќРћРЎРРўР•Р›Р¬РќРћ Р¤РђР—Р« Р—РђРњРђРҐРђ (0.0 -> 1.0)
                // РќСѓР¶РЅРѕ РґР»СЏ Lerp Р°РЅРёРјР°С†РёРё
                float phaseProgress = timer / windupDuration;

                // Р›РѕРіРёРєР° Р±Р»РѕРєРёСЂРѕРІРєРё
                if (!locked && globalProgress >= _lockTime)
                {
                    _moveCtrl.SetLock(true);
                    locked = true;
                }

                // РђРЅРёРјР°С†РёСЏ
                _animCtrl.LerpSlashWindup(phaseProgress);

                timer += Time.deltaTime;
                yield return null;
            }
            
            // Р“Р°СЂР°РЅС‚РёСЂСѓРµРј, С‡С‚Рѕ Р±Р»РѕРєРёСЂРѕРІРєР° СЃСЂР°Р±РѕС‚Р°Р»Р°, РґР°Р¶Рµ РµСЃР»Рё Р»Р°РіР°РЅСѓР»Рѕ
            if (!locked) _moveCtrl.SetLock(true);
        }

        private void PerformImpact()
        {
            // Р’РёР·СѓР°Р»
            _animCtrl.SetWeaponVisible(false);
            _animCtrl.SnapToSlashImpact();

            // Р›РѕРіРёРєР° (VFX + РЈСЂРѕРЅ)
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
                // Р’СЂРµРјСЏ РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ РІСЃРµРіРѕ СЃРєРёР»Р»Р°
                float globalProgress = (impactTimeSeconds + timer) / _currentDuration;
                
                // Р’СЂРµРјСЏ РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ С„Р°Р·С‹ РІРѕР·РІСЂР°С‚Р° (0.0 -> 1.0)
                float phaseProgress = timer / recoveryDuration;

                // Р›РѕРіРёРєР° СЂР°Р·Р±Р»РѕРєРёСЂРѕРІРєРё
                if (!unlocked && globalProgress >= _unlockTime)
                {
                    _moveCtrl.SetLock(false);
                    // РњРѕР¶РЅРѕ РІРµСЂРЅСѓС‚СЊ РѕСЂСѓР¶РёРµ РІРёР·СѓР°Р»СЊРЅРѕ, РµСЃР»Рё РёРіСЂРѕРє РїРѕР±РµР¶Р°Р»
                    // _animCtrl.SetWeaponVisible(true); // РћРїС†РёРѕРЅР°Р»СЊРЅРѕ
                    unlocked = true;
                }

                // РђРЅРёРјР°С†РёСЏ
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
