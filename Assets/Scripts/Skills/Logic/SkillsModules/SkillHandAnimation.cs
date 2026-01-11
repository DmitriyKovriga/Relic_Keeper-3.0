using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// Процедурная анимация руки (HandPivot) и скрытие оружия.
    /// </summary>
    public class SkillHandAnimation : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Угол максимального замаха (назад)")]
        [SerializeField] private float _startAngle = 110f; 
        
        [Tooltip("Угол конечного удара (вперед)")]
        [SerializeField] private float _endAngle = -30f;   

        private Transform _handPivot;
        private SpriteRenderer _weaponRenderer;
        
        private Quaternion _rotStart;
        private Quaternion _rotEnd;
        private Quaternion _rotDefault = Quaternion.identity;

        private void Awake()
        {
            _rotStart = Quaternion.Euler(0, 0, _startAngle);
            _rotEnd = Quaternion.Euler(0, 0, _endAngle);
        }

        public void Initialize(PlayerStats stats)
        {
            _handPivot = stats.transform.Find("Visuals/HandPivot");
            if (_handPivot != null)
            {
                _weaponRenderer = _handPivot.GetComponentInChildren<SpriteRenderer>();
            }
            else
            {
                Debug.LogError($"[SkillHandAnimation] HandPivot not found on {stats.name}");
            }
        }

        // --- НОВЫЙ МЕТОД: ЗАМАХ ---
        /// <summary>
        /// Поднимает руку из обычного положения в позицию замаха.
        /// </summary>
        public void LerpWindup(float t)
        {
            if (_handPivot == null) return;
            // От 0 (Default) к 110 (Start)
            _handPivot.localRotation = Quaternion.Slerp(_rotDefault, _rotStart, t);
        }

        // --- МЕТОД УДАРА ---
        /// <summary>
        /// Мгновенно ставит руку в конечную точку удара (для кадра Impact).
        /// </summary>
        public void SnapToImpact()
        {
             if (_handPivot == null) return;
             _handPivot.localRotation = _rotEnd;
        }

        /// <summary>
        /// Возвращает руку в исходное (мирное) положение.
        /// </summary>
        public void LerpRecovery(float t)
        {
            if (_handPivot == null) return;
            // От -30 (End) к 0 (Default)
            _handPivot.localRotation = Quaternion.Slerp(_rotEnd, _rotDefault, t);
        }

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