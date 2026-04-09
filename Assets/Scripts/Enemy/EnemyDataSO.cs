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
        public string ProjectileVisualResourcePath;
        public Vector2 ProjectileSpawnOffset = new Vector2(0f, 0.15f);
        [Min(0f)] public float ProjectileSpeed = 6f;
        [Min(0f)] public float ProjectileLifetime = 8f;
        [Min(0f)] public float ProjectileHitRadius = 0.22f;
        [Min(1f)] public float ProjectileAnimationFps = 10f;
        public bool ProjectileStopsOnGround = true;
    }

    [Serializable]
    public class EnemyChargeAttackConfig
    {
        public bool Enabled;
        [Min(0f)] public float TriggerMinDistance = 2.2f;
        [Min(0f)] public float TriggerMaxDistance = 4.8f;
        [Min(0f)] public float AttackCooldown = 3f;
        public EnemyAttackDeliveryType DeliveryType = EnemyAttackDeliveryType.Melee;
        public EnemyAttackDamageType DamageType = EnemyAttackDamageType.Physical;
        [Min(0f)] public float Windup = 0.3f;
        [Min(0f)] public float ActiveTime = 0.1f;
        [Min(0f)] public float Recovery = 0.35f;
        [Min(0f)] public float DamageMultiplier = 1.35f;
        public Vector2 HitboxSize = new Vector2(1.6f, 0.9f);
        public Vector2 HitboxOffset = new Vector2(0.95f, 0f);
        [Min(0f)] public float DashSpeed = 5.25f;
        [Min(0f)] public float DashDuration = 0.22f;
        [Min(0f)] public float DashOvershootDistance = 0.95f;
        public bool IgnoreLedgesDuringDash;
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
    public class EnemyBurrowConfig
    {
        public bool Enabled;
        [Min(0f)] public float TriggerMinDistance = 1.6f;
        [Min(0f)] public float Cooldown = 2.2f;
        [Min(0f)] public float ExitOffsetRadius = 0.35f;
        [Min(0f)] public float HiddenDelay = 0.04f;
        [Min(0f)] public float PostExitPause = 0.12f;
        [Min(0f)] public float DestinationSearchHeight = 3.5f;
        [Min(0f)] public float DestinationSearchDepth = 6f;
    }

    [Serializable]
    public class EnemyDeathEffectConfig
    {
        public bool Enabled = true;
        [Min(1)] public int ChunkCount = 6;
        [Min(0f)] public float ChunkHorizontalForce = 3.2f;
        [Min(0f)] public float ChunkVerticalForce = 4.8f;
        [Min(0f)] public float BloodHorizontalSpread = 1.4f;
        [Min(0f)] public float BloodVerticalSpread = 1.8f;

        [HideInInspector] public float Lifetime = 30f;
        [HideInInspector] public float FadeDuration = 5f;
        [HideInInspector] public float GravityScale = 2.8f;
        [HideInInspector] public float ChunkLinearDamping = 1.35f;
        [HideInInspector] public float ChunkAngularDamping = 1.1f;
        [HideInInspector] public float RestCheckDelay = 0.3f;
        [HideInInspector] public float RestVelocityThreshold = 0.18f;
        [HideInInspector] public float RestAngularVelocityThreshold = 8f;
        public Color BloodColor = new Color(0.45f, 0.04f, 0.07f, 1f);
        public Color GoreColor = new Color(0.26f, 0.03f, 0.04f, 1f);
    }

    [Serializable]
    public class EnemyAnimationConfig
    {
        public RuntimeAnimatorController Controller;
        public string IdleStateName = "Idle";
        public string MoveStateName = "Walk";
        public string AttackStateName = "Attack";
        public string ChargeStateName = "Charge";
        public string HitStateName = "Hit";
        public string DigInStateName = "DigIn";
        public string DigOutStateName = "DigOut";
        public string IdleSpritesResourcePath;
        public string MoveSpritesResourcePath;
        public string AttackSpritesResourcePath;
        public string ChargeSpritesResourcePath;
        public string HitSpritesResourcePath;
        public string DigInSpritesResourcePath;
        public string DigOutSpritesResourcePath;
        public Vector2 VisualLocalOffset = Vector2.zero;
        public bool InvertFacingX;
        [Min(1f)] public float IdleFps = 8f;
        [Min(1f)] public float MoveFps = 8f;
        [Min(1f)] public float AttackFps = 10f;
        [Min(1f)] public float ChargeFps = 10f;
        [Min(1f)] public float HitFps = 10f;
        [Min(1f)] public float DigInFps = 10f;
        [Min(1f)] public float DigOutFps = 10f;
        public int AttackImpactFrame = -1;
        public int ChargeImpactFrame = -1;

        public bool UsesSpriteSheets =>
            Controller == null &&
            (!string.IsNullOrWhiteSpace(IdleSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(MoveSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(AttackSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(ChargeSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(HitSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(DigInSpritesResourcePath) ||
             !string.IsNullOrWhiteSpace(DigOutSpritesResourcePath));
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

        [Header("Charge Attack")]
        public EnemyChargeAttackConfig ChargeAttack = new EnemyChargeAttackConfig();

        [Header("Behaviour")]
        public EnemyBehaviourConfig Behaviour = new EnemyBehaviourConfig();

        [Header("Burrow")]
        public EnemyBurrowConfig Burrow = new EnemyBurrowConfig();

        [Header("Death Effect")]
        public EnemyDeathEffectConfig DeathEffect = new EnemyDeathEffectConfig();

        [Header("Animation")]
        public EnemyAnimationConfig Animation = new EnemyAnimationConfig();

        [Header("Rewards")]
        public float XPReward = 10f;

        [Tooltip("Используется только для legacy Base Stats, если новые Stats ещё не заполнены.")]
        public float LegacyGrowthPerLevelPercent = 25f;

        public StatType GetAttackDamageStatType()
        {
            return GetDamageStatType(Attack.DamageType);
        }

        public StatType GetChargeDamageStatType()
        {
            return GetDamageStatType(ChargeAttack.DamageType);
        }

        private static StatType GetDamageStatType(EnemyAttackDamageType damageType)
        {
            return damageType switch
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
            ChargeAttack ??= new EnemyChargeAttackConfig();
            Behaviour ??= new EnemyBehaviourConfig();
            Burrow ??= new EnemyBurrowConfig();
            DeathEffect ??= new EnemyDeathEffectConfig();
            Animation ??= new EnemyAnimationConfig();
        }
    }
}
