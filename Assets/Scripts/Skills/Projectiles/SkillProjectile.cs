using System.Collections.Generic;
using System.Collections;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills.Modules;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using Scripts.Visuals;
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
        public int SkillSlotIndex = -1;
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
        public float HitRadius = 1f;
        public float RotationDegreesPerSecond = 720f;
        public int RemainingForks;
        public int RemainingChains;
        public int RemainingPierces;
        public bool InfinitePierce;
        public bool IgnoreFork;
        public bool IgnoreChain;
        public float ForkAngle = 18f;
        public float ChainSearchRadius = 12f;
        public bool GroundMotion;
        public LayerMask GroundLayer;
        public bool BreakOnGroundObstacles = true;
        public float GroundSnapUp = 0.7f;
        public float GroundSnapDown = 2.5f;
        public float GroundYOffset = 0.06f;
        public bool Homing;
        public float HomingSearchRadius = 14f;
        public float HomingTurnSpeedDegreesPerSecond;
        public int RemainingReversals;
        public float FirstReverseAtSeconds = 1f;
        public float ReverseInterval = 1f;
        public bool ReturnToOwnerOnReverse = true;
        public bool ClearHitHistoryOnReverse = true;
        public bool OrbitOwner;
        public Vector2 OrbitCenterOffset;
        public float OrbitRadius = 1.2f;
        public float OrbitAngularSpeedDegreesPerSecond = 180f;
        public float OrbitAngleDegrees;
        public float RehitCooldownSeconds;
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
        private const string OneWayPlatformLayerName = "OneWayPlatform";
        private const float GroundSurfaceNormalThreshold = 0.55f;
        private const float SameLevelSurfaceTolerance = 3f / 24f;

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
        private bool _defaultFlipX;
        private bool _defaultVisualStateCaptured;
        private CircleCollider2D _collider;
        private Rigidbody2D _rigidbody;
        private GameObject _template;
        private HashSet<IDamageable> _hitHistory = new HashSet<IDamageable>();
        private readonly Dictionary<IDamageable, float> _nextTargetHitAllowedAt = new Dictionary<IDamageable, float>();
        private Transform _homingTarget;
        private float _nextReverseAt;

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
            CaptureDefaultVisualState();
        }

        private void Initialize(SkillProjectileLaunchData data, Vector2 direction, bool usePool, GameObject template)
        {
            EnsureComponents();
            CaptureDefaultVisualState();

            _data = data.Clone();
            _template = template;
            _usePool = usePool;
            _age = 0f;
            _despawning = false;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _nextReverseAt = Mathf.Max(0.01f, _data.FirstReverseAtSeconds);
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

            _rigidbody.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody.gravityScale = 0f;
            _rigidbody.simulated = true;
            _nextTargetHitAllowedAt.Clear();

            ApplyVisual();
            WorldRenderSorting.ConfigureAutoSorter(gameObject, RenderDepthCategory.HeroAttackVfx, transform.position.y);
            _collider.isTrigger = true;
            _collider.radius = ResolveColliderRadius();
            if (_data.OrbitOwner)
                UpdateOrbitPosition();
            if (_data.GroundMotion)
                SnapToGroundOrDespawn();
            if (_data.Homing)
                AcquireHomingTarget();
            ApplyFacingRotation();
            ResetProjectileVisualEffects();
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

            UpdateReversal();
            UpdateHoming(dt);
            if (_data.OrbitOwner)
            {
                _data.OrbitAngleDegrees += _data.OrbitAngularSpeedDegreesPerSecond * dt;
                UpdateOrbitPosition();
            }
            else
            {
                float travelDistance = Mathf.Max(0.01f, _data.Speed) * dt;
                if (_data.GroundMotion && TryBreakOnGroundObstacle(travelDistance))
                    return;

                transform.position += (Vector3)(_direction * travelDistance);
                if (_data.GroundMotion && !SnapToGroundOrDespawn())
                    return;
            }

            float spin = _data.RotationDegreesPerSecond;
            if (!Mathf.Approximately(spin, 0f))
                transform.Rotate(0f, 0f, spin * dt, Space.Self);

            ScanImmediateOverlaps();
        }

        private void UpdateOrbitPosition()
        {
            if (_data == null || _data.OwnerTransform == null)
                return;

            float angleRadians = _data.OrbitAngleDegrees * Mathf.Deg2Rad;
            Vector2 orbitOffset = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * Mathf.Max(0.01f, _data.OrbitRadius);
            Vector2 center = (Vector2)_data.OwnerTransform.position + _data.OrbitCenterOffset;
            transform.position = center + orbitOffset;
            _direction = new Vector2(-Mathf.Sin(angleRadians), Mathf.Cos(angleRadians)).normalized;
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

                if (target == null || IsTargetHitBlocked(target))
                    return false;

                RegisterTargetHit(target);
                DamageSnapshot snapshot = DealDamage(target);
                ExecuteOnHitEffects(target, targetTransform, snapshot);

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
                if (_data.GroundMotion && IsInLayerMask(other.gameObject.layer, _data.GroundLayer))
                    return false;

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

        private bool IsTargetHitBlocked(IDamageable target)
        {
            if (target == null || _data == null)
                return true;

            float cooldown = Mathf.Max(0f, _data.RehitCooldownSeconds);
            if (cooldown <= 0f)
                return _hitHistory.Contains(target);

            return _nextTargetHitAllowedAt.TryGetValue(target, out float nextAllowedAt) && Time.time < nextAllowedAt;
        }

        private void RegisterTargetHit(IDamageable target)
        {
            if (target == null || _data == null)
                return;

            float cooldown = Mathf.Max(0f, _data.RehitCooldownSeconds);
            if (cooldown <= 0f)
            {
                _hitHistory.Add(target);
                return;
            }

            _nextTargetHitAllowedAt[target] = Time.time + cooldown;
        }

        private DamageSnapshot DealDamage(IDamageable target)
        {
            if (_data == null || target == null || _data.OwnerStats == null || _data.Step == null)
                return null;

            IStatsProvider scopedStats = BuildScopedStatsProvider(target);
            DamageSnapshot snapshot = DamageCalculator.CreateDamageSnapshot(
                scopedStats,
                Mathf.Max(0f, _data.DamageMultiplier),
                _data.DamageContext,
                _data.Step.DamageConversions);
            snapshot.Source = _data.OwnerStats;
            target.TakeDamage(snapshot);
            TryApplyAilmentsFromHit(scopedStats, target, snapshot);
            return snapshot;
        }

        private void ExecuteOnHitEffects(IDamageable primaryTarget, Transform primaryTransform, DamageSnapshot sourceSnapshot)
        {
            if (_data?.Step?.OnHitEffects == null || _data.Step.OnHitEffects.Count == 0 || sourceSnapshot == null)
                return;

            Vector3 origin = primaryTransform != null ? primaryTransform.position : transform.position;
            for (int i = 0; i < _data.Step.OnHitEffects.Count; i++)
            {
                SkillOnHitEffectRule rule = _data.Step.OnHitEffects[i];
                if (rule == null)
                    continue;

                switch (rule.Type)
                {
                    case SkillOnHitEffectType.SpawnVfxDamageCircle:
                        StartCoroutine(RunOnHitVfxDamageCircle(rule, primaryTarget, origin, sourceSnapshot));
                        break;
                }
            }
        }

        private IEnumerator RunOnHitVfxDamageCircle(
            SkillOnHitEffectRule rule,
            IDamageable primaryTarget,
            Vector3 origin,
            DamageSnapshot sourceSnapshot)
        {
            float lifetime = Mathf.Max(0.01f, rule.Lifetime);
            float scale = Mathf.Max(0.01f, rule.ScaleMultiplier);
            GameObject vfx = null;
            if (rule.VfxPrefab != null)
            {
                vfx = Instantiate(rule.VfxPrefab, origin, Quaternion.identity);
                vfx.transform.localScale = new Vector3(
                    Mathf.Abs(vfx.transform.localScale.x) * scale,
                    Mathf.Abs(vfx.transform.localScale.y) * scale,
                    vfx.transform.localScale.z);

                Animator anim = vfx.GetComponentInChildren<Animator>();
                if (anim != null)
                    anim.speed = SkillVFX.GetAnimatorPlaybackDurationAtSpeedOne(anim, lifetime) / lifetime;

                AutoDestroyVFX autoDestroy = AutoDestroyVFX.Ensure(vfx);
                if (autoDestroy != null)
                    autoDestroy.Initialize(lifetime, rule.FadeOutEnabled, rule.FadeOutStartLifePercent, rule.FadeStartAlphaMultiplier);
            }

            float delay = lifetime * Mathf.Clamp01(rule.HitAtLifePercent);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_data == null)
                yield break;

            Vector2 center = vfx != null ? (Vector2)vfx.transform.position : (Vector2)origin;
            float radius = Mathf.Max(0.01f, rule.Radius) * scale;
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, _data.TargetLayer);
            DamageSnapshot scaledSnapshot = CloneScaledSnapshot(sourceSnapshot, Mathf.Max(0f, rule.DamageMultiplier));
            var uniqueTargets = new HashSet<IDamageable>();
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || IsOwner(hit.transform))
                    continue;

                if (!TryResolveDamageable(hit.transform, out IDamageable target, out _))
                    continue;

                if (target == null || !uniqueTargets.Add(target))
                    continue;

                if (rule.ExcludePrimaryTarget && ReferenceEquals(target, primaryTarget))
                    continue;

                target.TakeDamage(CloneScaledSnapshot(scaledSnapshot, 1f));
            }
        }

        private IStatsProvider BuildScopedStatsProvider(IDamageable target)
        {
            IStatsProvider weaponStats = WeaponHandStatScope.ForSkill(_data.OwnerStats, _data.SkillSlotIndex);
            StepEntry step = _data.Step;
            if ((step.ScopedStatModifiers == null || step.ScopedStatModifiers.Count == 0) &&
                (step.TargetAilmentStackModifiers == null || step.TargetAilmentStackModifiers.Count == 0))
                return weaponStats;

            var modifiers = new List<SerializableStatModifier>();
            if (step.ScopedStatModifiers != null)
                modifiers.AddRange(step.ScopedStatModifiers);

            AppendTargetAilmentStackModifiers(step, target, modifiers);
            return modifiers.Count > 0 ? new ScopedStatsProvider(weaponStats, modifiers) : weaponStats;
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
            if (_data.Homing)
                AcquireHomingTarget();
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
            {
                if (_data.Homing)
                    AcquireHomingTarget();
                return true;
            }

            if (_data.RemainingPierces <= 0)
                return false;

            _data.RemainingPierces--;
            if (_data.Homing)
                AcquireHomingTarget();
            return true;
        }

        private void UpdateReversal()
        {
            if (_data == null || _data.RemainingReversals <= 0 || _age < _nextReverseAt)
                return;

            _data.RemainingReversals--;
            if (_data.ReturnToOwnerOnReverse && _data.OwnerTransform != null)
            {
                Vector2 toOwner = (Vector2)_data.OwnerTransform.position - (Vector2)transform.position;
                if (toOwner.sqrMagnitude > 0.0001f)
                    _direction = toOwner.normalized;
                else
                    _direction = -_direction;
            }
            else
            {
                _direction = -_direction;
            }

            if (_data.ClearHitHistoryOnReverse)
            {
                _hitHistory.Clear();
                _nextTargetHitAllowedAt.Clear();
            }

            _homingTarget = null;
            if (_data.Homing)
                AcquireHomingTarget();

            _nextReverseAt += Mathf.Max(0.01f, _data.ReverseInterval);
            ApplyFacingRotation();
        }

        private void UpdateHoming(float dt)
        {
            if (_data == null || !_data.Homing)
                return;

            if (_homingTarget == null || !IsValidHomingTarget(_homingTarget))
                AcquireHomingTarget();

            if (_homingTarget == null)
                return;

            Vector2 desired = (Vector2)_homingTarget.position - (Vector2)transform.position;
            if (desired.sqrMagnitude <= 0.0001f)
                return;

            desired.Normalize();
            float turnSpeed = _data.HomingTurnSpeedDegreesPerSecond;
            if (turnSpeed <= 0f)
            {
                _direction = desired;
            }
            else
            {
                float maxRadians = turnSpeed * Mathf.Deg2Rad * dt;
                _direction = Vector3.RotateTowards(_direction, desired, maxRadians, 0f).normalized;
            }

            ApplyFacingRotation();
        }

        private bool SnapToGroundOrDespawn()
        {
            if (_data == null || !_data.GroundMotion)
                return true;

            if (!TryFindBestGroundSurface(out RaycastHit2D hit))
            {
                Despawn();
                return false;
            }

            SnapVisualBottomToGround(hit.point.y + _data.GroundYOffset);
            return true;
        }

        private bool TryBreakOnGroundObstacle(float travelDistance)
        {
            if (_data == null || !_data.GroundMotion || !_data.BreakOnGroundObstacles)
                return false;

            Vector2 direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector2.right;
            float probeDistance = Mathf.Max(0.01f, travelDistance) + Mathf.Max(0.02f, _collider != null ? _collider.radius : 0.02f);
            Vector2 origin = TryGetVisualBounds(out Bounds bounds) ? (Vector2)bounds.center : (Vector2)transform.position;
            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction, probeDistance, _data.GroundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || IsOwner(hit.transform) || hit.normal.y > 0.5f || IsOneWayPlatformLayer(hit.collider.gameObject.layer))
                    continue;

                Despawn();
                return true;
            }

            return false;
        }

        private bool TryFindBestGroundSurface(out RaycastHit2D bestHit)
        {
            bestHit = default;
            float currentBottomY = TryGetVisualBounds(out Bounds bounds)
                ? bounds.min.y
                : transform.position.y;

            float upDistance = Mathf.Max(0.01f, _data.GroundSnapUp);
            float downDistance = Mathf.Max(0.01f, _data.GroundSnapDown);
            float castDistance = upDistance + downDistance;
            Vector2 direction = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector2.right;

            float centerX = TryGetVisualBounds(out bounds) ? bounds.center.x : transform.position.x;
            float forwardProbe = TryGetVisualBounds(out bounds)
                ? bounds.extents.x + Mathf.Max(0.01f, _collider != null ? _collider.radius * 0.25f : 0.01f)
                : Mathf.Max(0.02f, _collider != null ? _collider.radius : 0.02f);

            bool found = false;
            float bestScore = float.MaxValue;
            ProbeGroundSurfaceAtX(centerX, currentBottomY, upDistance, castDistance, ref found, ref bestHit, ref bestScore);
            ProbeGroundSurfaceAtX(centerX + direction.x * forwardProbe, currentBottomY, upDistance, castDistance, ref found, ref bestHit, ref bestScore);

            return found;
        }

        private void ProbeGroundSurfaceAtX(
            float x,
            float currentBottomY,
            float upDistance,
            float castDistance,
            ref bool found,
            ref RaycastHit2D bestHit,
            ref float bestScore)
        {
            Vector2 rayOrigin = new Vector2(x, currentBottomY + upDistance);
            RaycastHit2D[] hits = Physics2D.RaycastAll(rayOrigin, Vector2.down, castDistance, _data.GroundLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                if (hit.collider == null || hit.collider.isTrigger || IsOwner(hit.transform))
                    continue;
                if (hit.normal.y < GroundSurfaceNormalThreshold)
                    continue;

                float surfaceY = hit.point.y + _data.GroundYOffset;
                float levelDelta = Mathf.Abs(surfaceY - currentBottomY);
                bool sameLevel = levelDelta <= SameLevelSurfaceTolerance;
                float verticalPenalty = surfaceY > currentBottomY + SameLevelSurfaceTolerance
                    ? (surfaceY - currentBottomY) * 100f
                    : Mathf.Max(0f, currentBottomY - surfaceY);
                float score = sameLevel ? levelDelta : 10f + verticalPenalty + hit.distance * 0.01f;
                if (found && score >= bestScore)
                    continue;

                found = true;
                bestHit = hit;
                bestScore = score;
            }
        }

        private void SnapVisualBottomToGround(float groundY)
        {
            if (!TryGetVisualBounds(out Bounds bounds))
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                return;
            }

            float deltaY = groundY - bounds.min.y;
            transform.position = new Vector3(transform.position.x, transform.position.y + deltaY, transform.position.z);
        }

        private void AcquireHomingTarget()
        {
            _homingTarget = null;
            if (_data == null)
                return;

            Vector2 origin = transform.position;
            float bestDistance = float.MaxValue;
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, Mathf.Max(0.1f, _data.HomingSearchRadius), _data.TargetLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || IsOwner(hit.transform))
                    continue;

                if (!TryResolveDamageable(hit.transform, out IDamageable damageable, out Transform targetTransform))
                    continue;

                if (damageable == null || _hitHistory.Contains(damageable) || targetTransform == null || !IsTargetOnScreen(targetTransform))
                    continue;

                float sqrDistance = ((Vector2)targetTransform.position - origin).sqrMagnitude;
                if (sqrDistance >= bestDistance)
                    continue;

                bestDistance = sqrDistance;
                _homingTarget = targetTransform;
            }
        }

        private bool IsValidHomingTarget(Transform target)
        {
            if (target == null || IsOwner(target))
                return false;

            if (!TryResolveDamageable(target, out IDamageable damageable, out _))
                return false;

            return damageable != null && !_hitHistory.Contains(damageable) && IsTargetOnScreen(target);
        }

        private static bool IsTargetOnScreen(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null || target == null)
                return true;

            Vector3 viewport = cam.WorldToViewportPoint(target.position);
            return viewport.z >= 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
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

        private static DamageSnapshot CloneScaledSnapshot(DamageSnapshot source, float multiplier)
        {
            multiplier = Mathf.Max(0f, multiplier);
            return new DamageSnapshot(source.Source)
            {
                Physical = source.Physical * multiplier,
                Fire = source.Fire * multiplier,
                Cold = source.Cold * multiplier,
                Lightning = source.Lightning * multiplier,
                IsCrit = source.IsCrit,
                CritMultiplier = source.CritMultiplier,
                IsDirectHit = source.IsDirectHit
            };
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

        private static bool IsOneWayPlatformLayer(int layer)
        {
            int platformLayer = LayerMask.NameToLayer(OneWayPlatformLayerName);
            return platformLayer >= 0 && layer == platformLayer;
        }

        private void ApplyVisual()
        {
            if (_spriteRenderer == null)
                return;

            _spriteRenderer.sprite = _data.OverrideSprite != null ? _data.OverrideSprite : _defaultSprite;
            _spriteRenderer.enabled = _spriteRenderer.sprite != null;
            _spriteRenderer.flipX = _defaultFlipX;
        }

        private float ResolveColliderRadius()
        {
            float scale = _data != null ? Mathf.Max(0.02f, _data.HitRadius) : 1f;
            if (_spriteRenderer == null || _spriteRenderer.sprite == null)
                return 0.18f * scale;

            Bounds bounds = _spriteRenderer.bounds;
            float worldDiameter = Mathf.Max(bounds.size.x, bounds.size.y);
            if (worldDiameter <= 0.0001f)
                return 0.18f * scale;

            float worldRadius = worldDiameter * 0.5f * scale;
            Vector3 lossyScale = transform.lossyScale;
            float rootScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y), 0.0001f);
            return Mathf.Max(0.02f, worldRadius / rootScale);
        }

        private void ApplyFacingRotation()
        {
            if (_data != null && _data.GroundMotion)
            {
                transform.rotation = Quaternion.identity;
                if (_spriteRenderer != null)
                    _spriteRenderer.flipX = _defaultFlipX ^ (_direction.x < -0.0001f);
                return;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _defaultFlipX;

            float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void ResetProjectileVisualEffects()
        {
            var visualEffects = GetComponent<ProjectileVisualEffects>() ?? GetComponentInChildren<ProjectileVisualEffects>(true);
            if (visualEffects != null)
                visualEffects.ResetVisualState();
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

        private void CaptureDefaultVisualState()
        {
            if (_spriteRenderer == null)
                return;

            if (_defaultSprite == null)
                _defaultSprite = _spriteRenderer.sprite;

            if (_defaultVisualStateCaptured)
                return;

            _defaultFlipX = _spriteRenderer.flipX;
            _defaultVisualStateCaptured = true;
        }

        private bool TryGetVisualBounds(out Bounds bounds)
        {
            bounds = default;
            if (_spriteRenderer == null || !_spriteRenderer.enabled || _spriteRenderer.sprite == null)
                return false;

            bounds = _spriteRenderer.bounds;
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f;
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
            _nextTargetHitAllowedAt.Clear();
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
