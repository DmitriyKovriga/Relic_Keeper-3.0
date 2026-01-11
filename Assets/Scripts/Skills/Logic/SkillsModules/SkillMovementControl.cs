using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// Отвечает за блокировку и разблокировку передвижения игрока во время скилла.
    /// </summary>
    public class SkillMovementControl : MonoBehaviour
    {
        private PlayerMovement _playerMovement;

        public void Initialize(PlayerStats stats)
        {
            _playerMovement = stats.GetComponent<PlayerMovement>();
        }

        public void SetLock(bool isLocked)
        {
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementLock(isLocked);
            }
        }

        // Страховка на случай прерывания скилла
        private void OnDisable()
        {
            SetLock(false);
        }
    }
}