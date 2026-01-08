using UnityEngine;
using UnityEngine.InputSystem;
using Scripts.Stats; // <--- ВАЖНО: Подключаем наш Enum

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
        // Отписываемся, чтобы избежать ошибок
        InputRebindSaver.RebindsChanged -= ApplyBindingOverrides;
        
        _input.Player.Jump.performed -= OnJumpPerformed;
        _input.Disable();
    }

    private void Update()
    {
        // Читаем ввод каждый кадр
        Vector2 moveInput = _input.Player.Move.ReadValue<Vector2>();
        _horizontalInput = moveInput.x;
    }

    private void FixedUpdate()
    {
        CheckGround();
        ApplyMovement();
        HandleSpriteFlip();
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (_isGrounded)
        {
            ApplyJumpForce();
        }
    }

    // Метод для обновления биндов (вызывается при старте и после сохранения настроек)
    private void ApplyBindingOverrides()
    {
        InputRebindSaver.Load(_input.asset);
    }

    #region Physics Logic

    private void ApplyMovement()
    {
        // 1. Получаем бонус скорости в процентах (например, 20 = 20%)
        float speedBonusPercent = _stats.GetValue(StatType.MoveSpeed);
        
        // 2. Формула: Base * (1 + Bonus/100)
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
        
        // 1. Получаем бонус прыжка в процентах
        float jumpBonusPercent = _stats.GetValue(StatType.JumpForce);
        
        // 2. Формула: Base * (1 + Bonus/100)
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