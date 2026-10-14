using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// Movement bridge used by skill steps.
    /// </summary>
    public class SkillMovementControl : MonoBehaviour
    {
        private PlayerMovement _playerMovement;
        private bool _charging;

        public void Charge(float speed)
        {
            if (_playerMovement == null) return;
            var velocity = new Vector2(speed, 0);
            if (!_charging)
            {
                _playerMovement.BeginMotionOverride(velocity, false, true);
                _charging = true;
            }
            else _playerMovement.UpdateMotionOverride(velocity);
        }

        public void EndCharge()
        {
            if (!_charging) return;
            _charging = false;
            if (_playerMovement != null)
                _playerMovement.EndMotionOverride(new Vector2(0, _playerMovement.CurrentVelocity.y));
        }

        public void Initialize(PlayerStats stats)
        {
            _playerMovement = stats.GetComponent<PlayerMovement>();
            if (_playerMovement == null)
            {
                Debug.LogWarning($"[SkillMovementControl] PlayerMovement not found on {stats.name}");
            }
        }

        public void SetLock(bool isLocked)
        {
            if (_playerMovement != null)
                _playerMovement.SetMovementLock(isLocked);
        }

        public void ApplyImpulse(float angleDegrees, float force, bool relativeToFacing, bool clearCurrentVelocity)
        {
            if (_playerMovement != null)
                _playerMovement.ApplySkillImpulse(angleDegrees, force, relativeToFacing, clearCurrentVelocity);
        }

        private void OnDisable()
        {
            EndCharge();
            SetLock(false);
        }
    }
}
