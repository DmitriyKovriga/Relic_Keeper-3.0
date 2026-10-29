using NUnit.Framework;
using Scripts.Skills;
using Scripts.UI;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class SkillSlotFeedbackStateTests
    {
        [Test] public void InitiallyReadyAndRestoredCooldownsStayQuietWithoutAnAcceptedCast()
        {
            var state = new SkillSlotFeedbackState();
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
            state.ObserveCooldown(4, true, false, false);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [Test] public void ACommittedLongCooldownNotifiesExactlyOnceAfterRecovery()
        {
            var state = new SkillSlotFeedbackState();
            Assert.That(state.RecordAttempt(SkillCastResult.Accepted, 10, 0), Is.EqualTo(SkillSlotCue.None));
            Assert.That(state.ObserveCooldown(10, true, false, false), Is.False);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.True);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [TestCase(0f)] [TestCase(1f)] [TestCase(2.99f)]
        public void PrimariesAndShortCooldownsDoNotPulse(float duration)
        {
            var state = new SkillSlotFeedbackState(); state.RecordAttempt(SkillCastResult.Accepted, duration, 0);
            state.ObserveCooldown(duration, true, false, false);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [Test] public void RejectedCastDoesNotArmReadinessOrProduceAnAcceptedCue()
        {
            var state = new SkillSlotFeedbackState();
            Assert.That(state.RecordAttempt(SkillCastResult.InsufficientMana, 10, 0), Is.EqualTo(SkillSlotCue.NoMana));
            state.ObserveCooldown(10, true, false, false);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [Test] public void RebuildOrWeaponSwapClearsArmedReadiness()
        {
            var state = new SkillSlotFeedbackState(); state.RecordAttempt(SkillCastResult.Accepted, 10, 0);
            state.ObserveCooldown(10, true, false, false); state.Reset();
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [Test] public void ReadinessWaitsForManaCastCompletionAndUnpauseWithoutQueuingPulses()
        {
            var state = new SkillSlotFeedbackState(); state.RecordAttempt(SkillCastResult.Accepted, 10, 0);
            state.ObserveCooldown(10, true, false, false);
            Assert.That(state.ObserveCooldown(0, false, false, false), Is.False);
            Assert.That(state.ObserveCooldown(0, true, true, false), Is.False);
            Assert.That(state.ObserveCooldown(0, true, false, true), Is.False);
            Assert.That(state.ObserveCooldown(0, true, false, false, true), Is.False, "A temporary skill lock must not be labelled ready.");
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.True);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [Test] public void RepeatedDenialsAreBoundedButAChangedReasonAppearsImmediately()
        {
            var state = new SkillSlotFeedbackState();
            Assert.That(state.RecordAttempt(SkillCastResult.Cooldown, 10, 0), Is.EqualTo(SkillSlotCue.Cooldown));
            for (int i = 1; i < 60; i++) Assert.That(state.RecordAttempt(SkillCastResult.Cooldown, 10, i * .01f), Is.EqualTo(SkillSlotCue.None));
            Assert.That(state.RecordAttempt(SkillCastResult.InsufficientMana, 10, .6f), Is.EqualTo(SkillSlotCue.NoMana));
            Assert.That(state.RecordAttempt(SkillCastResult.InsufficientMana, 10, 1.3f), Is.EqualTo(SkillSlotCue.NoMana));
        }
        [Test] public void ANewFailureAfterSuccessIsNotHiddenByAnEarlierRejection()
        {
            var state = new SkillSlotFeedbackState(); state.RecordAttempt(SkillCastResult.Cooldown, 10, 0);
            state.RecordAttempt(SkillCastResult.Accepted, 10, .1f);
            Assert.That(state.RecordAttempt(SkillCastResult.Cooldown, 10, .2f), Is.EqualTo(SkillSlotCue.Cooldown));
        }
        [Test] public void ImmediateRefundsWithoutObservedRecoveryDoNotSpamReady()
        {
            var state = new SkillSlotFeedbackState(); state.RecordAttempt(SkillCastResult.Accepted, 10, 0);
            Assert.That(state.ObserveCooldown(0, true, false, false), Is.False);
        }
        [TestCase(SkillCastResult.Paused)] [TestCase(SkillCastResult.Unavailable)]
        public void MenusAndEmptySlotsStayQuiet(SkillCastResult result)
        {
            Assert.That(new SkillSlotFeedbackState().RecordAttempt(result, 0, 0), Is.EqualTo(SkillSlotCue.None));
        }
    }
}
