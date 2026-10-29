using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class SkillCastFeedbackTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _host;
        private PlayerStats _stats;
        private PlayerSkillManager _manager;
        private SkillCastFeedbackProbe _skill;
        private SkillDataSO _data;
        private readonly List<SkillCastResult> _events = new List<SkillCastResult>();
        [SetUp] public void Setup()
        {
            _host = new GameObject("Skill cast feedback"); _host.SetActive(false);
            _stats = _host.AddComponent<PlayerStats>(); typeof(PlayerStats).GetMethod("Awake", Private).Invoke(_stats, null);
            _stats.Initialize(null); _stats.GetStat(StatType.MaxMana).BaseValue = 100; _stats.NotifyChanged(); _stats.Mana.RestoreFull();
            _manager = _host.AddComponent<PlayerSkillManager>(); typeof(PlayerSkillManager).GetMethod("Awake", Private).Invoke(_manager, null);
            _data = ScriptableObject.CreateInstance<SkillDataSO>(); _data.Cooldown = 5; _data.ManaCost = 25;
            _skill = _host.AddComponent<SkillCastFeedbackProbe>(); _skill.Initialize(_stats, _data); _skill.SetRuntimeSlot(_manager, 1);
            ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(_manager))[1] = _skill;
            _events.Clear(); _manager.OnSkillCastAttempted += (_, _, result) => _events.Add(result);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(_host); Object.DestroyImmediate(_data); }

        [Test] public void AcceptanceChargesOnceAndCooldownRejectionPreservesTheCommittedAttack()
        {
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Accepted));
            var attack = _skill.AcceptedAttack;
            Assert.That(_stats.Mana.Current, Is.EqualTo(75));
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Cooldown));
            Assert.That(_stats.Mana.Current, Is.EqualTo(75)); Assert.That(_skill.Executions, Is.EqualTo(1));
            Assert.That(_skill.AcceptedAttack, Is.SameAs(attack));
            Assert.That(_events, Is.EqualTo(new[] { SkillCastResult.Accepted, SkillCastResult.Cooldown }));
        }
        [Test] public void ManaRejectionDoesNotStartCooldownOrEmitSuccess()
        {
            _stats.Mana.SetCurrent(0);
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.InsufficientMana));
            Assert.That(_skill.CooldownRemaining, Is.Zero); Assert.That(_skill.Executions, Is.Zero);
            Assert.That(_events, Is.EqualTo(new[] { SkillCastResult.InsufficientMana }));
        }
        [Test] public void ExistingBusyBeforeCooldownBeforeManaPriorityIsPreserved()
        {
            _manager.UseSkill(1); _skill.SetBusy(true); _stats.Mana.SetCurrent(0);
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Busy));
            _skill.SetBusy(false);
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Cooldown));
            _skill.Clock = 6;
            Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.InsufficientMana));
        }
        [Test] public void HeldRetriesKeepCastingRulesButDoNotRepeatRejectionEvents()
        {
            _manager.UseSkillWithResult(1); _events.Clear();
            for (int i = 0; i < 60; i++) Assert.That(_manager.UseSkillWithResult(1, false), Is.EqualTo(SkillCastResult.Cooldown));
            Assert.That(_events.Count, Is.Zero);
            _skill.Clock = 6; Assert.That(_manager.UseSkillWithResult(1, false), Is.EqualTo(SkillCastResult.Accepted));
            Assert.That(_events, Is.EqualTo(new[] { SkillCastResult.Accepted })); Assert.That(_skill.Executions, Is.EqualTo(2));
        }
        [Test] public void PausedSuppressedAndEmptySlotsDoNotProduceAcceptedEvents()
        {
            using (GamePauseService.Acquire(GamePauseReason.Inventory))
                Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Paused));
            _manager.SetSkillUsageSuppressed(true); Assert.That(_manager.UseSkillWithResult(1), Is.EqualTo(SkillCastResult.Blocked));
            _manager.SetSkillUsageSuppressed(false); Assert.That(_manager.UseSkillWithResult(5), Is.EqualTo(SkillCastResult.Unavailable));
            Assert.That(_stats.Mana.Current, Is.EqualTo(100)); Assert.That(_events.Count, Is.Zero);
        }
        [Test] public void LegacyVoidCastEntryStillUsesTheSameManaAndCooldownGate()
        {
            _skill.TryCast(); _skill.TryCast();
            Assert.That(_skill.Executions, Is.EqualTo(1)); Assert.That(_stats.Mana.Current, Is.EqualTo(75));
        }
    }
    public sealed class SkillCastFeedbackProbe : SkillBehaviour
    {
        public float Clock;
        public int Executions;
        protected override float CurrentTime => Clock;
        protected override void Execute() { Executions++; }
        public void SetBusy(bool busy) => _isCasting = busy;
    }
}
