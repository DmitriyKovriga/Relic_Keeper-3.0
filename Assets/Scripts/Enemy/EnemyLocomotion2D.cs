using UnityEngine;

namespace Scripts.Enemies
{
    public class EnemyLocomotion2D : MonoBehaviour
    {
        private const int GroundLayerMask = 1 << 6;

        private EnemyDataSO _data;
        private Rigidbody2D _rb;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private float _moveInput;

        public bool IsGrounded { get; private set; }
        public bool IsNearWall { get; private set; }
        public bool IsApproachingLedge { get; private set; }
        public int FacingDirection { get; private set; } = 1;
        public float CurrentHorizontalSpeed => _rb != null ? _rb.linearVelocity.x : 0f;

        public void Initialize(EnemyEntity entity, EnemyDataSO data)
        {
            _data = data;
            _spriteRenderer = GetComponent<SpriteRenderer>();
            EnsurePhysicsComponents();
            _moveInput = 0f;
        }

        private void FixedUpdate()
        {
            if (_data == null)
                return;

            RefreshEnvironmentState();
            ApplyMovement();
        }

        public void SetMoveInput(float input)
        {
            _moveInput = Mathf.Clamp(input, -1f, 1f);
            if (Mathf.Abs(_moveInput) > 0.01f)
                FaceDirection(_moveInput > 0f ? 1 : -1);
        }

        public void Stop()
        {
            _moveInput = 0f;
        }

        public bool TryJump()
        {
            if (_data == null || !_data.Movement.CanJump || !IsGrounded || _rb == null)
                return false;

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);
            _rb.AddForce(Vector2.up * _data.Movement.JumpForce, ForceMode2D.Impulse);
            return true;
        }

        private void ApplyMovement()
        {
            if (_rb == null || _data == null)
                return;

            float desiredInput = _moveInput;
            if (Mathf.Abs(desiredInput) > 0.01f)
            {
                int moveDir = desiredInput > 0f ? 1 : -1;
                if (IsNearWall)
                    desiredInput = 0f;
                else if (IsApproachingLedge && !_data.Movement.CanFallFromPlatform)
                    desiredInput = 0f;
                else
                    FaceDirection(moveDir);
            }

            float targetSpeed = desiredInput * _data.Movement.MoveSpeed;
            float currentX = _rb.linearVelocity.x;
            float nextX = Mathf.MoveTowards(currentX, targetSpeed, _data.Movement.Acceleration * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(nextX, _rb.linearVelocity.y);
        }

        private void FaceDirection(int direction)
        {
            FacingDirection = direction >= 0 ? 1 : -1;
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = FacingDirection < 0;
        }

        private void RefreshEnvironmentState()
        {
            if (_collider == null)
            {
                IsGrounded = false;
                IsNearWall = false;
                IsApproachingLedge = false;
                return;
            }

            Bounds bounds = _collider.bounds;
            float groundCheckDistance = Mathf.Max(0.05f, _data.Movement.GroundCheckDistance);
            float wallCheckDistance = Mathf.Max(0.05f, _data.Movement.WallCheckDistance);
            float ledgeCheckDistance = Mathf.Max(0.05f, _data.Movement.LedgeCheckDistance);

            Vector2 groundOrigin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
            Vector2 groundSize = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.8f), 0.05f);
            IsGrounded = Physics2D.BoxCast(groundOrigin, groundSize, 0f, Vector2.down, groundCheckDistance, GroundLayerMask).collider != null;

            Vector2 wallOrigin = new Vector2(bounds.center.x + FacingDirection * (bounds.extents.x + 0.02f), bounds.center.y);
            IsNearWall = Physics2D.Raycast(wallOrigin, Vector2.right * FacingDirection, wallCheckDistance, GroundLayerMask).collider != null;

            Vector2 ledgeOrigin = new Vector2(bounds.center.x + FacingDirection * (bounds.extents.x + 0.05f), bounds.min.y + 0.05f);
            IsApproachingLedge = Physics2D.Raycast(ledgeOrigin, Vector2.down, bounds.extents.y + ledgeCheckDistance, GroundLayerMask).collider == null;
        }

        private void EnsurePhysicsComponents()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null)
                _rb = gameObject.AddComponent<Rigidbody2D>();

            _rb.gravityScale = 3f;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _collider = GetComponent<Collider2D>();
            if (_collider == null)
                _collider = gameObject.AddComponent<BoxCollider2D>();

            if (_collider is BoxCollider2D boxCollider)
            {
                boxCollider.isTrigger = false;
                AutoFitCollider(boxCollider);
                SnapToGround(boxCollider);
            }
        }

        private void AutoFitCollider(BoxCollider2D collider)
        {
            if (_spriteRenderer == null || _spriteRenderer.sprite == null)
                return;

            Vector2 spriteSize = _spriteRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0.01f || spriteSize.y <= 0.01f)
                return;

            float width = Mathf.Clamp(spriteSize.x * 0.42f, 0.45f, 0.85f);
            float height = Mathf.Clamp(spriteSize.y * 0.72f, 0.75f, 1.25f);
            collider.size = new Vector2(width, height);
            collider.offset = new Vector2(0f, -(spriteSize.y - height) * 0.32f);
        }

        private void SnapToGround(BoxCollider2D collider)
        {
            if (collider == null)
                return;

            Bounds bounds = collider.bounds;
            Vector2 castOrigin = new Vector2(bounds.center.x, bounds.max.y + 0.1f);
            Vector2 castSize = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.9f), Mathf.Max(0.1f, bounds.size.y * 0.5f));
            RaycastHit2D hit = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, 4f, GroundLayerMask);
            if (hit.collider == null)
                return;

            float desiredBottomY = hit.point.y + 0.01f;
            float currentBottomY = bounds.min.y;
            float deltaY = desiredBottomY - currentBottomY;
            if (Mathf.Abs(deltaY) < 0.001f)
                return;

            transform.position = new Vector3(transform.position.x, transform.position.y + deltaY, transform.position.z);
        }
    }
}
