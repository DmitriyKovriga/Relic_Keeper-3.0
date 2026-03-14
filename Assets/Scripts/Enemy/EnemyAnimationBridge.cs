using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyAnimationBridge : MonoBehaviour
    {
        private EnemyDataSO _data;
        private Animator _animator;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private string _currentState;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _data = data;
            _animator = GetComponent<Animator>();
            if (_animator == null && _data != null && _data.Animation != null && _data.Animation.Controller != null)
            {
                _animator = gameObject.AddComponent<Animator>();
                _animator.runtimeAnimatorController = _data.Animation.Controller;
            }
            _locomotion = GetComponent<EnemyLocomotion2D>();
            _attack = GetComponent<EnemyAttackController>();
            _currentState = string.Empty;

            if (_animator != null && _data != null && _data.Animation != null && _data.Animation.Controller != null)
            {
                _animator.runtimeAnimatorController = _data.Animation.Controller;
                PlayState(_data.Animation.IdleStateName, true);
            }
        }

        private void Update()
        {
            if (_animator == null || _data == null || _data.Animation == null || _data.Animation.Controller == null)
                return;

            if (_attack != null && _attack.IsBusy)
                return;

            bool isMoving = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
            PlayState(isMoving ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, false);
        }

        public void PlayAttack()
        {
            if (_animator == null || _data == null || _data.Animation == null)
                return;

            PlayState(_data.Animation.AttackStateName, true);
        }

        private void PlayState(string stateName, bool restart)
        {
            if (string.IsNullOrEmpty(stateName) || _animator == null)
                return;

            if (!restart && _currentState == stateName)
                return;

            _currentState = stateName;
            _animator.Play(stateName, 0, restart ? 0f : 0f);
        }
    }
}
