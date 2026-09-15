using NUnit.Framework;
using Scripts.Items.Affixes;

namespace RelicKeeper.Tests.EditMode
{
    public class AffixTierHelperTests
    {
        [Test]
        public void LowLevelItem_CanOnlyRollWeakestTier()
        {
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(1, 1), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(4, 2), Is.False);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(4, 5), Is.False);
        }

        [Test]
        public void UnlockingATier_KeepsPreviousTiersAvailable()
        {
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(5, 1), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(5, 2), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(5, 3), Is.False);

            Assert.That(AffixTierHelper.IsTierAllowedForLevel(15, 1), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(15, 2), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(15, 3), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(15, 4), Is.True);
            Assert.That(AffixTierHelper.IsTierAllowedForLevel(15, 5), Is.False);
        }

        [Test]
        public void HighLevelItem_CanRollEveryUnlockedTier()
        {
            for (int tier = 1; tier <= 5; tier++)
            {
                Assert.That(AffixTierHelper.IsTierAllowedForLevel(25, tier), Is.True, $"T{tier} should stay available at 25");
                Assert.That(AffixTierHelper.IsTierAllowedForLevel(30, tier), Is.True, $"T{tier} should stay available at 30");
            }
        }

        [Test]
        public void UnlockLevels_MatchDesignedBreakpoints()
        {
            Assert.That(AffixTierHelper.GetUnlockLevelForTier(1), Is.EqualTo(1));
            Assert.That(AffixTierHelper.GetUnlockLevelForTier(2), Is.EqualTo(5));
            Assert.That(AffixTierHelper.GetUnlockLevelForTier(3), Is.EqualTo(10));
            Assert.That(AffixTierHelper.GetUnlockLevelForTier(4), Is.EqualTo(15));
            Assert.That(AffixTierHelper.GetUnlockLevelForTier(5), Is.EqualTo(25));
        }
    }
}
