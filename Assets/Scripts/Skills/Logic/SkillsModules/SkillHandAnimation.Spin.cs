using UnityEngine;

namespace Scripts.Skills.Modules
{
    public partial class SkillHandAnimation
    {
        private object _spinToken;
        private bool _spinWeaponEnabled;

        // The sweep VFX represents the weapon during the turn. Keep the equipped
        // sprite hidden instead of stretching it to match the Area of Effect.
        public void SampleSpin(object token, float phase, float radius, float squash, float centerHeight, float turnProgress = -1)
        {
            if (token == null || _weaponRenderer == null || _ownerStats == null || _anchor == null) return;
            if (!ReferenceEquals(_spinToken, token))
            {
                EndSpin(_spinToken);
                _anchor.Acquire(this);
                _spinToken = token;
                _spinWeaponEnabled = _weaponRenderer.enabled;
            }
            _weaponRenderer.enabled = false;
        }

        public void EndSpin(object token)
        {
            if (token == null || !ReferenceEquals(_spinToken, token)) return;
            if (_anchor != null && _anchor.IsOwnedBy(this))
            {
                if (_weaponRenderer != null) _weaponRenderer.enabled = _spinWeaponEnabled;
                if (_handPivot != null)
                {
                    _handPivot.localPosition = _defaultPos;
                    _handPivot.localRotation = _defaultRot;
                }
            }
            _spinToken = null;
        }
    }
}
