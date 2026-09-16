using NUnit.Framework;
using Scripts.Skills;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillDescriptionHighlightTests
    {
        [Test]
        public void ColorsActionNumberAndQuotedStat()
        {
            string marked = SkillDescriptionHighlight.Colorize("Наносит врагам 100% урона. +50% к параметру «Шанс Яда».");

            Assert.That(marked, Does.Contain($"<color={SkillDescriptionHighlight.ActionColor}>Наносит</color>"));
            Assert.That(marked, Does.Contain($"<color={SkillDescriptionHighlight.NumberColor}>100%</color>"));
            Assert.That(marked, Does.Contain($"<color={SkillDescriptionHighlight.NumberColor}>+50%</color>"));
            Assert.That(marked, Does.Contain($"«<color={SkillDescriptionHighlight.StatColor}>Шанс Яда</color>»"));
        }

        [Test]
        public void ColorsEnglishActionAndDuration()
        {
            string marked = SkillDescriptionHighlight.Colorize("Applies to the character for 12s.");

            Assert.That(marked, Does.Contain($"<color={SkillDescriptionHighlight.ActionColor}>Applies</color>"));
            Assert.That(marked, Does.Contain($"<color={SkillDescriptionHighlight.NumberColor}>12s</color>"));
        }

        [Test]
        public void LeavesAlreadyMarkedTextAlone()
        {
            const string original = "<color=#ffffff>Наносит</color> 10";
            Assert.That(SkillDescriptionHighlight.Colorize(original), Is.EqualTo(original));
        }

        [Test]
        public void LeavesEmptyTextAlone()
        {
            Assert.That(SkillDescriptionHighlight.Colorize(null), Is.Null);
            Assert.That(SkillDescriptionHighlight.Colorize(string.Empty), Is.EqualTo(string.Empty));
        }
    }
}
