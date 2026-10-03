using NUnit.Framework;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class CooldownRecoveryWordingTests
    {
        [Test]
        public void PositiveSeconds_ReadAsFasterRecovery()
        {
            string line = StatPresentation.FormatCooldownRecoveryLine(
                StatType.SkillCooldownRecovery, 5f, StatModType.Flat, false);

            Assert.That(line, Is.EqualTo("+5s faster Skill Recovery"));
        }

        [Test]
        public void NegativeSeconds_ReadAsSlowerRecovery()
        {
            string line = StatPresentation.FormatCooldownRecoveryLine(
                StatType.SkillCooldownRecovery, -5f, StatModType.Flat, false);

            Assert.That(line, Is.EqualTo("+5s slower Skill Recovery"));
        }

        [Test]
        public void RussianPositiveSeconds_IsAFullSentence()
        {
            string line = StatPresentation.FormatCooldownRecoveryLine(
                StatType.SkillCooldownRecovery, 5f, StatModType.Flat, true);

            Assert.That(line, Is.EqualTo("Навыки восстанавливаются на 5 с быстрее"));
        }

        [Test]
        public void DecreasedPercent_ReadsSlowerForTheSlot()
        {
            string line = StatPresentation.FormatCooldownRecoveryLine(
                StatType.HelmetSkillCooldownRecovery, 20f, StatModType.PercentSub, false);

            Assert.That(line, Is.EqualTo("+20% slower Helmet Skill Recovery"));
        }

        [Test]
        public void AffixTemplate_UsesTheSameWords()
        {
            Assert.That(
                StatPresentation.CooldownRecoveryAffixTemplate(StatType.SkillCooldownRecovery, StatAffixModifierKind.Flat, false),
                Is.EqualTo("+{0:0.##}s faster Skill Recovery"));
            Assert.That(
                StatPresentation.CooldownRecoveryAffixTemplate(StatType.BootsSkillCooldownRecovery, StatAffixModifierKind.Decrease, true),
                Is.EqualTo("Навык ботинок восстанавливается на {0}% медленнее"));
        }
    }
}
