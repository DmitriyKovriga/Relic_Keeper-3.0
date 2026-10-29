using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Stats;
using Scripts.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class SkillSlotBindingFeedbackTests
    {
        private GameObject _host, _uiHost;
        private SkillDataSO _data;
        private Sprite _icon;
        private SkillCastFeedbackProbe _runtime;
        private UISkillSlot _slot;
        [SetUp] public void Setup()
        {
            _host = new GameObject("HUD runtime binding"); _host.SetActive(false);
            var stats = _host.AddComponent<PlayerStats>();
            typeof(PlayerStats).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stats, null);
            stats.Initialize(null); stats.GetStat(StatType.MaxMana).BaseValue = 100; stats.NotifyChanged(); stats.Mana.RestoreFull();
            _data = ScriptableObject.CreateInstance<SkillDataSO>(); _data.Cooldown = 5;
            _icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1); _data.Icon = _icon;
            _runtime = _host.AddComponent<SkillCastFeedbackProbe>(); _runtime.Initialize(stats, _data);
            _uiHost = new GameObject("HUD slot", typeof(RectTransform)); _uiHost.SetActive(false);
            var iconHost = new GameObject("Icon", typeof(RectTransform), typeof(Image)); iconHost.transform.SetParent(_uiHost.transform, false);
            _slot = _uiHost.AddComponent<UISkillSlot>();
            typeof(UISkillSlot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_slot, null);
            _slot.Setup(_data, "F", 1); _slot.BindFeedbackRuntime(_runtime);
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_uiHost); Object.DestroyImmediate(_host); Object.DestroyImmediate(_data); Object.DestroyImmediate(_icon);
        }
        private void AcceptAndObserve()
        {
            Assert.That(_runtime.TryCastWithResult(), Is.EqualTo(SkillCastResult.Accepted));
            Assert.That(_slot.NotifyCastResult(_runtime, SkillCastResult.Accepted), Is.EqualTo(SkillSlotCue.None));
            Assert.That(_slot.ObserveFeedbackCooldown(5, true, false, false), Is.False);
        }
        [Test] public void RebindingTheSameSkillForAHudRefreshDoesNotFlashReady()
        {
            AcceptAndObserve(); _slot.Setup(_data, "G", 1); _slot.BindFeedbackRuntime(_runtime);
            Assert.That(_slot.ObserveFeedbackCooldown(0, true, false, false), Is.False);
            Assert.That(_uiHost.transform.Find("SkillStateFrame"), Is.Null);
        }
        [Test] public void ReplacementWithTheSameDataIgnoresOldRuntimeEventsAndCooldownCompletion()
        {
            AcceptAndObserve(); var replacement = _host.AddComponent<SkillCastFeedbackProbe>(); replacement.Initialize(null, _data);
            _slot.BindFeedbackRuntime(replacement);
            Assert.That(_slot.NotifyCastResult(_runtime, SkillCastResult.Cooldown), Is.EqualTo(SkillSlotCue.None));
            Assert.That(_slot.ObserveFeedbackCooldown(0, true, false, false), Is.False);
            Assert.That(_uiHost.transform.Find("SkillStateFrame"), Is.Null);
        }
        [Test] public void ZeroCooldownPrimaryHasNoAcceptedOrReadyPulse()
        {
            _data.Cooldown = 0; _slot.Setup(_data, "Mouse1", 0); _slot.BindFeedbackRuntime(_runtime);
            Assert.That(_runtime.TryCastWithResult(), Is.EqualTo(SkillCastResult.Accepted));
            Assert.That(_slot.NotifyCastResult(_runtime, SkillCastResult.Accepted), Is.EqualTo(SkillSlotCue.None));
            Assert.That(_slot.ObserveFeedbackCooldown(0, true, false, false), Is.False);
            Assert.That(_uiHost.transform.Find("SkillStateFrame"), Is.Null);
        }
        [Test] public void RejectionUsesANonblockingRimWithoutAnOpaqueOutlineOverTheIcon()
        {
            AcceptAndObserve(); _slot.NotifyCastResult(_runtime, SkillCastResult.Cooldown);
            var frame = _uiHost.transform.Find("SkillStateFrame");
            Assert.That(frame.GetComponent<Outline>(), Is.Null, "An opaque uGUI Outline duplicates the entire quad and hides the icon.");
            Assert.That(frame.GetComponent<Image>().color.a, Is.LessThan(.05f));
            foreach (var image in frame.GetComponentsInChildren<Image>()) Assert.That(image.raycastTarget, Is.False);
            _slot.ResetStateFeedback(); Assert.That(frame.gameObject.activeSelf, Is.False);
        }
    }
}
