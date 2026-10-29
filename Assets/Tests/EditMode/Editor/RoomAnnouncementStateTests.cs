using NUnit.Framework;
using Scripts.UI;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class RoomAnnouncementStateTests
    {
        [TestCase(true)] [TestCase(false)]
        public void LevelUpAndRoomResolutionMergeInEitherOrderWithoutLosingInformation(bool levelFirst)
        {
            var state = new RoomAnnouncementState();
            state.Add(levelFirst ? RoomNotice.LevelUp : RoomNotice.PortalUnlocked, 0);
            state.Add(levelFirst ? RoomNotice.PortalUnlocked : RoomNotice.LevelUp, .05f);
            state.Add(RoomNotice.Cleared, .1f);
            Assert.That(state.Current, Is.EqualTo(RoomNotice.LevelUp | RoomNotice.PortalUnlocked | RoomNotice.Cleared));
            Assert.That(state.StartedAt, Is.Zero, "A merge must not restart the fade-in.");
            Assert.That(state.IsVisible(.3f), Is.True);
        }
        [Test] public void RepeatedEventsDoNotExtendOrQueueTheSameMessage()
        {
            var state = new RoomAnnouncementState(); state.Add(RoomNotice.PortalUnlocked, 0); float end = state.ExpiresAt;
            for (int i = 0; i < 20; i++) Assert.That(state.Add(RoomNotice.PortalUnlocked, .01f * i), Is.False);
            Assert.That(state.ExpiresAt, Is.EqualTo(end)); Assert.That(state.IsVisible(end), Is.False);
        }
        [Test] public void ANewClearAfterTheOldUnlockExpiresIsNotANewUnlockCelebration()
        {
            var state = new RoomAnnouncementState(); state.Add(RoomNotice.PortalUnlocked, 0);
            state.Add(RoomNotice.Cleared, 4);
            Assert.That(state.Current, Is.EqualTo(RoomNotice.Cleared));
        }
        [Test] public void HideOrTransitionDropsAllPendingInformationAndHasNoReplay()
        {
            var state = new RoomAnnouncementState(); state.Add(RoomNotice.LevelUp | RoomNotice.Cleared, 0); state.Clear();
            Assert.That(state.Current, Is.EqualTo(RoomNotice.None)); Assert.That(state.IsVisible(.2f), Is.False);
            state.Add(RoomNotice.PortalUnlocked, 1); Assert.That(state.Current, Is.EqualTo(RoomNotice.PortalUnlocked));
        }
        [Test] public void OpacityHasABoundedFadeWithoutMovingTheAnnouncement()
        {
            var state = new RoomAnnouncementState(); state.Add(RoomNotice.Cleared, 0);
            Assert.That(state.Opacity(0), Is.Zero); Assert.That(state.Opacity(.3f), Is.EqualTo(1));
            Assert.That(state.Opacity(state.ExpiresAt - .1f), Is.InRange(0.01f, .99f));
            Assert.That(state.Opacity(state.ExpiresAt), Is.Zero);
        }
        [TestCase(.5f, .5f, ExitHintDirection.None)]
        [TestCase(0f, 1f, ExitHintDirection.None)]
        [TestCase(1.2f, .5f, ExitHintDirection.Right)]
        [TestCase(-.2f, .5f, ExitHintDirection.Left)]
        [TestCase(.5f, 1.2f, ExitHintDirection.Above)]
        [TestCase(.5f, -.2f, ExitHintDirection.Below)]
        public void DirectionHintOnlyPointsToAnOffscreenExit(float x, float y, ExitHintDirection expected)
        {
            Assert.That(RoomAnnouncementState.ExitDirection(new Vector3(x, y, 10)), Is.EqualTo(expected));
        }
        [Test] public void BehindCameraIsNotPresentedAsAValidNavigationDirection()
        {
            Assert.That(RoomAnnouncementState.ExitDirection(new Vector3(3, 3, -1)), Is.EqualTo(ExitHintDirection.None));
        }
    }
}
