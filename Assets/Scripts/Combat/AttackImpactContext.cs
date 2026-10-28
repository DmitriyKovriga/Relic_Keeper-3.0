using Scripts.Items;
using Scripts.Skills;
using Scripts.Skills.Steps;
using UnityEngine;

namespace Scripts.Combat
{
    public enum AttackImpactWeight { Light, Normal, Heavy }

    /// <summary>Captured at acceptance, retained by delayed projectiles and their clones.</summary>
    public sealed class AttackImpactContext
    {
        private static ulong _nextId;
        public ulong CastId { get; }
        public string SkillId { get; }
        public WeaponItemSO Weapon { get; }
        public int SkillSlot { get; }
        public SkillHitStopGate HitStop { get; }
        private bool _cameraImpulseConsumed;

        // Shared by projectile clones, repeat passes and every target of this accepted cast.
        public bool TryConsumeCameraImpulse()
        {
            if (_cameraImpulseConsumed) return false;
            _cameraImpulseConsumed = true;
            return true;
        }

        public AttackImpactContext(SkillDataSO skill, int slot, WeaponItemSO weapon = null,
            SkillHitStopGate hitStop = null, bool enableHitStop = true)
        {
            CastId = ++_nextId;
            SkillId = skill != null ? skill.ID : null;
            Weapon = weapon;
            SkillSlot = slot;
            HitStop = enableHitStop ? hitStop ?? new SkillHitStopGate(skill?.Recipe != null ? skill.Recipe.HitStopFrames : 3) : null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetIds() => _nextId = 0;
    }

    /// <summary>Attack-side hints only. No receiver's final damage or contact is written into a shared snapshot.</summary>
    public sealed class AttackImpactContact
    {
        public AttackImpactContext Attack { get; }
        public Vector2 Origin { get; }
        public Vector2 Direction { get; }
        public AttackImpactWeight Weight { get; }
        public bool HasAuthoredImpact { get; }

        public AttackImpactContact(AttackImpactContext attack, Vector2 origin, Vector2 direction,
            float damageMultiplier = 1f, bool hasAuthoredImpact = false)
        {
            Attack = attack;
            Origin = origin;
            Direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.zero;
            Weight = damageMultiplier >= 2f ? AttackImpactWeight.Heavy
                : damageMultiplier < .75f ? AttackImpactWeight.Light : AttackImpactWeight.Normal;
            HasAuthoredImpact = hasAuthoredImpact;
        }

        public static bool HasAuthoredContact(StepEntry step)
        {
            if (step == null) return false;
            if (step.GetObject<GameObject>("ImpactVfxPrefab") != null ||
                step.GetObject<GameObject>("CriticalImpactVfxPrefab") != null ||
                step.GetObject<GameObject>("SawImpactPrefab") != null)
                return true;
            if (step.OnHitEffects != null)
                foreach (var rule in step.OnHitEffects)
                    if (rule != null && rule.VfxPrefab != null) return true;
            return false;
        }
    }
}
