using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class DeathScreenFlowTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _host;
        private PanelSettings _panel;
        private DeathScreenOverlay _overlay;
        private UIDocument _document;
        private IEnumerator _sequence;
        private float _clock;
        private bool _held, _pressed;
        private int _callbacks;
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
        private Button Continue => _document.rootVisualElement.Q<Button>("DeathScreenContinue");

        [SetUp]
        public void Setup()
        {
            _host = new GameObject("Owned death UI"); _host.SetActive(false);
            _panel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Inventory/PixelArtPanelSettings.asset"));
            _document = _host.AddComponent<UIDocument>(); _document.panelSettings = _panel;
            _overlay = _host.AddComponent<DeathScreenOverlay>(); Set(_overlay, "_document", _document);
            _host.SetActive(true);
            _clock = 0; _callbacks = 0; _held = _pressed = false;
        }

        [TearDown]
        public void Cleanup()
        {
            (_sequence as IDisposable)?.Dispose();
            Object.DestroyImmediate(_host); Object.DestroyImmediate(_panel);
        }

        private IEnumerator Start(int stage = 7, int kills = 23, Action callback = null)
        {
            _sequence = (IEnumerator)Call(_overlay, "PlayDeathSequenceCore", callback ?? (() => _callbacks++), stage, kills,
                (Func<float>)(() => { _clock += .1f; return .1f; }), (Func<bool>)(() => _held), (Func<bool>)(() => _pressed));
            Assert.That(_sequence.MoveNext(), Is.True);
            return _sequence;
        }

        private void Finish()
        {
            int limit = 200;
            while (_sequence.MoveNext() && --limit > 0) { }
            Assert.That(limit, Is.GreaterThan(0), "Sequence must finish without a real-time wait.");
        }

        [Test]
        public void FreshContinueShortensWaitButCallsReturnOnceOnlyAfterCoveredFrame()
        {
            bool sawCoverYield = false;
            Start(callback: () =>
            {
                Assert.That(sawCoverYield, Is.True, "There must be a yielded covered frame before the callback.");
                Assert.That(Continue.style.opacity.value, Is.Zero);
                _callbacks++;
            });
            Assert.That(_overlay.TryContinue(), Is.False);
            while (_clock < 1.2f) Assert.That(_sequence.MoveNext(), Is.True);
            Assert.That(_overlay.TryContinue(), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(_overlay.TryContinue(), Is.False);
            int limit = 100;
            while (_sequence.MoveNext() && --limit > 0)
            {
                var background = _document.rootVisualElement.Q("DeathScreenBackground");
                if (background.style.backgroundColor.value.a == 1 && Continue.style.opacity.value == 0)
                    sawCoverYield = true;
            }
            Assert.That(limit, Is.GreaterThan(0));
            Assert.That(_callbacks, Is.EqualTo(1));
            Assert.That(_clock, Is.LessThan(4f));
            Assert.That(_document.rootVisualElement.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(_overlay.TryContinue(), Is.False);
        }

        [Test]
        public void HeldConfirmationCannotSkipUntilReleasedThenPressedAgain()
        {
            _held = _pressed = true;
            Start();
            while (_clock < 1.6f) Assert.That(_sequence.MoveNext(), Is.True);
            Assert.That(_overlay.TryContinue(), Is.False);
            Assert.That(Continue.enabledSelf, Is.False);
            _held = _pressed = false;
            Assert.That(_sequence.MoveNext(), Is.True);
            Assert.That(Continue.enabledSelf, Is.True);
            _held = _pressed = true;
            Assert.That(_sequence.MoveNext(), Is.True);
            Assert.That((bool)Get(_overlay, "_continueRequested"), Is.True);
            Finish();
            Assert.That(_callbacks, Is.EqualTo(1));
            Assert.That(_clock, Is.LessThan(4f));
        }

        [Test]
        public void AutomaticReturnRetainsSixSecondSummaryWhenNoInputIsGiven()
        {
            float callbackTime = 0;
            Start(callback: () => { callbackTime = _clock; _callbacks++; });
            Finish();
            Assert.That(callbackTime, Is.InRange(6f, 6.3f));
            Assert.That(_callbacks, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentPlayCannotReplaceStatisticsOrRunASecondReturn()
        {
            Start();
            var duplicate = _overlay.PlayDeathSequence(() => _callbacks += 100, 99, 999);
            Assert.That(duplicate.MoveNext(), Is.False);
            Assert.That(_document.rootVisualElement.Q<Label>("DeathScreenHighestStage").text, Does.EndWith(": 7"));
            Assert.That(_document.rootVisualElement.Q<Label>("DeathScreenMonstersKilled").text, Does.EndWith(": 23"));
            Finish();
            Assert.That(_callbacks, Is.EqualTo(1));
        }

        [Test]
        public void DisposalClearsControlAndAllowsNextSequenceWithoutStaleConfirmation()
        {
            Start();
            while (_clock < 1.2f) _sequence.MoveNext();
            Assert.That(_overlay.TryContinue(), Is.True);
            ((IDisposable)_sequence).Dispose();
            Assert.That(_overlay.TryContinue(), Is.False);
            Assert.That(_callbacks, Is.Zero);
            Start(12, 60);
            Assert.That(_overlay.TryContinue(), Is.False);
            Assert.That(_document.rootVisualElement.Q<Label>("DeathScreenMonstersKilled").text, Does.EndWith(": 60"));
            Assert.That(_document.rootVisualElement.Q("DeathScreenBackground").pickingMode, Is.EqualTo(PickingMode.Position));
        }

        [Test]
        public void MissingDocumentStillInvokesReturnExactlyOnceWithoutWaiting()
        {
            Set(_overlay, "_document", null);
            _sequence = _overlay.PlayDeathSequence(() => _callbacks++);
            Assert.That(_sequence.MoveNext(), Is.False);
            Assert.That(_callbacks, Is.EqualTo(1));
            Assert.That(_overlay.TryContinue(), Is.False);
        }

        [TestCase("en", "Return to hub")]
        [TestCase("ru", "Вернуться в хаб")]
        public void ContinueLabelHasEnglishAndRussianTableEntries(string locale, string expected)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Localization/LocalizationTables/MenuLabels_" + locale + ".asset");
            Assert.That(table.GetEntry(DeathScreenOverlay.ReturnToHubKey)?.LocalizedValue, Is.EqualTo(expected));
        }
    }
}
