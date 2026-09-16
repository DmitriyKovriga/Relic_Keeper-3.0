using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class TooltipPinPolicyTests
    {
        [Test]
        public void Unpinned_HidesImmediatelyWhenLeavingOwner()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();

            Assert.That(pin.Tick(0.5f, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsPinned, Is.False);
            Assert.That(pin.Tick(0.01f, overOwner: false, overTooltip: true, clickOutsideTooltip: false), Is.True);
            Assert.That(pin.IsVisible, Is.False);
        }

        [Test]
        public void Pinned_GapBetweenOwnerAndTooltipDoesNotHide()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(0.1f, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.Tick(0.1f, overOwner: false, overTooltip: true, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.IsPinned, Is.True);
        }

        [Test]
        public void StayingOnOwner_PinsAfterTwoSeconds()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();

            Assert.That(pin.Tick(1.99f, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsPinned, Is.False);
            Assert.That(pin.Tick(0.02f, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsPinned, Is.True);
            Assert.That(pin.JustPinned, Is.True);
        }

        [Test]
        public void Pinned_AllowsMovingOntoTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(0.5f, overOwner: false, overTooltip: true, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.IsPinned, Is.True);
        }

        [Test]
        public void Pinned_HidesOnClickOutsideTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(0f, overOwner: true, overTooltip: false, clickOutsideTooltip: true), Is.True);
            Assert.That(pin.IsVisible, Is.False);
        }

        [Test]
        public void Pinned_HidesAfterTwoSecondsAwayFromOwnerAndTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(1.99f, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.Tick(0.02f, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.True);
            Assert.That(pin.IsVisible, Is.False);
        }

        [Test]
        public void Pinned_ReturningToOwnerResetsAwayTimer()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);
            pin.Tick(1.5f, overOwner: false, overTooltip: false, clickOutsideTooltip: false);
            Assert.That(pin.Tick(0.1f, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.Tick(1.5f, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
        }

        [Test]
        public void PinProgress_FillsWhileHoveringOwnerThenLocks()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();

            Assert.That(pin.PinProgress, Is.EqualTo(0f));
            pin.Tick(1f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);
            Assert.That(pin.PinProgress, Is.EqualTo(0.5f).Within(0.001f));
            pin.Tick(1f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);
            Assert.That(pin.IsPinned, Is.True);
            Assert.That(pin.PinProgress, Is.EqualTo(1f));
        }

        [Test]
        public void Pinned_BlocksReplacementUntilClosed()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            Assert.That(pin.BlocksReplacement, Is.False);

            pin.Tick(2f, overOwner: true, overTooltip: false, clickOutsideTooltip: false);
            Assert.That(pin.BlocksReplacement, Is.True);

            pin.Tick(2f, overOwner: false, overTooltip: false, clickOutsideTooltip: false);
            Assert.That(pin.IsVisible, Is.False);
            Assert.That(pin.BlocksReplacement, Is.False);
        }

        [Test]
        public void CraftedItemTooltip_ClearsOldPinBeforeNextHover()
        {
            var gameObject = new GameObject("CraftTooltipPinTest");
            gameObject.SetActive(false);
            try
            {
                var tooltip = gameObject.AddComponent<ItemTooltipController>();
                var field = typeof(ItemTooltipController).GetField("_pin",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var pin = (TooltipPinPolicy)field.GetValue(tooltip);
                pin.Show();
                pin.Tick(TooltipPinPolicy.PinAfterSeconds, overOwner: true,
                    overTooltip: false, clickOutsideTooltip: false);
                Assert.That(pin.BlocksReplacement, Is.True);

                tooltip.ShowCraftedItemTooltip(null, null, ItemTooltipPriceMode.None);

                Assert.That(pin.IsVisible, Is.False);
                Assert.That(pin.BlocksReplacement, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
