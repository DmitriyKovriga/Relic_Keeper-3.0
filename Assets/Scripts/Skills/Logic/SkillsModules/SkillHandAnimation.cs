using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// Модуль отвечает за процедурную анимацию руки и скрытие оружия во время VFX.
    /// </summary>
    public class SkillHandAnimation : MonoBehaviour
    {
        // Хардкод углов для стандартизации всех рубящих ударов
        private const float SLASH_START_ANGLE = 110f; // Замах назад
        private const float SLASH_END_ANGLE = -30f;   // Удар вперед

        private Transform _handPivot;
        private SpriteRenderer _weaponRenderer;
        
        // Кэшированные кватернионы
        private Quaternion _rotStart;
        private Quaternion _rotEnd;
        private Quaternion _rotDefault = Quaternion.identity;

        private void Awake()
        {
            _rotStart = Quaternion.Euler(0, 0, SLASH_START_ANGLE);
            _rotEnd = Quaternion.Euler(0, 0, SLASH_END_ANGLE);
        }

        public void Initialize(PlayerStats stats)
        {
            // Ищем структуру визуализации игрока
            _handPivot = stats.transform.Find("Visuals/HandPivot");
            
            if (_handPivot != null)
            {
                _weaponRenderer = _handPivot.GetComponentInChildren<SpriteRenderer>();
            }
            else
            {
                Debug.LogError($"[SkillHandAnimation] HandPivot не найден в иерархии {stats.name}!");
            }
        }

        // --- API АНИМАЦИИ ---

        /// <summary>
        /// Плавный переход от 0 (покой) к Замаху.
        /// </summary>
        /// <param name="t">0.0 -> 1.0</param>
        public void LerpSlashWindup(float t)
        {
            if (_handPivot == null) return;
            _handPivot.localRotation = Quaternion.Slerp(_rotDefault, _rotStart, t);
        }

        /// <summary>
        /// Мгновенная установка руки в конечную точку удара (для кадра Impact).
        /// </summary>
        public void SnapToSlashImpact()
        {
             if (_handPivot == null) return;
             _handPivot.localRotation = _rotEnd;
        }

        /// <summary>
        /// Плавный возврат руки в исходное положение.
        /// </summary>
        /// <param name="t">0.0 -> 1.0</param>
        public void LerpSlashRecovery(float t)
        {
            if (_handPivot == null) return;
            _handPivot.localRotation = Quaternion.Slerp(_rotEnd, _rotDefault, t);
        }

        // --- УПРАВЛЕНИЕ ВИДИМОСТЬЮ ---

        public void SetWeaponVisible(bool isVisible)
        {
            if (_weaponRenderer != null) _weaponRenderer.enabled = isVisible;
        }

        public void ForceReset()
        {
            if (_handPivot != null) _handPivot.localRotation = _rotDefault;
            SetWeaponVisible(true);
        }

        private void OnDisable()
        {
            ForceReset();
        }
    }
}