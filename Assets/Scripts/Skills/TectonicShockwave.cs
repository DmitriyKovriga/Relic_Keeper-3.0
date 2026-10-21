using System;
using System.Collections.Generic;
using Scripts.Combat;
using Scripts.Skills.Steps;
using Scripts.Skills.Visuals;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills
{
    // Captured on the strike, so equipment changes during flight cannot change a collision's damage.
    public sealed class TectonicShockwave
    {
        private readonly PlayerStats _owner;
        private readonly IStatsProvider _stats;
        private readonly StepEntry _step;
        private readonly Vector2 _size;
        private readonly int _targetMask, _worldMask;
        private readonly DamageSnapshot _damage;
        public float CollisionRadius { get; }

        public TectonicShockwave(PlayerStats owner, IStatsProvider stats, StepEntry step, int targetMask)
        {
            _owner = owner; _stats = new FrozenStats(stats); _step = step; _targetMask = targetMask;
            _worldMask = step.GetInt("WorldLayerMask", 1 << 6);
            float aoe = Mathf.Max(.01f, 1 + _stats.GetValue(StatType.AreaOfEffect) / 100);
            _size = SkillAoeScale.ScaleSize(Vector2.one * (2 * Mathf.Max(.05f, step.GetFloat("ShockwaveRadius", 1.5f))), aoe);
            CollisionRadius = Mathf.Max(0, step.GetFloat("CollisionRadius", .1f)) * aoe;
            float multiplier = Mathf.Max(0, step.GetFloat("ShockwaveDamageMultiplier", 2)) * Mathf.Max(0, _stats.GetValue(StatType.ShockwaveDamage)) / 100;
            // Roll through the original hand scope while it still knows the equipped weapon's range.
            _damage = DamageCalculator.CreateDamageSnapshot(stats, multiplier,
                new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Melee | StatContextTagFlags.Area), step.DamageConversions);
        }

        public EmpoweredPushback CreateCharge() => new EmpoweredPushback(CollisionRadius, Detonate);

        public void Detonate(Vector2 point)
        {
            if (_owner == null || !_owner.gameObject.activeInHierarchy || _owner.Health == null || _owner.Health.Current <= 0) return;
            var targets = new HashSet<IDamageable>();
            foreach (var hit in Physics2D.OverlapBoxAll(point, _size, 0, _targetMask))
            {
                if (hit == null || hit.transform.IsChildOf(_owner.transform)) continue;
                var target = hit.GetComponentInParent<IDamageable>();
                if (target == null || target is Scripts.Enemies.EnemyHealth health && health.IsDead) continue;
                Vector2 destination = hit.bounds.ClosestPoint(point);
                Vector2 ray = destination - point;
                if (ray.x * ray.x / (_size.x * _size.x * .25f) + ray.y * ray.y / (_size.y * _size.y * .25f) > 1 || !targets.Add(target)) continue;
                bool blocked = false;
                if (ray.sqrMagnitude > .0001f)
                    foreach (var wall in Physics2D.RaycastAll(point, ray.normalized, ray.magnitude, _worldMask))
                    {
                        if (wall.collider == null || wall.collider.isTrigger ||
                            wall.collider.GetComponentInParent<IDamageable>() == target) continue;
                        blocked = true; break;
                    }
                if (blocked) continue;
                var damage = new DamageSnapshot(_owner)
                {
                    Physical = _damage.Physical, Fire = _damage.Fire, Cold = _damage.Cold, Lightning = _damage.Lightning,
                    IsCrit = _damage.IsCrit, CritMultiplier = _damage.CritMultiplier,
                    ExecutionThreshold = _damage.ExecutionThreshold, HitOrigin = point
                };
                // No pushback charge is propagated by the secondary wave.
                if (target.TakeDamage(damage) && target is Component component)
                    AilmentController.TryApplyHitAilments(_stats, _owner, component.transform, damage);
            }
            TectonicPressVisual.Spawn(_step.GetObject<GameObject>("ShockwaveVfxPrefab"), point, _size, 1);
        }

        private sealed class FrozenStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _values = new Dictionary<StatType, CharacterStat>();
            public FrozenStats(IStatsProvider source)
            {
                foreach (StatType type in Enum.GetValues(typeof(StatType)))
                {
                    if (source.TryGetStat(type, out var original) && original != null)
                    {
                        var copy = new CharacterStat(original.BaseValue);
                        foreach (var modifier in original.Modifiers) copy.AddModifier(modifier);
                        _values[type] = copy;
                    }
                    else _values[type] = new CharacterStat(source.GetValue(type));
                }
            }
            public float GetValue(StatType type) => _values.TryGetValue(type, out var value) ? value.Value : 0;
            public bool TryGetStat(StatType type, out CharacterStat stat) => _values.TryGetValue(type, out stat);
        }
    }
}
