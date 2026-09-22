using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RelicKeeper.Tests.EditMode
{
    public class DefaultSkillBindingsTests
    {
        private static readonly (string actionName, string path)[] Expected =
        {
            ("FirstSkill", "<Mouse>/leftButton"),
            ("SecondSkill", "<Mouse>/rightButton"),
            ("ThirdSkill", "<Keyboard>/1"),
            ("FourthSkill", "<Keyboard>/2"),
            ("FifthSkill", "<Keyboard>/3"),
            ("SixthSkill", "<Keyboard>/4")
        };

        [Test]
        public void InputAssetAndControlsConfig_UseExpectedSkillDefaults()
        {
            ControlsEditorConfig config = Resources.Load<ControlsEditorConfig>("Controls/ControlsEditorConfig");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.inputActionAsset, Is.Not.Null);

            foreach ((string actionName, string path) in Expected)
            {
                InputAction action = config.inputActionAsset.FindAction(actionName, true);
                int bindingIndex = ControlEntry.GetFirstBindableBindingIndex(action);
                Assert.That(bindingIndex, Is.GreaterThanOrEqualTo(0), $"Missing binding for {actionName}");
                Assert.That(action.bindings[bindingIndex].path, Is.EqualTo(path), $"Input asset mismatch for {actionName}");

                ControlEntry entry = config.entries.Find(candidate => candidate.actionName == actionName);
                Assert.That(entry, Is.Not.Null, $"Missing controls config entry for {actionName}");
                Assert.That(entry.defaultBindingPath, Is.EqualTo(path), $"Controls config mismatch for {actionName}");
            }
        }

        [Test]
        public void RuntimeFallback_UsesExpectedSkillDefaults()
        {
            FieldInfo field = typeof(InputRebindSaver).GetField(
                "DefaultSkillBindings",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);

            var actual = ((string actionName, string path)[])field.GetValue(null);
            Assert.That(actual, Is.EqualTo(Expected));
        }
    }
}
