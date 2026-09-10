using System.Collections.Generic;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Combat
{
    public readonly struct SkillHitDpsPreview
    {
        public readonly float PhysicalPerHit;
        public readonly float FirePerHit;
        public readonly float ColdPerHit;
        public readonly float LightningPerHit;
        public readonly float CastsPerSecond;

        public SkillHitDpsPreview(float physicalPerHit, float firePerHit, float coldPerHit, float lightningPerHit, float castsPerSecond)
        {
            PhysicalPerHit = physicalPerHit;
            FirePerHit = firePerHit;
            ColdPerHit = coldPerHit;
            LightningPerHit = lightningPerHit;
            CastsPerSecond = castsPerSecond;
        }

        public float TotalPerHit => PhysicalPerHit + FirePerHit + ColdPerHit + LightningPerHit;
        public float PhysicalDps => PhysicalPerHit * CastsPerSecond;
        public float FireDps => FirePerHit * CastsPerSecond;
        public float ColdDps => ColdPerHit * CastsPerSecond;
        public float LightningDps => LightningPerHit * CastsPerSecond;
        public float TotalDps => TotalPerHit * CastsPerSecond;
        public int PresentChannelCount
        {
            get
            {
                int count = 0;
                if (PhysicalDps > 0.049f) count++;
                if (FireDps > 0.049f) count++;
                if (ColdDps > 0.049f) count++;
                if (LightningDps > 0.049f) count++;
                return count;
            }
        }
    }

    public readonly struct SkillDotDpsPreview
    {
        public readonly AilmentType Type;
        public readonly float TickDps;
        public readonly float ChancePercent;

        public SkillDotDpsPreview(AilmentType type, float tickDps, float chancePercent)
        {
            Type = type;
            TickDps = tickDps;
            ChancePercent = chancePercent;
        }
    }

    public readonly struct SkillDpsPreview
    {
        public readonly SkillHitDpsPreview Hit;
        public readonly SkillDotDpsPreview[] Dots;

        public SkillDpsPreview(SkillHitDpsPreview hit, SkillDotDpsPreview[] dots)
        {
            Hit = hit;
            Dots = dots ?? System.Array.Empty<SkillDotDpsPreview>();
        }

        public bool HasHitDamage => Hit.TotalPerHit > 0.049f;
        public bool HasDot => Dots.Length > 0;
        public bool HasContent => HasHitDamage || HasDot;
        public float CombinedDotDps
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Dots.Length; i++)
                    total += Dots[i].TickDps;
                return total;
            }
        }

        public static SkillDpsPreview Build(SkillDataSO skill, IStatsProvider stats, int skillSlot)
        {
            IStatsProvider scopedStats = WeaponHandStatScope.ForSkill(stats, skillSlot) ?? stats;
            float castsPerSecond = ResolveCastsPerSecond(skill, scopedStats);
            SkillHitDpsPreview hit = BuildHitPreview(skill, scopedStats, castsPerSecond);
            return new SkillDpsPreview(hit, BuildDotPreviews(scopedStats, hit));
        }

        public static string FormatAmount(float value)
        {
            float rounded = value >= 10f
                ? Mathf.Round(value)
                : Mathf.Round(value * 10f) / 10f;
            if (Mathf.Abs(rounded - Mathf.Round(rounded)) < 0.05f)
                return Mathf.Round(rounded).ToString();
            return rounded.ToString("0.0");
        }

        public static string FormatChance(float percent)
        {
            return $"{Mathf.Round(percent)}%";
        }

        public static DamageContext ResolveDamageContext(SkillDataSO skill)
        {
            StatContextTagFlags tags = skill != null ? skill.DamageContextTags : StatContextTagFlags.None;
            if (tags == StatContextTagFlags.None)
                tags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;
            return new DamageContext(tags);
        }

        public static float ResolveCastsPerSecond(SkillDataSO skill, IStatsProvider stats)
        {
            float actionSpeed = ResolveActionSpeed(skill, stats);
            float interval = 1f / Mathf.Max(0.05f, actionSpeed);
            float cooldown = skill != null ? Mathf.Max(0f, skill.Cooldown) : 0f;
            if (cooldown > interval)
                interval = cooldown;
            return 1f / interval;
        }

        private static SkillHitDpsPreview BuildHitPreview(SkillDataSO skill, IStatsProvider stats, float castsPerSecond)
        {
            float physical = 0f;
            float fire = 0f;
            float cold = 0f;
            float lightning = 0f;
            bool anyStep = false;

            CollectDamagingSteps(skill, (step, isProjectile) =>
            {
                anyStep = true;
                IStatsProvider stepStats = ApplyStepScopedStats(stats, step);
                DamageContext context = ResolveDamageContext(skill);
                if (isProjectile)
                    context = new DamageContext(context.Tags | StatContextTagFlags.Projectile);

                DamageSnapshot snapshot = DamageCalculator.CreatePreviewSnapshot(
                    stepStats,
                    Mathf.Max(0f, step.GetFloat("DamageMultiplier", 1f)),
                    context,
                    step.DamageConversions);
                physical += snapshot.Physical;
                fire += snapshot.Fire;
                cold += snapshot.Cold;
                lightning += snapshot.Lightning;
            });

            if (!anyStep)
            {
                DamageSnapshot snapshot = DamageCalculator.CreatePreviewSnapshot(
                    stats,
                    1f,
                    ResolveDamageContext(skill));
                physical = snapshot.Physical;
                fire = snapshot.Fire;
                cold = snapshot.Cold;
                lightning = snapshot.Lightning;
            }

            return new SkillHitDpsPreview(physical, fire, cold, lightning, castsPerSecond);
        }

        private static SkillDotDpsPreview[] BuildDotPreviews(IStatsProvider stats, SkillHitDpsPreview hit)
        {
            var snapshot = new DamageSnapshot(stats)
            {
                Physical = hit.PhysicalPerHit,
                Fire = hit.FirePerHit,
                Cold = hit.ColdPerHit,
                Lightning = hit.LightningPerHit
            };

            var dots = new List<SkillDotDpsPreview>(3);
            TryAddDot(dots, stats, snapshot, AilmentType.Bleed);
            TryAddDot(dots, stats, snapshot, AilmentType.Poison);
            TryAddDot(dots, stats, snapshot, AilmentType.Ignite);
            return dots.ToArray();
        }

        private static void TryAddDot(
            List<SkillDotDpsPreview> dots,
            IStatsProvider stats,
            DamageSnapshot snapshot,
            AilmentType type)
        {
            if (!AilmentController.TryPreviewHitAilment(stats, snapshot, type, out float tick, out float chance))
                return;
            if (tick <= 0.049f || chance < 1f)
                return;

            dots.Add(new SkillDotDpsPreview(type, tick, chance));
        }

        private static IStatsProvider ApplyStepScopedStats(IStatsProvider stats, StepEntry step)
        {
            if (stats == null || step?.ScopedStatModifiers == null || step.ScopedStatModifiers.Count == 0)
                return stats;

            bool any = false;
            for (int i = 0; i < step.ScopedStatModifiers.Count; i++)
            {
                if (Mathf.Abs(step.ScopedStatModifiers[i].Value) > 0.0001f)
                {
                    any = true;
                    break;
                }
            }

            return any ? new ScopedStatsProvider(stats, step.ScopedStatModifiers) : stats;
        }

        private static void CollectDamagingSteps(SkillDataSO skill, System.Action<StepEntry, bool> onStep)
        {
            if (skill?.Recipe?.Steps == null)
                return;

            CollectDamagingSteps(skill.Recipe.Steps, onStep);
        }

        private static void CollectDamagingSteps(List<StepEntry> steps, System.Action<StepEntry, bool> onStep)
        {
            if (steps == null)
                return;

            for (int i = 0; i < steps.Count; i++)
            {
                StepEntry step = steps[i];
                if (step == null)
                    continue;

                if (step.IsParallelGroup)
                {
                    CollectDamagingSteps(step.SubSteps, onStep);
                    continue;
                }

                if (!IsDamagingStep(step, out bool isProjectile))
                    continue;

                onStep(step, isProjectile);
            }
        }

        private static bool IsDamagingStep(StepEntry step, out bool isProjectile)
        {
            isProjectile = false;
            string id = step?.StepDefinition != null ? step.StepDefinition.Id : null;
            if (string.IsNullOrEmpty(id))
                return false;

            isProjectile = id == "SpawnProjectile"
                || id == "SpawnGroundProjectile"
                || id == "SpawnOrbitProjectiles";
            return isProjectile
                || id == "DealDamageCircle"
                || id == "DealDamageRectangle"
                || id == "ChainDamage"
                || id == "PersistentDamageCircle"
                || id == "PersistentDamageRectangle";
        }

        private static float ResolveActionSpeed(SkillDataSO skill, IStatsProvider stats)
        {
            const float defaultSpeed = 1f;
            const float minSpeed = 0.05f;
            const float maxSpeed = 12f;

            float attackSpeed = ResolveAttackActionSpeed(stats, defaultSpeed, minSpeed);
            float spellSpeed = ResolveSpellActionSpeed(stats, defaultSpeed, minSpeed);
            float speed = skill != null
                ? skill.ActionSpeedMode switch
                {
                    SkillActionSpeedMode.Spell => spellSpeed,
                    SkillActionSpeedMode.Universal => Mathf.Max(attackSpeed, spellSpeed),
                    _ => attackSpeed
                }
                : attackSpeed;

            float skillMult = skill != null && skill.SkillSpeedMultiplier > 0f ? skill.SkillSpeedMultiplier : 1f;
            speed *= Mathf.Max(0.05f, skillMult);
            if (speed <= 0f)
                speed = defaultSpeed;
            return Mathf.Clamp(speed, minSpeed, maxSpeed);
        }

        private static float ResolveAttackActionSpeed(IStatsProvider stats, float defaultSpeed, float minSpeed)
        {
            if (stats != null && stats.TryGetStat(StatType.AttackSpeed, out CharacterStat attackStat) && attackStat != null)
            {
                float flatSpeed = attackStat.GetRawFlatValue();
                if (flatSpeed <= 0f)
                    flatSpeed = defaultSpeed;
                float additiveFactor = Mathf.Max(0f, 1f + attackStat.GetTotalPercentAdd() / 100f);
                return flatSpeed * additiveFactor * attackStat.GetTotalMultiplier();
            }

            float value = stats != null ? stats.GetValue(StatType.AttackSpeed) : 0f;
            return value > 0f ? Mathf.Max(minSpeed, value) : defaultSpeed;
        }

        private static float ResolveSpellActionSpeed(IStatsProvider stats, float defaultSpeed, float minSpeed)
        {
            float baseSpeed = defaultSpeed;
            if (stats != null && stats.TryGetStat(StatType.AttackSpeed, out CharacterStat attackStat) && attackStat != null)
            {
                float rawFlat = attackStat.GetRawFlatValue();
                if (rawFlat > 0f)
                    baseSpeed = rawFlat;
            }

            float castFlatBonus = 0f;
            float castPercentAdd = 0f;
            float castMultiplier = 1f;
            if (stats != null && stats.TryGetStat(StatType.CastSpeed, out CharacterStat castStat) && castStat != null)
            {
                for (int i = 0; i < castStat.Modifiers.Count; i++)
                {
                    StatModifier modifier = castStat.Modifiers[i];
                    if (modifier.Type == StatModType.Flat)
                        castFlatBonus += modifier.Value;
                }

                castPercentAdd = castStat.GetTotalPercentAdd();
                castMultiplier = castStat.GetTotalMultiplier();
            }

            float flatSpeed = Mathf.Max(minSpeed, baseSpeed + castFlatBonus);
            float additiveFactor = Mathf.Max(0f, 1f + castPercentAdd / 100f);
            return flatSpeed * additiveFactor * castMultiplier;
        }
    }
}
