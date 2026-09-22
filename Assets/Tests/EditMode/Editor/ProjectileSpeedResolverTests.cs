using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class ProjectileSpeedResolverTests
    {
        [Test]
        public void FlatProjectileSpeedIsAddedToTheSkillBaseSpeed()
        {
            var stats = new TestStats();
            stats.Add(2f, StatModType.Flat);

            Assert.That(ProjectileSpeedResolver.Resolve(8f, stats), Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void IncreasedProjectileSpeedWorksWithoutAFlatModifier()
        {
            var stats = new TestStats();
            stats.Add(50f, StatModType.PercentAdd);

            Assert.That(ProjectileSpeedResolver.Resolve(8f, stats), Is.EqualTo(12f).Within(0.0001f));
        }

        [Test]
        public void ModifierLayersApplyInTheExpectedOrder()
        {
            var stats = new TestStats();
            stats.Add(5f, StatModType.Flat);
            stats.Add(50f, StatModType.PercentAdd);
            stats.Add(20f, StatModType.PercentMult);

            Assert.That(ProjectileSpeedResolver.Resolve(10f, stats), Is.EqualTo(27f).Within(0.0001f));
        }

        [Test]
        public void DecreasedAndLessProjectileSpeedCanSlowAProjectile()
        {
            var stats = new TestStats();
            stats.Add(25f, StatModType.PercentSub);
            stats.Add(20f, StatModType.PercentLess);

            Assert.That(ProjectileSpeedResolver.Resolve(10f, stats), Is.EqualTo(6f).Within(0.0001f));
        }

        [Test]
        public void ProjectileSpeedMetadataTreatsFlatAsAbsoluteAndAllowsAllLayers()
        {
            Assert.That(StatsDatabaseSO.DefaultFormatFor(StatType.ProjectileSpeed), Is.EqualTo(StatDisplayFormat.Number));
            Assert.That(StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(StatType.ProjectileSpeed), Is.False);
            Assert.That(StatsDatabaseSO.DefaultAffixGenTypeFor(StatType.ProjectileSpeed), Is.EqualTo(StatAffixGenType.FullCalcStat));
            Assert.That(
                StatsDatabaseSO.DefaultAllowedAffixKindsFor(StatType.ProjectileSpeed, StatAffixGenType.FullCalcStat),
                Is.EqualTo(StatAffixModifierKindFlags.Full));
        }

        private sealed class TestStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

            public void Add(float value, StatModType type)
            {
                if (!_stats.TryGetValue(StatType.ProjectileSpeed, out CharacterStat stat))
                {
                    stat = new CharacterStat();
                    _stats[StatType.ProjectileSpeed] = stat;
                }

                stat.AddModifier(new StatModifier(value, type));
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
