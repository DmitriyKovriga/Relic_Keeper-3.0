using NUnit.Framework;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class ResourceGainWordingTests
    {
        [Test]
        public void HealthRegenPercent_Flat_PutsTheAmountInsideTheSentence()
        {
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.HealthRegenPercent, 5f, StatModType.Flat, russian: true),
                Is.EqualTo("Восстановление 5% здоровья в сек"));
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.HealthRegenPercent, 5f, StatModType.Flat, russian: false),
                Is.EqualTo("Regenerate 5% Health per second"));
        }

        [Test]
        public void SimilarResourceGains_UseOneSentence()
        {
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.ManaRegenPercent, 3f, StatModType.Flat, russian: true),
                Is.EqualTo("Восстановление 3% маны в сек"));
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.HealthRegen, 4f, StatModType.Flat, russian: true),
                Is.EqualTo("Восстановление 4 здоровья в сек"));
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.HealthOnHit, 2f, StatModType.Flat, russian: true),
                Is.EqualTo("2 здоровья за удар"));
            Assert.That(
                StatPresentation.FormatResourceGainLine(StatType.ManaOnBlock, 1.5f, StatModType.Flat, russian: false),
                Is.EqualTo("1.5 Mana on block"));
        }

        [Test]
        public void ResourceGainIncrease_DoesNotRepeatTheStatLabel()
        {
            Assert.That(
                StatPresentation.ResourceGainAffixTemplate(StatType.HealthRegenPercent, StatAffixModifierKind.Increase, false, true),
                Is.EqualTo("{0}% увеличение процента здоровья в сек"));
            Assert.That(
                StatPresentation.FormatModifierLine(null, StatType.HealthRegenPercent, "Восстановление % здоровья в секунду", 5f, StatModType.Flat, StatPresentation.ModifierLineStyle.StatThenValue),
                Does.Not.Contain(":"));
        }
    }
}
