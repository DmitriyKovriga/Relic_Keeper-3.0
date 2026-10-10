using System;
using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills.PassiveTree
{
    public enum PassiveTriState
    {
        Any,
        Yes,
        No
    }

    /// <summary>
    /// Describes which gameplay event counts: "the owner dealt a critical hit to a poisoned target",
    /// "the owner took a hit", "the owner evaded". Shared by triggers and "recently" conditions.
    /// </summary>
    [Serializable]
    public class PassiveEventFilter
    {
        public GameplayEventType Event = GameplayEventType.DamageDealt;
        [Tooltip("Player = the character that allocated the node.")]
        public StatusEventSubject Subject = StatusEventSubject.CarrierAsSource;
        [Tooltip("Direct hit = damage from an attack or projectile, not damage over time.")]
        public PassiveTriState DirectHit = PassiveTriState.Any;
        public PassiveTriState Crit = PassiveTriState.Any;

        [Tooltip("The other participant must carry this ailment when the event happens (before this hit applies its own ailments).")]
        public bool RequireOtherAilment;
        public AilmentType OtherAilment = AilmentType.Poison;
        [Min(1)] public int MinAilmentStacks = 1;

        public bool Matches(GameplayEventContext context, GameObject owner)
        {
            if (context == null || owner == null || context.Type != Event)
                return false;

            bool subjectMatches = Subject switch
            {
                StatusEventSubject.CarrierAsSource => context.Source == owner,
                StatusEventSubject.CarrierAsTarget => context.Target == owner,
                StatusEventSubject.CarrierAsSourceOrTarget => context.HasParticipant(owner),
                StatusEventSubject.Any => true,
                _ => false
            };
            if (!subjectMatches)
                return false;

            bool isDirectHit = context.Damage != null && context.Damage.IsDirectHit;
            if (!MatchesTriState(DirectHit, isDirectHit))
                return false;

            bool isCrit = context.Damage != null && context.Damage.IsCrit;
            if (!MatchesTriState(Crit, isCrit))
                return false;

            if (RequireOtherAilment)
            {
                GameObject other = context.Source == owner ? context.Target : context.Source;
                if (other == null || !AilmentController.TryResolve(other.transform, out AilmentController ailments) || ailments == null)
                    return false;
                if (ailments.GetStackCount(OtherAilment) < Mathf.Max(1, MinAilmentStacks))
                    return false;
            }

            return true;
        }

        private static bool MatchesTriState(PassiveTriState state, bool value)
        {
            return state == PassiveTriState.Any || (state == PassiveTriState.Yes) == value;
        }
    }

    public enum PassiveConditionKind
    {
        [InspectorName("Event timing")]
        EventRecency,
        [InspectorName("Resource level")]
        ResourceThreshold,
        [InspectorName("Distance to enemy")]
        TargetDistance
    }

    public enum PassiveRecencyMode
    {
        [InspectorName("Not recently")]
        NotRecently,
        [InspectorName("Recently")]
        Recently
    }

    public enum PassiveResource
    {
        Health,
        Mana
    }

    public enum PassiveComparison
    {
        [InspectorName("At least")]
        AtLeast,
        [InspectorName("At most")]
        AtMost
    }

    [Serializable]
    public class PassiveCondition
    {
        public PassiveConditionKind Kind = PassiveConditionKind.EventRecency;

        public PassiveRecencyMode Recency = PassiveRecencyMode.NotRecently;
        public PassiveEventFilter Event = new PassiveEventFilter
        {
            Event = GameplayEventType.DamageTaken,
            Subject = StatusEventSubject.CarrierAsTarget
        };
        [Min(0.05f)] public float WindowSeconds = 4f;

        public PassiveResource Resource = PassiveResource.Health;
        public PassiveComparison Comparison = PassiveComparison.AtLeast;
        [Range(0f, 100f)] public float ThresholdPercent = 100f;
        // Hub grid: 24 pixels per unit, so one 24-pixel cell is one world unit.
        public const int DistanceCellPixels = 24;
        public const float DistanceCellWorldSize = 1f;
        [Min(0f), InspectorName("Distance (cells)")]
        [Tooltip("Distance in 24-pixel cells. At the game's 24 PPU, one cell equals one world unit.")]
        public float DistanceUnits = 8f;
        public float DistanceInWorldUnits => DistanceUnits * DistanceCellWorldSize;
    }

    public enum PassiveConditionalDestination
    {
        [InspectorName("Player")]
        Owner,
        [InspectorName("Enemy")]
        EnemyDamageTaken
    }

    /// <summary>Stat modifiers that exist only while the condition holds.</summary>
    [Serializable]
    public class PassiveConditionalModifiers
    {
        public PassiveConditionalDestination Destination;
        public PassiveCondition Condition = new PassiveCondition();
        public List<SerializableStatModifier> Modifiers = new List<SerializableStatModifier>();
    }

    public enum PassiveTriggerAction
    {
        [InspectorName("Reduce skill cooldown")]
        ReduceSkillCooldown,
        [InspectorName("Apply timed buff")]
        ApplyBuff,
        [InspectorName("Restore resource")]
        RestoreResource
    }

    public enum PassiveCooldownTarget
    {
        [InspectorName("Random skill on cooldown")]
        RandomOnCooldown,
        [InspectorName("All skills")]
        AllSkills,
        [InspectorName("Specific slot")]
        Slot
    }

    public enum PassiveBuffStacking
    {
        [InspectorName("Refresh (does not stack)")]
        Refresh,
        [InspectorName("Independent stacks")]
        IndependentStacks,
        [InspectorName("Ignore while active")]
        IgnoreWhileActive
    }

    /// <summary>"When [event] happens: [action]" with optional chance and internal cooldown.</summary>
    [Serializable]
    public class PassiveTriggeredEffect
    {
        public PassiveEventFilter Trigger = new PassiveEventFilter();
        [Range(0f, 100f)] public float ChancePercent = 100f;
        [Min(0f)]
        [Tooltip("Minimum seconds between two activations. 0 = every matching event.")]
        public float InternalCooldownSeconds;

        public PassiveTriggerAction Action = PassiveTriggerAction.ReduceSkillCooldown;

        [Min(0f)] public float CooldownSeconds = 1f;
        public PassiveCooldownTarget CooldownTarget = PassiveCooldownTarget.RandomOnCooldown;
        [Range(0, 5)] public int SkillSlot;

        [Min(0.05f)] public float BuffDurationSeconds = 5f;
        public List<SerializableStatModifier> BuffModifiers = new List<SerializableStatModifier>();
        [Tooltip("Свечение безымянного бафа. None выключает подсветку.")]
        public StatusAuraColor BuffAuraColor = StatusAuraColor.Green;
        [Tooltip("Optional authored effect with an icon for the HUD. When set, it is applied instead of the inline modifiers.")]
        public StatusEffectSO StatusEffect;
        public PassiveBuffStacking Stacking = PassiveBuffStacking.Refresh;
        [Min(1)] public int MaxStacks = 1;

        public PassiveResource Resource = PassiveResource.Health;
        public float RestoreAmount = 5f;
        public bool RestoreAsPercentOfMax;
    }

    public enum PassiveHitScalingSource
    {
        [InspectorName("Projectile flight time (s)")]
        ProjectileFlightSeconds,
        [InspectorName("Projectile flight distance (units)")]
        ProjectileFlightDistance
    }

    /// <summary>Per-hit modifiers that grow with a property of the hit, e.g. how long the projectile flew.</summary>
    [Serializable]
    public class PassiveHitScalingRule
    {
        public PassiveHitScalingSource Source = PassiveHitScalingSource.ProjectileFlightSeconds;
        [Min(0.0001f)] public float SourcePerStep = 0.1f;
        public bool UseWholeSteps;
        [Min(0f)]
        [Tooltip("0 = no cap.")]
        public float MaxSteps = 10f;
        public List<SerializableStatModifier> ModifiersPerStep = new List<SerializableStatModifier>();

        public float CalculateSteps(in PassiveHitContext context)
        {
            float source = Source switch
            {
                PassiveHitScalingSource.ProjectileFlightSeconds => context.IsProjectile ? context.FlightSeconds : 0f,
                PassiveHitScalingSource.ProjectileFlightDistance => context.IsProjectile ? context.FlightDistance : 0f,
                _ => 0f
            };
            if (source <= 0f || SourcePerStep <= 0.0001f)
                return 0f;

            float steps = source / SourcePerStep;
            if (UseWholeSteps)
                steps = Mathf.Floor(steps + 0.0001f);
            if (MaxSteps > 0f)
                steps = Mathf.Min(steps, MaxSteps);
            return steps;
        }
    }

    public readonly struct PassiveHitContext
    {
        public readonly bool IsProjectile;
        public readonly float FlightSeconds;
        public readonly float FlightDistance;

        public PassiveHitContext(bool isProjectile, float flightSeconds, float flightDistance)
        {
            IsProjectile = isProjectile;
            FlightSeconds = flightSeconds;
            FlightDistance = flightDistance;
        }

        public static PassiveHitContext Projectile(float flightSeconds, float flightDistance)
        {
            return new PassiveHitContext(true, flightSeconds, flightDistance);
        }
    }
}
