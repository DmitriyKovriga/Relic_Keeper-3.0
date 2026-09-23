using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public class HudShortcutBarTests
    {
        [Test]
        public void EvaluateBreathAlpha_StaysInSoftYellowRange()
        {
            const float period = 1.6f;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i <= 32; i++)
            {
                float t = period * (i / 32f);
                float alpha = HudShortcutBar.EvaluateBreathAlpha(t, period);
                min = Mathf.Min(min, alpha);
                max = Mathf.Max(max, alpha);
            }

            Assert.That(min, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(max, Is.EqualTo(0.62f).Within(0.001f));
        }

        [Test]
        public void EvaluateBreathAlpha_PeaksAtQuarterPeriod()
        {
            const float period = 1.6f;
            float trough = HudShortcutBar.EvaluateBreathAlpha(period * 0.75f, period);
            float peak = HudShortcutBar.EvaluateBreathAlpha(period * 0.25f, period);
            float mid = HudShortcutBar.EvaluateBreathAlpha(0f, period);

            Assert.That(trough, Is.EqualTo(0.18f).Within(0.001f));
            Assert.That(peak, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(mid, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(peak, Is.GreaterThan(mid));
            Assert.That(mid, Is.GreaterThan(trough));
        }

        [Test]
        public void CreateBarElement_UsesIntegerPixelLayout()
        {
            VisualElement bar = HudShortcutBar.CreateBarElement(default, out VisualElement breath);

            Assert.That(bar.style.left.value.value, Is.EqualTo(HudShortcutBar.ScreenInsetPixels));
            Assert.That(bar.style.top.value.value, Is.EqualTo(HudShortcutBar.ScreenInsetPixels));
            Assert.That(bar.style.width.value.value, Is.EqualTo(HudShortcutBar.BarWidthPixels));
            Assert.That(bar.style.height.value.value, Is.EqualTo(HudShortcutBar.BarHeightPixels));
            Assert.IsNotNull(bar.Q("Inventory"));
            Assert.IsNotNull(bar.Q("Craft"));
            Assert.IsNotNull(bar.Q("Passives"));
            Assert.IsNotNull(bar.Q("Stats"));
            Assert.IsNotNull(bar.Q("Pause"));
            Assert.IsNotNull(breath);
            Assert.AreEqual(PickingMode.Position, bar.pickingMode);
            Assert.AreEqual(PickingMode.Position, bar.Q("Inventory").pickingMode);
        }

        [Test]
        public void GameWindows_RenderAbovePersistentHud()
        {
            Assert.That(WindowManager.WindowSortingOrderBase, Is.GreaterThan(HudShortcutBar.SortingOrder));
        }

        [TestCase("<Keyboard>/i", "I")]
        [TestCase("<Keyboard>/k", "K")]
        [TestCase("<Keyboard>/escape", "Esc")]
        [TestCase("<Mouse>/leftButton", "M1")]
        public void GetBindingLabel_UsesCompactInputSymbols(string path, string expected)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                InputActionMap map = asset.AddActionMap("Player");
                map.AddAction("Shortcut", InputActionType.Button).AddBinding(path);

                Assert.That(HudShortcutBar.GetBindingLabel(asset, "Shortcut"), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ShortcutActions_AreConfiguredAndHaveBindings()
        {
            ControlsEditorConfig config = Resources.Load<ControlsEditorConfig>("Controls/ControlsEditorConfig");
            Assert.That(config, Is.Not.Null);

            string[] actionNames =
            {
                "OpenInventory",
                "OpenCrafting",
                "OpenSkillTree",
                "OpenCharacter",
                "PauseMenu"
            };

            foreach (string actionName in actionNames)
            {
                ControlEntry entry = config.entries.Find(item => item.actionName == actionName);
                Assert.That(entry, Is.Not.Null, $"Missing controls entry for {actionName}");

                InputAction action = config.inputActionAsset.FindAction(actionName, false);
                Assert.That(action, Is.Not.Null, $"Missing input action {actionName}");
                Assert.That(ControlEntry.GetFirstBindableBindingIndex(action), Is.GreaterThanOrEqualTo(0));
            }
        }

        [TestCase(0, "Q")]
        [TestCase(1, "Tab")]
        public void InventoryModeToggle_UsesBindingForDestinationTab(int currentTab, string expected)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                InputActionMap map = asset.AddActionMap("Player");
                map.AddAction("OpenCrafting", InputActionType.Button).AddBinding("<Keyboard>/q");
                map.AddAction("OpenInventory", InputActionType.Button).AddBinding("<Keyboard>/tab");

                MethodInfo resolveLabel = typeof(InventoryUI).GetMethod(
                    "ResolveModeToggleBindingLabel",
                    BindingFlags.Static | BindingFlags.NonPublic);

                Assert.That(resolveLabel, Is.Not.Null);
                Assert.That(resolveLabel.Invoke(null, new object[] { asset, currentTab }), Is.EqualTo(expected));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }
    }
}
