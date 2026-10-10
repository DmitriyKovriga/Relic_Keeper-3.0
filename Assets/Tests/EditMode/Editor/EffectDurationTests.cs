using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EffectDurationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private PlayerStats _stats;
        private StatusEffectController _controller;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = Random.state;
            var host = Track(new GameObject("EffectDurationSource"));
            _stats = host.AddComponent<PlayerStats>();
            _controller = host.AddComponent<StatusEffectController>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            Random.state = _randomState;
        }

        [Test]
        public void Calculation_SubstitutesDurationAndAppliesAllModifierLayers()
        {
            CharacterStat template = _stats.GetStat(StatType.EffectDuration);
            template.BaseValue = 999f;
            template.AddModifier(new StatModifier(2f, StatModType.Flat));
            template.AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            template.AddModifier(new StatModifier(10f, StatModType.PercentSub));
            template.AddModifier(new StatModifier(100f, StatModType.PercentMult));
            template.AddModifier(new StatModifier(50f, StatModType.PercentLess));
            Assert.That(EffectDurationCalculator.Resolve(10f, _stats), Is.EqualTo(16.8f).Within(0.0001f));
            Assert.That(EffectDurationCalculator.Resolve(5f, _stats), Is.EqualTo(9.8f).Within(0.0001f));
            Assert.That(template.BaseValue, Is.EqualTo(999f));
            Assert.That(template.Modifiers.Count, Is.EqualTo(5));
        }

        [Test]
        public void SeparateEffects_SnapshotDurationsWithoutSharedMutation()
        {
            Increase(50f);
            StatusEffectSO first = Effect("First", 4f);
            StatusEffectSO second = Effect("Second", 10f);
            _controller.ApplyStatusEffect(first);
            _controller.ApplyStatusEffect(second);
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
            Assert.That(_controller.ActiveEffects[1].DurationSeconds, Is.EqualTo(15f));
            Increase(50f);
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
            _controller.ApplyStatusEffect(first);
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(8f));
            Assert.That(_controller.ActiveEffects[1].DurationSeconds, Is.EqualTo(15f));
            Assert.That(_stats.GetStat(StatType.EffectDuration).BaseValue, Is.Zero);
        }

        [Test]
        public void Debuff_UsesApplyingCharactersStatsInsteadOfTargetsStats()
        {
            Increase(50f);
            var target = Track(new GameObject("EffectDurationTarget"));
            var targetStats = target.AddComponent<PlayerStats>();
            targetStats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(200f, StatModType.PercentAdd));
            var controller = target.AddComponent<StatusEffectController>();
            StatusEffectSO effect = Effect("Debuff", 4f);
            effect.Kind = StatusEffectKind.Debuff;
            controller.ApplyStatusEffect(effect, _stats);
            Assert.That(controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void DurationBuff_RefreshUsesActiveModifiersWithoutCompounding()
        {
            StatusEffectSO effect = Effect("DurationBuff", 4f);
            effect.Modifiers.Add(new SerializableStatModifier { Stat = StatType.EffectDuration, Value = 50f, Type = StatModType.PercentAdd });
            _controller.ApplyStatusEffect(effect);
            _controller.ApplyStatusEffect(effect);
            _controller.ApplyStatusEffect(effect);
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
        }

        [Test]
        public void RuntimeBuff_IsScaledAndUntimedHandleRemainsUntimed()
        {
            Increase(50f);
            var modifiers = new[] { new SerializableStatModifier { Stat = StatType.Armor, Value = 10f, Type = StatModType.Flat } };
            using (var timed = _controller.ApplyRuntimeStatusEffect(modifiers, 4f, StatusEffectKind.Buff))
            {
                Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
            }
            _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(100f, StatModType.Flat));
            using (var untimed = _controller.ApplyRuntimeStatusEffect(modifiers, 0f, StatusEffectKind.Buff))
            {
                Assert.That(untimed, Is.Not.Null);
                Assert.That(_controller.ActiveEffects.Count, Is.Zero);
                Assert.That(_stats.GetValue(StatType.Armor), Is.EqualTo(10f));
            }
            Assert.That(_stats.GetValue(StatType.Armor), Is.Zero);
        }

        [Test]
        public void FullyReducedDuration_DoesNotLeaveActiveModifiers()
        {
            _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(100f, StatModType.PercentLess));
            StatusEffectSO effect = Effect("ZeroDuration", 4f);
            effect.Modifiers.Add(new SerializableStatModifier { Stat = StatType.Armor, Value = 10f, Type = StatModType.Flat });
            Assert.That(_controller.ApplyStatusEffect(effect), Is.False);
            Assert.That(_controller.ActiveEffects.Count, Is.Zero);
            Assert.That(_stats.GetValue(StatType.Armor), Is.Zero);
        }

        [Test]
        public void PoisonBleedIgniteShock_UseFinalAilmentDurationBeforeGlobalModifiers()
        {
            Increase(50f);
            var target = Track(new GameObject("AilmentDurationTarget"));
            var stats = target.AddComponent<EnemyStats>();
            stats.Initialize(null, 1);
            target.AddComponent<EnemyHealth>().Initialize();
            var ailments = target.AddComponent<AilmentController>();
            var hit = new DamageSnapshot(_stats) { Physical = 100f, Fire = 100f, Lightning = 100f };
            foreach (StatType type in new[] { StatType.PoisonChance, StatType.BleedChance, StatType.IgniteChance, StatType.ShockChance })
                _stats.GetStat(type).BaseValue = 100f;
            _stats.GetStat(StatType.PoisonDuration).BaseValue = 4f;
            _stats.GetStat(StatType.BleedDuration).BaseValue = 4f;
            _stats.GetStat(StatType.IgniteDuration).BaseValue = 4f;
            _stats.GetStat(StatType.ShockDuration).BaseValue = 4f;
            Assert.That(ailments.TryApplyPoison(_stats, _stats, hit), Is.True);
            Assert.That(ailments.TryApplyBleed(_stats, _stats, hit), Is.True);
            Assert.That(ailments.TryApplyIgnite(_stats, _stats, hit), Is.True);
            Assert.That(ailments.TryApplyShock(_stats, _stats, hit), Is.True);
            Assert.That(Field<float>(ailments, "_poisonRemainingSeconds"), Is.EqualTo(6f));
            Assert.That(Field<float>(ailments, "_shockRemainingSeconds"), Is.EqualTo(6f));
            // Spread transfers an already resolved duration and must not apply the modifier twice.
            ailments.ReceiveSpreadIgnite(new object(), _stats, 10f, 6f);
            var stacks = Field<System.Collections.IList>(ailments, "_igniteStacks");
            foreach (object stack in stacks)
                Assert.That(stack.GetType().GetField("RemainingSeconds").GetValue(stack), Is.EqualTo(6f));
            var bleeds = Field<System.Collections.IList>(ailments, "_bleedStacks");
            Assert.That(bleeds[0].GetType().GetField("RemainingSeconds").GetValue(bleeds[0]), Is.EqualTo(6f));
        }

        [Test]
        public void EffectDurationNode_AppliesThroughPassiveTreeAndChangesBuffDuration()
        {
            var template = Resources.Load<PassiveNodeTemplateSO>("PassiveTrees/Templates/Utility/NewNotableNode 2/EDNode");
            Assert.That(template, Is.Not.Null);
            var tree = Track(ScriptableObject.CreateInstance<PassiveSkillTreeSO>());
            tree.Nodes.Add(new PassiveNodeDefinition { ID = "duration", Template = template });
            var manager = _stats.gameObject.AddComponent<PassiveTreeManager>();
            typeof(PassiveTreeManager).GetField("_playerStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, _stats);
            manager.SetTreeData(tree);
            manager.LoadState(new List<string> { "duration" });
            _controller.ApplyStatusEffect(Effect("NodeBuff", 10f));
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(14.5f));
        }

        [TestCase("en", "6s")]
        [TestCase("ru", "6 с")]
        public void Tooltip_UsesSameDurationAsRuntime(string locale, string expected)
        {
            Increase(50f);
            var effect = Effect("TooltipBuff", 4f);
            _controller.ApplyStatusEffect(effect);
            string tooltip = SkillDescriptionGenerator.BuildStatusEffectTooltip(effect, locale, durationStats: _stats);
            Assert.That(tooltip, Does.Contain(expected));
            Assert.That(_controller.ActiveEffects[0].DurationSeconds, Is.EqualTo(6f));
        }

        [TestCase("en", "6s")]
        [TestCase("ru", "6 с")]
        public void SkillDescriptions_ScaleAuthoredAndQuickDurations(string locale, string expected)
        {
            Increase(50f);
            var skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            skill.Recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            var definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            definition.Id = "ApplyStatusSelf";
            var authored = new StepEntry { StepDefinition = definition };
            authored.SetOverrideObject("StatusEffect", Effect("TooltipBuff", 4f));
            skill.Recipe.Steps.Add(authored);
            Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, locale, durationStats: _stats), Does.Contain(expected));
            skill.Recipe.Steps.Clear();
            definition.Id = "ApplyQuickStatusSelf";
            var quick = new StepEntry { StepDefinition = definition };
            quick.SetOverrideFloat("QuickStatusDuration", 4f);
            quick.SetOverrideFloat("QuickStatusValue", 30f);
            skill.Recipe.Steps.Add(quick);
            Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, locale, durationStats: _stats), Does.Contain(expected));
        }

        [Test]
        public void Freeze_UsesGlobalDurationModifiers()
        {
            Increase(50f);
            var target = Track(new GameObject("FreezeDurationTarget"));
            var stats = target.AddComponent<EnemyStats>();
            stats.Initialize(null, 1);
            target.AddComponent<EnemyHealth>().Initialize();
            var freeze = target.AddComponent<EnemyFreezeController>();
            var ailments = target.AddComponent<AilmentController>();
            _stats.GetStat(StatType.FreezeChance).BaseValue = 100f;
            _stats.GetStat(StatType.FreezeDuration).BaseValue = 4f;
            float start = Time.time;
            Assert.That(ailments.TryApplyFreeze(_stats, _stats, new DamageSnapshot(_stats) { Cold = 100f }), Is.True);
            Assert.That(Field<float>(freeze, "_frozenUntil") - start, Is.EqualTo(6f).Within(0.01f));
        }

        private void Increase(float percent) => _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(percent, StatModType.PercentAdd));
        private StatusEffectSO Effect(string id, float duration)
        {
            var effect = Track(ScriptableObject.CreateInstance<StatusEffectSO>());
            effect.Id = id;
            effect.BaseDurationSeconds = duration;
            return effect;
        }
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
