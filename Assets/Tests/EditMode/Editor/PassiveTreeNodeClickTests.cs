using NUnit.Framework;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.PassiveTree.UI;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveTreeNodeClickTests
    {
        private sealed class TestWindow : EditorWindow { }
        private TestWindow _window;
        private PassiveSkillTreeSO _tree;
        private PassiveTreeThemeSO _theme;
        private VisualElement _node;
        private int _allocations;
        private int _refunds;

        [SetUp]
        public void SetUp()
        {
            _allocations = _refunds = 0;
            _tree = ScriptableObject.CreateInstance<PassiveSkillTreeSO>();
            _theme = ScriptableObject.CreateInstance<PassiveTreeThemeSO>();
            _tree.Nodes.Add(new PassiveNodeDefinition { ID = "node", Position = Vector2.zero });
            var container = new VisualElement();
            _window = ScriptableObject.CreateInstance<TestWindow>();
            _window.rootVisualElement.Add(container);
            _window.Show();
            var renderer = new PassiveTreeRenderer(container, _theme, new PassiveTreeTooltip(_window.rootVisualElement),
                _ => _allocations++, _ => _refunds++);
            renderer.BuildGraph(_tree);
            _node = container[0];
        }

        [TearDown]
        public void TearDown()
        {
            _window.Close();
            Object.DestroyImmediate(_tree);
            Object.DestroyImmediate(_theme);
        }

        [Test]
        public void PressDoesNotAllocateAndReleaseClickAllocatesExactlyOnce()
        {
            Press();
            Assert.That(_allocations, Is.Zero);
            ReleaseClick();
            Assert.That(_allocations, Is.EqualTo(1));
            ReleaseClick();
            Assert.That(_allocations, Is.EqualTo(1));
        }

        [Test]
        public void ReleaseWithoutPressingThisNodeDoesNotAllocate()
        {
            ReleaseClick();
            Assert.That(_allocations, Is.Zero);
        }

        [Test]
        public void DragReturningToPressPositionDoesNotAllocate()
        {
            Press();
            using (var move = PointerMoveEvent.GetPooled(new Event
                { type = EventType.MouseMove, mousePosition = new Vector2(10f, 0f) }))
                Send(move);
            ReleaseClick();
            Assert.That(_allocations, Is.Zero);
        }

        [Test]
        public void SmallPointerMotionStillAllowsClick()
        {
            Press();
            using (var move = PointerMoveEvent.GetPooled(new Event
                { type = EventType.MouseMove, mousePosition = new Vector2(1f, 1f) }))
                Send(move);
            ReleaseClick();
            Assert.That(_allocations, Is.EqualTo(1));
        }

        [Test]
        public void LeavingNodeCancelsPendingAllocation()
        {
            Press();
            using (var leave = PointerLeaveEvent.GetPooled()) Send(leave);
            ReleaseClick();
            Assert.That(_allocations, Is.Zero);
        }

        [Test]
        public void CancelledPointerDoesNotAllocate()
        {
            Press();
            using (var cancel = PointerCancelEvent.GetPooled()) Send(cancel);
            ReleaseClick();
            Assert.That(_allocations, Is.Zero);
        }

        [Test]
        public void RightButtonStillRefundsWithoutAllocating()
        {
            Press(1);
            ReleaseClick();
            Assert.That(_refunds, Is.EqualTo(1));
            Assert.That(_allocations, Is.Zero);
        }

        private void Press(int button = 0)
        {
            using var press = PointerDownEvent.GetPooled(new Event
                { type = EventType.MouseDown, button = button, mousePosition = Vector2.zero });
            Send(press);
        }

        private void ReleaseClick()
        {
            using var release = PointerUpEvent.GetPooled(new Event
                { type = EventType.MouseUp, button = 0, mousePosition = Vector2.zero });
            using var click = ClickEvent.GetPooled(release);
            Send(click);
        }

        private void Send(EventBase evt)
        {
            evt.target = _node;
            _node.SendEvent(evt);
        }
    }
}
