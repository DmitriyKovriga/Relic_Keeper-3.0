using UnityEngine;
using Scripts.Combat;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Visuals;
using Scripts.Skills;
using System.Collections.Generic;
using System.Linq;

namespace Scripts.Enemies
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        private static readonly Dictionary<string, GameObject> TemplateCache = new();

        private EnemyDataSO _data;
        private DamageSnapshot _damageSnapshot;
        private Vector2 _direction;
        private float _speed;
        private float _lifetime;
        private float _age;
        private GameObject _owner;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _collider;
        private Sprite[] _frames;
        private float _animationFps;
        private float _animationTimer;
        private int _currentFrameIndex;
        private bool _usePool;
        private bool _despawning;

        private const int GroundLayerMask = 1 << 6;

        public static void Spawn(EnemyDataSO data, DamageSnapshot snapshot, Vector2 origin, Vector2 direction, Transform parent, GameObject owner)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Attack.ProjectileVisualResourcePath))
                return;

            GameObject template = GetOrCreateTemplate(data);
            if (template == null)
                return;

            bool usePool = PoolManager.Instance != null;
            GameObject instance = usePool
                ? PoolManager.Instance.Spawn(template, origin, Quaternion.identity, parent)
                : Object.Instantiate(template, origin, Quaternion.identity, parent);

            if (instance == null)
                return;

            // Templates are hidden, but their live clones must belong to the scene.
            instance.hideFlags = HideFlags.None;
            foreach (var component in instance.GetComponents<Component>())
                if (component != null) component.hideFlags = HideFlags.None;

            var projectile = instance.GetComponent<EnemyProjectile>();
            if (projectile == null)
                return;

            projectile.Initialize(data, snapshot, direction, owner, usePool);
            instance.SetActive(true);
        }

        private static GameObject GetOrCreateTemplate(EnemyDataSO data)
        {
            string key = $"{data.ID}|{data.Attack.ProjectileVisualResourcePath}";
            if (TemplateCache.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var template = new GameObject($"{data.ID}_ProjectileTemplate");
            template.hideFlags = HideFlags.HideAndDontSave;
            template.SetActive(false);
            template.layer = 0;

            var renderer = template.AddComponent<SpriteRenderer>();

            var collider = template.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, data.Attack.ProjectileHitRadius);

            template.AddComponent<EnemyProjectile>();
            TemplateCache[key] = template;
            return template;
        }

        public void Initialize(EnemyDataSO data, DamageSnapshot snapshot, Vector2 direction, GameObject owner, bool usePool)
        {
            _data = data;
            _damageSnapshot = snapshot;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _speed = Mathf.Max(0.1f, data.Attack.ProjectileSpeed);
            _lifetime = Mathf.Max(0.1f, data.Attack.ProjectileLifetime);
            _age = 0f;
            _owner = owner;
            _usePool = usePool;
            _despawning = false;

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_collider == null)
                _collider = GetComponent<CircleCollider2D>();

            _collider.radius = Mathf.Max(0.05f, data.Attack.ProjectileHitRadius);
            _frames = Resources.LoadAll<Sprite>(data.Attack.ProjectileVisualResourcePath)
                .OrderBy(sprite => sprite.name)
                .ToArray();
            _animationFps = Mathf.Max(1f, data.Attack.ProjectileAnimationFps);
            _animationTimer = 0f;
            _currentFrameIndex = -1;
            ApplyFrame(force: true);
            _spriteRenderer.flipX = _direction.x < 0f;
            WorldRenderSorting.ConfigureSorter(gameObject, RenderDepthCategory.GameplayVfx, transform.position.y, 0, staticAnchor: false);
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            if (_despawning) return;
            if (_data == null || _data.Attack == null)
            {
                Despawn();
                return;
            }

            dt = Mathf.Max(0, dt);
            _age += dt;
            if (_age >= _lifetime)
            {
                Despawn();
                return;
            }

            Vector2 start = transform.position;
            Vector2 end = start + _direction * _speed * dt;
            // Sweep to the first relevant contact: fast projectiles cannot skip a
            // thin barrier, nor can a wall behind the player intercept an earlier hit.
            float travel = Vector2.Distance(start, end);
            var contacts = Physics2D.CircleCastAll(start, _collider.radius, _direction, travel);
            // RaycastHit2D.transform can be the attached Rigidbody's parent, not
            // the collider transform passed to trigger callbacks. Use one target
            // predicate for both paths so an ignored child cannot stop flight.
            var first = contacts.Where(hit => CanHandleContact(hit.collider))
                .OrderBy(hit => hit.distance).FirstOrDefault();
            Vector2 limit = first.collider != null ? start + _direction * first.distance : end;
            if (PlayerFrostWallController.TryIntercept(start, limit, _collider.radius, _owner, out _))
            {
                Despawn(); return;
            }
            if (first.collider != null)
            {
                transform.position = limit;
                if (TryHandleContact(first.collider)) return;
            }
            transform.position = end;
            _animationTimer += dt;
            ApplyFrame(force: false);

            if (_data.Attack.ProjectileStopsOnGround &&
                Physics2D.OverlapCircle(transform.position, Mathf.Max(0.02f, _collider.radius), GroundLayerMask))
                Despawn();
        }

        private bool CanHandleContact(Collider2D other)
        {
            if (_despawning || _data == null || _data.Attack == null || other == null || other == _collider) return false;
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return false;
            return (_data.Attack.ProjectileStopsOnGround && ((1 << other.gameObject.layer) & GroundLayerMask) != 0)
                || EnemyAttackController.TryResolveDamageable(other.transform, out _);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHandleContact(other);

        private bool TryHandleContact(Collider2D other)
        {
            if (!CanHandleContact(other)) return false;

            if (_data.Attack.ProjectileStopsOnGround && ((1 << other.gameObject.layer) & GroundLayerMask) != 0)
            {
                Despawn();
                return true;
            }

            if (EnemyAttackController.TryResolveDamageable(other.transform, out var damageable))
            {
                if (damageable.TakeDamage(_damageSnapshot))
                    AilmentController.TryApplyHitAilmentsFromSource(_damageSnapshot?.Source, other.transform, _damageSnapshot);
                Despawn();
                return true;
            }
            return false;
        }

        private void ApplyFrame(bool force)
        {
            if (_frames == null || _frames.Length == 0 || _spriteRenderer == null)
                return;

            int frameIndex = Mathf.Abs(Mathf.FloorToInt(_animationTimer * _animationFps)) % _frames.Length;
            if (!force && frameIndex == _currentFrameIndex)
                return;

            _currentFrameIndex = frameIndex;
            _spriteRenderer.sprite = _frames[frameIndex];
        }

        private void Despawn()
        {
            if (_despawning) return;
            _despawning = true;
            if (_usePool && PoolManager.Instance != null)
                PoolManager.Instance.ReturnToPool(gameObject);
            else
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }

        private void OnDisable()
        {
            _data = null;
            _damageSnapshot = null;
            _owner = null;
            _frames = null;
            _animationTimer = 0f;
            _currentFrameIndex = -1;
        }
    }
}
