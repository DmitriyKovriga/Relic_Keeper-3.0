using System.Collections;
using NUnit.Framework;
using Scripts.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

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

        [UnityTest]
        public IEnumerator CircularNodesKeepRoundBordersAndClearCornersAtEverySize()
        {
            _feedback.Dispose(); _feedback = new UiTransactionFeedback(_root, circular: true);
            foreach (float size in new[] { 24f, 32f, 48f })
            {
                _target.style.width = _target.style.height = size;
                yield return null;
                foreach (bool accepted in new[] { true, false })
                {
                    _feedback.Show(_target, accepted);
                    yield return null;
                    var cue = _root.Q("TransactionFeedback");
                    Assert.That(cue, Is.Not.Null);
                    Assert.That(cue.style.borderTopLeftRadius.value.value, Is.EqualTo(size / 2).Within(.1f));
                    Assert.That(cue.style.borderTopRightRadius.value.value, Is.EqualTo(size / 2).Within(.1f));
                    Assert.That(cue.style.borderBottomLeftRadius.value.value, Is.EqualTo(size / 2).Within(.1f));
                    Assert.That(cue.style.borderBottomRightRadius.value.value, Is.EqualTo(size / 2).Within(.1f));
                    Assert.That(cue.style.backgroundColor.value.a, Is.Zero, "Node icons must not receive a rectangular wash.");
                    Assert.That(cue.Q<Label>(), Is.Null, "No corner badge should hang outside a circular node.");
                    Assert.That(cue.pickingMode, Is.EqualTo(PickingMode.Ignore));
                }
            }
        }

        [UnityTest]
        public IEnumerator CircularCueTracksFractionalPanAndZoomWithoutMovingTheNode()
        {
            _feedback.Dispose();
            var viewport = new VisualElement();
            viewport.style.position = Position.Absolute;
            viewport.style.left = 27.25f; viewport.style.top = 30.75f;
            viewport.style.width = 100; viewport.style.height = 100;
            _root.Add(viewport); viewport.Add(_target);
            _feedback = new UiTransactionFeedback(viewport, circular: true);
            viewport.style.transformOrigin = new TransformOrigin(0, 0);
            viewport.transform.scale = Vector3.one * .65f;
            yield return null;
            _feedback.Show(_target, true);
            yield return null;
            var cue = _root.Q("TransactionFeedback");
            Assert.That(Vector2.Distance(cue.worldBound.center, _target.worldBound.center), Is.LessThan(.1f));
            Assert.That(cue.worldBound.width, Is.EqualTo(_target.worldBound.width).Within(.1f));
            Assert.That(_target.style.width.value.value, Is.EqualTo(24));
            Assert.That(_target.transform.scale, Is.EqualTo(Vector3.one));
            _feedback.Clear();
            Assert.That(_root.Q("TransactionFeedback"), Is.Null);
        }
    }
}
