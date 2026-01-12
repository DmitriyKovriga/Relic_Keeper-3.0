using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills.Modules
{
    /// <summary>
    /// Модуль отвечает только за блокировку передвижения персонажа.
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
            {
                _playerMovement.SetMovementLock(isLocked);
            }
        }

        // Гарантируем разблокировку при отключении/прерывании скилла
        private void OnDisable()
        {
            SetLock(false);
        }
    }
}