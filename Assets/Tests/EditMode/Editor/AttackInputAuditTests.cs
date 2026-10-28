using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class AttackInputAuditTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private PlayerAttackInput _input;
        private PlayerSkillManager _manager;
        private PlayerStats _stats;
        private SkillDataSO _data;
        private AttackInputAuditProbe _skill;
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private void Press(bool pressed)
        {
            var handlers = (Action<InputAction.CallbackContext>[])typeof(PlayerAttackInput)
                .GetField(pressed ? "_skillStartedHandlers" : "_skillCanceledHandlers", Private).GetValue(_input);
            handlers[0](default);
        }
        private bool Held => ((bool[])typeof(PlayerAttackInput).GetField("_pressedSkillInputs", Private).GetValue(_input))[0];
        [SetUp] public void SetUp()
        {
            _root = new GameObject("Attack input audit"); _root.SetActive(false);
            _input = _root.AddComponent<PlayerAttackInput>(); _manager = _root.GetComponent<PlayerSkillManager>();
            _stats = _root.GetComponent<PlayerStats>();
            Call(_stats, "Awake");
            _stats.Initialize(Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior"));
            _stats.Mana.RestoreFull();
            Call(_manager, "Awake"); Call(_input, "Awake"); Call(_input, "OnEnable");
            _data = ScriptableObject.CreateInstance<SkillDataSO>(); _data.Cooldown = 0; _data.ManaCost = 5;
            _skill = _root.AddComponent<AttackInputAuditProbe>(); _skill.Initialize(_stats, _data);
            ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(_manager))[0] = _skill;
        }
        [TearDown] public void TearDown()
        {
            Call(_input, "OnDisable"); Object.DestroyImmediate(_root); Object.DestroyImmediate(_data);
        }
        [Test] public void HeldInputRetriesOnlyAfterCastFinishes_ReleaseDoesNotQueueAnotherAttack()
        {
            Press(true); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
            Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
            _skill.Finish(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(2));
            Press(false); _skill.Finish(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(2));
        }
        [Test] public void ReleasedTapDuringRecoveryIsCurrentlyNotBuffered()
        {
            _manager.UseSkill(0); Press(true); Call(_input, "Update"); Press(false);
            _skill.Finish(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
        }
        [Test] public void QuickTapBetweenUpdatesStillAttemptsOneAttack()
        {
            Press(true); Press(false); Call(_input, "Update");
            Assert.That(_skill.Executions, Is.EqualTo(1));
            _skill.Finish(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
        }
        [Test] public void CancelAllSkillsClearsHeldIntentBeforeWeaponReplacement()
        {
            Press(true); _manager.CancelAllSkills();
            Assert.That(Held, Is.False, "Dodge/weapon swap cancellation must not transfer old held intent to a replacement skill.");
            Call(_input, "Update"); Assert.That(_skill.Executions, Is.Zero);
        }
        [Test] public void QuickTapRejectedByDodgeDoesNotFireAfterDodgeEnds()
        {
            typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(_input, true);
            Press(true); Press(false); Call(_input, "Update");
            typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(_input, false);
            Call(_input, "Update"); Assert.That(_skill.Executions, Is.Zero);
        }
        [TestCase(GamePauseReason.PauseMenu)] [TestCase(GamePauseReason.Inventory)]
        public void MenuPauseBlocksDirectSkillUseAndRequiresFreshAttackInput(GamePauseReason reason)
        {
            Press(true);
            using (GamePauseService.Acquire(reason))
            {
                _skill.TryCast(); Assert.That(_skill.Executions, Is.Zero, "The direct skill API must also reject menu-time casts.");
                _manager.UseSkill(0); Assert.That(_skill.Executions, Is.Zero, "Skills must not execute while menus pause gameplay.");
                Assert.That(Held, Is.False); Press(true); Assert.That(Held, Is.False);
            }
            Call(_input, "Update"); Assert.That(_skill.Executions, Is.Zero);
            Press(true); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
        }
        [Test] public void ManaAndCooldownRejectionsDoNotSpendResourcesOrQueueReleasedInput()
        {
            _stats.Mana.SetCurrent(0); Press(true); Call(_input, "Update"); Assert.That(_skill.Executions, Is.Zero);
            Press(false); _stats.Mana.RestoreFull(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.Zero);
            _data.Cooldown = 5; Press(true); Call(_input, "Update"); float mana = _stats.Mana.Current;
            _skill.Finish(); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1)); Assert.That(_stats.Mana.Current, Is.EqualTo(mana));
        }
        [Test] public void BothHandsStayIndependent_AndSourceChangesClearOnlyTheirSlot()
        {
            var other = _root.AddComponent<AttackInputAuditProbe>(); other.Initialize(_stats, _data);
            ((Dictionary<int, SkillBehaviour>)typeof(PlayerSkillManager).GetField("_activeSkills", Private).GetValue(_manager))[1] = other;
            Press(true);
            var handlers = (Action<InputAction.CallbackContext>[])typeof(PlayerAttackInput).GetField("_skillStartedHandlers", Private).GetValue(_input);
            handlers[1](default); Call(_input, "Update");
            Assert.That(_skill.Executions, Is.EqualTo(1)); Assert.That(other.Executions, Is.EqualTo(1));
            var changed = (Action<int, SkillDataSO>)typeof(PlayerSkillManager).GetField("OnSkillSlotUpdated", Private).GetValue(_manager);
            changed?.Invoke(0, null);
            var held = (bool[])typeof(PlayerAttackInput).GetField("_pressedSkillInputs", Private).GetValue(_input);
            Assert.That(held[0], Is.False); Assert.That(held[1], Is.True);
            _manager.CancelAllSkills(); Assert.That(held[1], Is.False);
        }
        [Test] public void CombatFreezeDoesNotDiscardHeldAttack_AndMenuDoesNotCancelAnExistingCast()
        {
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0; Press(true); Call(_input, "Update"); Assert.That(_skill.Executions, Is.EqualTo(1));
                using (GamePauseService.Acquire(GamePauseReason.PauseMenu))
                {
                    Assert.That(_skill.IsCasting, Is.True, "Pause suspends existing casts rather than cancelling them.");
                    Assert.That(Held, Is.False);
                }
            }
            finally { Time.timeScale = scale; }
        }
    }
    public sealed class AttackInputAuditProbe : SkillBehaviour
    {
        public int Executions { get; private set; }
        protected override void Execute() { Executions++; _isCasting = true; }
        public void Finish() => _isCasting = false;
    }
}
