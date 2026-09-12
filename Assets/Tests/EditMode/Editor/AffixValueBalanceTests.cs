using NUnit.Framework;
using Scripts.Items.Affixes;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class AffixValueBalanceTests
    {
        [Test]
        public void ColdResist_Light_WeakestTier_StartsNearTenPercent()
        {
            var roll = AffixValueBalance.GetRoll(StatType.ColdResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 5);
            Assert.That(roll.Min, Is.EqualTo(8f));
            Assert.That(roll.Max, Is.EqualTo(11f));
        }

        [Test]
        public void ColdResist_Strong_BestTier_ReachesHighTwenties()
        {
            var roll = AffixValueBalance.GetRoll(StatType.ColdResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 1);
            Assert.That(roll.Min, Is.GreaterThanOrEqualTo(28f));
            Assert.That(roll.Max, Is.GreaterThanOrEqualTo(36f));
            Assert.That(roll.Max, Is.LessThanOrEqualTo(50f));
        }

        [Test]
        public void HealthRegen_Strong_BestTier_StaysAFewHpPerSecond()
        {
            var roll = AffixValueBalance.GetRoll(StatType.HealthRegen, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 1);
            Assert.That(roll.Min, Is.LessThan(6f));
            Assert.That(roll.Max, Is.LessThan(6f));
            Assert.That(roll.Max, Is.GreaterThan(2f));
        }

        [Test]
        public void ProjectilePierce_AlwaysOne()
        {
            foreach (int tier in new[] { 1, 3, 5 })
            {
                var roll = AffixValueBalance.GetRoll(StatType.ProjectilePierce, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, tier);
                Assert.That(roll.Min, Is.EqualTo(1f));
                Assert.That(roll.Max, Is.EqualTo(1f));
            }
        }

        [Test]
        public void MaxResist_StaysAtOneExceptStrongBestTier()
        {
            var weak = AffixValueBalance.GetRoll(StatType.MaxFireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 5);
            var best = AffixValueBalance.GetRoll(StatType.MaxFireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 1);
            Assert.That(weak.Min, Is.EqualTo(1f));
            Assert.That(weak.Max, Is.EqualTo(1f));
            Assert.That(best.Min, Is.EqualTo(1f));
            Assert.That(best.Max, Is.EqualTo(2f));
        }

        [Test]
        public void HigherTiersAreStrictlyStrongerForResists()
        {
            var t5 = AffixValueBalance.GetRoll(StatType.FireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 5);
            var t1 = AffixValueBalance.GetRoll(StatType.FireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 1);
            Assert.That(t1.Min, Is.GreaterThan(t5.Min));
            Assert.That(t1.Max, Is.GreaterThan(t5.Max));
        }

        [Test]
        public void CritMultiplier_IsUsefulPercentPoints()
        {
            var t5 = AffixValueBalance.GetRoll(StatType.CritMultiplier, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 5);
            var t1 = AffixValueBalance.GetRoll(StatType.CritMultiplier, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 1);
            Assert.That(t5.Min, Is.GreaterThanOrEqualTo(10f));
            Assert.That(t1.Max, Is.GreaterThanOrEqualTo(30f));
        }

        [Test]
        public void CritChance_Flat_IsAdditivePointsNotAFullCritBuild()
        {
            var t5 = AffixValueBalance.GetRoll(StatType.CritChance, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 5);
            var t1 = AffixValueBalance.GetRoll(StatType.CritChance, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 1);
            Assert.That(t5.Min, Is.EqualTo(1f));
            Assert.That(t5.Max, Is.EqualTo(2f));
            Assert.That(t1.Min, Is.GreaterThanOrEqualTo(5f));
            Assert.That(t1.Max, Is.LessThanOrEqualTo(12f));
        }

        [Test]
        public void AddedDamage_RangeRoll_HasRisingMaxBand()
        {
            var roll = AffixValueBalance.GetRoll(StatType.DamagePhysical, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 5, includeSecondary: true);
            Assert.That(roll.HasSecondary, Is.True);
            Assert.That(roll.Min, Is.EqualTo(1f));
            Assert.That(roll.Max, Is.EqualTo(3f));
            Assert.That(roll.RangeMin, Is.EqualTo(3f));
            Assert.That(roll.RangeMax, Is.EqualTo(5f));
        }

        [Test]
        public void AreaOfEffect_Light_WeakestTier_IsAVisibleRadiusIncrease()
        {
            var roll = AffixValueBalance.GetRoll(StatType.AreaOfEffect, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 5);
            Assert.That(roll.Min, Is.GreaterThanOrEqualTo(6f));
            Assert.That(roll.Max, Is.GreaterThanOrEqualTo(9f));
        }

        [Test]
        public void StunDuration_NeverQuantizesToZero()
        {
            float rolled = AffixValueBalance.QuantizeRolled(0.24f, 0.3f, 0.5f, 1);
            Assert.That(rolled, Is.EqualTo(0.3f));
        }

        [Test]
        public void IntegerResists_StillQuantizeToWholeNumbers()
        {
            float rolled = AffixValueBalance.QuantizeRolled(10.4f, 8f, 11f, 0);
            Assert.That(rolled, Is.EqualTo(10f));
        }
    }
}
