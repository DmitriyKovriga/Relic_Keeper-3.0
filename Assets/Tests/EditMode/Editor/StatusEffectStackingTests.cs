using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class StatusEffectStackingTests
    {
        private GameObject _host;
        private PlayerStats _stats;
        private StatusEffectController _controller;
        private readonly List<Object> _createdAssets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("StatusEffectStackingHost");
            _stats = _host.AddComponent<PlayerStats>();
            _controller = _host.AddComponent<StatusEffectController>();
            InvokeLifecycle(_controller, "Awake");
            InvokeLifecycle(_controller, "OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null)
                InvokeLifecycle(_controller, "OnDisable");

            Object.DestroyImmediate(_host);
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }
        }

        [Test]
        public void AuthoredBuff_ReapplyRefreshesInsteadOfStacking()
        {
            StatusEffectSO effect = CreateBuff("WarriorStep", StatType.MoveSpeed, 30f, StatModType.PercentAdd, 10f);
            _stats.GetStat(StatType.MoveSpeed).BaseValue = 10f;

            _controller.ApplyStatusEffect(effect);
            _controller.ApplyStatusEffect(effect);

            Assert.That(_controller.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).GetTotalPercentAdd(), Is.EqualTo(30f).Within(0.01f));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).Value, Is.EqualTo(13f).Within(0.01f));
        }

        [Test]
        public void AuthoredBuff_SameIdFromDifferentAssets_SharesOneInstance()
        {
            StatusEffectSO first = CreateBuff("SharedBuff", StatType.Armor, 20f, StatModType.PercentAdd, 8f);
            StatusEffectSO second = CreateBuff("SharedBuff", StatType.Armor, 20f, StatModType.PercentAdd, 8f);
            _stats.GetStat(StatType.Armor).BaseValue = 100f;

            _controller.ApplyStatusEffect(first);
            _controller.ApplyStatusEffect(second);

            Assert.That(_controller.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_stats.GetStat(StatType.Armor).GetTotalPercentAdd(), Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void RuntimeBuff_ReapplySameId_RefreshesModifiersAndTimer()
        {
            var modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier
                {
                    Stat = StatType.MoveSpeed,
                    Value = 15f,
                    Type = StatModType.PercentAdd
                }
            };
            _stats.GetStat(StatType.MoveSpeed).BaseValue = 10f;

            _controller.ApplyRuntimeStatusEffect(modifiers, 6f, StatusEffectKind.Buff, this, "SkillQuickSelf:Test:0");
            _controller.ApplyRuntimeStatusEffect(modifiers, 6f, StatusEffectKind.Buff, this, "SkillQuickSelf:Test:0");

            Assert.That(_controller.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_controller.ActiveEffects[0].RemainingSeconds, Is.EqualTo(6f).Within(0.01f));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).GetTotalPercentAdd(), Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void RuntimeBuffs_DifferentIds_CanCoexist()
        {
            var first = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = StatType.MoveSpeed, Value = 10f, Type = StatModType.PercentAdd }
            };
            var second = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = StatType.Armor, Value = 25f, Type = StatModType.PercentAdd }
            };

            _controller.ApplyRuntimeStatusEffect(first, 5f, StatusEffectKind.Buff, this, "SkillQuickSelf:A:0");
            _controller.ApplyRuntimeStatusEffect(second, 5f, StatusEffectKind.Buff, this, "SkillQuickSelf:B:0");

            Assert.That(_controller.ActiveEffects.Count, Is.EqualTo(2));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).GetTotalPercentAdd(), Is.EqualTo(10f).Within(0.01f));
            Assert.That(_stats.GetStat(StatType.Armor).GetTotalPercentAdd(), Is.EqualTo(25f).Within(0.01f));
        }

        [Test]
        public void VeilOfEvasion_ProcDoesNotStackOnRepeatedEvades()
        {
            StatusEffectSO veil = Resources.Load<StatusEffectSO>("StatusEffects/Buffs/Evasion/VeilOfEvasionBuff");
            Assert.That(veil, Is.Not.Null);

            _controller.ApplyStatusEffect(veil);
            GameplayEventBus.Raise(GameplayEventType.Evaded, source: _host, target: _host);
            GameplayEventBus.Raise(GameplayEventType.Evaded, source: _host, target: _host);

            Assert.That(_controller.ActiveEffects.Count, Is.EqualTo(2), "parent buff + one refreshed proc");
            Assert.That(_stats.GetStat(StatType.HealthRegenPercent).GetRawFlatValue(), Is.EqualTo(5f).Within(0.01f));
            Assert.That(_stats.GetStat(StatType.ManaRegenPercent).GetRawFlatValue(), Is.EqualTo(5f).Within(0.01f));
        }

        private StatusEffectSO CreateBuff(string id, StatType stat, float value, StatModType type, float duration)
        {
            StatusEffectSO effect = ScriptableObject.CreateInstance<StatusEffectSO>();
            effect.Id = id;
            effect.Kind = StatusEffectKind.Buff;
            effect.ShowInHud = false;
            effect.BaseDurationSeconds = duration;
            effect.Modifiers = new List<SerializableStatModifier>
            {
                new SerializableStatModifier { Stat = stat, Value = value, Type = type }
            };
            _createdAssets.Add(effect);
            return effect;
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(target, null);
        }
    }
}
