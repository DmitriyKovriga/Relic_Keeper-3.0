using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ElementalAndRangeDamageTests
    {
        [Test]
        public void ElementalDamage_ScalesElementalHitsWithoutASkillTag()
        {
            var stats = Stats(100f, 80f);
            stats.Add(StatType.ElementalDamage, 50f, StatModType.PercentAdd);
            var melee = new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Melee);

            DamageSnapshot hit = DamageCalculator.CreatePreviewSnapshot(stats, 1f, melee);

            Assert.That(hit.Fire, Is.EqualTo(150f).Within(0.001f));
            Assert.That(hit.Physical, Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void SpellDamage_DoesNotApplyToAMeleeHit()
        {
            var stats = Stats(100f, 0f);
            stats.Add(StatType.SpellDamage, 50f, StatModType.PercentAdd);

            DamageSnapshot hit = DamageCalculator.CreatePreviewSnapshot(
                stats,
                1f,
                new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Melee));

            Assert.That(hit.Fire, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void RangeDamage_AppliesOnlyWhenTheSkillIsMarkedRange()
        {
            var stats = Stats(40f, 60f);
            stats.Add(StatType.RangeDamage, 50f, StatModType.PercentAdd);

            DamageSnapshot melee = DamageCalculator.CreatePreviewSnapshot(
                stats,
                1f,
                new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Melee));
            DamageSnapshot ranged = DamageCalculator.CreatePreviewSnapshot(
                stats,
                1f,
                new DamageContext(StatContextTagFlags.Attack | StatContextTagFlags.Range));

            Assert.That(melee.Fire, Is.EqualTo(40f).Within(0.001f));
            Assert.That(melee.Physical, Is.EqualTo(60f).Within(0.001f));
            Assert.That(ranged.Fire, Is.EqualTo(60f).Within(0.001f));
            Assert.That(ranged.Physical, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void Defaults_MatchElementalChannelsAndRangeTag()
        {
            Assert.That(StatsDatabaseSO.DefaultSemanticKindFor(StatType.ElementalDamage), Is.EqualTo(StatSemanticKind.ContextModifier));
            Assert.That(StatsDatabaseSO.DefaultSemanticKindFor(StatType.RangeDamage), Is.EqualTo(StatSemanticKind.ContextModifier));
            Assert.That(StatsDatabaseSO.DefaultContextTagsFor(StatType.ElementalDamage), Is.EqualTo(StatContextTagFlags.None));
            Assert.That(StatsDatabaseSO.DefaultContextTagsFor(StatType.RangeDamage), Is.EqualTo(StatContextTagFlags.Range));
            Assert.That(
                StatsDatabaseSO.DefaultDamageChannelsFor(StatType.ElementalDamage),
                Is.EqualTo(StatDamageChannelFlags.Fire | StatDamageChannelFlags.Cold | StatDamageChannelFlags.Lightning));
            Assert.That(StatsDatabaseSO.DefaultDamageChannelsFor(StatType.RangeDamage), Is.EqualTo(StatDamageChannelFlags.All));
        }

        private static TestStats Stats(float fire, float physical)
        {
            var stats = new TestStats();
            stats.Add(StatType.DamageFire, fire, StatModType.Flat);
            stats.Add(StatType.DamagePhysical, physical, StatModType.Flat);
            return stats;
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
