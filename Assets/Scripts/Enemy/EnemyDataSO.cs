using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using Scripts.Stats;

namespace Scripts.Enemies
{
    public enum EnemyAIType
    {
        GroundChaser = 0,
        AgileJumper = 1,
        StaticCaster = 2,
        KitingRanged = 3
    }

    public enum EnemyAttackDeliveryType
    {
        Melee = 0,
        Projectile = 1,
        Area = 2
    }

    public enum EnemyAttackDamageType
    {
        Physical = 0,
        Fire = 1,
        Cold = 2,
        Lightning = 3
    }

    public enum EnemyStatScalingMode
    {
        None = 0,
        FlatPerLevel = 1,
        PercentPerLevel = 2
    }

    [Serializable]
    public class EnemyStatEntry
    {
        public StatType Type;
        public float BaseValue;
        public EnemyStatScalingMode ScalingMode;
        public float ScalingValue;

        public float Evaluate(int level)
        {
            int clampedLevel = Mathf.Max(1, level);
            float value = BaseValue;

            switch (ScalingMode)
            {
                case EnemyStatScalingMode.FlatPerLevel:
                    value += ScalingValue * (clampedLevel - 1);
                    break;

                case EnemyStatScalingMode.PercentPerLevel:
                    value *= 1f + (ScalingValue / 100f) * (clampedLevel - 1);
                    break;
            }

            return value;
        }
    }

    [Serializable]
    public class EnemyPerceptionConfig
    {
        [Min(0f)] public float AggroRange = 6f;
        [Min(0f)] public float LoseTargetRange = 10f;
        public bool RequireLineOfSight;
    }

    [Serializable]
    public class EnemyMovementConfig
    {
        [Min(0f)] public float MoveSpeed = 2f;
        [Min(0f)] public float StopDistance = 0.85f;
        [Min(0f)] public float Acceleration = 20f;
        public bool CanJump;
        public bool CanUseJumpLinks;
        public bool CanFallFromPlatform;
        [Min(0f)] public float JumpForce = 8f;
        [Min(0f)] public float GroundCheckDistance = 0.15f;
        [Min(0f)] public float WallCheckDistance = 0.2f;
        [Min(0f)] public float LedgeCheckDistance = 0.35f;
    }

    [Serializable]
    public class EnemyAttackConfig
    {
        public EnemyAttackDeliveryType DeliveryType = EnemyAttackDeliveryType.Melee;
        public EnemyAttackDamageType DamageType = EnemyAttackDamageType.Physical;
        [Min(0f)] public float AttackRange = 1.1f;
        [Min(0f)] public float AttackCooldown = 1f;
        [Min(0f)] public float Windup = 0.15f;
        [Min(0f)] public float ActiveTime = 0.1f;
        [Min(0f)] public float Recovery = 0.25f;
        [Min(0f)] public float DamageMultiplier = 1f;
        public Vector2 HitboxSize = new Vector2(1.2f, 0.8f);
        public Vector2 HitboxOffset = new Vector2(0.8f, 0f);
    }

    [Serializable]
    public class EnemyBehaviourConfig
    {
        [Min(0f)] public float DecisionIntervalMin = 0.03f;
        [Min(0f)] public float DecisionIntervalMax = 0.08f;
        [Min(0f)] public float PostActionPauseMin = 0.05f;
        [Min(0f)] public float PostActionPauseMax = 0.12f;
        [Min(0f)] public float StopDistanceVariance = 0.1f;
        [Min(0f)] public float TurnDelayMin = 0.04f;
        [Min(0f)] public float TurnDelayMax = 0.1f;
        [Min(0f)] public float MissRecoveryMultiplier = 1.2f;
    }

    [Serializable]
    public class EnemyAnimationConfig
    {
        public RuntimeAnimatorController Controller;
        public string IdleStateName = "Idle";
        public string MoveStateName = "Walk";
        public string AttackStateName = "Attack";
    }

    [CreateAssetMenu(menuName = "RPG/Enemies/Enemy Data")]
    public class EnemyDataSO : ScriptableObject
    {
        [Header("Info")]
        public string ID;
        public string DisplayName;
        public EnemyEntity Prefab;
        public EnemyAIType AIType = EnemyAIType.GroundChaser;

        [Header("Legacy Base Stats")]
        [Tooltip("Старый формат. Оставлен для обратной совместимости. Новые враги должны использовать Stats.")]
        public List<CharacterDataSO.StatConfig> BaseStats;

        [Header("Stats")]
        public List<EnemyStatEntry> Stats = new List<EnemyStatEntry>();

        [Header("AI / Perception")]
        public EnemyPerceptionConfig Perception = new EnemyPerceptionConfig();

        [Header("Movement")]
        public EnemyMovementConfig Movement = new EnemyMovementConfig();

        [Header("Attack")]
        public EnemyAttackConfig Attack = new EnemyAttackConfig();

        [Header("Behaviour")]
        public EnemyBehaviourConfig Behaviour = new EnemyBehaviourConfig();

        [Header("Animation")]
        public EnemyAnimationConfig Animation = new EnemyAnimationConfig();

        [Header("Rewards")]
        public float XPReward = 10f;

        [Tooltip("Используется только для legacy Base Stats, если новые Stats ещё не заполнены.")]
        public float LegacyGrowthPerLevelPercent = 25f;

        public StatType GetAttackDamageStatType()
        {
            return Attack.DamageType switch
            {
                EnemyAttackDamageType.Fire => StatType.DamageFire,
                EnemyAttackDamageType.Cold => StatType.DamageCold,
                EnemyAttackDamageType.Lightning => StatType.DamageLightning,
                _ => StatType.DamagePhysical
            };
        }

        private void OnEnable()
        {
            BaseStats ??= new List<CharacterDataSO.StatConfig>();
            Stats ??= new List<EnemyStatEntry>();
            Perception ??= new EnemyPerceptionConfig();
            Movement ??= new EnemyMovementConfig();
            Attack ??= new EnemyAttackConfig();
            Behaviour ??= new EnemyBehaviourConfig();
            Animation ??= new EnemyAnimationConfig();
        }
    }
}
