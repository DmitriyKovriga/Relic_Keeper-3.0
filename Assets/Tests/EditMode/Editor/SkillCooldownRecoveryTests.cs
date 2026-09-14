using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Skills;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillCooldownRecoveryTests
    {
        [Test]
        public void SpecificStageIsAppliedBeforeGlobalStage()
        {
            var stats = new TestStats();
            stats.Add(StatType.SpecialSkillCooldownRecovery, 1f, StatModType.Flat);
            stats.Add(StatType.SpecialSkillCooldownRecovery, 25f, StatModType.PercentAdd);
            stats.Add(StatType.SkillCooldownRecovery, 1f, StatModType.Flat);
            stats.Add(StatType.SkillCooldownRecovery, 50f, StatModType.PercentAdd);

            float result = SkillCooldownRecovery.Resolve(5f, stats, SkillCooldownRecovery.SpecialSkillSlot);

            Assert.That(result, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void SlotRecoveryDoesNotAffectOtherSlots()
        {
            var stats = new TestStats();
            stats.Add(StatType.HelmetSkillCooldownRecovery, 1f, StatModType.Flat);
            stats.Add(StatType.HelmetSkillCooldownRecovery, 50f, StatModType.PercentAdd);

            Assert.That(
                SkillCooldownRecovery.Resolve(4f, stats, SkillCooldownRecovery.HelmetSkillSlot),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(
                SkillCooldownRecovery.Resolve(4f, stats, SkillCooldownRecovery.BootsSkillSlot),
                Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void MoreAndLessApplyIndependentlyInTheirStage()
        {
            var stats = new TestStats();
            stats.Add(StatType.SkillCooldownRecovery, 20f, StatModType.PercentMult);
            stats.Add(StatType.SkillCooldownRecovery, 25f, StatModType.PercentLess);

            float result = SkillCooldownRecovery.Resolve(10f, stats, SkillCooldownRecovery.MainSkillSlot);

            Assert.That(result, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void DecreasedRecoveryIncreasesCooldown()
        {
            var stats = new TestStats();
            stats.Add(StatType.GlovesSkillCooldownRecovery, 25f, StatModType.PercentSub);

            float result = SkillCooldownRecovery.Resolve(4f, stats, SkillCooldownRecovery.GlovesSkillSlot);

            Assert.That(result, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void DpsPreviewUsesEffectiveCooldownForTheSlot()
        {
            var stats = new TestStats();
            stats.Add(StatType.SpecialSkillCooldownRecovery, 1f, StatModType.Flat);
            stats.Add(StatType.SpecialSkillCooldownRecovery, 25f, StatModType.PercentAdd);
            SkillDataSO skill = ScriptableObject.CreateInstance<SkillDataSO>();
            skill.Cooldown = 5f;

            try
            {
                float castsPerSecond = SkillDpsPreview.ResolveCastsPerSecond(
                    skill,
                    stats,
                    SkillCooldownRecovery.SpecialSkillSlot);

                Assert.That(castsPerSecond, Is.EqualTo(1f / 3f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(skill);
            }
        }

        private sealed class TestStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

            public void Add(StatType type, float value, StatModType modifierType)
            {
                if (!_stats.TryGetValue(type, out CharacterStat stat))
                {
                    stat = new CharacterStat();
                    _stats[type] = stat;
                }

                stat.AddModifier(new StatModifier(value, modifierType));
            }

            public float GetValue(StatType type)
            {
                return _stats.TryGetValue(type, out CharacterStat stat) ? stat.Value : 0f;
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                return _stats.TryGetValue(type, out stat);
            }
        }
    }
}
