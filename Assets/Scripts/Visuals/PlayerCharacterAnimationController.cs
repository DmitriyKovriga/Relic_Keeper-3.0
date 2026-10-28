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
        [SerializeField] private CombatPoseProfile _combatPoseProfile;
        private float _landingWeight = 1f;
        private int[] _runContactFrames = Array.Empty<int>();
        public int RunContactVersion { get; private set; }
        public bool HasRunContactFrames => _runContactFrames.Length > 0;

        private PlayerMovement _movement;
        private PlayerStats _stats;
        private PlayerAttackInput _attack;
        private Sprite _idleSprite;
        private Sprite[] _neutralDodgeFrames = Array.Empty<Sprite>();
        private bool _neutralDodgePlaying;
        private Sprite[] _spinFrames = Array.Empty<Sprite>();
        private object _spinOwner;
        public bool IsPlayingSkillSpin => _spinOwner != null;
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
        private bool _confirmedLandingContact;

        public float PresentationHorizontalScale => PresentationScale.x;
        public bool HasAuthoredPresentation => _current.PresentationScales != null && _current.PresentationScales.Length == _current.Frames.Length && _current.IsValid;
        public Vector2 PresentationScale
        {
            get
            {
                if (_neutralDodgePlaying || IsPlayingSkillSpin) return Vector2.one;
                if (!HasAuthoredPresentation) return new Vector2(_current.IsValid ? _current.HorizontalScale : 1f, 1f);
                int next = Mathf.Min(_frameIndex + 1, _current.Frames.Length - 1);
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_frameTimer / _current.FrameDuration));
                Vector2 scale = Vector2.Lerp(_current.PresentationScales[_frameIndex], _current.PresentationScales[next], t);
                return _state == AnimationState.Land ? Vector2.Lerp(Vector2.one, scale, _landingWeight) : scale;
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
                _movement.OnSkillJumpStarted += HandleJumpStarted;
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
            if (_spinOwner != null) EndSkillSpin(_spinOwner);
            EndNeutralDodge();
            if (_movement != null)
            {
                _movement.OnJumpStarted -= HandleJumpStarted;
                _movement.OnSkillJumpStarted -= HandleJumpStarted;
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

            if (IsPlayingSkillSpin) return;
            if (_state == AnimationState.Land && _attack != null && _attack.IsDashing)
                PlayIdle();

            Vector2 velocity = _movement.CurrentVelocity;
            bool isGrounded = _movement.IsGrounded;
            bool hasJumpGroundLock = Time.time < _suppressGroundRunUntilTime;
            UpdateGroundContactTimer(isGrounded, hasJumpGroundLock, velocity.y);

            bool hasConfirmedGround = HasConfirmedGroundContact();
            bool stillVisuallyFalling = !hasConfirmedGround && _state == AnimationState.Fall && velocity.y <= FallingThreshold;
            bool shouldTreatAsAirborne = !hasConfirmedGround || hasJumpGroundLock || velocity.y > UpwardThreshold || stillVisuallyFalling;
            bool running = Mathf.Abs(velocity.x) > HorizontalMoveThreshold && !Scripts.Skills.PlayerIceSkatesController.IsActiveFor(this);

            if (IsLandingPlaying && isGrounded && !hasJumpGroundLock && !running)
            {
                TickCurrentAnimation();
                return;
            }

            if (!shouldTreatAsAirborne)
            {
                _wasMovingUp = false;
                _hasStartedFallThisAirborne = false;
                if (running)
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
                _confirmedLandingContact = false;
            }

            _wasRawGrounded = isGrounded;
        }

        private bool HasConfirmedGroundContact()
        {
            return _confirmedLandingContact || _groundContactStartedAt >= 0f && Time.time - _groundContactStartedAt >= LandingGroundConfirmSeconds;
        }

        private void HandleJumpStarted()
        {
            bool jumpingInAir = _state == AnimationState.Jump || _state == AnimationState.Fall;
            _wasMovingUp = true;
            _hasStartedFallThisAirborne = false;
            _groundContactStartedAt = -1f;
            _confirmedLandingContact = false;
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
            // The physics landing event is authoritative. Do not make moving
            // feet wait for recovery or the raw-probe confirmation delay.
            _confirmedLandingContact = true;
            _suppressGroundRunUntilTime = Time.time;
            if (_combatPoseProfile == null) _combatPoseProfile = CombatPoseProfile.LoadDefault();
            _landingWeight = _combatPoseProfile != null ? _combatPoseProfile.EvaluateLandingWeight(_movement.LastLandingSpeed) : 1f;
            _wasMovingUp = false;
            _hasStartedFallThisAirborne = false;
            if (_landingWeight <= 0) { PlayIdle(); return; }
            Play(AnimationState.Land, _land);
        }

        private void ApplyCharacterData(CharacterDataSO data)
        {
            _spinOwner = null;
            _spinFrames = RuntimeAnimation.From(data?.SpinAnimation).Frames;
            _neutralDodgeFrames = RuntimeAnimation.From(data?.NeutralDodgeAnimation).Frames;
            _neutralDodgePlaying = false;
            _run = RuntimeAnimation.From(data?.RunAnimation);
            _runContactFrames = data?.RunAnimation?.FootContactFrames ?? Array.Empty<int>();
            _landingWeight = 1f;
            _confirmedLandingContact = false;
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

        public void SampleSkillSpin(object owner, float progress)
        {
            if (owner == null || _renderer == null || _spinFrames.Length == 0) return;
            _spinOwner = owner;
            int frame = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(progress) * (_spinFrames.Length - 1)), 0, _spinFrames.Length - 1);
            _renderer.sprite = _spinFrames[frame];
        }

        public void EndSkillSpin(object owner)
        {
            if (!ReferenceEquals(owner, _spinOwner)) return;
            _spinOwner = null;
            if (_renderer != null) _renderer.sprite = _current.IsValid ? _current.Frames[_frameIndex] : _idleSprite;
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
            RegisterRunContact();
        }

        private void RegisterRunContact()
        {
            if (_state == AnimationState.Run && Array.IndexOf(_runContactFrames, _frameIndex) >= 0)
                RunContactVersion++;
        }

        private void TickCurrentAnimation()
        {
            if (!_current.IsValid || _current.Frames.Length <= 1)
                return;

            _frameTimer += Time.deltaTime;
            int previousFrame = _frameIndex;
            bool wrapped = false;
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
                        wrapped = true;
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
            // Only the final displayed frame can produce a contact. Skipped art
            // at low frame rates must not emit a delayed cloud between steps.
            if (wrapped || previousFrame != _frameIndex) RegisterRunContact();
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
