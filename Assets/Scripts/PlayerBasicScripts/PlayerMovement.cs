// ==========================================
// FILENAME: Assets/Scripts/PlayerBasicScripts/PlayerMovement.cs
// ==========================================
using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Stats;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [Header("Environment Detection")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(0.01f)] private float _groundCheckRadius = 0.2f;

    [Tooltip("Базовая скорость без предметов (например, 5)")]
    [SerializeField] private float _baseMoveSpeed = 5f;
    
    [Tooltip("Базовая сила прыжка (например, 12)")]
    [SerializeField] private float _baseJumpForce = 12f;

    [Header("Movement Settings")]
    [SerializeField] private float _stopThreshold = 0.01f;

    private Rigidbody2D _rb;
    private PlayerStats _stats;
    // --- УДАЛЕНО: private GameInput _input; ---

    private float _horizontalInput;
    private bool _isGrounded;
    private bool _isFacingRight = true;
    private bool _isMovementLocked = false;

    public void SetMovementLock(bool isLocked)
    {
        _isMovementLocked = isLocked;
        if (_isMovementLocked)
        {
            _horizontalInput = 0;
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<PlayerStats>();
        // --- УДАЛЕНО: _input = new GameInput(); ---
    }

    private void OnEnable()
    {
        ApplyBindingOverrides();
        InputRebindSaver.RebindsChanged += ApplyBindingOverrides;
        
        // --- ИЗМЕНЕНО: Используем InputManager ---
        // InputManager.InputActions.Enable(); // Не нужно, InputManager делает это сам
        InputManager.InputActions.Player.Jump.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        InputRebindSaver.RebindsChanged -= ApplyBindingOverrides;
        
        // --- ИЗМЕНЕНО: Проверяем, что InputManager еще существует ---
        if (InputManager.InputActions != null)
        {
            InputManager.InputActions.Player.Jump.performed -= OnJumpPerformed;
        }
        // --- УДАЛЕНО: _input.Disable(); ---
    }

    private void Update()
    {
        if (_isMovementLocked) 
        {
            _horizontalInput = 0;
            return;
        }

        // --- ИЗМЕНЕНО: Используем InputManager ---
        Vector2 moveInput = InputManager.InputActions.Player.Move.ReadValue<Vector2>();
        _horizontalInput = moveInput.x;
    }

    private void FixedUpdate()
    {
        CheckGround();
        
        if (!_isMovementLocked)
        {
            ApplyMovement();
            HandleSpriteFlip();
        }
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (_isGrounded && !_isMovementLocked)
        {
            ApplyJumpForce();
        }
    }

    private void ApplyBindingOverrides()
    {
        // --- ИЗМЕНЕНО: Используем InputManager ---
        if (InputManager.InputActions != null)
        {
            InputRebindSaver.Load(InputManager.InputActions.asset);
        }
    }
    
    // ... Остальной код файла (ApplyMovement, Flip и т.д.) без изменений ...
    #region Physics Logic

    private void ApplyMovement()
    {
        float speedBonusPercent = _stats.GetValue(StatType.MoveSpeed);
        float finalSpeed = _baseMoveSpeed * (1f + (speedBonusPercent / 100f));
        float targetSpeed = _horizontalInput * finalSpeed;
        float currentVerticalSpeed = _rb.linearVelocity.y;

        if (Mathf.Abs(targetSpeed) < _stopThreshold && _isGrounded)
        {
            _rb.linearVelocity = new Vector2(0, currentVerticalSpeed);
        }
        else
        {
            _rb.linearVelocity = new Vector2(targetSpeed, currentVerticalSpeed);
        }
    }

    private void ApplyJumpForce()
    {
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0);
        float jumpBonusPercent = _stats.GetValue(StatType.JumpForce);
        float finalJump = _baseJumpForce * (1f + (jumpBonusPercent / 100f));
        _rb.AddForce(Vector2.up * finalJump, ForceMode2D.Impulse);
    }

    private void CheckGround()
    {
        if (_groundCheckPoint != null)
            _isGrounded = Physics2D.OverlapCircle(_groundCheckPoint.position, _groundCheckRadius, _groundLayer);
    }

    #endregion

    #region Visuals

    private void HandleSpriteFlip()
    {
        if (_horizontalInput > 0 && !_isFacingRight) Flip();
        else if (_horizontalInput < 0 && _isFacingRight) Flip();
    }

    private void Flip()
    {
        _isFacingRight = !_isFacingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
        }
    }
}