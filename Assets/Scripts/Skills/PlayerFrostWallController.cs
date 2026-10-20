using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Skills.Projectiles;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerStats))]
    public sealed class PlayerFrostWallController : MonoBehaviour
    {
        private static readonly List<PlayerFrostWallController> Walls = new List<PlayerFrostWallController>();
        private PlayerStats _stats;
        private StepEntry _step;
        private SkillDataSO _skill;
        private FrostWallVisual _visual;
        public bool IsActive { get; private set; }
        public bool IsEmpowered { get; private set; }
        public float RemainingDuration { get; private set; }
        public float DamageMultiplier { get; private set; }
        public Bounds BarrierBounds { get; private set; }
        public int Retaliations { get; private set; }

        public bool Begin(SkillDataSO skill, StepEntry step)
        {
            _stats = GetComponent<PlayerStats>();
            if (!isActiveAndEnabled || skill == null || step == null || _stats.Health == null || _stats.Health.Current <= 0) return false;
            var body = GetComponent<Collider2D>();
            float feet = body != null ? body.bounds.min.y : transform.position.y;
            float facing = transform.localScale.x < 0 ? -1 : 1;
            Vector2 size = new Vector2(step.GetFloat("Width", .5f), step.GetFloat("Height", 2));
            Vector2 start = new Vector2(transform.position.x, feet + size.y * .5f);
            float offset = Mathf.Max(size.x, step.GetFloat("OffsetX", 1.1f));
            int world = step.GetInt("WorldLayerMask", 1 << 6);
            // Reject blocked placement before spending a charge or replacing a wall.
            if (Physics2D.BoxCast(start, size * .95f, 0, Vector2.right * facing, offset, world).collider != null) return false;
            End(); _step = step; _skill = skill; Retaliations = 0;
            BarrierBounds = new Bounds(start + Vector2.right * facing * offset, size);
            var shield = GetComponent<MysticShieldController>();
            IsEmpowered = shield != null && shield.TryConsumeCharges(1, out int consumed) && consumed == 1;
            float effectiveness = IsEmpowered ? MysticShieldSkillEffectiveness.Resolve(_stats) : 1;
            DamageMultiplier = step.GetFloat("DamageMultiplier", .5f) * (IsEmpowered ? step.GetFloat("EmpoweredDamageMultiplier", 2) * effectiveness : 1);
            RemainingDuration = EffectDurationCalculator.Resolve(step.GetFloat("Duration", 5)
                * (IsEmpowered ? step.GetFloat("EmpoweredDurationMultiplier", 2) * effectiveness : 1), _stats);
            var prefab = step.GetObject<GameObject>("FieldVfxPrefab");
            if (prefab != null)
            {
                var instance = Instantiate(prefab, BarrierBounds.center, Quaternion.identity);
                _visual = instance.GetComponent<FrostWallVisual>();
                if (_visual != null) _visual.Initialize(IsEmpowered, size, feet);
                else DestroyObject(instance);
            }
            IsActive = true; Walls.Add(this); Tick(0); return true;
        }

        private void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (!IsActive) return;
            RemainingDuration = Mathf.Max(0, RemainingDuration - Mathf.Max(0, dt));
            if (_stats == null || _stats.Health == null || _stats.Health.Current <= 0 || RemainingDuration <= 0) End();
        }

        // First barrier along the segment wins. Bounds are expanded by projectile radius.
        public static bool TryIntercept(Vector2 source, Vector2 destination, float radius, GameObject attacker, out Vector2 contact)
        {
            contact = destination; PlayerFrostWallController nearest = null;
            Vector2 delta = destination - source; float distance = delta.magnitude, best = distance + .0001f;
            for (int i = Walls.Count - 1; i >= 0; i--)
            {
                var wall = Walls[i];
                if (wall == null || !wall.IsActive) { Walls.RemoveAt(i); continue; }
                if (!wall.isActiveAndEnabled || wall._stats.Health.Current <= 0) continue;
                var bounds = wall.BarrierBounds; bounds.Expand(new Vector3(radius * 2, radius * 2, 1));
                float hit = 0;
                bool intersects = bounds.Contains(source) || (distance > .0001f && bounds.IntersectRay(new Ray(source, delta.normalized), out hit) && hit <= distance);
                if (!intersects || hit >= best) continue;
                best = hit; nearest = wall;
            }
            if (nearest == null) return false;
            contact = source + (distance > .0001f ? delta.normalized * best : Vector2.zero);
            nearest.Retaliate(attacker, contact); return true;
        }

        public static bool TryInterceptAttack(GameObject attacker, Transform target)
        {
            if (attacker == null || target == null) return false;
            var sourceBody = attacker.GetComponent<Collider2D>();
            var targetBody = target.GetComponent<Collider2D>() ?? target.GetComponentInParent<Collider2D>();
            Vector2 from = sourceBody != null ? sourceBody.bounds.center : attacker.transform.position;
            Vector2 to = targetBody != null ? targetBody.bounds.center : target.position;
            return TryIntercept(from, to, 0, attacker, out _);
        }

        public static bool TryInterceptHitbox(GameObject attacker, Vector2 center, Vector2 size)
        {
            var attack = new Bounds(center, new Vector3(size.x, size.y, 1));
            PlayerFrostWallController nearest = null; float best = float.PositiveInfinity;
            foreach (var wall in Walls)
            {
                if (wall == null || !wall.IsActive || !wall.isActiveAndEnabled || wall._stats.Health.Current <= 0 || !attack.Intersects(wall.BarrierBounds)) continue;
                float distance = ((Vector2)wall.BarrierBounds.center - (Vector2)attacker.transform.position).sqrMagnitude;
                if (distance < best) { best = distance; nearest = wall; }
            }
            if (nearest == null) return false;
            nearest.Retaliate(attacker, nearest.BarrierBounds.center); return true;
        }

        private void Retaliate(GameObject attacker, Vector2 contact)
        {
            Retaliations++; _visual?.Impact(contact);
            if (attacker == null) return;
            var body = attacker.GetComponent<Collider2D>();
            Vector2 target = body != null ? body.bounds.center : attacker.transform.position;
            Vector2 direction = target - contact;
            if (direction.sqrMagnitude < .0001f) direction = Vector2.right;
            SkillProjectile.Spawn(new SkillProjectileLaunchData
            {
                OwnerStats = _stats, OwnerTransform = transform, SkillSlotIndex = 0, Skill = _skill,
                Step = _step, DamageContext = new DamageContext(StatContextTagFlags.Spell | StatContextTagFlags.Projectile),
                DamageMultiplier = DamageMultiplier, TargetLayer = 1 << 7, WorldLayer = 1 << 6,
                OverrideSprite = _step.GetObject<Sprite>("ShardSprite"),
                Speed = ProjectileSpeedResolver.Resolve(_step.GetFloat("ShardSpeed", 12), _stats),
                Lifetime = _step.GetFloat("ShardLifetime", 3), HitRadius = .12f,
                RotationDegreesPerSecond = 0, IgnoreFork = true, IgnoreChain = true
            }, contact + direction.normalized * .15f, direction.normalized);
        }

        public void End()
        {
            IsActive = false; RemainingDuration = 0; Walls.Remove(this);
            if (_visual != null) { DestroyObject(_visual.gameObject); _visual = null; }
        }
        private static void DestroyObject(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable() => End();
    }
}
