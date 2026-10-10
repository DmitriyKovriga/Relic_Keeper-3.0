using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Editor.PassiveTree;
using Scripts.Enemies;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveTargetDistanceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private PlayerStats _owner;
        private EnemyStats _enemyStats;
        private EnemyHealth _health;
        private PassiveEffectRuntime _runtime;
        private PassiveConditionalModifiers _group;
        private Random.State _random;

        [SetUp]
        public void SetUp()
        {
            _random = Random.state;
            _owner = Track(new GameObject("DistanceOwner")).AddComponent<PlayerStats>();
            var enemy = Track(new GameObject("DistanceEnemy"));
            _enemyStats = enemy.AddComponent<EnemyStats>();
            _enemyStats.Initialize(null, 1);
            _enemyStats.TryGetStat(StatType.MaxHealth, out CharacterStat hp);
            hp.BaseValue = 1000f;
            _health = enemy.AddComponent<EnemyHealth>();
            _health.Initialize();
            _runtime = new PassiveEffectRuntime(_owner);
            _group = new PassiveConditionalModifiers
            {
                Destination = PassiveConditionalDestination.EnemyDamageTaken,
                Condition = new PassiveCondition { Kind = PassiveConditionKind.TargetDistance, DistanceUnits = 5f },
                Modifiers = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.DamageTaken, Value = 25f, Type = StatModType.PercentAdd }
                }
            };
            Rebuild(_group);
        }

        [TearDown]
        public void TearDown()
        {
            _runtime.Dispose();
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            Random.state = _random;
        }

        [TestCase(4.99f, 100f)]
        [TestCase(5f, 125f)]
        [TestCase(10f, 125f)]
        public void FarCondition_IncludesBoundary(float distance, float expected)
        {
            _health.transform.position = new Vector3(distance, 0f, 99f);
            Assert.That(Damage(_owner), Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void NearCondition_UsesAtMostComparison()
        {
            _group.Condition.Comparison = PassiveComparison.AtMost;
            _health.transform.position = new Vector3(5f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(125f));
            _health.transform.position = new Vector3(5.01f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(100f));
        }

        [Test]
        public void Distance_IsMeasuredFromOwnerAtDamageTime()
        {
            _health.transform.position = new Vector3(8f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(125f));
            _owner.transform.position = new Vector3(6f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(100f));
        }

        [Test]
        public void Bonus_IsSourceSpecificAndResolvesChildSources()
        {
            _health.transform.position = new Vector3(10f, 0f);
            var other = Track(new GameObject("OtherOwner")).AddComponent<PlayerStats>();
            Assert.That(Damage(other), Is.EqualTo(100f));
            Assert.That(Damage(null), Is.EqualTo(100f));
            var child = Track(new GameObject("SkillSource"));
            child.transform.SetParent(_owner.transform);
            Assert.That(Damage(child), Is.EqualTo(125f));
        }

        [Test]
        public void HitAndDot_UseSameBonusWithoutPersistingTargetModifiers()
        {
            _health.transform.position = new Vector3(10f, 0f);
            Assert.That(_health.TakeDamage(new DamageSnapshot(_owner) { Fire = 100f }), Is.True);
            Assert.That(_health.CurrentHealth, Is.EqualTo(875f));
            _health.ApplyPureDamage(100f, _owner, "Poison");
            Assert.That(_health.CurrentHealth, Is.EqualTo(750f));
            Assert.That(_enemyStats.GetValue(StatType.DamageTaken), Is.Zero);
            Assert.That(_owner.GetValue(StatType.DamageTaken), Is.Zero);
            Assert.That(_runtime.ActiveConditionalCount, Is.Zero);
        }

        [Test]
        public void IncreasedDamageTaken_AddsWithShockAndTargetStatsBeforeMore()
        {
            _health.transform.position = new Vector3(10f, 0f);
            _owner.GetStat(StatType.ShockChance).BaseValue = 100f;
            var ailments = _health.gameObject.AddComponent<AilmentController>();
            Assert.That(ailments.TryApplyShock(_owner, _owner, new DamageSnapshot(_owner) { Lightning = 300f }), Is.True);
            _enemyStats.AddModifier(StatType.DamageTaken, new StatModifier(20f, StatModType.PercentAdd));
            _enemyStats.AddModifier(StatType.DamageTaken, new StatModifier(100f, StatModType.PercentMult));
            Assert.That(Damage(_owner), Is.EqualTo(390f).Within(0.001f));
        }

        [Test]
        public void RebuildAndDispose_RemoveBonusWithoutResidue()
        {
            _health.transform.position = new Vector3(10f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(125f));
            _runtime.Rebuild(new PassiveNodeDefinition[0]);
            Assert.That(Damage(_owner), Is.EqualTo(100f));
            Rebuild(_group);
            Assert.That(Damage(_owner), Is.EqualTo(125f));
            _runtime.Dispose();
            Assert.That(Damage(_owner), Is.EqualTo(100f));
        }

        [Test]
        public void InvalidDestinationOrStat_IsRejectedByEditorAndDoesNotChangeDamage()
        {
            _group.Destination = PassiveConditionalDestination.Owner;
            var issues = new List<string>();
            PassiveEffectValidation.Validate(_group, issues);
            Assert.That(issues, Has.Count.EqualTo(1));
            _runtime.Tick();
            Assert.That(_owner.GetValue(StatType.DamageTaken), Is.Zero);
            _group.Destination = PassiveConditionalDestination.EnemyDamageTaken;
            _group.Modifiers[0] = new SerializableStatModifier { Stat = StatType.Armor, Value = 25f, Type = StatModType.Flat };
            issues.Clear();
            PassiveEffectValidation.Validate(_group, issues);
            Assert.That(issues, Has.Count.EqualTo(1));
            _health.transform.position = new Vector3(10f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(100f));
        }

        [TestCase(false, "cells away", "from you")]
        [TestCase(true, "клеток от вас", "от вас")]
        public void DescriptionAndEditorSummary_ExplainEnemyDistance(bool ru, string distance, string source)
        {
            string text = PassiveEffectDescriber.Describe(_group, ru, PassiveEffectDescriber.DefaultModifierFormatter());
            Assert.That(text, Does.Contain(distance));
            Assert.That(text, Does.Contain(source));
            Assert.That(text, Does.Contain("25"));
            Assert.That(PassiveEffectEditorText.Summarize(_group), Does.Contain("Enemy distance ≥ 5 cells (24 px)"));
        }

        [Test]
        public void PushbackNode_ContainsConfiguredDistanceBonusAndPreservesPushback()
        {
            var template = Resources.Load<PassiveNodeTemplateSO>("PassiveTrees/Templates/Misc/NewNotableNode 2/PushbackNode");
            Assert.That(template, Is.Not.Null);
            Assert.That(template.Modifiers[0].Stat, Is.EqualTo(StatType.Pushback));
            Assert.That(template.Modifiers[0].Value, Is.EqualTo(100f));
            Assert.That(template.ConditionalModifiers[0].Destination, Is.EqualTo(PassiveConditionalDestination.EnemyDamageTaken));
            Assert.That(template.ConditionalModifiers[0].Modifiers[0].Value, Is.EqualTo(25f));
            Assert.That(template.ConditionalModifiers[0].Condition.DistanceUnits, Is.EqualTo(8f));
            Assert.That(template.ConditionalModifiers[0].Condition.DistanceInWorldUnits, Is.EqualTo(8f));
            _runtime.Rebuild(new[] { new PassiveNodeDefinition { ID = "pushback", Template = template } });
            _health.transform.position = new Vector3(7.99f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(100f));
            _health.transform.position = new Vector3(8f, 0f);
            Assert.That(Damage(_owner), Is.EqualTo(125f));
        }

        private float Damage(object source) => DamageTakenCalculator.Apply(100f, _enemyStats, _health.transform, out _, out _, out _, source);
        private void Rebuild(PassiveConditionalModifiers group)
        {
            var template = Track(ScriptableObject.CreateInstance<PassiveNodeTemplateSO>());
            template.ConditionalModifiers.Add(group);
            _runtime.Rebuild(new[] { new PassiveNodeDefinition { ID = "distance", Template = template } });
        }
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
