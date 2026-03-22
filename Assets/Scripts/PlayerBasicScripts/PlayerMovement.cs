using System.Collections.Generic;
using Scripts.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    private const float DefaultDropThroughDuration = 0.3f;
    private const float DefaultDropThroughDownwardVelocity = -3f;
    private const float OneWayGroundRaycastLift = 0.08f;
    private const float OneWayGroundProbeDistance = 0.18f;
    private const float OneWayGroundNormalThreshold = 0.6f;
    private const float GroundedVerticalVelocityThreshold = 0.5f;

    [Header("Environment Detection")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _oneWayPlatformLayer;
    [SerializeField, Min(0.01f)] private float _groundCheckRadius = 0.2f;
    [SerializeField, Min(0.05f)] private float _dropThroughDuration = DefaultDropThroughDuration;
    [SerializeField, Range(-1f, 0f)] private float _dropThroughInputThreshold = -0.5f;

    [Header("Movement")]
    [SerializeField] private float _baseMoveSpeed = 5f;
    [SerializeField] private float _baseJumpForce = 12f;
    [SerializeField] private float _stopThreshold = 0.01f;

    [Header("Jump")]
    [SerializeField, Min(1)] private int _maxJumpCount = 2;

    private readonly List<Collider2D> _ignoredPlatformColliders = new();

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
    private float _dropThroughEndTime = -1f;
    private int _availableJumpCount;

    private bool _hasMotionOverride;
    private Vector2 _motionOverrideVelocity;
    private bool _motionOverrideSuspendsGravity;
    private float _cachedGravityScale;
    private bool _hasCachedGravityScale;

    public bool IsGrounded => _isGrounded;
    public Vector2 CurrentMoveInput => _moveInput;
    public Vector2 CurrentVelocity => _rb != null ? _rb.linearVelocity : Vector2.zero;

    public void SetMovementLock(bool isLocked)
    {
        _isMovementLocked = isLocked;
        if (_isMovementLocked)
        {
            _moveInput = Vector2.zero;
            _horizontalInput = 0f;
            if (!_hasMotionOverride && _rb != null)
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
        }
    }

    public void BeginMotionOverride(Vector2 velocity, bool suspendGravity)
    {
        if (_rb == null)
            return;

        _hasMotionOverride = true;
        _motionOverrideVelocity = velocity;
        _motionOverrideSuspendsGravity = suspendGravity;

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
        _rb.linearVelocity = restoredVelocity;
    }

    public void ForceFaceDirection(float horizontalDirection)
    {
        if (horizontalDirection > 0.01f && !_isFacingRight)
            Flip();
        else if (horizontalDirection < -0.01f && _isFacingRight)
            Flip();
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _mainCollider = GetComponent<Collider2D>();
        _stats = GetComponent<PlayerStats>();
        EnsureOneWayPlatformMask();
        _availableJumpCount = Mathf.Max(1, _maxJumpCount);
    }

    private void OnEnable()
    {
        ApplyBindingOverrides();
        InputRebindSaver.RebindsChanged += ApplyBindingOverrides;
        InputManager.InputActions.Player.Jump.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        InputRebindSaver.RebindsChanged -= ApplyBindingOverrides;
        if (InputManager.InputActions != null)
            InputManager.InputActions.Player.Jump.performed -= OnJumpPerformed;

        ClearIgnoredPlatformCollisions();

        if (_hasMotionOverride)
            EndMotionOverride(Vector2.zero);
    }

    private void Update()
    {
        if (_isMovementLocked)
        {
            _moveInput = Vector2.zero;
            _horizontalInput = 0f;
            return;
        }

        _moveInput = InputManager.InputActions.Player.Move.ReadValue<Vector2>();
        _horizontalInput = _moveInput.x;
    }

    private void FixedUpdate()
    {
        UpdateDropThroughState();
        CheckGround();
        RefreshJumpCountIfLanded();

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
        if (_isMovementLocked || _hasMotionOverride)
            return;

        if (TryStartDropThrough())
            return;

        if (!CanPerformJump())
            return;

        ApplyJumpForce();
        ConsumeJump();
    }

    private void ApplyBindingOverrides()
    {
        if (InputManager.InputActions != null)
            InputRebindSaver.Load(InputManager.InputActions.asset);
    }

    private void ApplyMovement()
    {
        float speedBonusPercent = _stats.GetValue(StatType.MoveSpeed);
        float finalSpeed = _baseMoveSpeed * (1f + (speedBonusPercent / 100f));
        float targetSpeed = _horizontalInput * finalSpeed;
        float currentVerticalSpeed = _rb.linearVelocity.y;

        if (Mathf.Abs(targetSpeed) < _stopThreshold && _isGrounded)
            _rb.linearVelocity = new Vector2(0f, currentVerticalSpeed);
        else
            _rb.linearVelocity = new Vector2(targetSpeed, currentVerticalSpeed);
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

        _rb.linearVelocity = _motionOverrideVelocity;
    }

    private void ApplyJumpForce()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
        float jumpBonusPercent = _stats.GetValue(StatType.JumpForce);
        float finalJump = _baseJumpForce * (1f + (jumpBonusPercent / 100f));
        _rb.AddForce(Vector2.up * finalJump, ForceMode2D.Impulse);
        _isGrounded = false;
    }

    private void CheckGround()
    {
        if (_groundCheckPoint == null)
        {
            _isGrounded = false;
            return;
        }

        int combinedMask = _groundLayer.value;
        if (!_isDroppingThroughPlatform)
            combinedMask |= _oneWayPlatformLayer.value;

        Collider2D[] hits = Physics2D.OverlapCircleAll(_groundCheckPoint.position, _groundCheckRadius, combinedMask);
        _isGrounded = false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
                continue;
            if (_ignoredPlatformColliders.Contains(hit))
                continue;

            bool isOneWayPlatform = ((_oneWayPlatformLayer.value & (1 << hit.gameObject.layer)) != 0);
            if (isOneWayPlatform && !IsStandingOnOneWayPlatform(hit))
                continue;

            _isGrounded = true;
            break;
        }
    }

    private bool TryStartDropThrough()
    {
        if (_groundCheckPoint == null || _mainCollider == null)
            return false;
        if (_oneWayPlatformLayer.value == 0)
            return false;
        if (_moveInput.y > _dropThroughInputThreshold)
            return false;

        Collider2D[] platforms = Physics2D.OverlapCircleAll(_groundCheckPoint.position, _groundCheckRadius + 0.05f, _oneWayPlatformLayer);
        bool ignoredAny = false;
        for (int i = 0; i < platforms.Length; i++)
        {
            Collider2D platform = platforms[i];
            if (platform == null || platform.isTrigger)
                continue;
            if (_ignoredPlatformColliders.Contains(platform))
                continue;

            Physics2D.IgnoreCollision(_mainCollider, platform, true);
            _ignoredPlatformColliders.Add(platform);
            ignoredAny = true;
        }

        if (!ignoredAny)
            return false;

        _isDroppingThroughPlatform = true;
        _dropThroughEndTime = Time.time + _dropThroughDuration;
        _isGrounded = false;
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, Mathf.Min(_rb.linearVelocity.y, DefaultDropThroughDownwardVelocity));
        transform.position += Vector3.down * 0.05f;
        return true;
    }

    private void UpdateDropThroughState()
    {
        if (!_isDroppingThroughPlatform)
            return;

        bool canAttemptRestore = Time.time >= _dropThroughEndTime;
        for (int i = _ignoredPlatformColliders.Count - 1; i >= 0; i--)
        {
            Collider2D platform = _ignoredPlatformColliders[i];
            if (platform == null)
            {
                _ignoredPlatformColliders.RemoveAt(i);
                continue;
            }

            if (!canAttemptRestore)
                continue;

            if (!CanRestoreCollisionWithPlatform(platform))
                continue;

            Physics2D.IgnoreCollision(_mainCollider, platform, false);
            _ignoredPlatformColliders.RemoveAt(i);
        }

        if (canAttemptRestore && _ignoredPlatformColliders.Count == 0)
            _isDroppingThroughPlatform = false;
    }

    private bool CanRestoreCollisionWithPlatform(Collider2D platform)
    {
        if (_mainCollider == null || platform == null)
            return true;

        Bounds playerBounds = _mainCollider.bounds;
        Bounds platformBounds = platform.bounds;

        bool separatedHorizontally = playerBounds.max.x < platformBounds.min.x - 0.01f || playerBounds.min.x > platformBounds.max.x + 0.01f;
        bool separatedVertically = playerBounds.max.y < platformBounds.min.y - 0.01f || playerBounds.min.y > platformBounds.max.y + 0.01f;
        return separatedHorizontally || separatedVertically;
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
        if (_isGrounded && !_wasGroundedLastFixedUpdate)
            _availableJumpCount = Mathf.Max(1, _maxJumpCount);
    }

    private bool CanPerformJump()
    {
        if (_isGrounded)
            return true;

        return _availableJumpCount > 0;
    }

    private void ConsumeJump()
    {
        if (_isGrounded)
            _availableJumpCount = Mathf.Max(0, _maxJumpCount - 1);
        else
            _availableJumpCount = Mathf.Max(0, _availableJumpCount - 1);
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

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
    }
}
