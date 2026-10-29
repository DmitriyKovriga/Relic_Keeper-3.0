using NUnit.Framework;
using Scripts.Visuals;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class CombatDecorationBudgetTests
    {
        [TestCase(false, 384)] [TestCase(true, 96)]
        public void LongCrowdRunNeverExceedsLiveParticleLimit(bool reduced, int limit)
        {
            var budget = new CombatParticleBudget();
            Assert.That(CombatEffectsPolicy.ParticleDecorationLimit(reduced), Is.EqualTo(limit));
            int admitted = 0;
            for (int tick = 0; tick < 10000; tick++)
            {
                admitted += budget.Admit(40, .75f, tick / 120f, limit);
                Assert.That(budget.Count, Is.LessThanOrEqualTo(limit));
            }
            Assert.That(admitted, Is.GreaterThan(limit));
            Assert.That(budget.Admit(limit, .75f, 1000, limit), Is.EqualTo(limit));
        }

        [Test]
        public void ExpiryPauseLowerLimitAndDeniedRequestsDoNotCreateBacklog()
        {
            var budget = new CombatParticleBudget();
            Assert.That(budget.Admit(384, 1, 0, 384), Is.EqualTo(384));
            for (int i = 0; i < 100; i++) Assert.That(budget.Admit(100, 1, 0, 384), Is.Zero);
            Assert.That(budget.Admit(10, 1, .5f, 96), Is.Zero);
            Assert.That(budget.Admit(10, 1, 1, 96), Is.EqualTo(10));
            Assert.That(budget.Count, Is.EqualTo(10));
        }

        [TestCase(-1)] [TestCase(0)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidParticleLifetimeCannotReserveCapacity(float life)
        {
            var budget = new CombatParticleBudget();
            Assert.That(budget.Admit(10, life, 0, 100), Is.Zero);
            Assert.That(budget.Count, Is.Zero);
        }
    }
}
