using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.GameplayEvents;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveNodeEffectsTests
    {
        private sealed class FakeCooldowns : IPassiveSkillCooldowns
        {
            public readonly List<float> RandomReductions = new List<float>();
            public bool HasSkillOnCooldown = true;

            public bool ReduceRandomSkillCooldown(float seconds, Func<int, int> pickIndex)
            {
                if (!HasSkillOnCooldown)
                    return false;
                RandomReductions.Add(seconds);
                return true;
            }

            public void ReduceAllCooldowns(float seconds) { }
            public void ReduceSkillCooldown(int slotIndex, float seconds) { }
        }

        private GameObject _host;
        private GameObject _enemy;
        private PlayerStats _stats;
        private StatusEffectController _statusEffects;
        private PassiveEffectRuntime _runtime;
        private FakeCooldowns _cooldowns;
        private float _now;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PassiveEffectsHost");
            _stats = _host.AddComponent<PlayerStats>();
            Invoke(_stats, "Awake");
            _statusEffects = _host.GetComponent<StatusEffectController>();
            Invoke(_statusEffects, "Awake");
            Invoke(_statusEffects, "OnEnable");
            _enemy = new GameObject("PassiveEffectsEnemy");

            _now = 100f;
            _cooldowns = new FakeCooldowns();
            _runtime = new PassiveEffectRuntime(_stats)
            {
                Clock = () => _now,
                Random01 = () => 0f,
                Cooldowns = _cooldowns
            };
        }

        [TearDown]
        public void TearDown()
        {
            _runtime?.Dispose();
            Invoke(_statusEffects, "OnDisable");
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_enemy);
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }
        }

        [Test]
        public void MoreCritChance_WhileNotDamagedRecently_ComesBackAfterWindow()
        {
            _stats.GetStat(StatType.CritChance).BaseValue = 10f;
            Allocate(Template(conditional: NotRecently(DamageTaken(PassiveTriState.Any), StatType.CritChance, 50f, StatModType.PercentMult)));

            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(15f).Within(0.01f));

            RaiseTaken(directHit: true);
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(10f).Within(0.01f));

            _now += 3.9f;
            _runtime.Tick();
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(10f).Within(0.01f));

            _now += 0.2f;
            _runtime.Tick();
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void MoreEvasion_WhileNotDamagedRecently_UsesSameConditionForAnotherStat()
        {
            _stats.GetStat(StatType.Evasion).BaseValue = 200f;
            Allocate(Template(conditional: NotRecently(DamageTaken(PassiveTriState.Any), StatType.Evasion, 50f, StatModType.PercentMult)));

            Assert.That(_stats.GetValue(StatType.Evasion), Is.EqualTo(300f).Within(0.01f));
            RaiseTaken(directHit: false);
            Assert.That(_stats.GetValue(StatType.Evasion), Is.EqualTo(200f).Within(0.01f));
        }

        [Test]
        public void NotHitRecently_IgnoresDamageOverTime()
        {
            Allocate(Template(conditional: NotRecently(DamageTaken(PassiveTriState.Yes), StatType.SkillCooldownRecovery, 50f, StatModType.PercentAdd)));
            Assert.That(_runtime.ActiveConditionalCount, Is.EqualTo(1));

            RaiseTaken(directHit: false);
            Assert.That(_runtime.ActiveConditionalCount, Is.EqualTo(1), "poison ticks are not hits");

            RaiseTaken(directHit: true);
            Assert.That(_runtime.ActiveConditionalCount, Is.EqualTo(0));
        }

        [Test]
        public void Crit_ReducesRandomSkillCooldown_NonCritDoesNot()
        {
            Allocate(Template(trigger: ReduceRandomCooldown(DealtHit(PassiveTriState.Yes))));

            RaiseDealt(crit: false);
            Assert.That(_cooldowns.RandomReductions, Is.Empty);

            RaiseDealt(crit: true);
            Assert.That(_cooldowns.RandomReductions, Is.EqualTo(new[] { 1f }));
        }

        [Test]
        public void HitOnPoisonedTarget_ReducesCooldown_UnpoisonedDoesNot()
        {
            PassiveEventFilter filter = DealtHit(PassiveTriState.Any);
            filter.RequireOtherAilment = true;
            filter.OtherAilment = AilmentType.Poison;
            Allocate(Template(trigger: ReduceRandomCooldown(filter)));

            AilmentController ailments = _enemy.AddComponent<AilmentController>();
            RaiseDealt(crit: false);
            Assert.That(_cooldowns.RandomReductions, Is.Empty);

            var poisoner = new GameObject("Poisoner").AddComponent<PlayerStats>();
            _created.Add(poisoner.gameObject);
            poisoner.GetStat(StatType.PoisonChance).BaseValue = 100f;
            Assert.That(ailments.TryApplyPoison(poisoner, poisoner, new DamageSnapshot(poisoner) { Physical = 10f }), Is.True);

            RaiseDealt(crit: false);
            Assert.That(_cooldowns.RandomReductions, Is.EqualTo(new[] { 1f }));
        }

        [Test]
        public void EvadeBuff_Refresh_DoesNotStack()
        {
            _stats.GetStat(StatType.MoveSpeed).BaseValue = 10f;
            Allocate(Template(trigger: Buff(new PassiveEventFilter { Event = GameplayEventType.Evaded, Subject = StatusEventSubject.CarrierAsTarget }, 5f, PassiveBuffStacking.Refresh, 1)));

            GameplayEventBus.Raise(GameplayEventType.Evaded, source: _enemy, target: _host);
            GameplayEventBus.Raise(GameplayEventType.Evaded, source: _enemy, target: _host);

            Assert.That(_statusEffects.ActiveEffects.Count, Is.EqualTo(1));
            Assert.That(_statusEffects.ActiveEffects[0].RemainingSeconds, Is.EqualTo(5f).Within(0.01f));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).GetTotalPercentAdd(), Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void CritBuff_IndependentStacks_CapAtMaxStacks()
        {
            Allocate(Template(trigger: Buff(DealtHit(PassiveTriState.Yes), 1f, PassiveBuffStacking.IndependentStacks, 2)));

            RaiseDealt(crit: true);
            RaiseDealt(crit: true);
            RaiseDealt(crit: true);

            Assert.That(_statusEffects.ActiveEffects.Count, Is.EqualTo(2));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).GetTotalPercentAdd(), Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void InternalCooldown_LimitsActivations()
        {
            PassiveTriggeredEffect effect = ReduceRandomCooldown(DealtHit(PassiveTriState.Yes));
            effect.InternalCooldownSeconds = 2f;
            Allocate(Template(trigger: effect));

            RaiseDealt(crit: true);
            _now += 1f;
            RaiseDealt(crit: true);
            _now += 1.5f;
            RaiseDealt(crit: true);

            Assert.That(_cooldowns.RandomReductions.Count, Is.EqualTo(2));
        }

        [Test]
        public void NoSkillOnCooldown_DoesNotStartInternalCooldown()
        {
            PassiveTriggeredEffect effect = ReduceRandomCooldown(DealtHit(PassiveTriState.Yes));
            effect.InternalCooldownSeconds = 5f;
            Allocate(Template(trigger: effect));

            _cooldowns.HasSkillOnCooldown = false;
            RaiseDealt(crit: true);
            _cooldowns.HasSkillOnCooldown = true;
            RaiseDealt(crit: true);

            Assert.That(_cooldowns.RandomReductions.Count, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileHitScaling_GrowsWithFlightTime_AndCaps()
        {
            var rule = new PassiveHitScalingRule
            {
                Source = PassiveHitScalingSource.ProjectileFlightSeconds,
                SourcePerStep = 0.1f,
                MaxSteps = 10f,
                ModifiersPerStep = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.DamagePhysical, Value = 5f, Type = StatModType.PercentMult }
                }
            };
            Allocate(Template(hitRule: rule));

            var modifiers = new List<SerializableStatModifier>();
            PassiveEffectRuntime.AppendHitModifiers(_stats, PassiveHitContext.Projectile(0.35f, 3f), modifiers);
            Assert.That(modifiers.Count, Is.EqualTo(1));
            Assert.That(modifiers[0].Value, Is.EqualTo(17.5f).Within(0.01f));

            modifiers.Clear();
            PassiveEffectRuntime.AppendHitModifiers(_stats, PassiveHitContext.Projectile(3f, 30f), modifiers);
            Assert.That(modifiers[0].Value, Is.EqualTo(50f).Within(0.01f));

            modifiers.Clear();
            PassiveEffectRuntime.AppendHitModifiers(_stats, new PassiveHitContext(false, 3f, 30f), modifiers);
            Assert.That(modifiers, Is.Empty, "melee hits have no flight");
        }

        [Test]
        public void Rebuild_AfterExternalModifierWipe_ReappliesConditionalOnce()
        {
            _stats.GetStat(StatType.CritChance).BaseValue = 10f;
            PassiveNodeTemplateSO template = Template(conditional: NotRecently(DamageTaken(PassiveTriState.Any), StatType.CritChance, 50f, StatModType.PercentMult));
            var node = new PassiveNodeDefinition { ID = "n1", Template = template };
            _runtime.Rebuild(new[] { node });

            _stats.ClearAllStatModifiers();
            _runtime.Rebuild(new[] { node });

            Assert.That(_stats.GetStat(StatType.CritChance).Modifiers.Count, Is.EqualTo(1));
            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(15f).Within(0.01f));
        }

        [Test]
        public void Dispose_RemovesConditionalModifiers_AndStopsListening()
        {
            _stats.GetStat(StatType.CritChance).BaseValue = 10f;
            Allocate(Template(
                conditional: NotRecently(DamageTaken(PassiveTriState.Any), StatType.CritChance, 50f, StatModType.PercentMult),
                trigger: ReduceRandomCooldown(DealtHit(PassiveTriState.Yes))));

            _runtime.Dispose();
            RaiseDealt(crit: true);

            Assert.That(_stats.GetValue(StatType.CritChance), Is.EqualTo(10f).Within(0.01f));
            Assert.That(_cooldowns.RandomReductions, Is.Empty);
        }

        [Test]
        public void Describer_WritesRussianAndEnglishLines()
        {
            PassiveNodeTemplateSO template = Template(
                conditional: NotRecently(DamageTaken(PassiveTriState.Any), StatType.CritChance, 50f, StatModType.PercentMult),
                trigger: ReduceRandomCooldown(DealtHit(PassiveTriState.Yes)));

            var ru = new List<string>();
            PassiveEffectDescriber.AppendAll(template, true, null, ru);
            Assert.That(ru[0], Does.StartWith("Если вы не получали урон последние 4 с:"));
            Assert.That(ru[1], Is.EqualTo("Когда вы наносите критический удар по врагу, восстановление случайного навыка ускоряется на 1 с."));

            var en = new List<string>();
            PassiveEffectDescriber.AppendAll(template, false, null, en);
            Assert.That(en[1], Is.EqualTo("When you deal a critical hit to an enemy, a random skill on cooldown recovers 1 s faster."));
        }

        // ---------------------------------------------------------------- cooldown progress

        private sealed class TestSkill : SkillBehaviour
        {
            public float Now;
            protected override float CurrentTime => Now;
            protected override void Execute() { }
        }

        [Test]
        public void Cooldown_RecoveryBonusSpeedsUpRemainingTime_WithoutRewritingThePast()
        {
            var skillData = ScriptableObject.CreateInstance<SkillDataSO>();
            _created.Add(skillData);
            skillData.Cooldown = 10f;
            skillData.ManaCost = 0f;
            var skill = _host.AddComponent<TestSkill>();
            skill.Initialize(_stats, skillData);
            skill.SetRuntimeSlot(null, SkillCooldownRecovery.MainSkillSlot);

            skill.Now = 1f;
            skill.TryCast();
            skill.Now = 5f;
            Assert.That(skill.CooldownRemaining, Is.EqualTo(6f).Within(0.01f));

            var bonus = new StatModifier(50f, StatModType.PercentAdd);
            _stats.GetStat(StatType.SkillCooldownRecovery).AddModifier(bonus);
            Assert.That(skill.CooldownRemaining, Is.EqualTo(3f).Within(0.01f), "60% left of a 5 s cooldown");

            skill.Now = 6f;
            Assert.That(skill.CooldownRemaining, Is.EqualTo(2f).Within(0.01f));

            _stats.GetStat(StatType.SkillCooldownRecovery).RemoveModifier(bonus);
            Assert.That(skill.CooldownRemaining, Is.EqualTo(4f).Within(0.01f), "progress is kept when the bonus ends");

            skill.ReduceCooldownRemaining(1f);
            Assert.That(skill.CooldownRemaining, Is.EqualTo(3f).Within(0.01f));

            skill.Now = 9f;
            Assert.That(skill.CooldownRemaining, Is.EqualTo(0f).Within(0.01f));
        }

        // ---------------------------------------------------------------- helpers

        private void Allocate(PassiveNodeTemplateSO template)
        {
            _runtime.Rebuild(new[] { new PassiveNodeDefinition { ID = "node", Template = template } });
        }

        private PassiveNodeTemplateSO Template(
            PassiveConditionalModifiers conditional = null,
            PassiveTriggeredEffect trigger = null,
            PassiveHitScalingRule hitRule = null)
        {
            var template = ScriptableObject.CreateInstance<PassiveNodeTemplateSO>();
            _created.Add(template);
            if (conditional != null) template.ConditionalModifiers.Add(conditional);
            if (trigger != null) template.TriggeredEffects.Add(trigger);
            if (hitRule != null) template.HitScalingRules.Add(hitRule);
            return template;
        }

        private static PassiveEventFilter DamageTaken(PassiveTriState directHit)
        {
            return new PassiveEventFilter { Event = GameplayEventType.DamageTaken, Subject = StatusEventSubject.CarrierAsTarget, DirectHit = directHit };
        }

        private static PassiveEventFilter DealtHit(PassiveTriState crit)
        {
            return new PassiveEventFilter { Event = GameplayEventType.DamageDealt, Subject = StatusEventSubject.CarrierAsSource, DirectHit = PassiveTriState.Yes, Crit = crit };
        }

        private static PassiveConditionalModifiers NotRecently(PassiveEventFilter filter, StatType stat, float value, StatModType type)
        {
            return new PassiveConditionalModifiers
            {
                Condition = new PassiveCondition { Recency = PassiveRecencyMode.NotRecently, WindowSeconds = 4f, Event = filter },
                Modifiers = new List<SerializableStatModifier> { new SerializableStatModifier { Stat = stat, Value = value, Type = type } }
            };
        }

        private static PassiveTriggeredEffect ReduceRandomCooldown(PassiveEventFilter filter)
        {
            return new PassiveTriggeredEffect
            {
                Trigger = filter,
                Action = PassiveTriggerAction.ReduceSkillCooldown,
                CooldownSeconds = 1f,
                CooldownTarget = PassiveCooldownTarget.RandomOnCooldown
            };
        }

        private static PassiveTriggeredEffect Buff(PassiveEventFilter filter, float moveSpeedPercent, PassiveBuffStacking stacking, int maxStacks)
        {
            return new PassiveTriggeredEffect
            {
                Trigger = filter,
                Action = PassiveTriggerAction.ApplyBuff,
                BuffDurationSeconds = 5f,
                BuffModifiers = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.MoveSpeed, Value = moveSpeedPercent, Type = StatModType.PercentAdd }
                },
                Stacking = stacking,
                MaxStacks = maxStacks
            };
        }

        private void RaiseTaken(bool directHit)
        {
            GameplayEventBus.Raise(GameplayEventType.DamageTaken, source: _enemy, target: _host, amount: 5f,
                damage: new DamageSnapshot(_enemy) { Physical = 5f, IsDirectHit = directHit });
        }

        private void RaiseDealt(bool crit)
        {
            GameplayEventBus.Raise(GameplayEventType.DamageDealt, source: _host, target: _enemy, amount: 5f,
                damage: new DamageSnapshot(_stats) { Physical = 5f, IsCrit = crit, IsDirectHit = true });
        }

        private static void Invoke(object target, string methodName)
        {
            if (target == null)
                return;
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(target, null);
        }
    }
}
