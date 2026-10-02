using NUnit.Framework;
using Scripts.Skills;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class ChainFirstTargetBoxTests
    {
        [Test]
        public void RaisedStart_ReachesAsFarBelowTheCasterAsAbove()
        {
            Vector2 owner = new Vector2(0f, 0f);
            Vector2 start = new Vector2(0.65f, 0.35f);

            SkillStepRunner.ResolveFirstChainTargetBox(owner, start, 1f, 7f, 2.2f, out Vector2 center, out Vector2 size);

            float top = center.y + size.y * 0.5f;
            float bottom = center.y - size.y * 0.5f;
            Assert.That(top, Is.EqualTo(1.45f).Within(0.001f));
            Assert.That(bottom, Is.EqualTo(-1.45f).Within(0.001f));
            Assert.That(center.x, Is.EqualTo(4.15f).Within(0.001f));
        }

        [Test]
        public void LevelStart_StaysCenteredOnTheCaster()
        {
            SkillStepRunner.ResolveFirstChainTargetBox(Vector2.zero, new Vector2(1f, 0f), -1f, 4f, 2f, out Vector2 center, out Vector2 size);

            Assert.That(center, Is.EqualTo(new Vector2(-1f, 0f)));
            Assert.That(size, Is.EqualTo(new Vector2(4f, 2f)));
        }
    }
}
