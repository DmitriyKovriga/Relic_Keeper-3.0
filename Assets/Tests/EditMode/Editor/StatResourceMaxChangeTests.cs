using NUnit.Framework;
using Scripts.Stats;

namespace RelicKeeper.Tests.EditMode
{
    public class StatResourceMaxChangeTests
    {
        [Test]
        public void MaxIncrease_RaisesCurrentByTheSameAmount()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);

            max.BaseValue = 600f;
            resource.ReevaluateMax();

            Assert.That(resource.Max, Is.EqualTo(600f));
            Assert.That(resource.Current, Is.EqualTo(200f));
        }

        [Test]
        public void MaxIncreaseWhileFull_StaysFull()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);

            max.BaseValue = 600f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(600f));
            Assert.That(resource.Max, Is.EqualTo(600f));
        }

        [Test]
        public void MaxDecreaseWhileFull_ClampsToNewCap()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);

            max.BaseValue = 400f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(400f));
            Assert.That(resource.Max, Is.EqualTo(400f));
        }

        [Test]
        public void MaxDecreaseWhileInjured_DoesNotPullCurrentDownOrKill()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);

            max.BaseValue = 400f;
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(100f));
            Assert.That(resource.Max, Is.EqualTo(400f));
        }

        [Test]
        public void AdjustCurrentForMaxChange_MatchesHealthAndManaPolicy()
        {
            Assert.That(StatResource.AdjustCurrentForMaxChange(500f, 500f, 400f), Is.EqualTo(400f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(100f, 500f, 400f), Is.EqualTo(100f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(100f, 500f, 600f), Is.EqualTo(200f));
            Assert.That(StatResource.AdjustCurrentForMaxChange(0f, 500f, 400f), Is.EqualTo(0f));
        }

        [Test]
        public void SetCurrent_DoesNotTreatExistingMaxAsAnIncrease()
        {
            var max = new CharacterStat(500f);
            var resource = new StatResource(max);
            resource.SetCurrent(100f);
            resource.ReevaluateMax();

            Assert.That(resource.Current, Is.EqualTo(100f));
            Assert.That(resource.Max, Is.EqualTo(500f));
        }
    }
}
