using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class StatLayerOverlayTests
    {
        [Test]
        public void PoisonStackOverlay_MatchesCombinedStatAndLeavesBaseUntouched()
        {
            CharacterStat physical = new CharacterStat(100f);
            physical.AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            physical.AddModifier(new StatModifier(20f, StatModType.PercentMult));
            var attacker = new DictionaryStats();
            attacker.Set(StatType.DamagePhysical, physical);
            int baseModifierCount = physical.Modifiers.Count;

            var lightPoison = Overlay(attacker, 10f);
            var heavyPoison = Overlay(attacker, 40f);

            var expectedLight = new CharacterStat(100f);
            expectedLight.AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            expectedLight.AddModifier(new StatModifier(20f, StatModType.PercentMult));
            expectedLight.AddModifier(new StatModifier(10f, StatModType.PercentAdd));

            var expectedHeavy = new CharacterStat(100f);
            expectedHeavy.AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            expectedHeavy.AddModifier(new StatModifier(20f, StatModType.PercentMult));
            expectedHeavy.AddModifier(new StatModifier(40f, StatModType.PercentAdd));

            Assert.That(lightPoison.GetValue(StatType.DamagePhysical), Is.EqualTo(expectedLight.Value));
            Assert.That(heavyPoison.GetValue(StatType.DamagePhysical), Is.EqualTo(expectedHeavy.Value));
            Assert.That(physical.Modifiers.Count, Is.EqualTo(baseModifierCount));
            Assert.That(physical.Value, Is.EqualTo(180f));
        }

        [Test]
        public void PoisonStackOverlay_ScalesHitDamagePerTarget()
        {
            var attacker = new DictionaryStats();
            CharacterStat physical = new CharacterStat(100f);
            physical.AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            attacker.Set(StatType.DamagePhysical, physical);

            DamageSnapshot lightHit = DamageCalculator.CreateDamageSnapshot(Overlay(attacker, 10f));
            DamageSnapshot heavyHit = DamageCalculator.CreateDamageSnapshot(Overlay(attacker, 40f));

            Assert.That(lightHit.Physical, Is.EqualTo(160f).Within(0.001f));
            Assert.That(heavyHit.Physical, Is.EqualTo(190f).Within(0.001f));
            Assert.That(physical.Modifiers.Count, Is.EqualTo(1));
        }

        [Test]
        public void Overlay_AppliesFlatIncreaseAndLessInTheSameFormula()
        {
            CharacterStat physical = new CharacterStat(10f);
            physical.AddModifier(new StatModifier(50f, StatModType.PercentMult));
            var attacker = new DictionaryStats();
            attacker.Set(StatType.DamagePhysical, physical);

            var overlay = new ScopedStatsProvider(attacker, new[]
            {
                new SerializableStatModifier { Stat = StatType.DamagePhysical, Type = StatModType.Flat, Value = 5f },
                new SerializableStatModifier { Stat = StatType.DamagePhysical, Type = StatModType.PercentLess, Value = 50f }
            });

            var expected = new CharacterStat(10f);
            expected.AddModifier(new StatModifier(50f, StatModType.PercentMult));
            expected.AddModifier(new StatModifier(5f, StatModType.Flat));
            expected.AddModifier(new StatModifier(50f, StatModType.PercentLess));

            Assert.That(overlay.GetValue(StatType.DamagePhysical), Is.EqualTo(expected.Value));
        }

        [Test]
        public void WeaponHandLayers_MatchBaseValueWithoutCopyingModifiers()
        {
            CharacterStat physical = new CharacterStat(40f);
            physical.AddModifier(new StatModifier(25f, StatModType.PercentAdd));
            physical.AddModifier(new StatModifier(10f, StatModType.PercentMult));
            var attacker = new DictionaryStats();
            attacker.Set(StatType.DamagePhysical, physical);

            var hand = new WeaponHandStatsProvider(attacker, null);
            Assert.That(hand.GetValue(StatType.DamagePhysical), Is.EqualTo(physical.Value));
            Assert.That(physical.Modifiers.Count, Is.EqualTo(2));
        }

        private static ScopedStatsProvider Overlay(DictionaryStats attacker, float poisonIncreased)
        {
            return new ScopedStatsProvider(attacker, new[]
            {
                new SerializableStatModifier
                {
                    Stat = StatType.DamagePhysical,
                    Type = StatModType.PercentAdd,
                    Value = poisonIncreased
                }
            });
        }

        private sealed class DictionaryStats : IStatsProvider
        {
            private readonly Dictionary<StatType, CharacterStat> _stats = new Dictionary<StatType, CharacterStat>();

            public void Set(StatType type, CharacterStat stat)
            {
                _stats[type] = stat;
            }

            public float GetValue(StatType type)
            {
                return _stats.TryGetValue(type, out CharacterStat stat) && stat != null ? stat.Value : 0f;
            }

            public bool TryGetStat(StatType type, out CharacterStat stat)
            {
                return _stats.TryGetValue(type, out stat) && stat != null;
            }
        }
    }
}
