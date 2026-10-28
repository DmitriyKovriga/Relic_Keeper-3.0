using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MovementWeightShiftTests
    {
        private CombatPoseProfile _profile;
        [SetUp] public void Setup() => _profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
        [TearDown] public void Cleanup() => Object.DestroyImmediate(_profile);
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void StartBrakingAndSteadyTravelHaveDistinctBoundedResponses(int fps)
        {
            var state = new MovementWeightShift(); float dt = 1f / fps;
            state.Sample(0, 0, 5, 1, dt, true, _profile);
            AttackBodyPose pose = state.Sample(3, 1, 5, 1, dt, true, _profile);
            Assert.That(pose.Degrees, Is.LessThan(0)); Assert.That(pose.Height, Is.LessThan(1));
            for (int i = 0; i < fps; i++) pose = state.Sample(5, 1, 5, 1, dt, true, _profile);
            Assert.That(pose.Degrees, Is.EqualTo(0).Within(.01f));
            pose = state.Sample(0, 0, 5, 1, dt, true, _profile); Assert.That(pose.Degrees, Is.GreaterThan(0));
            for (int i = 0; i < fps * 2; i++) pose = state.Sample(0, 0, 5, 1, dt, true, _profile);
            Assert.That(pose.Degrees, Is.Zero); Assert.That(pose.Height, Is.EqualTo(1));
        }
        [Test] public void FacingFlipPreservesWorldLeanAndPauseHoldsPose()
        {
            var state = new MovementWeightShift(); state.Sample(0, 0, 5, 1, .016f, true, _profile);
            var right = state.Sample(3, 1, 5, 1, .016f, true, _profile);
            var left = state.Sample(3, 1, 5, -1, 0, true, _profile);
            Assert.That(left.Degrees, Is.EqualTo(-right.Degrees)); Assert.That(left.Height, Is.EqualTo(right.Height));
            Assert.That(state.Sample(3, 1, 5, 1, 0, true, _profile).Degrees, Is.EqualTo(right.Degrees));
        }
        [Test] public void ReversalHasWeightButPressingAgainstAWallDoesNot()
        {
            var state = new MovementWeightShift(); state.Sample(5, 1, 5, 1, .016f, true, _profile);
            var turn = state.Sample(2, -1, 5, -1, .016f, true, _profile);
            Assert.That(turn.Height, Is.LessThan(.995f));
            state.Clear(); state.Sample(0, 0, 5, 1, .016f, true, _profile);
            for (int i = 0; i < 100; i++)
            { var blocked = state.Sample(0, 1, 5, 1, .016f, true, _profile); Assert.That(blocked.Degrees, Is.Zero); Assert.That(blocked.Height, Is.EqualTo(1)); }
        }
        [Test] public void SuppressionAndDisableClearHistoryWithoutInventingAResumeImpulse()
        {
            var state = new MovementWeightShift(); state.Sample(0, 0, 5, 1, .016f, true, _profile);
            state.Sample(5, 1, 5, 1, .016f, true, _profile);
            var pose = state.Sample(5, 1, 5, 1, .016f, false, _profile);
            Assert.That(pose.Degrees, Is.Zero); Assert.That(pose.Height, Is.EqualTo(1));
            Assert.That(state.Sample(-5, -1, 5, -1, .016f, true, _profile).Degrees, Is.Zero);
            _profile.EnableMovementWeight = false;
            Assert.That(state.Sample(0, 0, 5, 1, .016f, true, _profile).Height, Is.EqualTo(1));
        }
        [Test] public void ExtremeSpeedsAndUnsafeTuningRemainSmallAndFinite()
        {
            _profile.MovementLeanDegrees = 100; _profile.MovementCompression = 100; _profile.TurnCompression = 100;
            var state = new MovementWeightShift(); state.Sample(0, 0, 5, 1, .016f, true, _profile);
            for (int i = 0; i < 200; i++)
            {
                float direction = i % 2 == 0 ? 1 : -1;
                var pose = state.Sample(direction * 1000, direction, 5, direction, .016f, true, _profile);
                Assert.That(Mathf.Abs(pose.Degrees), Is.LessThanOrEqualTo(4)); Assert.That(pose.Height, Is.InRange(.97f, 1));
            }
            Assert.That(state.Sample(float.NaN, 0, 5, 1, .016f, true, _profile).Height, Is.EqualTo(1));
        }
    }
}
