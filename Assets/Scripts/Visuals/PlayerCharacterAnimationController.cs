using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Visuals
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(PlayerMovement))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerCharacterAnimationController : MonoBehaviour
    {
        private const float HorizontalMoveThreshold = 0.08f;
        private const float UpwardThreshold = 0.08f;
        private const float FallingThreshold = -0.08f;
        private const float JumpGroundedVisualLockSeconds = 0.12f;
        private const float LandingGroundConfirmSeconds = 0.06f;

        private enum AnimationState
        {
            Idle,
            Run,
            Jump,
            Fall,
            Land
        }

        [SerializeField] private SpriteRenderer _renderer;

        private PlayerMovement _movement;
        private PlayerStats _stats;
        private PlayerAttackInput _attack;
        private Sprite _idleSprite;
        private Sprite[] _neutralDodgeFrames = Array.Empty<Sprite>();
        private bool _neutralDodgePlaying;
        public bool IsPlayingNeutralDodge => _neutralDodgePlaying;
        public Bounds NeutralDodgeReferenceBounds => _idleSprite != null ? _idleSprite.bounds : default;
        private Vector3 _idleWeaponOffset;
        private RuntimeAnimation _run = RuntimeAnimation.Empty;
        private RuntimeAnimation _jump = RuntimeAnimation.Empty;
        private RuntimeAnimation _fall = RuntimeAnimation.Empty;
        private RuntimeAnimation _land = RuntimeAnimation.Empty;
        private RuntimeAnimation _current = RuntimeAnimation.Empty;
        private AnimationState _state = AnimationState.Idle;
        private float _frameTimer;
        private int _frameIndex;
        private bool _animationComplete;
        private int _airborneJumpStartFrame;
        private bool _wasMovingUp;
        private bool _hasStartedFallThisAirborne;
        private float _suppressGroundRunUntilTime;
        private float _groundContactStartedAt = -1f;
        private bool _wasRawGrounded;

        public float PresentationHorizontalScale => PresentationScale.x;
        public bool HasAuthoredPresentation => _current.PresentationScales != null && _current.PresentationScales.Length == _current.Frames.Length && _current.IsValid;
        public Vector2 PresentationScale
        {
            get
            {
                if (_neutralDodgePlaying) return Vector2.one;
                if (!HasAuthoredPresentation) return new Vector2(_current.IsValid ? _current.HorizontalScale : 1f, 1f);
                int next = Mathf.Min(_frameIndex + 1, _current.Frames.Length - 1);
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_frameTimer / _current.FrameDuration));
                return Vector2.Lerp(_current.PresentationScales[_frameIndex], _current.PresentationScales[next], t);
            }
        }
        public float PresentationWeaponVerticalOffset => _state == AnimationState.Idle ? _idleWeaponOffset.y : HasAuthoredPresentation
            ? (PresentationScale.y - 1f) * 0.5f +
                (_state != AnimationState.Run && _current.WeaponVerticalOffsets != null && _current.WeaponVerticalOffsets.Length == _current.Frames.Length
                    ? _current.WeaponVerticalOffsets[_frameIndex] : 0f)
            : RunWeaponVerticalOffset;
        public Vector3 PresentationWeaponOffset => _neutralDodgePlaying ? Vector3.zero : _state == AnimationState.Idle ? _idleWeaponOffset : new Vector3(
            _current.IsValid && _current.WeaponHorizontalOffsets != null && _current.WeaponHorizontalOffsets.Length == _current.Frames.Length
                ? _current.WeaponHorizontalOffsets[_frameIndex] : 0f, PresentationWeaponVerticalOffset, 0f);
        private bool IsLandingPlaying => _state == AnimationState.Land && _current.IsValid && !_animationComplete;

        public float RunWeaponVerticalOffset
        {
            get
            {
                var offsets = _current.WeaponVerticalOffsets;
                if (_state != AnimationState.Run || !_current.IsValid || offsets == null || offsets.Length != _current.Frames.Length)
                    return 0;
                // Pixel-art grip poses move on frame boundaries, so equipment must do the same.
                return offsets[_frameIndex];
            }
        }

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            _movement = GetComponent<PlayerMovement>();
            _stats = GetComponent<PlayerStats>();
            _attack = GetComponent<PlayerAttackInput>();
        }

        private void OnEnable()
        {
            if (_movement != null)
            {
                _movement.OnJumpStarted += HandleJumpStarted;
                _movement.OnLanded += HandleLanded;
            }

            if (_stats != null)
            {
                _stats.OnCharacterDataChanged += ApplyCharacterData;
                ApplyCharacterData(_stats.CurrentCharacterData);
            }
        }

        private void Start()
        {
            if (_stats != null && _stats.CurrentCharacterData != null)
                ApplyCharacterData(_stats.CurrentCharacterData);
        }

        private void OnDisable()
        {
            EndNeutralDodge();
            if (_movement != null)
            {
                _movement.OnJumpStarted -= HandleJumpStarted;
                _movement.OnLanded -= HandleLanded;
            }

            if (_stats != null)
                _stats.OnCharacterDataChanged -= ApplyCharacterData;
        }

        private void Update()
        {
            if (Scripts.Skills.PlayerStasisController.IsActiveFor(this)) return;
            if (_movement == null || _renderer == null)
                return;

            if (_attack != null && _attack.IsStationaryDodging)
            {
                SampleNeutralDodge(_attack.StationaryDodgeProgress);
                return;
            }
            EndNeutralDodge();

            Vector2 velocity = _movement.CurrentVelocity;
            bool isGrounded = _movement.IsGrounded;
            bool hasJumpGroundLock = Time.time < _suppressGroundRunUntilTime;
            UpdateGroundContactTimer(isGrounded, hasJumpGroundLock, velocity.y);

            bool hasConfirmedGround = HasConfirmedGroundContact();
            bool stillVisuallyFalling = !hasConfirmedGround && _state == AnimationState.Fall && velocity.y <= FallingThreshold;
            bool shouldTreatAsAirborne = !hasConfirmedGround || hasJumpGroundLock || velocity.y > UpwardThreshold || stillVisuallyFalling;

            if (IsLandingPlaying && isGrounded && !hasJumpGroundLock)
            {
                TickCurrentAnimation();
                return;
            }

            if (!shouldTreatAsAirborne)
            {
                _wasMovingUp = false;
                _hasStartedFallThisAirborne = false;
                if (Mathf.Abs(velocity.x) > HorizontalMoveThreshold)
                    Play(AnimationState.Run, _run);
                else
                    PlayIdle();
            }
            else
            {
                if (velocity.y > UpwardThreshold)
                    _wasMovingUp = true;

                bool crossedIntoFall = _wasMovingUp && velocity.y <= FallingThreshold;
                bool isFallingFromLedge = !_wasMovingUp && velocity.y <= FallingThreshold;
                if (!_hasStartedFallThisAirborne && (crossedIntoFall || isFallingFromLedge))
                {
                    _wasMovingUp = false;
                    _hasStartedFallThisAirborne = true;
                    Play(AnimationState.Fall, _fall);
                }
            }

            TickCurrentAnimation();
        }

        private void UpdateGroundContactTimer(bool isGrounded, bool hasJumpGroundLock, float verticalVelocity)
        {
            bool canAcceptGround = isGrounded && !hasJumpGroundLock && verticalVelocity <= UpwardThreshold;
            if (canAcceptGround)
            {
                if (!_wasRawGrounded || _groundContactStartedAt < 0f)
                    _groundContactStartedAt = Time.time;
            }
            else
            {
                _groundContactStartedAt = -1f;
            }

            _wasRawGrounded = isGrounded;
        }

        private bool HasConfirmedGroundContact()
        {
            return _groundContactStartedAt >= 0f && Time.time - _groundContactStartedAt >= LandingGroundConfirmSeconds;
        }

        private void HandleJumpStarted()
        {
            bool jumpingInAir = _state == AnimationState.Jump || _state == AnimationState.Fall;
            _wasMovingUp = true;
            _hasStartedFallThisAirborne = false;
            _groundContactStartedAt = -1f;
            _suppressGroundRunUntilTime = Time.time + JumpGroundedVisualLockSeconds;
            // A real second jump must restart even if the previous jump clip has already completed.
            // Skip ground anticipation in the air; artwork and grip offsets advance together.
            if (_jump.IsValid)
                _current = RuntimeAnimation.Empty;
            Play(AnimationState.Jump, _jump);
            if (jumpingInAir && _jump.IsValid)
            {
                _frameIndex = _airborneJumpStartFrame;
                _renderer.sprite = _current.Frames[_frameIndex];
            }
        }

        private void HandleLanded()
        {
            // Ignore initialization/duplicate contacts; a new jump interrupts recovery immediately.
            if (_state != AnimationState.Jump && _state != AnimationState.Fall) return;
            _wasMovingUp = false;
            _hasStartedFallThisAirborne = false;
            Play(AnimationState.Land, _land);
        }

        private void ApplyCharacterData(CharacterDataSO data)
        {
            _neutralDodgeFrames = RuntimeAnimation.From(data?.NeutralDodgeAnimation).Frames;
            _neutralDodgePlaying = false;
            _run = RuntimeAnimation.From(data?.RunAnimation);
            _idleWeaponOffset = new Vector3(
                _run.IsValid && _run.WeaponHorizontalOffsets != null && _run.WeaponHorizontalOffsets.Length == _run.Frames.Length ? _run.WeaponHorizontalOffsets[0] : 0f,
                _run.IsValid && _run.WeaponVerticalOffsets != null && _run.WeaponVerticalOffsets.Length == _run.Frames.Length ? _run.WeaponVerticalOffsets[0] : 0f, 0f);
            _jump = RuntimeAnimation.From(data?.JumpAnimation);
            _airborneJumpStartFrame = Mathf.Clamp(data?.JumpAnimation?.AirborneRestartFrame ?? 0, 0, Mathf.Max(0, _jump.Frames.Length - 1));
            _fall = RuntimeAnimation.From(data?.FallAnimation).WithLoop(false);
            _land = RuntimeAnimation.From(data?.LandingAnimation).WithLoop(false);
            _idleSprite = _run.IsValid
                ? _run.Frames[0]
                : data != null && data.Portrait != null
                    ? data.Portrait
                    : _renderer != null
                        ? _renderer.sprite
                        : null;

            _state = AnimationState.Idle;
            _current = RuntimeAnimation.Empty;
            _frameTimer = 0f;
            _frameIndex = 0;
            _animationComplete = false;
            _hasStartedFallThisAirborne = false;

            if (_renderer != null && _idleSprite != null)
                _renderer.sprite = _idleSprite;
        }

        public void SampleNeutralDodge(float progress)
        {
            if (_renderer == null || _neutralDodgeFrames.Length == 0) return;
            _neutralDodgePlaying = true;
            int frame = Mathf.Min((int)(Mathf.Clamp01(progress) * _neutralDodgeFrames.Length), _neutralDodgeFrames.Length - 1);
            _renderer.sprite = _neutralDodgeFrames[frame];
        }

        public void EndNeutralDodge()
        {
            if (!_neutralDodgePlaying) return;
            _neutralDodgePlaying = false;
            if (_renderer != null)
                _renderer.sprite = _current.IsValid ? _current.Frames[_frameIndex] : _idleSprite;
        }

        private void PlayIdle()
        {
            if (_state == AnimationState.Idle)
                return;

            _state = AnimationState.Idle;
            _current = RuntimeAnimation.Empty;
            _frameTimer = 0f;
            _frameIndex = 0;
            _animationComplete = false;

            if (_idleSprite != null)
                _renderer.sprite = _idleSprite;
        }

        private void Play(AnimationState state, RuntimeAnimation animation)
        {
            if (!animation.IsValid)
            {
                if (state == AnimationState.Run)
                    PlayIdle();
                return;
            }

            if (_state == state && ReferenceEquals(_current.Frames, animation.Frames))
                return;

            _state = state;
            _current = animation;
            _frameTimer = 0f;
            _frameIndex = 0;
            _animationComplete = false;
            _renderer.sprite = _current.Frames[0];
        }

        private void TickCurrentAnimation()
        {
            if (!_current.IsValid || _current.Frames.Length <= 1)
                return;

            _frameTimer += Time.deltaTime;
            float frameDuration = Mathf.Max(0.01f, _current.FrameDuration);
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                int nextFrame = _frameIndex + 1;
                if (nextFrame >= _current.Frames.Length)
                {
                    if (_current.Loop)
                    {
                        nextFrame = 0;
                    }
                    else
                    {
                        _animationComplete = true;
                        _frameIndex = _current.Frames.Length - 1;
                        _renderer.sprite = _current.Frames[_frameIndex];
                        return;
                    }
                }

                _frameIndex = nextFrame;
                _renderer.sprite = _current.Frames[_frameIndex];
            }
        }

        private readonly struct RuntimeAnimation
        {
            public static readonly RuntimeAnimation Empty = new RuntimeAnimation(Array.Empty<Sprite>(), 0.08f, true);

            public readonly Sprite[] Frames;
            public readonly float FrameDuration;
            public readonly bool Loop;
            public readonly float[] WeaponVerticalOffsets;
            public readonly float[] WeaponHorizontalOffsets;
            public readonly float HorizontalScale;
            public readonly Vector2[] PresentationScales;
            public bool IsValid => Frames != null && Frames.Length > 0;

            private RuntimeAnimation(Sprite[] frames, float frameDuration, bool loop, float[] weaponVerticalOffsets = null, float horizontalScale = 1f, Vector2[] presentationScales = null, float[] weaponHorizontalOffsets = null)
            {
                Frames = frames;
                FrameDuration = frameDuration;
                Loop = loop;
                WeaponVerticalOffsets = weaponVerticalOffsets;
                WeaponHorizontalOffsets = weaponHorizontalOffsets;
                PresentationScales = presentationScales;
                HorizontalScale = horizontalScale > 0 ? Mathf.Clamp(horizontalScale, 0.8f, 1.2f) : 1f;
            }

            public RuntimeAnimation WithLoop(bool loop)
            {
                return IsValid && Loop != loop
                    ? new RuntimeAnimation(Frames, FrameDuration, loop, WeaponVerticalOffsets, HorizontalScale, PresentationScales, WeaponHorizontalOffsets)
                    : this;
            }

            public static RuntimeAnimation From(CharacterDataSO.CharacterSpriteAnimation source)
            {
                if (source == null || !source.HasAnySource)
                    return Empty;

                Sprite[] frames = source.Frames != null && source.Frames.Length > 0
                    ? CleanFrames(source.Frames)
                    : LoadFramesFromResources(source.SpriteSheetResourcesPath);

                return frames.Length > 0
                    ? new RuntimeAnimation(frames, source.FrameDuration, source.Loop, source.WeaponVerticalOffsets, source.HorizontalScale, source.PresentationScales, source.WeaponHorizontalOffsets)
                    : Empty;
            }

            private static Sprite[] CleanFrames(Sprite[] source)
            {
                var frames = new List<Sprite>(source.Length);
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null)
                        frames.Add(source[i]);
                }

                return frames.ToArray();
            }

            private static Sprite[] LoadFramesFromResources(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return Array.Empty<Sprite>();

                Sprite[] frames = Resources.LoadAll<Sprite>(path.Trim());
                if (frames == null || frames.Length == 0)
                    return Array.Empty<Sprite>();

                Array.Sort(frames, CompareSpriteNamesNaturally);
                return frames;
            }

            private static int CompareSpriteNamesNaturally(Sprite a, Sprite b)
            {
                if (a == null && b == null)
                    return 0;
                if (a == null)
                    return -1;
                if (b == null)
                    return 1;

                return ExtractTrailingNumber(a.name).CompareTo(ExtractTrailingNumber(b.name));
            }

            private static int ExtractTrailingNumber(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return 0;

                int end = value.Length - 1;
                while (end >= 0 && char.IsDigit(value[end]))
                    end--;

                if (end == value.Length - 1)
                    return 0;

                string digits = value.Substring(end + 1);
                return int.TryParse(digits, out int number) ? number : 0;
            }
        }
    }
}
