using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills.Projectiles
{
    public enum SkillProjectileSpreadMode
    {
        Cone = 0,
        ParallelRows = 1
    }

    public sealed class SkillProjectileLaunchData
    {
        public PlayerStats OwnerStats;
        public Transform OwnerTransform;
        public StepEntry Step;
        public DamageContext DamageContext;
        public float DamageMultiplier = 1f;
        public LayerMask TargetLayer = ~0;
        public LayerMask WorldLayer;
        public bool StopOnWorld = true;
        public GameObject ProjectilePrefab;
        public Sprite OverrideSprite;
        public float Speed = 8f;
        public float Lifetime = 4f;
        public float HitRadius = 0.18f;
        public float RotationDegreesPerSecond = 720f;
        public int RemainingForks;
        public int RemainingChains;
        public int RemainingPierces;
        public bool InfinitePierce;
        public bool IgnoreFork;
        public bool IgnoreChain;
        public float ForkAngle = 18f;
        public float ChainSearchRadius = 12f;
        public int SortingLayerId;
        public int SortingOrder = 20050;
        public HashSet<IDamageable> HitHistory;

        public SkillProjectileLaunchData Clone()
        {
            return (SkillProjectileLaunchData)MemberwiseClone();
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class SkillProjectile : MonoBehaviour
    {
        private static GameObject _defaultTemplate;
        private static readonly HashSet<SkillProjectile> ActiveProjectiles = new HashSet<SkillProjectile>();

        private SkillProjectileLaunchData _data;
        private Vector2 _direction = Vector2.right;
        private float _age;
        private bool _despawning;
        private bool _registeredActive;
        private bool _usePool;
        private SpriteRenderer _spriteRenderer;
        private Sprite _defaultSprite;
        private CircleCollider2D _collider;
        private Rigidbody2D _rigidbody;
        private GameObject _template;
        private HashSet<IDamageable> _hitHistory = new HashSet<IDamageable>();

        public static void Spawn(SkillProjectileLaunchData data, Vector2 origin, Vector2 direction, Transform parent = null)
        {
            if (data == null)
                return;

            GameObject template = data.ProjectilePrefab != null ? data.ProjectilePrefab : GetDefaultTemplate();
            if (template == null)
                return;

            bool usePool = PoolManager.Instance != null;
            GameObject instance = usePool
                ? PoolManager.Instance.Spawn(template, origin, Quaternion.identity, parent)
                : Instantiate(template, origin, Quaternion.identity, parent);
            if (instance == null)
                return;

            PrepareRuntimeInstance(instance);

            SkillProjectile projectile = instance.GetComponent<SkillProjectile>();
            if (projectile == null)
                projectile = instance.AddComponent<SkillProjectile>();

            projectile.Initialize(data, direction, usePool, template);
        }

        private static void PrepareRuntimeInstance(GameObject instance)
        {
            if (instance == null)
                return;

            instance.hideFlags = HideFlags.None;
            Transform[] children = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                GameObject childObject = children[i] != null ? children[i].gameObject : null;
                if (childObject != null)
                    childObject.hideFlags = HideFlags.None;

                Component[] components = childObject != null ? childObject.GetComponents<Component>() : null;
                if (components == null)
                    continue;

                for (int j = 0; j < components.Length; j++)
                {
                    if (components[j] != null)
                        components[j].hideFlags = HideFlags.None;
                }
            }
        }

        public static void DespawnAll()
        {
            if (ActiveProjectiles.Count == 0)
                return;

            var active = new List<SkillProjectile>(ActiveProjectiles);
            for (int i = 0; i < active.Count; i++)
            {
                SkillProjectile projectile = active[i];
                if (projectile != null)
                    projectile.Despawn();
            }
        }

        public static void DespawnAllForOwner(PlayerStats owner)
        {
            if (owner == null || ActiveProjectiles.Count == 0)
                return;

            var active = new List<SkillProjectile>(ActiveProjectiles);
            for (int i = 0; i < active.Count; i++)
            {
                SkillProjectile projectile = active[i];
                if (projectile != null && projectile._data != null && projectile._data.OwnerStats == owner)
                    projectile.Despawn();
            }
        }

        private static GameObject GetDefaultTemplate()
        {
            if (_defaultTemplate != null)
                return _defaultTemplate;

            _defaultTemplate = new GameObject("SkillProjectile_RuntimeTemplate");
            _defaultTemplate.hideFlags = HideFlags.HideAndDontSave;
            _defaultTemplate.SetActive(false);

            var renderer = _defaultTemplate.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 20050;

            var collider = _defaultTemplate.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.18f;

            var body = _defaultTemplate.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.simulated = true;

            _defaultTemplate.AddComponent<SkillProjectile>();
            return _defaultTemplate;
        }

        private void Awake()
        {
            EnsureComponents();
            CaptureDefaultSprite();
        }

        private void Initialize(SkillProjectileLaunchData data, Vector2 direction, bool usePool, GameObject template)
        {
            EnsureComponents();
            CaptureDefaultSprite();

            _data = data.Clone();
            _template = template;
            _usePool = usePool;
            _age = 0f;
            _despawning = false;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            RegisterActive();

            _hitHistory.Clear();
            if (_data.HitHistory != null)
            {
                foreach (IDamageable target in _data.HitHistory)
                {
                    if (target != null)
                        _hitHistory.Add(target);
                }
            }

            _collider.isTrigger = true;
            _collider.radius = Mathf.Max(0.02f, _data.HitRadius);
            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.simulated = true;

            ApplyVisual();
            ApplyFacingRotation();
        }

        private void Update()
        {
            if (_data == null || _data.OwnerStats == null || _data.OwnerTransform == null)
            {
                Despawn();
                return;
            }

            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= Mathf.Max(0.05f, _data.Lifetime))
            {
                Despawn();
                return;
            }

            transform.position += (Vector3)(_direction * Mathf.Max(0.01f, _data.Speed) * dt);

            float spin = _data.RotationDegreesPerSecond;
            if (!Mathf.Approximately(spin, 0f))
                transform.Rotate(0f, 0f, spin * dt, Space.Self);

            ScanImmediateOverlaps();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandleCollision(other);
        }

        private void ScanImmediateOverlaps()
        {
            if (_data == null || _collider == null)
                return;

            float radius = Mathf.Max(0.02f, _collider.radius);
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryHandleCollision(hits[i]) || _data == null)
                    return;
            }
        }

        private bool TryHandleCollision(Collider2D other)
        {
            if (_data == null || other == null || !other.enabled)
                return false;

            if (IsOwner(other.transform))
                return false;

            // Damageables are resolved before world filtering: some enemies use child colliders
            // or custom layers, and projectiles should still hit their root health component.
            if (TryResolveDamageable(other.transform, out IDamageable target, out Transform targetTransform))
            {
                if (!IsAllowedTargetLayer(other, targetTransform))
                    return false;

                if (target == null || _hitHistory.Contains(target))
                    return false;

                _hitHistory.Add(target);
                DealDamage(target);

                if (TryFork())
                    return true;

                if (TryChain(targetTransform))
                    return true;

                if (TryPierce())
                    return true;

                Despawn();
                return true;
            }

            if (_data.StopOnWorld && IsInLayerMask(other.gameObject.layer, _data.WorldLayer))
            {
                Despawn();
                return true;
            }

            return false;
        }

        private bool IsAllowedTargetLayer(Collider2D other, Transform targetTransform)
        {
            if (_data == null)
                return false;

            // Empty masks are easy to create accidentally in editor UI. For projectiles,
            // treat that as "no target filter" instead of silently disabling all hits.
            if (_data.TargetLayer.value == 0)
                return true;

            if (other != null && IsInLayerMask(other.gameObject.layer, _data.TargetLayer))
                return true;

            return targetTransform != null && IsInLayerMask(targetTransform.gameObject.layer, _data.TargetLayer);
        }

        private void DealDamage(IDamageable target)
        {
            if (_data == null || target == null || _data.OwnerStats == null || _data.Step == null)
                return;

            IStatsProvider scopedStats = BuildScopedStatsProvider(target);
            DamageSnapshot snapshot = DamageCalculator.CreateDamageSnapshot(
                scopedStats,
                Mathf.Max(0f, _data.DamageMultiplier),
                _data.DamageContext,
                _data.Step.DamageConversions);
            snapshot.Source = _data.OwnerStats;
            target.TakeDamage(snapshot);
            TryApplyAilmentsFromHit(scopedStats, target, snapshot);
        }

        private IStatsProvider BuildScopedStatsProvider(IDamageable target)
        {
            StepEntry step = _data.Step;
            if ((step.ScopedStatModifiers == null || step.ScopedStatModifiers.Count == 0) &&
                (step.TargetAilmentStackModifiers == null || step.TargetAilmentStackModifiers.Count == 0))
                return _data.OwnerStats;

            var modifiers = new List<SerializableStatModifier>();
            if (step.ScopedStatModifiers != null)
                modifiers.AddRange(step.ScopedStatModifiers);

            AppendTargetAilmentStackModifiers(step, target, modifiers);
            return modifiers.Count > 0 ? new ScopedStatsProvider(_data.OwnerStats, modifiers) : _data.OwnerStats;
        }

        private static void AppendTargetAilmentStackModifiers(StepEntry step, IDamageable target, List<SerializableStatModifier> modifiers)
        {
            if (step.TargetAilmentStackModifiers == null || step.TargetAilmentStackModifiers.Count == 0 || modifiers == null)
                return;

            Transform targetTransform = ResolveDamageableTransform(target);
            if (!AilmentController.TryResolve(targetTransform, out AilmentController ailments) || ailments == null)
                return;

            for (int i = 0; i < step.TargetAilmentStackModifiers.Count; i++)
            {
                TargetAilmentStackStatModifierRule rule = step.TargetAilmentStackModifiers[i];
                if (rule == null || Mathf.Approximately(rule.ValuePerStack, 0f))
                    continue;

                int stackCount = ailments.GetStackCount(rule.Ailment);
                if (rule.MaxStacksCounted > 0)
                    stackCount = Mathf.Min(stackCount, rule.MaxStacksCounted);
                if (stackCount <= 0)
                    continue;

                modifiers.Add(new SerializableStatModifier
                {
                    Stat = rule.Stat,
                    Type = rule.Type,
                    Value = rule.ValuePerStack * stackCount
                });
            }
        }

        private void TryApplyAilmentsFromHit(IStatsProvider scopedStats, IDamageable target, DamageSnapshot hitSnapshot)
        {
            Transform targetTransform = ResolveDamageableTransform(target);
            AilmentController.TryApplyHitAilments(scopedStats, _data.OwnerStats, targetTransform, hitSnapshot);
        }

        private bool TryChain(Transform currentTarget)
        {
            if (_data.IgnoreChain || _data.RemainingChains <= 0 || currentTarget == null)
                return false;

            if (!TryFindChainTarget(currentTarget.position, out Transform nextTarget))
                return false;

            _data.RemainingChains--;
            Vector2 toTarget = (Vector2)nextTarget.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                _direction = toTarget.normalized;
            ApplyFacingRotation();
            return true;
        }

        private bool TryFork()
        {
            if (_data.IgnoreFork || _data.RemainingForks <= 0)
                return false;

            SkillProjectileLaunchData childData = _data.Clone();
            childData.RemainingForks = Mathf.Max(0, _data.RemainingForks - 1);
            childData.HitHistory = new HashSet<IDamageable>(_hitHistory);

            float angle = Mathf.Max(0f, _data.ForkAngle);
            Vector2 origin = (Vector2)transform.position + _direction * Mathf.Max(0.02f, _collider.radius * 1.5f);
            SkillProjectile.Spawn(childData, origin, Rotate(_direction, angle), null);
            SkillProjectile.Spawn(childData, origin, Rotate(_direction, -angle), null);
            Despawn();
            return true;
        }

        private bool TryPierce()
        {
            if (_data.InfinitePierce)
                return true;

            if (_data.RemainingPierces <= 0)
                return false;

            _data.RemainingPierces--;
            return true;
        }

        private bool TryFindChainTarget(Vector2 origin, out Transform target)
        {
            target = null;
            float bestDistance = float.MaxValue;
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, Mathf.Max(0.1f, _data.ChainSearchRadius), _data.TargetLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || IsOwner(hit.transform))
                    continue;

                if (!TryResolveDamageable(hit.transform, out IDamageable damageable, out Transform damageableTransform))
                    continue;

                if (damageable == null || _hitHistory.Contains(damageable) || damageableTransform == null)
                    continue;

                float sqrDistance = ((Vector2)damageableTransform.position - origin).sqrMagnitude;
                if (sqrDistance >= bestDistance)
                    continue;

                bestDistance = sqrDistance;
                target = damageableTransform;
            }

            return target != null;
        }

        private static bool TryResolveDamageable(Transform candidate, out IDamageable damageable, out Transform damageableTransform)
        {
            damageable = null;
            damageableTransform = null;
            if (candidate == null)
                return false;

            if (candidate.TryGetComponent(out IDamageable direct))
            {
                damageable = direct;
                damageableTransform = candidate;
                return true;
            }

            Component parentDamageable = candidate.GetComponentInParent(typeof(IDamageable)) as Component;
            if (parentDamageable is IDamageable parent)
            {
                damageable = parent;
                damageableTransform = parentDamageable.transform;
                return true;
            }

            PlayerStats playerStats = candidate.GetComponent<PlayerStats>() ?? candidate.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                PlayerDamageReceiver receiver = playerStats.GetComponent<PlayerDamageReceiver>();
                if (receiver == null)
                    receiver = playerStats.gameObject.AddComponent<PlayerDamageReceiver>();
                damageable = receiver;
                damageableTransform = playerStats.transform;
                return true;
            }

            return false;
        }

        private static Transform ResolveDamageableTransform(IDamageable target)
        {
            return target is Component component ? component.transform : null;
        }

        private bool IsOwner(Transform candidate)
        {
            if (_data?.OwnerTransform == null || candidate == null)
                return false;

            Transform owner = _data.OwnerTransform;
            return candidate == owner || candidate.IsChildOf(owner) || owner.IsChildOf(candidate);
        }

        private static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private void ApplyVisual()
        {
            if (_spriteRenderer == null)
                return;

            _spriteRenderer.sprite = _data.OverrideSprite != null ? _data.OverrideSprite : _defaultSprite;
            _spriteRenderer.enabled = _spriteRenderer.sprite != null;

            _spriteRenderer.sortingLayerID = _data.SortingLayerId;
            _spriteRenderer.sortingOrder = _data.SortingOrder;
        }

        private void ApplyFacingRotation()
        {
            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void EnsureComponents()
        {
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>(true);
            if (_collider == null)
                _collider = GetComponent<CircleCollider2D>();
            if (_rigidbody == null)
                _rigidbody = GetComponent<Rigidbody2D>();
        }

        private void CaptureDefaultSprite()
        {
            if (_spriteRenderer != null && _defaultSprite == null)
                _defaultSprite = _spriteRenderer.sprite;
        }

        private void RegisterActive()
        {
            if (_registeredActive)
                return;

            ActiveProjectiles.Add(this);
            _registeredActive = true;
        }

        private void UnregisterActive()
        {
            if (!_registeredActive)
                return;

            ActiveProjectiles.Remove(this);
            _registeredActive = false;
        }

        private void Despawn()
        {
            if (_despawning)
                return;

            _despawning = true;

            if (_usePool && PoolManager.Instance != null)
                PoolManager.Instance.ReturnToPool(gameObject);
            else
                Destroy(gameObject);
        }

        private void OnDisable()
        {
            UnregisterActive();
            _data = null;
            _template = null;
            _hitHistory.Clear();
            _age = 0f;
            _despawning = false;
            if (_spriteRenderer != null)
                _spriteRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            UnregisterActive();
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos).normalized;
        }
    }
}
