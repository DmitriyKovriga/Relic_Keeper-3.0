using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RelicKeeper.Tests.EditMode
{
    public class TooltipPinPolicyTests
    {
        [Test]
        public void HoverAlone_NeverPinsTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();

            for (int i = 0; i < 600; i++)
                Assert.That(pin.Tick(lockHeld: false, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);

            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.IsPinned, Is.False);
        }

        [Test]
        public void HoldingLockInput_PinsImmediately()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();

            Assert.That(pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsPinned, Is.True);
            Assert.That(pin.JustPinned, Is.True);
            Assert.That(pin.BlocksReplacement, Is.True);
        }

        [Test]
        public void ControlsConfig_HasRebindableLeftShiftTooltipLock()
        {
            ControlsEditorConfig config = Resources.Load<ControlsEditorConfig>("Controls/ControlsEditorConfig");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.inputActionAsset, Is.Not.Null);

            InputAction action = config.inputActionAsset.FindAction("TooltipLock", true);
            int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);

            Assert.That(bindingIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(action.bindings[bindingIndex].path, Is.EqualTo("<Keyboard>/leftShift"));
            Assert.That(config.GetVisibleEntries().Exists(entry =>
                entry.actionName == "TooltipLock"
                && entry.defaultBindingPath == "<Keyboard>/leftShift"), Is.True);
        }

        [Test]
        public void TooltipLockReader_WorksWhilePlayerActionMapIsDisabled()
        {
            InputActionMap playerMap = InputManager.InputActions.Player.Get();
            bool wasPlayerMapEnabled = playerMap.enabled;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("TooltipLockDisabledMapKeyboard");
            var gameObject = new GameObject("TooltipLockDisabledMapTest");
            gameObject.SetActive(false);

            MethodInfo resolve = typeof(ItemTooltipController).GetMethod(
                "ResolveTooltipLockAction", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo isHeld = typeof(ItemTooltipController).GetMethod(
                "IsTooltipLockHeld", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo dispose = typeof(ItemTooltipController).GetMethod(
                "DisposeTooltipLockReader", BindingFlags.Instance | BindingFlags.NonPublic);

            try
            {
                playerMap.Disable();
                var tooltip = gameObject.AddComponent<ItemTooltipController>();
                resolve.Invoke(tooltip, null);

                InputState.Change(keyboard, new KeyboardState(Key.LeftShift));

                Assert.That(playerMap.enabled, Is.False);
                Assert.That(keyboard.leftShiftKey.isPressed, Is.True);
                Assert.That((bool)isHeld.Invoke(tooltip, null), Is.True);

                dispose.Invoke(tooltip, null);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                Object.DestroyImmediate(gameObject);
                if (wasPlayerMapEnabled)
                    playerMap.Enable();
            }
        }

        [Test]
        public void HeldLock_AllowsMovingFromOwnerToTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(lockHeld: true, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.Tick(lockHeld: true, overOwner: false, overTooltip: true, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.IsPinned, Is.True);
        }

        [Test]
        public void ReleasingLockAwayFromOwner_HidesImmediately()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(lockHeld: false, overOwner: false, overTooltip: false, clickOutsideTooltip: false), Is.True);
            Assert.That(pin.IsVisible, Is.False);
            Assert.That(pin.IsPinned, Is.False);
        }

        [Test]
        public void ReleasingLockOverOwner_ReturnsToHoverMode()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(lockHeld: false, overOwner: true, overTooltip: false, clickOutsideTooltip: false), Is.False);
            Assert.That(pin.IsVisible, Is.True);
            Assert.That(pin.IsPinned, Is.False);
            Assert.That(pin.BlocksReplacement, Is.False);
        }

        [Test]
        public void HeldLock_HidesOnClickOutsideTooltip()
        {
            var pin = new TooltipPinPolicy();
            pin.Show();
            pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: false);

            Assert.That(pin.Tick(lockHeld: true, overOwner: true, overTooltip: false, clickOutsideTooltip: true), Is.True);
            Assert.That(pin.IsVisible, Is.False);
        }

        [Test]
        public void CraftedItemTooltip_ClearsHeldLockBeforeNextHover()
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
                pin.Tick(lockHeld: true, overOwner: true,
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
