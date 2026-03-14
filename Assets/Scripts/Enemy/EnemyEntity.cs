using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyEntity : MonoBehaviour
    {
        private const int PlayerLayer = 0;
        private const int EnemyLayer = 7;

        [Header("Config")]
        [SerializeField] private EnemyDataSO _defaultData;
        [SerializeField, Range(1, 100)] private int _level = 1;

        private EnemyStats _stats;
        private EnemyHealth _health;
        private EnemySensor2D _sensor;
        private EnemyLocomotion2D _locomotion;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;
        private EnemyBrain _brain;
        private bool _isInitialized;
        private static bool s_collisionMatrixConfigured;

        public EnemyDataSO Data => _defaultData;
        public int Level => _level;

        private void Awake()
        {
            EnsureCharacterCollisionRules();
            EnsureCoreComponents();
        }

        private void Start()
        {
            if (!_isInitialized && _defaultData != null)
                Setup(_defaultData, _level);
        }

        public void Setup(EnemyDataSO data, int levelOverride = -1)
        {
            if (data == null)
                return;

            _defaultData = data;
            if (levelOverride > 0)
                _level = levelOverride;

            EnsureCoreComponents();
            EnsureRuntimeComponents(data);

            _stats.Initialize(data, _level);
            _health.Initialize();
            _sensor.Initialize(this, data);
            _locomotion.Initialize(this, data);
            _attack.Initialize(this, data);
            _animation.Initialize(this, data);
            _brain.Initialize(this, data);

            name = $"[{_level}] {data.DisplayName}";
            _isInitialized = true;
        }

        [ContextMenu("Refresh Stats")]
        public void Refresh()
        {
            if (_defaultData != null)
                Setup(_defaultData, _level);
        }

        private void EnsureCoreComponents()
        {
            _stats = GetComponent<EnemyStats>();
            if (_stats == null)
                _stats = gameObject.AddComponent<EnemyStats>();

            _health = GetComponent<EnemyHealth>();
            if (_health == null)
                _health = gameObject.AddComponent<EnemyHealth>();
        }

        private void EnsureRuntimeComponents(EnemyDataSO data)
        {
            _sensor = GetComponent<EnemySensor2D>();
            if (_sensor == null)
                _sensor = gameObject.AddComponent<EnemySensor2D>();

            _locomotion = GetComponent<EnemyLocomotion2D>();
            if (_locomotion == null)
                _locomotion = gameObject.AddComponent<EnemyLocomotion2D>();

            _attack = GetComponent<EnemyAttackController>();
            if (_attack == null)
                _attack = gameObject.AddComponent<EnemyAttackController>();

            _animation = GetComponent<EnemyAnimationBridge>();
            if (_animation == null)
                _animation = gameObject.AddComponent<EnemyAnimationBridge>();

            _brain = GetComponent<EnemyBrain>();
            if (_brain == null)
                _brain = gameObject.AddComponent<EnemyBrain>();

            ConfigurePhysicsIfNeeded(data);
            ConfigureAnimatorIfNeeded(data);
            ConfigureRendererDefaults();
        }

        private void ConfigurePhysicsIfNeeded(EnemyDataSO data)
        {
            bool needsPhysics = data != null &&
                               (data.Movement.MoveSpeed > 0f || data.Perception.AggroRange > 0f || data.Attack.AttackRange > 0f);
            if (!needsPhysics)
                return;

            var rb = GetComponent<Rigidbody2D>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody2D>();

            if (rb == null)
                return;

            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = GetComponent<BoxCollider2D>();
            if (collider == null)
                collider = gameObject.AddComponent<BoxCollider2D>();

            if (collider == null)
                return;

            collider.isTrigger = false;
            AutoFitCollider(collider);
            SnapToGround(collider);
        }

        private void ConfigureAnimatorIfNeeded(EnemyDataSO data)
        {
            if (data == null || data.Animation == null || data.Animation.Controller == null)
                return;

            var animator = GetComponent<Animator>();
            if (animator == null)
                animator = gameObject.AddComponent<Animator>();

            if (animator == null)
                return;

            animator.runtimeAnimatorController = data.Animation.Controller;
            if (!string.IsNullOrEmpty(data.Animation.IdleStateName))
                animator.Play(data.Animation.IdleStateName, 0, 0f);
        }

        private void ConfigureRendererDefaults()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null)
                return;

            if (sr.sortingLayerID == 0)
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 10);
        }

        private static void EnsureCharacterCollisionRules()
        {
            if (s_collisionMatrixConfigured)
                return;

            Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);
            Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
            s_collisionMatrixConfigured = true;
        }

        private void AutoFitCollider(BoxCollider2D collider)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null)
                return;

            Vector2 spriteSize = sr.sprite.bounds.size;
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

            const int groundLayerMask = 1 << 6;
            Bounds bounds = collider.bounds;
            Vector2 castOrigin = new Vector2(bounds.center.x, bounds.center.y + 0.05f);
            Vector2 castSize = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.9f), Mathf.Max(0.05f, bounds.size.y));
            RaycastHit2D hit = Physics2D.BoxCast(castOrigin, castSize, 0f, Vector2.down, 2f, groundLayerMask);
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
