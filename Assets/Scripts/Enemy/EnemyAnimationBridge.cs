using System;
using System.Linq;
using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyAnimationBridge : MonoBehaviour
    {
        private enum AnimationMode
        {
            AnimatorController,
            SpriteSheets
        }

        private EnemyDataSO _data;
        private EnemyEntity _entity;
        private Animator _animator;
        private SpriteRenderer _spriteRenderer;
        private Transform _visualRoot;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private AnimationMode _mode;
        private string _currentState;
        private Sprite[] _idleFrames = Array.Empty<Sprite>();
        private Sprite[] _moveFrames = Array.Empty<Sprite>();
        private Sprite[] _attackFrames = Array.Empty<Sprite>();
        private float _stateTime;
        private int _currentFrameIndex = -1;
        private float _baselineBottom;
        private Vector3 _baseLocalPosition;
        private bool _attackImpactSent;
        private bool _attackImpactQueued;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            _spriteRenderer = entity != null ? entity.VisualRenderer : GetComponentInChildren<SpriteRenderer>(true);
            _visualRoot = entity != null ? entity.VisualRoot : (_spriteRenderer != null ? _spriteRenderer.transform : transform);
            _baseLocalPosition = _visualRoot != null ? _visualRoot.localPosition : Vector3.zero;

            bool useSpriteSheets = _data != null && _data.Animation != null && _data.Animation.UsesSpriteSheets;
            _mode = useSpriteSheets ? AnimationMode.SpriteSheets : AnimationMode.AnimatorController;

            _animator = GetComponent<Animator>();
            if (_mode == AnimationMode.AnimatorController && _animator == null && _data != null && _data.Animation != null && _data.Animation.Controller != null)
            {
                _animator = gameObject.AddComponent<Animator>();
                _animator.runtimeAnimatorController = _data.Animation.Controller;
            }

            _locomotion = GetComponent<EnemyLocomotion2D>();
            _attack = GetComponent<EnemyAttackController>();
            _currentState = string.Empty;
            _stateTime = 0f;
            _currentFrameIndex = -1;
            _attackImpactSent = false;
            _attackImpactQueued = false;

            if (_mode == AnimationMode.SpriteSheets)
            {
                LoadSpriteSheets();
                var baselineSprite = _idleFrames.FirstOrDefault() ?? _moveFrames.FirstOrDefault() ?? _attackFrames.FirstOrDefault();
                _baselineBottom = baselineSprite != null ? baselineSprite.bounds.min.y : 0f;
                PlayState(_data.Animation.IdleStateName, true);
                return;
            }

            if (_animator != null && _data != null && _data.Animation != null && _data.Animation.Controller != null)
            {
                _animator.runtimeAnimatorController = _data.Animation.Controller;
                PlayState(_data.Animation.IdleStateName, true);
            }
        }

        private void Update()
        {
            if (_mode == AnimationMode.SpriteSheets)
            {
                UpdateSpriteSheetAnimation();
                return;
            }

            if (_animator == null || _data == null || _data.Animation == null || _data.Animation.Controller == null)
                return;

            if (_attack != null && _attack.IsBusy)
                return;

            bool isMoving = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
            PlayState(isMoving ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, false);
        }

        public void PlayAttack()
        {
            if (_data == null || _data.Animation == null)
                return;

            PlayState(_data.Animation.AttackStateName, true);
        }

        public bool ConsumeAttackImpactSignal()
        {
            bool queued = _attackImpactQueued;
            _attackImpactQueued = false;
            return queued;
        }

        private void PlayState(string stateName, bool restart)
        {
            if (string.IsNullOrEmpty(stateName))
                return;

            if (_mode == AnimationMode.SpriteSheets)
            {
                if (!restart && _currentState == stateName)
                    return;

                _currentState = stateName;
                _stateTime = 0f;
                _currentFrameIndex = -1;
                _attackImpactSent = false;
                ApplySpriteFrame(force: true);
                return;
            }

            if (_animator == null)
                return;

            if (!restart && _currentState == stateName)
                return;

            _currentState = stateName;
            _animator.Play(stateName, 0, 0f);
        }

        private void UpdateSpriteSheetAnimation()
        {
            if (_data == null || _data.Animation == null || _spriteRenderer == null)
                return;

            if (_attack != null && _attack.IsBusy)
            {
                if (_currentState != _data.Animation.AttackStateName)
                    PlayState(_data.Animation.AttackStateName, true);
            }
            else
            {
                bool isMoving = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
                PlayState(isMoving ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, false);
            }

            _stateTime += Time.deltaTime;
            ApplySpriteFrame(force: false);
        }

        private void ApplySpriteFrame(bool force)
        {
            if (_spriteRenderer == null)
                return;

            Sprite[] frames = GetFramesForState(_currentState);
            if (frames == null || frames.Length == 0)
                return;

            float fps = GetFpsForState(_currentState);
            bool loop = _currentState != _data.Animation.AttackStateName;
            int frameIndex = loop
                ? Mathf.Abs(Mathf.FloorToInt(_stateTime * fps)) % frames.Length
                : Mathf.Clamp(Mathf.FloorToInt(_stateTime * fps), 0, frames.Length - 1);

            if (!force && frameIndex == _currentFrameIndex)
                return;

            _currentFrameIndex = frameIndex;
            Sprite sprite = frames[frameIndex];
            _spriteRenderer.sprite = sprite;

            if (_visualRoot != null)
            {
                float currentBottom = sprite != null ? sprite.bounds.min.y : _baselineBottom;
                _visualRoot.localPosition = _baseLocalPosition + new Vector3(0f, _baselineBottom - currentBottom, 0f);
            }

            if (_currentState == _data.Animation.AttackStateName)
            {
                int impactFrame = _data.Animation.AttackImpactFrame < 0 ? frames.Length - 1 : Mathf.Clamp(_data.Animation.AttackImpactFrame, 0, frames.Length - 1);
                if (!_attackImpactSent && frameIndex >= impactFrame)
                {
                    _attackImpactSent = true;
                    _attackImpactQueued = true;
                }
            }
        }

        private Sprite[] GetFramesForState(string stateName)
        {
            if (_data == null || _data.Animation == null)
                return Array.Empty<Sprite>();

            if (stateName == _data.Animation.MoveStateName)
                return _moveFrames.Length > 0 ? _moveFrames : _idleFrames;
            if (stateName == _data.Animation.AttackStateName)
                return _attackFrames.Length > 0 ? _attackFrames : _idleFrames;
            return _idleFrames;
        }

        private float GetFpsForState(string stateName)
        {
            if (_data == null || _data.Animation == null)
                return 8f;

            if (stateName == _data.Animation.MoveStateName)
                return Mathf.Max(1f, _data.Animation.MoveFps);
            if (stateName == _data.Animation.AttackStateName)
                return Mathf.Max(1f, _data.Animation.AttackFps);
            return Mathf.Max(1f, _data.Animation.IdleFps);
        }

        private void LoadSpriteSheets()
        {
            _idleFrames = LoadFrames(_data.Animation.IdleSpritesResourcePath);
            _moveFrames = LoadFrames(_data.Animation.MoveSpritesResourcePath);
            _attackFrames = LoadFrames(_data.Animation.AttackSpritesResourcePath);
        }

        private static Sprite[] LoadFrames(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return Array.Empty<Sprite>();

            return Resources.LoadAll<Sprite>(resourcePath)
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
