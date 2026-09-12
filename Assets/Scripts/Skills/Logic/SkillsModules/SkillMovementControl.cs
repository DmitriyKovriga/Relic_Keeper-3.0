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
            SetLock(false);
        }
    }
}
