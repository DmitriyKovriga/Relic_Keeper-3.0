using NUnit.Framework;
using Scripts.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class UiTransactionFeedbackTests
    {
        private sealed class TestWindow : EditorWindow { }
        private TestWindow _window;
        private VisualElement _root, _target;
        private UiTransactionFeedback _feedback;
        [SetUp] public void Setup()
        {
            _window = ScriptableObject.CreateInstance<TestWindow>(); _window.Show();
            _root = _window.rootVisualElement; _target = new VisualElement();
            _target.style.width = 24; _target.style.height = 24; _root.Add(_target);
            _feedback = new UiTransactionFeedback(_root);
        }
        [TearDown] public void Cleanup() { _feedback.Dispose(); _window.Close(); }
        [Test] public void RapidTransactionsReplaceOneOverlayWithoutMovingOrBlockingTheTarget()
        {
            for (int i = 0; i < 20; i++) _feedback.Show(_target, true);
            Assert.That(_root.Query<VisualElement>("TransactionFeedback").ToList().Count, Is.EqualTo(1));
            var overlay = _root.Q("TransactionFeedback");
            Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(overlay.Q<Label>().pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(_target.style.width.value.value, Is.EqualTo(24));
            Assert.That(_target.style.height.value.value, Is.EqualTo(24));
            Assert.That(_target.style.translate.keyword, Is.EqualTo(StyleKeyword.Null));
        }
        [Test] public void RejectionHasADifferentShapeAndClearingRemovesIt()
        {
            _feedback.Show(_target, true); Assert.That(_root.Q("TransactionFeedback").Q<Label>().text, Is.EqualTo("+"));
            _feedback.Show(_target, false); Assert.That(_root.Q("TransactionFeedback").Q<Label>().text, Is.EqualTo("!"));
            _feedback.Clear(); Assert.That(_root.Q("TransactionFeedback"), Is.Null);
        }
        [Test] public void DisposeRemovesTheOverlayWithoutChangingTheTargetHierarchy()
        {
            _feedback.Show(_target, true); _feedback.Dispose();
            Assert.That(_root.Q("TransactionFeedback"), Is.Null); Assert.That(_target.parent, Is.SameAs(_root));
        }
        [Test] public void RejectionAfterASuccessReplacesTheGoldCueEvenDuringTheRejectionThrottle()
        {
            _feedback.Show(_target, false); _feedback.Show(_target, true); _feedback.Show(_target, false);
            Assert.That(_root.Q("TransactionFeedback").Q<Label>().text, Is.EqualTo("!"));
        }
    }
}
