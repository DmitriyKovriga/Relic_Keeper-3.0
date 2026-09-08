using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    private const float DropThroughFailsafeDuration = 0.55f;
    private const float DefaultDropThroughDownwardVelocity = -3f;
    private const float OneWayGroundRaycastLift = 0.08f;
    private const float OneWayGroundProbeDistance = 0.18f;
    private const float OneWayGroundNormalThreshold = 0.6f;
    private const float GroundedVerticalVelocityThreshold = 0.5f;

    public event System.Action OnJumpStarted;
    public event System.Action OnLanded;

    [Header("Environment Detection")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _oneWayPlatformLayer;
    [SerializeField, Min(0.01f)] private float _groundCheckRadius = 0.2f;
    [SerializeField, Min(0.05f)] private float _dropThroughDuration = DropThroughFailsafeDuration;
    [SerializeField, Range(-1f, 0f)] private float _dropThroughInputThreshold = -0.5f;

    [Header("Movement")]
    [Tooltip("Fallback only. Real movement speed is StatType.MoveSpeed after flat/increased/more stat calculation.")]
    [SerializeField] private float _baseMoveSpeed = 5f;
    [SerializeField] private float _baseJumpForce = 13f;
    [SerializeField] private float _stopThreshold = 0.01f;
    [SerializeField, Min(0.01f)] private float _groundAcceleration = 90f;
    [SerializeField, Min(0.01f)] private float _groundDeceleration = 55f;
    [SerializeField, Min(0.01f)] private float _airAcceleration = 90f;
    [SerializeField, Min(0.01f)] private float _airDeceleration = 45f;
    [SerializeField, Min(1f)] private float _groundTurnAcceleration = 140f;
    [SerializeField, Min(1f)] private float _airTurnAcceleration = 120f;
    [SerializeField, Min(1f)] private float _momentumDeceleration = 35f;

    [Header("Jump")]
    [SerializeField, Min(1)] private int _maxJumpCount = 2;
    [SerializeField, Min(0.01f)] private float _jumpBufferDuration = 0.12f;
    [SerializeField, Min(0f)] private float _coyoteTime = 0.1f;
    [SerializeField, Min(1f)] private float _riseGravityMultiplier = 1.65f;
    [SerializeField, Min(1f)] private float _fallGravityMultiplier = 1.95f;
    [SerializeField, Min(1f)] private float _maxFallSpeed = 24f;

    [Header("Dash Jump")]
    [SerializeField, Min(0.01f)] private float _dashJumpHorizontalSpeed = 8.5f;
    [SerializeField, Range(0.3f, 1.5f)] private float _dashJumpVerticalMultiplier = 0.7f;
    [SerializeField, Min(0.01f)] private float _dashJumpCarryDuration = 0.1f;

    [Header("Fast Fall")]
    [SerializeField, Range(-1f, 0f)] private float _fastFallInputThreshold = -0.6f;
    [SerializeField, Min(0.01f)] private float _fastFallPrimeDuration = 0.14f;
    [SerializeField, Min(0f)] private float _fastFallInitialDownwardSpeed = 4.4f;
    [SerializeField, Min(1f)] private float _fastFallGravityMultiplier = 2.05f;

    private readonly List<Collider2D> _ignoredPlatformColliders = new();
    private readonly Dictionary<Collider2D, float> _ignoredPlatformSurfaceY = new();

    private Rigidbody2D _rb;
    private Collider2D _mainCollider;
    private PlayerStats _stats;
    private Vector2 _moveInput;
    private float _horizontalInput;
    private bool _isGrounded;
    private bool _isFacingRight = true;
    private bool _isMovementLocked;
    private bool _isDroppingThroughPlatform;
    private bool _wasGroundedLastFixedUpdate;
    private bool _hasQueuedJump;
    private float _dropThroughEndTime = -1f;
    private float _jumpQueuedUntilTime = -1f;
    private int _availableJumpCount;

    private bool _hasMotionOverride;
    private Vector2 _motionOverrideVelocity;
    private bool _motionOverrideSuspendsGravity;
    private bool _motionOverridePreservesVerticalVelocity;
    private float _cachedGravityScale;
    private bool _hasCachedGravityScale;
    private bool _isFastFallPriming;
    private bool _isFastFalling;
    private float _baseGravityScale;
    private bool _hasHorizontalLaunch;
    private float _horizontalLaunchEndTime;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _jumpStartedTime = float.NegativeInfinity;
    private bool _groundJumpAvailable;
    private bool _isDashJump;
    private PlayerAttackInput _attackInput;

    public bool IsGrounded => _isGrounded;
    public int FacingDirection => _isFacingRight ? 1 : -1;
    public Vector2 CurrentMoveInput => _moveInput;
    public Vector2 CurrentVelocity => _rb != null ? _rb.linearVelocity : Vector2.zero;
    public float CurrentMoveSpeed => ResolveMoveSpeed();
    public bool IsMovementLocked => _isMovementLocked;
    public bool HasBufferedJump => _hasQueuedJump && Time.time <= _jumpQueuedUntilTime;
    public float DashJumpHorizontalSpeed => _dashJumpHorizontalSpeed;
    public float DashJumpVerticalMultiplier => _dashJumpVerticalMultiplier;
    public float DashJumpCarryDuration => _dashJumpCarryDuration;
    public void SetMovementLock(bool isLocked)
    {
        _isMovementLocked = isLocked;
        if (_isMovementLocked)
        {
            _hasHorizontalLaunch = false;
            if (!_hasMotionOverride && _rb != null)
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }
    }

    public void BeginMotionOverride(Vector2 velocity, bool suspendGravity, bool preserveVerticalVelocity = false)
    {
        if (_rb == null)
            return;

        _hasMotionOverride = true;
        _motionOverrideVelocity = velocity;
        _motionOverrideSuspendsGravity = suspendGravity;
        _motionOverridePreservesVerticalVelocity = preserveVerticalVelocity;
        _hasHorizontalLaunch = false;
        _isFastFallPriming = false;
        _isFastFalling = false;
        RestoreBaseGravity();

        if (suspendGravity && !_hasCachedGravityScale)
        {
            _cachedGravityScale = _rb.gravityScale;
            _hasCachedGravityScale = true;
            _rb.gravityScale = 0f;
        }
    }

    public void UpdateMotionOverride(Vector2 velocity)
    {
        _motionOverrideVelocity = velocity;
    }

    public void ApplyHorizontalMomentumCarry(float speed, float duration)
    {
        BeginHorizontalLaunch(speed, duration);
    }

    public void EndMotionOverride(Vector2 restoredVelocity)
    {
        if (_rb == null)
            return;

        _hasMotionOverride = false;
        _motionOverrideVelocity = Vector2.zero;

        if (_motionOverrideSuspendsGravity && _hasCachedGravityScale)
        {
            _rb.gravityScale = _cachedGravityScale;
            _hasCachedGravityScale = false;
        }

        _motionOverrideSuspendsGravity = false;
        _motionOverridePreservesVerticalVelocity = false;
        _rb.linearVelocity = restoredVelocity;
    }

    private void BeginHorizontalLaunch(float speed, float duration)
    {
        _hasHorizontalLaunch = true;
        _horizontalLaunchEndTime = Time.time + Mathf.Max(0.01f, duration);
    }

    public void ForceFaceDirection(float horizontalDirection)
    {
        if (horizontalDirection > 0.01f && !_isFacingRight)
            Flip();
        else if (horizontalDirection < -0.01f && _isFacingRight)
            Flip();
    }

    public void ApplySkillImpulse(float angleDegrees, float force, bool relativeToFacing, bool clearCurrentVelocity)
    {
        if (_rb == null || force <= 0f)
            return;

        float radians = angleDegrees * Mathf.Deg2Rad;
        float facingMultiplier = relativeToFacing ? FacingDirection : 1f;
        var direction = new Vector2(Mathf.Cos(radians) * facingMultiplier, Mathf.Sin(radians));
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        _isFastFallPriming = false;
        _isFastFalling = false;
        RestoreBaseGravity();

        if (clearCurrentVelocity)
            _rb.linearVelocity = Vector2.zero;

        _rb.AddForce(direction * force, ForceMode2D.Impulse);

        if (Mathf.Abs(direction.x) > 0.01f)
            ForceFaceDirection(direction.x);
    }

    public bool TryPerformDashJump(float horizontalDirection)
    {
        if (_rb == null)
            return false;
        if (Mathf.Abs(horizontalDirection) < 0.01f)
            return false;
        if (!CanPerformJump())
            return false;

        ForceFaceDirection(horizontalDirection);

        float direction = Mathf.Sign(horizontalDirection);
        // A dash jump has a deliberate launch speed, not the peak speed of an interrupted dash.
        float horizontalSpeed = _dashJumpHorizontalSpeed * direction;
        _isFastFalling = false;
        ApplyJumpForce(_dashJumpVerticalMultiplier, horizontalSpeed);
        _isDashJump = true;
        ConsumeJump();
        BeginHorizontalLaunch(horizontalSpeed, _dashJumpCarryDuration);
        return true;
    }

    public bool ConsumeBufferedJump()
    {
        if (!HasBufferedJump)
            return false;

        _hasQueuedJump = false;
        _jumpQueuedUntilTime = -1f;
        return true;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _mainCollider = GetComponent<Collider2D>();
        _stats = GetComponent<PlayerStats>();
        _attackInput = GetComponent<PlayerAttackInput>();
        if (GetComponent<GroundingVisualController>() == null)
            gameObject.AddComponent<GroundingVisualController>();
        if (GetComponent<PlayerCharacterAnimationController>() == null)
            gameObject.AddComponent<PlayerCharacterAnimationController>();
        if (GetComponent<PlayerMovementVisual>() == null)
            gameObject.AddComponent<PlayerMovementVisual>();
        EnsureRenderDepthSort();
        _baseGravityScale = _rb != null ? _rb.gravityScale : 1f;
        EnsureOneWayPlatformMask();
        _availableJumpCount = Mathf.Max(0, _maxJumpCount - 1);
    }

    private void OnEnable()
    {
        InputManager.InputActions.Player.Jump.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        if (InputManager.InputActions != null)
            InputManager.InputActions.Player.Jump.performed -= OnJumpPerformed;

        ClearIgnoredPlatformCollisions();

        if (_hasMotionOverride)
            EndMotionOverride(Vector2.zero);
        _hasQueuedJump = false;
        _hasHorizontalLaunch = false;
        _isFastFallPriming = false;
        _isFastFalling = false;
        RestoreBaseGravity();
    }

    private void Update()
    {
        _moveInput = InputManager.InputActions.Player.Move.ReadValue<Vector2>();
        _horizontalInput = _moveInput.x;
    }

    private void FixedUpdate()
    {
        // Sample intent even during a dash/skill lock; locks restrict motion, not input.
        _moveInput = InputManager.InputActions.Player.Move.ReadValue<Vector2>();
        _horizontalInput = _moveInput.x;
        UpdateDropThroughState();
        CheckGround();
        RefreshJumpCountIfLanded();
        if (_attackInput != null && _attackInput.isActiveAndEnabled)
            _attackInput.TickMovementActions();
        UpdateFastFallState();
        ProcessQueuedJump();

        if (_hasMotionOverride)
        {
            ApplyMotionOverride();
        }
        else if (!_isMovementLocked)
        {
            ApplyMovement();
            HandleSpriteFlip();
        }

        _wasGroundedLastFixedUpdate = _isGrounded;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        _hasQueuedJump = true;
        _jumpQueuedUntilTime = Time.time + Mathf.Max(0.01f, _jumpBufferDuration);
    }

    private void ApplyMovement()
    {
        float finalSpeed = ResolveMoveSpeed();
        float targetSpeed = _horizontalInput * finalSpeed;
        float currentHorizontalSpeed = _rb.linearVelocity.x;

        bool hasInput = Mathf.Abs(_horizontalInput) > _stopThreshold;
        bool reversing = hasInput && currentHorizontalSpeed * _horizontalInput < 0f;
        if (Time.time >= _horizontalLaunchEndTime || reversing || !hasInput)
            _hasHorizontalLaunch = false;

        float acceleration = hasInput
            ? (_isGrounded ? _groundAcceleration : _airAcceleration)
            : (_isGrounded ? _groundDeceleration : _airDeceleration);
        if (reversing)
            acceleration = _isGrounded ? _groundTurnAcceleration : _airTurnAcceleration;
        else if (hasInput && Mathf.Abs(currentHorizontalSpeed) > Mathf.Abs(targetSpeed))
            acceleration = _momentumDeceleration;

        // Carry only slows the loss of excess launch speed, never normal air acceleration.
        if (_hasHorizontalLaunch && hasInput && !reversing
            && Mathf.Abs(currentHorizontalSpeed) > Mathf.Abs(targetSpeed))
            acceleration = Mathf.Min(acceleration, _momentumDeceleration * 0.5f);

        float nextHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);
        float currentVerticalSpeed = _rb.linearVelocity.y;

        if (Mathf.Abs(targetSpeed) < _stopThreshold && _isGrounded && Mathf.Abs(nextHorizontalSpeed) < _stopThreshold)
            _rb.linearVelocity = new Vector2(0f, currentVerticalSpeed);
        else
            _rb.linearVelocity = new Vector2(nextHorizontalSpeed, currentVerticalSpeed);
    }

    private float ResolveMoveSpeed()
    {
        if (_stats == null)
            return Mathf.Max(0f, _baseMoveSpeed);

        CharacterStat moveSpeed = _stats.GetStat(StatType.MoveSpeed);
        if (moveSpeed.BaseValue <= 0f && moveSpeed.Modifiers.Count == 0)
            moveSpeed.BaseValue = Mathf.Max(0f, _baseMoveSpeed);

        return Mathf.Max(0f, moveSpeed.Value);
    }

    private void UpdateFastFallState()
    {
        if (_rb == null)
            return;

        if (_hasMotionOverride)
        {
            _isFastFallPriming = false;
            _isFastFalling = false;
            if (!_motionOverrideSuspendsGravity)
                RestoreBaseGravity();
            return;
        }

        if (_isGrounded)
        {
            _isFastFallPriming = false;
            _isFastFalling = false;
            RestoreBaseGravity();
            return;
        }

        bool wantsFastFall = !_isMovementLocked && _moveInput.y <= _fastFallInputThreshold;
        float minimumRiseTime = _isDashJump ? 0.04f : _fastFallPrimeDuration;
        _isFastFallPriming = wantsFastFall && Time.time < _jumpStartedTime + minimumRiseTime;
        if (wantsFastFall && !_isFastFallPriming && !_isFastFalling)
        {
            _isFastFalling = true;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x,
                Mathf.Min(_rb.linearVelocity.y, -_fastFallInitialDownwardSpeed));
        }
        if (!wantsFastFall)
            _isFastFalling = false;

        float gravityMultiplier = _isFastFalling ? _fastFallGravityMultiplier
            : _rb.linearVelocity.y < 0f ? _fallGravityMultiplier : _riseGravityMultiplier;
        _rb.gravityScale = _baseGravityScale * gravityMultiplier;
        if (_rb.linearVelocity.y < -_maxFallSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -_maxFallSpeed);
    }

    private void RestoreBaseGravity()
    {
        if (_rb != null)
            _rb.gravityScale = _baseGravityScale;
    }

    private void ProcessQueuedJump()
    {
        if (!HasBufferedJump)
        {
            _hasQueuedJump = false;
            return;
        }

        if (_isMovementLocked || _hasMotionOverride)
            return;

        if (TryStartDropThrough())
        {
            ConsumeBufferedJump();
            return;
        }

        if (!CanPerformJump())
            return;

        ApplyJumpForce();
        ConsumeJump();
        ConsumeBufferedJump();
    }

    private void ApplyMotionOverride()
    {
        if (_rb == null)
            return;

        if (_motionOverrideSuspendsGravity && !_hasCachedGravityScale)
        {
            _cachedGravityScale = _rb.gravityScale;
            _hasCachedGravityScale = true;
            _rb.gravityScale = 0f;
        }

        float verticalVelocity = _motionOverridePreservesVerticalVelocity ? _rb.linearVelocity.y : _motionOverrideVelocity.y;
        _rb.linearVelocity = new Vector2(_motionOverrideVelocity.x, verticalVelocity);
    }

    private void ApplyJumpForce(float verticalMultiplier = 1f, float? horizontalOverride = null)
    {
        _isFastFallPriming = false;
        _isFastFalling = false;
        _isDashJump = false;
        _jumpStartedTime = Time.time;
        RestoreBaseGravity();
        float horizontalVelocity = horizontalOverride ?? _rb.linearVelocity.x;
        _rb.linearVelocity = new Vector2(horizontalVelocity, 0f);
        float jumpBonusPercent = _stats.GetValue(StatType.JumpForce);
        float finalJump = _baseJumpForce * (1f + (jumpBonusPercent / 100f)) * Mathf.Max(0f, verticalMultiplier);
        _rb.AddForce(Vector2.up * finalJump, ForceMode2D.Impulse);
        _rb.gravityScale = _baseGravityScale * _riseGravityMultiplier;
        _isGrounded = false;
        GameplayEventBus.Raise(GameplayEventType.Jumped, source: gameObject, target: gameObject);
        OnJumpStarted?.Invoke();
    }

    private void CheckGround()
    {
        if (_groundCheckPoint == null || _rb.linearVelocity.y > GroundedVerticalVelocityThreshold
            || Time.time < _jumpStartedTime + Time.fixedDeltaTime)
        {
            _isGrounded = false;
            return;
        }

        int combinedMask = _groundLayer.value | _oneWayPlatformLayer.value;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_groundCheckPoint.position, _groundCheckRadius, combinedMask);
        _isGrounded = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;
            if (hit.attachedRigidbody == _rb)
                continue;
            if (_ignoredPlatformColliders.Contains(hit))
                continue;

            bool isOneWayPlatform = ((_oneWayPlatformLayer.value & (1 << hit.gameObject.layer)) != 0);
            if (isOneWayPlatform && !IsStandingOnOneWayPlatform(hit))
                continue;
            if (!isOneWayPlatform && !HasSupportNormal(hit))
                continue;

            _isGrounded = true;
            break;
        }
    }

    private bool HasSupportNormal(Collider2D candidate)
    {
        for (int i = -1; i <= 1; i++)
        {
            Vector2 origin = (Vector2)_groundCheckPoint.position
                + new Vector2(i * _groundCheckRadius * 0.8f, OneWayGroundRaycastLift);
            RaycastHit2D support = Physics2D.Raycast(origin, Vector2.down,
                _groundCheckRadius + OneWayGroundRaycastLift, _groundLayer);
            if (support.collider == candidate && support.fraction > 0f && support.normal.y >= OneWayGroundNormalThreshold)
                return true;
        }
        return false;
    }

    private bool TryStartDropThrough()
    {
        if (_groundCheckPoint == null || _mainCollider == null)
            return false;
        if (_oneWayPlatformLayer.value == 0)
            return false;
        if (_moveInput.y > _dropThroughInputThreshold)
            return false;

        if (!TryGetCurrentOneWayPlatform(out Collider2D platform, out float surfaceY))
            return false;
        if (_ignoredPlatformColliders.Contains(platform))
            return false;

        Physics2D.IgnoreCollision(_mainCollider, platform, true);
        _ignoredPlatformColliders.Add(platform);
        _ignoredPlatformSurfaceY[platform] = surfaceY;

        _isDroppingThroughPlatform = true;
        _dropThroughEndTime = Time.time + Mathf.Max(DropThroughFailsafeDuration, _dropThroughDuration);
        _isGrounded = false;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Min(_rb.linearVelocity.y, DefaultDropThroughDownwardVelocity));
        transform.position += Vector3.down * 0.1f;
        return true;
    }

    private void UpdateDropThroughState()
    {
        if (!_isDroppingThroughPlatform)
            return;

        for (int i = _ignoredPlatformColliders.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _ignoredPlatformColliders[i];
            if (platform == null)
            {
                _ignoredPlatformColliders.RemoveAt(i);
                continue;
            }

            bool playerIsBelowDroppedSurface = IsBelowDroppedPlatformSurface(platform);
            bool failsafeElapsed = Time.time >= _dropThroughEndTime;
            if (!playerIsBelowDroppedSurface && !failsafeElapsed)
                continue;

            Physics2D.IgnoreCollision(_mainCollider, platform, false);
            _ignoredPlatformColliders.RemoveAt(i);
            _ignoredPlatformSurfaceY.Remove(platform);
        }

        if (_ignoredPlatformColliders.Count == 0)
            _isDroppingThroughPlatform = false;
    }

    private bool TryGetCurrentOneWayPlatform(out Collider2D platform, out float surfaceY)
    {
        platform = null;
        surfaceY = 0f;

        if (_groundCheckPoint == null)
            return false;

        Vector2 origin = (Vector2)_groundCheckPoint.position + Vector2.up * OneWayGroundRaycastLift;
        float distance = _groundCheckRadius + OneWayGroundProbeDistance;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, distance, _oneWayPlatformLayer);

        float bestDistance = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (!hit.collider || hit.collider.isTrigger)
                continue;
            if (hit.normal.y < OneWayGroundNormalThreshold)
                continue;
            if (hit.distance >= bestDistance)
                continue;

            platform = hit.collider;
            surfaceY = hit.point.y;
            bestDistance = hit.distance;
        }

        return platform != null;
    }

    private bool IsBelowDroppedPlatformSurface(Collider2D platform)
    {
        if (_mainCollider == null || platform == null)
            return true;
        if (!_ignoredPlatformSurfaceY.TryGetValue(platform, out float surfaceY))
            return false;

        return _mainCollider.bounds.max.y < surfaceY - 0.02f;
    }

    private void EnsureOneWayPlatformMask()
    {
        if (_oneWayPlatformLayer.value != 0)
            return;

        int oneWayLayer = LayerMask.NameToLayer("OneWayPlatform");
        if (oneWayLayer >= 0)
            _oneWayPlatformLayer = 1 << oneWayLayer;
    }

    private bool IsStandingOnOneWayPlatform(Collider2D platform)
    {
        if (platform == null || _rb == null || _groundCheckPoint == null)
            return false;
        if (_rb.linearVelocity.y > GroundedVerticalVelocityThreshold)
            return false;

        Vector2 origin = (Vector2)_groundCheckPoint.position + Vector2.up * OneWayGroundRaycastLift;
        float distance = _groundCheckRadius + OneWayGroundProbeDistance;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, distance, _oneWayPlatformLayer);
        if (!hit.collider || hit.collider != platform)
            return false;

        return hit.normal.y >= OneWayGroundNormalThreshold;
    }

    private void RefreshJumpCountIfLanded()
    {
        if (_isGrounded)
        {
            _lastGroundedTime = Time.time;
            _groundJumpAvailable = true;
        }
        else if (_groundJumpAvailable && Time.time > _lastGroundedTime + _coyoteTime)
        {
            // Walking off a ledge spends the ground jump, retaining the air jump.
            _groundJumpAvailable = false;
            _availableJumpCount = Mathf.Min(_availableJumpCount, Mathf.Max(0, _maxJumpCount - 1));
        }
        if (_isGrounded && !_wasGroundedLastFixedUpdate)
        {
            _availableJumpCount = Mathf.Max(1, _maxJumpCount);
            GameplayEventBus.Raise(GameplayEventType.Landed, source: gameObject, target: gameObject);
            OnLanded?.Invoke();
        }
    }

    private bool CanPerformJump()
    {
        if (_isGrounded)
            return true;

        return _availableJumpCount > 0;
    }

    private void ConsumeJump()
    {
        if (_groundJumpAvailable)
            _availableJumpCount = Mathf.Max(0, _maxJumpCount - 1);
        else
            _availableJumpCount = Mathf.Max(0, _availableJumpCount - 1);
        _groundJumpAvailable = false;
        _lastGroundedTime = float.NegativeInfinity;
    }

    private void ClearIgnoredPlatformCollisions()
    {
        if (_mainCollider == null)
            return;

        for (int i = _ignoredPlatformColliders.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _ignoredPlatformColliders[i];
            if (platform != null)
                Physics2D.IgnoreCollision(_mainCollider, platform, false);
        }

        _ignoredPlatformColliders.Clear();
        _ignoredPlatformSurfaceY.Clear();
        _isDroppingThroughPlatform = false;
    }

    private void HandleSpriteFlip()
    {
        if (_horizontalInput > 0f && !_isFacingRight)
            Flip();
        else if (_horizontalInput < 0f && _isFacingRight)
            Flip();
    }

    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1f;
        transform.localScale = scaler;
    }

    private void EnsureRenderDepthSort()
    {
        WorldDepthSort sorter = GetComponent<WorldDepthSort>();
        if (sorter == null)
            sorter = gameObject.AddComponent<WorldDepthSort>();

        sorter.Configure(RenderDepthCategory.Player, localOffset: 0, staticAnchor: false, anchorY: transform.position.y);
    }

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
    }
}
