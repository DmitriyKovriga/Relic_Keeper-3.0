using NUnit.Framework;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class AffixValueBalanceTests
    {
        [Test]
        public void ColdResist_Light_WeakestTier_StartsInSingleDigits()
        {
            var roll = AffixValueBalance.GetRoll(StatType.ColdResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 1);
            Assert.That(roll.Min, Is.EqualTo(6f));
            Assert.That(roll.Max, Is.EqualTo(9f));
        }

        [Test]
        public void ColdResist_Strong_BestTier_ReachesHighForties()
        {
            var roll = AffixValueBalance.GetRoll(StatType.ColdResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 5);
            Assert.That(roll.Min, Is.GreaterThanOrEqualTo(40f));
            Assert.That(roll.Max, Is.GreaterThanOrEqualTo(50f));
            Assert.That(roll.Max, Is.LessThanOrEqualTo(65f));
        }

        [Test]
        public void HealthRegen_Strong_BestTier_StaysAFewHpPerSecond()
        {
            var roll = AffixValueBalance.GetRoll(StatType.HealthRegen, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 5);
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
            var weak = AffixValueBalance.GetRoll(StatType.MaxFireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 1);
            var best = AffixValueBalance.GetRoll(StatType.MaxFireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 5);
            Assert.That(weak.Min, Is.EqualTo(1f));
            Assert.That(weak.Max, Is.EqualTo(1f));
            Assert.That(best.Min, Is.EqualTo(1f));
            Assert.That(best.Max, Is.EqualTo(2f));
        }

        [Test]
        public void HigherTiersAreStrictlyStrongerForResists()
        {
            var t1 = AffixValueBalance.GetRoll(StatType.FireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 1);
            var t5 = AffixValueBalance.GetRoll(StatType.FireResist, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 5);
            Assert.That(t5.Min, Is.GreaterThan(t1.Min * 2f));
            Assert.That(t5.Max, Is.GreaterThan(t1.Max * 2f));
        }

        [Test]
        public void CritMultiplier_IsUsefulPercentPoints()
        {
            var t1 = AffixValueBalance.GetRoll(StatType.CritMultiplier, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 1);
            var t5 = AffixValueBalance.GetRoll(StatType.CritMultiplier, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 5);
            Assert.That(t1.Min, Is.GreaterThanOrEqualTo(10f));
            Assert.That(t5.Max, Is.GreaterThanOrEqualTo(40f));
            Assert.That(t5.Min, Is.GreaterThan(t1.Max * 1.5f));
        }

        [Test]
        public void CritChance_Flat_IsAdditivePointsNotAFullCritBuild()
        {
            var t1 = AffixValueBalance.GetRoll(StatType.CritChance, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 1);
            var t5 = AffixValueBalance.GetRoll(StatType.CritChance, StatAffixModifierKind.Flat, AffixValueBalance.StrengthStrong, 5);
            Assert.That(t1.Min, Is.EqualTo(1f));
            Assert.That(t1.Max, Is.EqualTo(2f));
            Assert.That(t5.Min, Is.GreaterThanOrEqualTo(8f));
            Assert.That(t5.Max, Is.LessThanOrEqualTo(18f));
        }

        [Test]
        public void AddedDamage_RangeRoll_HasRisingMaxBand()
        {
            var roll = AffixValueBalance.GetRoll(StatType.DamagePhysical, StatAffixModifierKind.Flat, AffixValueBalance.StrengthMedium, 1, includeSecondary: true);
            Assert.That(roll.HasSecondary, Is.True);
            Assert.That(roll.Min, Is.EqualTo(1f));
            Assert.That(roll.Max, Is.EqualTo(2f));
            Assert.That(roll.RangeMin, Is.EqualTo(2f));
            Assert.That(roll.RangeMax, Is.EqualTo(4f));
        }

        [Test]
        public void LocalPhysIncrease_Strong_HasWideTierGap()
        {
            var t1 = AffixValueBalance.GetRoll(
                StatType.DamagePhysical,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                1,
                scope: StatScope.Local);
            var t5 = AffixValueBalance.GetRoll(
                StatType.DamagePhysical,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                5,
                scope: StatScope.Local);

            Assert.That(t1.Min, Is.EqualTo(20f));
            Assert.That(t1.Max, Is.EqualTo(30f));
            Assert.That(t5.Min, Is.EqualTo(100f));
            Assert.That(t5.Max, Is.EqualTo(120f));
        }

        [Test]
        public void GlobalPhysIncrease_IsWeakerThanLocalWeaponRolls()
        {
            var local = AffixValueBalance.GetRoll(
                StatType.DamagePhysical,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                5,
                scope: StatScope.Local);
            var global = AffixValueBalance.GetRoll(
                StatType.DamagePhysical,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                5,
                scope: StatScope.Global);

            Assert.That(global.Max, Is.LessThan(local.Min));
        }

        [Test]
        public void LocalAttackSpeed_Strong_StaysInPoELikeBand()
        {
            var t1 = AffixValueBalance.GetRoll(
                StatType.AttackSpeed,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                1,
                scope: StatScope.Local);
            var t5 = AffixValueBalance.GetRoll(
                StatType.AttackSpeed,
                StatAffixModifierKind.Increase,
                AffixValueBalance.StrengthStrong,
                5,
                scope: StatScope.Local);

            Assert.That(t1.Min, Is.GreaterThanOrEqualTo(6f));
            Assert.That(t1.Max, Is.LessThanOrEqualTo(12f));
            Assert.That(t5.Min, Is.GreaterThanOrEqualTo(20f));
            Assert.That(t5.Max, Is.LessThanOrEqualTo(32f));
        }

        [Test]
        public void AreaOfEffect_Light_WeakestTier_IsAVisibleRadiusIncrease()
        {
            var roll = AffixValueBalance.GetRoll(StatType.AreaOfEffect, StatAffixModifierKind.Flat, AffixValueBalance.StrengthLight, 1);
            Assert.That(roll.Min, Is.GreaterThanOrEqualTo(4f));
            Assert.That(roll.Max, Is.GreaterThanOrEqualTo(7f));
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
