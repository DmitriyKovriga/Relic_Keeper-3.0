using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerStats))]
[DisallowMultipleComponent]
public class PlayerMovement : MonoBehaviour
{
    [Header("Environment Detection")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField, Min(0.01f)] private float _groundCheckRadius = 0.2f;

    [Header("Movement Settings")]
    [SerializeField] private float _stopThreshold = 0.01f;

    private Rigidbody2D _rb;
    private PlayerStats _stats;
    private GameInput _input;

    private float _horizontalInput;
    private bool _isGrounded;
    private bool _isFacingRight = true;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<PlayerStats>();
        _input = new GameInput();
    }

    private void OnEnable()
    {
        // 1. Загружаем бинды при старте
        ApplyBindingOverrides();

        // 2. ПОДПИСЫВАЕМСЯ на уведомление об изменениях
        InputRebindSaver.RebindsChanged += ApplyBindingOverrides;

        _input.Enable();
        _input.Player.Jump.performed += OnJumpPerformed;
    }

    private void OnDisable()
    {
        // 3. ОТПИСЫВАЕМСЯ (обязательно!)
        InputRebindSaver.RebindsChanged -= ApplyBindingOverrides;

        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Disable();
    }

    // Этот метод вызывается и при старте, и когда UI сохраняет настройки
    private void ApplyBindingOverrides()
    {
        InputRebindSaver.Load(_input.asset);
    }

    private void Update()
    {
        ReadInput();
        HandleSpriteFlip();
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
    }

    #region Input Processing

    private void ReadInput()
    {
        float left = _input.Player.MoveLeft.ReadValue<float>();
        float right = _input.Player.MoveRight.ReadValue<float>();
        _horizontalInput = right - left;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (_isGrounded)
        {
            ApplyJumpForce();
        }
    }

    #endregion

    #region Physics & Movement

    private void ApplyMovement()
    {
        float targetSpeed = _horizontalInput * _stats.MoveSpeed.Value;
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
        _rb.AddForce(Vector2.up * _stats.JumpForce.Value, ForceMode2D.Impulse);
    }

    private void CheckGround()
    {
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

    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
        }
    }

    #endregion
}