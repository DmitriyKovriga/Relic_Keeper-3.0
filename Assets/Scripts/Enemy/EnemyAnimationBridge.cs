using System;
using System.Linq;
using System.Text.RegularExpressions;
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
        private Sprite[] _chargeFrames = Array.Empty<Sprite>();
        private Sprite[] _hitFrames = Array.Empty<Sprite>();
        private Sprite[] _digInFrames = Array.Empty<Sprite>();
        private Sprite[] _digOutFrames = Array.Empty<Sprite>();
        private float _stateTime;
        private int _currentFrameIndex = -1;
        private float _baselineBottom;
        private Vector3 _baseLocalPosition;
        private bool _attackImpactSent;
        private bool _attackImpactQueued;
        private bool _isTransientState;
        private bool _isVisualHidden;
        private bool _holdTransientLastFrame;
        private bool _digInCompleted;
        private bool _holdChargeImpactFrame;
        private bool _isFrozen;
        private bool _hasCachedAnimatorSpeed;
        private float _cachedAnimatorSpeed = 1f;
        private static readonly Regex TrailingNumberRegex = new Regex(@"_(\d+)$", RegexOptions.Compiled);

        public bool IsTransientStateActive => _isTransientState;
        public bool SupportsHitReaction
        {
            get
            {
                if (_data == null || _data.Animation == null)
                    return false;

                string stateName = _data.Animation.HitStateName;
                return GetFramesForState(stateName).Length > 0 || HasAnimatorState(stateName);
            }
        }

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _entity = entity;
            _data = data;
            _spriteRenderer = entity != null ? entity.VisualRenderer : GetComponentInChildren<SpriteRenderer>(true);
            _visualRoot = entity != null ? entity.VisualRoot : (_spriteRenderer != null ? _spriteRenderer.transform : transform);
            _baseLocalPosition = _visualRoot != null ? _visualRoot.localPosition : Vector3.zero;
            if (_data != null && _data.Animation != null)
                _baseLocalPosition += (Vector3)_data.Animation.VisualLocalOffset;

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
            _isTransientState = false;
            _isVisualHidden = false;
            _holdTransientLastFrame = false;
            _digInCompleted = false;
            _holdChargeImpactFrame = false;
            _isFrozen = false;
            _hasCachedAnimatorSpeed = false;
            _cachedAnimatorSpeed = 1f;
            if (_animator != null)
                _animator.speed = 1f;

            if (_mode == AnimationMode.SpriteSheets)
            {
                LoadSpriteSheets();
                var baselineSprite = _idleFrames.FirstOrDefault() ?? _moveFrames.FirstOrDefault() ?? _attackFrames.FirstOrDefault() ?? _chargeFrames.FirstOrDefault() ?? _hitFrames.FirstOrDefault() ?? _digOutFrames.FirstOrDefault();
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
            if (_isFrozen)
                return;

            if (_mode == AnimationMode.SpriteSheets)
            {
                UpdateSpriteSheetAnimation();
                return;
            }

            if (_animator == null || _data == null || _data.Animation == null || _data.Animation.Controller == null)
                return;

            if (_isTransientState)
            {
                AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime < 1f)
                    return;

                _isTransientState = false;
            }

            if (_attack != null && _attack.IsPlayingAttackAnimation)
            {
                PlayState(string.IsNullOrWhiteSpace(_attack.CurrentAttackAnimationStateName) ? _data.Animation.AttackStateName : _attack.CurrentAttackAnimationStateName, false);
                return;
            }

            bool isMoving = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
            PlayState(isMoving ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, false);
        }

        public void PlayAttack()
        {
            if (_data == null || _data.Animation == null)
                return;

            _isTransientState = false;
            _holdTransientLastFrame = false;
            _digInCompleted = false;
            PlayState(_data.Animation.AttackStateName, true);
        }

        public void PlayChargeAttack()
        {
            if (_data == null || _data.Animation == null)
                return;

            _isTransientState = false;
            _holdTransientLastFrame = false;
            _digInCompleted = false;
            PlayState(_data.Animation.ChargeStateName, true);
        }

        public bool TryPlayHitReaction()
        {
            if (_data == null || _data.Animation == null || _isTransientState)
                return false;

            if (_attack != null && _attack.IsBusy)
                return false;

            if (_mode == AnimationMode.SpriteSheets)
            {
                if (_hitFrames.Length == 0)
                    return false;

                PlayTransient(_data.Animation.HitStateName, holdLastFrame: false);
                return true;
            }

            if (!HasAnimatorState(_data.Animation.HitStateName))
                return false;

            PlayTransient(_data.Animation.HitStateName, holdLastFrame: false);
            return true;
        }

        public float PlayDigIn()
        {
            _digInCompleted = false;
            return PlayTransient(_data != null ? _data.Animation.DigInStateName : null, holdLastFrame: true);
        }

        public float PlayDigOut()
        {
            return PlayTransient(_data != null ? _data.Animation.DigOutStateName : null, holdLastFrame: false);
        }

        public void ReleaseTransientHold()
        {
            _holdTransientLastFrame = false;
        }

        public bool ConsumeDigInCompletedSignal()
        {
            bool completed = _digInCompleted;
            _digInCompleted = false;
            return completed;
        }

        public void SetChargeImpactFrameHold(bool hold)
        {
            _holdChargeImpactFrame = hold;
        }

        public void SetVisualHidden(bool hidden)
        {
            _isVisualHidden = hidden;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = !hidden;
        }

        public void SetFrozen(bool frozen)
        {
            if (_isFrozen == frozen)
                return;

            _isFrozen = frozen;
            if (_mode == AnimationMode.AnimatorController && _animator != null)
            {
                if (frozen)
                {
                    if (!_hasCachedAnimatorSpeed)
                    {
                        _cachedAnimatorSpeed = _animator.speed;
                        _hasCachedAnimatorSpeed = true;
                    }

                    _animator.speed = 0f;
                }
                else
                {
                    _animator.speed = _hasCachedAnimatorSpeed ? _cachedAnimatorSpeed : 1f;
                    _hasCachedAnimatorSpeed = false;
                }
            }

            if (frozen || _data == null || _data.Animation == null)
                return;

            _isTransientState = false;
            _holdTransientLastFrame = false;
            _holdChargeImpactFrame = false;
            _attackImpactSent = false;
            _attackImpactQueued = false;
            PlayState(_data.Animation.IdleStateName, true);
        }

        public float GetTransientDuration(string stateName)
        {
            if (_data == null || _data.Animation == null || string.IsNullOrWhiteSpace(stateName))
                return 0f;

            if (_mode == AnimationMode.SpriteSheets)
            {
                Sprite[] frames = GetFramesForState(stateName);
                if (frames == null || frames.Length == 0)
                    return 0f;

                return frames.Length / Mathf.Max(1f, GetFpsForState(stateName));
            }

            if (_animator == null || _animator.runtimeAnimatorController == null)
                return 0f;

            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && string.Equals(clip.name, stateName, StringComparison.Ordinal))
                    return clip.length;
            }

            return 0f;
        }

        public float GetCurrentStateRemainingDuration()
        {
            if (_data == null || _data.Animation == null || string.IsNullOrWhiteSpace(_currentState))
                return 0f;

            if (_mode == AnimationMode.SpriteSheets)
            {
                Sprite[] frames = GetFramesForState(_currentState);
                if (frames == null || frames.Length == 0)
                    return 0f;

                float totalDuration = frames.Length / Mathf.Max(1f, GetFpsForState(_currentState));
                return Mathf.Max(0f, totalDuration - _stateTime);
            }

            if (_animator == null)
                return 0f;

            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            float normalizedTime = Mathf.Clamp01(stateInfo.normalizedTime);
            return Mathf.Max(0f, stateInfo.length * (1f - normalizedTime));
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

            if (_isVisualHidden)
                return;

            if (_isTransientState)
            {
                _stateTime += Time.deltaTime;
                ApplySpriteFrame(force: false);
                if (HasSpriteSheetStateCompleted())
                {
                    if (_currentState == _data.Animation.DigInStateName)
                        _digInCompleted = true;

                    if (_holdTransientLastFrame)
                        return;

                    _isTransientState = false;
                    bool isMovingAfterTransient = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
                    PlayState(isMovingAfterTransient ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, true);
                }
                return;
            }

            bool pauseChargeAtImpactFrame = ShouldPauseChargeAtImpactFrame();

            if (_attack != null && _attack.IsPlayingAttackAnimation)
            {
                string desiredAttackState = string.IsNullOrWhiteSpace(_attack.CurrentAttackAnimationStateName)
                    ? _data.Animation.AttackStateName
                    : _attack.CurrentAttackAnimationStateName;

                if (_currentState != desiredAttackState)
                    PlayState(desiredAttackState, true);
            }
            else
            {
                bool isMoving = _locomotion != null && Mathf.Abs(_locomotion.CurrentHorizontalSpeed) > 0.05f;
                PlayState(isMoving ? _data.Animation.MoveStateName : _data.Animation.IdleStateName, false);
            }

            if (!pauseChargeAtImpactFrame)
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
            bool loop = IsLoopingState(_currentState);
            int frameIndex = loop
                ? Mathf.Abs(Mathf.FloorToInt(_stateTime * fps)) % frames.Length
                : Mathf.Clamp(Mathf.FloorToInt(_stateTime * fps), 0, frames.Length - 1);

            if (_currentState == _data.Animation.ChargeStateName && _holdChargeImpactFrame)
            {
                int holdFrame = _data.Animation.ChargeImpactFrame < 0 ? frames.Length - 1 : Mathf.Clamp(_data.Animation.ChargeImpactFrame, 0, frames.Length - 1);
                frameIndex = holdFrame;
            }

            if (!force && frameIndex == _currentFrameIndex)
                return;

            _currentFrameIndex = frameIndex;
            Sprite sprite = frames[frameIndex];
            _spriteRenderer.sprite = sprite;
            _spriteRenderer.enabled = !_isVisualHidden;

            if (_visualRoot != null)
            {
                float currentBottom = sprite != null ? sprite.bounds.min.y : _baselineBottom;
                _visualRoot.localPosition = _baseLocalPosition + new Vector3(0f, _baselineBottom - currentBottom, 0f);
            }

            if (_currentState == _data.Animation.AttackStateName || _currentState == _data.Animation.ChargeStateName)
            {
                int configuredImpactFrame = _currentState == _data.Animation.ChargeStateName
                    ? _data.Animation.ChargeImpactFrame
                    : _data.Animation.AttackImpactFrame;

                int impactFrame = configuredImpactFrame < 0 ? frames.Length - 1 : Mathf.Clamp(configuredImpactFrame, 0, frames.Length - 1);
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
            if (stateName == _data.Animation.ChargeStateName)
                return _chargeFrames.Length > 0 ? _chargeFrames : (_attackFrames.Length > 0 ? _attackFrames : _idleFrames);
            if (stateName == _data.Animation.HitStateName)
                return _hitFrames.Length > 0 ? _hitFrames : _idleFrames;
            if (stateName == _data.Animation.DigInStateName)
                return _digInFrames.Length > 0 ? _digInFrames : _idleFrames;
            if (stateName == _data.Animation.DigOutStateName)
                return _digOutFrames.Length > 0 ? _digOutFrames : _idleFrames;
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
            if (stateName == _data.Animation.ChargeStateName)
                return Mathf.Max(1f, _data.Animation.ChargeFps);
            if (stateName == _data.Animation.HitStateName)
                return Mathf.Max(1f, _data.Animation.HitFps);
            if (stateName == _data.Animation.DigInStateName)
                return Mathf.Max(1f, _data.Animation.DigInFps);
            if (stateName == _data.Animation.DigOutStateName)
                return Mathf.Max(1f, _data.Animation.DigOutFps);
            return Mathf.Max(1f, _data.Animation.IdleFps);
        }

        private void LoadSpriteSheets()
        {
            _idleFrames = LoadFrames(_data.Animation.IdleSpritesResourcePath);
            _moveFrames = LoadFrames(_data.Animation.MoveSpritesResourcePath);
            _attackFrames = LoadFrames(_data.Animation.AttackSpritesResourcePath);
            _chargeFrames = LoadFrames(_data.Animation.ChargeSpritesResourcePath);
            _hitFrames = LoadFrames(_data.Animation.HitSpritesResourcePath);
            _digInFrames = LoadFrames(_data.Animation.DigInSpritesResourcePath);
            _digOutFrames = LoadFrames(_data.Animation.DigOutSpritesResourcePath);
        }

        private float PlayTransient(string stateName, bool holdLastFrame)
        {
            if (_data == null || _data.Animation == null || string.IsNullOrWhiteSpace(stateName))
                return 0f;

            float duration = GetTransientDuration(stateName);
            if (duration <= 0f)
                return 0f;

            _isTransientState = true;
            _holdTransientLastFrame = holdLastFrame;
            PlayState(stateName, true);
            return duration;
        }

        private bool HasSpriteSheetStateCompleted()
        {
            Sprite[] frames = GetFramesForState(_currentState);
            if (frames == null || frames.Length == 0)
                return true;

            float fps = GetFpsForState(_currentState);
            return _stateTime >= Mathf.Max(0.01f, frames.Length / Mathf.Max(1f, fps));
        }

        private bool ShouldPauseChargeAtImpactFrame()
        {
            if (!_holdChargeImpactFrame || _data == null || _data.Animation == null)
                return false;

            return _currentState == _data.Animation.ChargeStateName;
        }

        private bool IsLoopingState(string stateName)
        {
            if (_data == null || _data.Animation == null || string.IsNullOrWhiteSpace(stateName))
                return true;

            return stateName == _data.Animation.IdleStateName || stateName == _data.Animation.MoveStateName;
        }

        private bool HasAnimatorState(string stateName)
        {
            if (_animator == null || _animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(stateName))
                return false;

            return _animator.runtimeAnimatorController.animationClips.Any(
                clip => clip != null && string.Equals(clip.name, stateName, StringComparison.Ordinal));
        }

        private static Sprite[] LoadFrames(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return Array.Empty<Sprite>();

            return Resources.LoadAll<Sprite>(resourcePath)
                .OrderBy(GetFrameSortIndex)
                .ThenBy(sprite => sprite.name, StringComparer.Ordinal)
                .ToArray();
        }

        private static int GetFrameSortIndex(Sprite sprite)
        {
            if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
                return int.MaxValue;

            Match match = TrailingNumberRegex.Match(sprite.name);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int frameIndex))
                return frameIndex;

            return int.MaxValue;
        }
    }
}
