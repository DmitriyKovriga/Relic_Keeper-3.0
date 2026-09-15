using NUnit.Framework;
using Scripts.Combat;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class EvasionMitigationTests
    {
        [Test]
        public void RatingCurve_MatchesArmorAndEvasionAnchors()
        {
            Assert.That(DefenseRatingCurve.ToPercent(0f), Is.EqualTo(0f));
            Assert.That(DefenseRatingCurve.ToPercent(500f), Is.EqualTo(50f));
            Assert.That(DefenseRatingCurve.ToPercent(1000f), Is.EqualTo(60f));
            Assert.That(DefenseRatingCurve.ToPercent(2000f), Is.EqualTo(70f));
            Assert.That(DefenseRatingCurve.ToPercent(5000f), Is.EqualTo(80f));
            Assert.That(DefenseRatingCurve.ToPercent(10000f), Is.EqualTo(90f));
            Assert.That(DefenseRatingCurve.ToPercent(20000f), Is.EqualTo(90f));
        }

        [Test]
        public void RatingCurve_InterpolatesBetweenAnchors()
        {
            Assert.That(DefenseRatingCurve.ToPercent(750f), Is.EqualTo(55f).Within(0.01f));
        }

        [Test]
        public void ArmorAndEvasion_ShareTheSameCurve()
        {
            foreach (float rating in new[] { 0f, 100f, 500f, 1500f, 10000f, 25000f })
            {
                Assert.That(
                    ArmorMitigation.ArmorToPhysicalResist(rating),
                    Is.EqualTo(EvasionMitigation.EvasionToDodgeChance(rating)));
            }
        }

        [Test]
        public void DirectHits_CanBeEvaded_DoTsCannot()
        {
            Assert.That(EvasionMitigation.CanEvade(new DamageSnapshot(null) { IsDirectHit = true }), Is.True);
            Assert.That(EvasionMitigation.CanEvade(new DamageSnapshot(null) { IsDirectHit = false }), Is.False);
            Assert.That(EvasionMitigation.CanEvade(null), Is.False);
        }

        [Test]
        public void Accuracy_HasNoAffixGeneration()
        {
            Assert.That(StatsDatabaseSO.IsRetiredStat(StatType.Accuracy), Is.True);
            Assert.That(
                StatsDatabaseSO.DefaultAllowedAffixKindsFor(StatType.Accuracy, StatAffixGenType.FullCalcStat),
                Is.EqualTo(StatAffixModifierKindFlags.None));
            Assert.That(StatsDatabaseSO.DefaultShowInCharacterWindow(StatType.Accuracy), Is.False);
        }
    }
}
