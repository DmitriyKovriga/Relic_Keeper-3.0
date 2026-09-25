using UnityEngine;
using Scripts.Stats;
using Scripts.Skills;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// HandPivot windup / strike / recovery for weapon skills.
    /// Hold pose stays on WeaponHolder; this only moves HandPivot.
    /// Style is set once per cast via SetActiveStyle.
    /// </summary>
    public class SkillHandAnimation : MonoBehaviour
    {
        // Slash — mostly rotation; tiny windup lift so it is not pure angle-only
        private const float SlashWindupZ = 110f;
        private static readonly Vector2 SlashWindupPos = new Vector2(0.05f, 0.08f);
        private const float SlashImpactZ = -30f;
        private static readonly Vector2 SlashImpactPos = Vector2.zero;

        // LowArc (stance 2 / LowGuard, tip-up space):
        // Hold tip-back (~+100 WeaponHolder) stays on the pose table -- do not change hold pose.
        // Effective tip ~= holdLocalEulerZ + HandPivot deltaZ. Quaternion.Slerp takes the SHORT arc.
        // ImpactZ -150 is tip up-forward (effective ~-50) after hold +100.
        // WindupZ = 0: position-only pull behind+up; tip keeps LowGuard hold (no windup tilt).
        // Positions scaled ~0.7 vs prior pull (was too far left/back).
        private const float LowArcWindupZ = 0f;
        private static readonly Vector2 LowArcWindupPos = new Vector2(-0.50f, 0.315f);
        private const float LowArcImpactZ = -150f; // tip up-forward (effective ~-50) after hold +100
        private static readonly Vector2 LowArcImpactPos = new Vector2(0.35f, -0.056f);

        // OverheadStab (stance 3 / Dagger) — raise high, drive tip down
        private const float StabWindupZ = 45f;
        private static readonly Vector2 StabWindupPos = new Vector2(0.06f, 0.48f);
        private const float StabImpactZ = -125f;
        private static readonly Vector2 StabImpactPos = new Vector2(0.10f, -0.40f);

        private Transform _handPivot;
        private SpriteRenderer _weaponRenderer;

        private Vector3 _defaultPos = Vector3.zero;
        private Quaternion _defaultRot = Quaternion.identity;

        private WeaponSwingStyle _activeStyle = WeaponSwingStyle.Slash;

        private Quaternion _windupRot;
        private Vector3 _windupPos;
        private Quaternion _impactRot;
        private Vector3 _impactPos;

        public void Initialize(PlayerStats stats)
        {
            _handPivot = stats.transform.Find("Visuals/HandPivot");

            if (_handPivot != null)
            {
                _weaponRenderer = _handPivot.GetComponentInChildren<SpriteRenderer>();
                _defaultPos = _handPivot.localPosition;
                _defaultRot = _handPivot.localRotation;
            }
            else
            {
                Debug.LogError($"[SkillHandAnimation] HandPivot not found under {stats.name}!");
            }

            ApplyStyleKeyframes(WeaponSwingStyle.Slash);
        }

        /// <summary>
        /// Select swing keyframes for the upcoming cast. Call before windup.
        /// </summary>
        public void SetActiveStyle(WeaponSwingStyle style)
        {
            if (style == WeaponSwingStyle.FromStance)
                style = WeaponSwingStyle.Slash;
            _activeStyle = style;
            ApplyStyleKeyframes(style);
        }

        public WeaponSwingStyle ActiveStyle => _activeStyle;

        // --- Generic API ---

        /// <summary>Lerp HandPivot from idle toward windup pose. t: 0..1</summary>
        public void LerpWindup(float t)
        {
            if (_handPivot == null) return;
            t = Mathf.Clamp01(t);
            _handPivot.localRotation = Quaternion.Slerp(_defaultRot, _windupRot, t);
            _handPivot.localPosition = Vector3.Lerp(_defaultPos, _windupPos, t);
        }

        /// <summary>Snap HandPivot to impact pose. Does not hide weapon (callers do).</summary>
        public void SnapImpact()
        {
            if (_handPivot == null) return;
            _handPivot.localRotation = _impactRot;
            _handPivot.localPosition = _impactPos;
        }

        /// <summary>Lerp HandPivot from impact back to idle. t: 0..1</summary>
        public void LerpRecovery(float t)
        {
            if (_handPivot == null) return;
            t = Mathf.Clamp01(t);
            _handPivot.localRotation = Quaternion.Slerp(_impactRot, _defaultRot, t);
            _handPivot.localPosition = Vector3.Lerp(_impactPos, _defaultPos, t);
        }

        // --- Legacy slash wrappers (force Slash style) ---

        public void LerpSlashWindup(float t)
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            LerpWindup(t);
        }

        public void SnapToSlashImpact()
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            SnapImpact();
        }

        public void LerpSlashRecovery(float t)
        {
            SetActiveStyle(WeaponSwingStyle.Slash);
            LerpRecovery(t);
        }

        // --- Visibility / reset ---

        public void SetWeaponVisible(bool isVisible)
        {
            if (_weaponRenderer != null) _weaponRenderer.enabled = isVisible;
        }

        public void ForceReset()
        {
            if (_handPivot != null)
            {
                _handPivot.localRotation = _defaultRot;
                _handPivot.localPosition = _defaultPos;
            }
            SetWeaponVisible(true);
        }

        private void OnDisable()
        {
            ForceReset();
        }

        private void ApplyStyleKeyframes(WeaponSwingStyle style)
        {
            float windupZ;
            float impactZ;
            Vector2 windupOff;
            Vector2 impactOff;

            switch (style)
            {
                case WeaponSwingStyle.LowArc:
                    windupZ = LowArcWindupZ;
                    impactZ = LowArcImpactZ;
                    windupOff = LowArcWindupPos;
                    impactOff = LowArcImpactPos;
                    break;
                case WeaponSwingStyle.OverheadStab:
                    windupZ = StabWindupZ;
                    impactZ = StabImpactZ;
                    windupOff = StabWindupPos;
                    impactOff = StabImpactPos;
                    break;
                case WeaponSwingStyle.Slash:
                default:
                    windupZ = SlashWindupZ;
                    impactZ = SlashImpactZ;
                    windupOff = SlashWindupPos;
                    impactOff = SlashImpactPos;
                    break;
            }

            _windupRot = _defaultRot * Quaternion.Euler(0f, 0f, windupZ);
            _impactRot = _defaultRot * Quaternion.Euler(0f, 0f, impactZ);
            _windupPos = _defaultPos + new Vector3(windupOff.x, windupOff.y, 0f);
            _impactPos = _defaultPos + new Vector3(impactOff.x, impactOff.y, 0f);
        }
    }
}
