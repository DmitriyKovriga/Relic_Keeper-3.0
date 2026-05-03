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
            Fall
        }

        [SerializeField] private SpriteRenderer _renderer;

        private PlayerMovement _movement;
        private PlayerStats _stats;
        private Sprite _idleSprite;
        private RuntimeAnimation _run = RuntimeAnimation.Empty;
        private RuntimeAnimation _jump = RuntimeAnimation.Empty;
        private RuntimeAnimation _fall = RuntimeAnimation.Empty;
        private RuntimeAnimation _current = RuntimeAnimation.Empty;
        private AnimationState _state = AnimationState.Idle;
        private float _frameTimer;
        private int _frameIndex;
        private bool _wasMovingUp;
        private bool _hasStartedFallThisAirborne;
        private float _suppressGroundRunUntilTime;
        private float _groundContactStartedAt = -1f;
        private bool _wasRawGrounded;

        private void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            _movement = GetComponent<PlayerMovement>();
            _stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            if (_movement != null)
                _movement.OnJumpStarted += HandleJumpStarted;

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
            if (_movement != null)
                _movement.OnJumpStarted -= HandleJumpStarted;

            if (_stats != null)
                _stats.OnCharacterDataChanged -= ApplyCharacterData;
        }

        private void Update()
        {
            if (_movement == null || _renderer == null)
                return;

            Vector2 velocity = _movement.CurrentVelocity;
            bool isGrounded = _movement.IsGrounded;
            bool hasJumpGroundLock = Time.time < _suppressGroundRunUntilTime;
            UpdateGroundContactTimer(isGrounded, hasJumpGroundLock, velocity.y);

            bool hasConfirmedGround = HasConfirmedGroundContact();
            bool stillVisuallyFalling = !hasConfirmedGround && _state == AnimationState.Fall && velocity.y <= FallingThreshold;
            bool shouldTreatAsAirborne = !hasConfirmedGround || hasJumpGroundLock || velocity.y > UpwardThreshold || stillVisuallyFalling;

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
            _wasMovingUp = true;
            _hasStartedFallThisAirborne = false;
            _groundContactStartedAt = -1f;
            _suppressGroundRunUntilTime = Time.time + JumpGroundedVisualLockSeconds;
            Play(AnimationState.Jump, _jump);
        }

        private void ApplyCharacterData(CharacterDataSO data)
        {
            _run = RuntimeAnimation.From(data?.RunAnimation);
            _jump = RuntimeAnimation.From(data?.JumpAnimation);
            _fall = RuntimeAnimation.From(data?.FallAnimation).WithLoop(false);
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
            _hasStartedFallThisAirborne = false;

            if (_renderer != null && _idleSprite != null)
                _renderer.sprite = _idleSprite;
        }

        private void PlayIdle()
        {
            if (_state == AnimationState.Idle)
                return;

            _state = AnimationState.Idle;
            _current = RuntimeAnimation.Empty;
            _frameTimer = 0f;
            _frameIndex = 0;

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
            public bool IsValid => Frames != null && Frames.Length > 0;

            private RuntimeAnimation(Sprite[] frames, float frameDuration, bool loop)
            {
                Frames = frames;
                FrameDuration = frameDuration;
                Loop = loop;
            }

            public RuntimeAnimation WithLoop(bool loop)
            {
                return IsValid && Loop != loop
                    ? new RuntimeAnimation(Frames, FrameDuration, loop)
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
                    ? new RuntimeAnimation(frames, source.FrameDuration, source.Loop)
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
