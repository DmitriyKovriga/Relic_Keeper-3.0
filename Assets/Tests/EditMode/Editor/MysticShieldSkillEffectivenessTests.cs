using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class MysticShieldSkillEffectivenessTests
    {
        private readonly List<Object> _created = new List<Object>();
        private PlayerStats _stats;
        private SkillStepRunner _runner;
        private SkillStepContext _context;
        private StatusEffectController _effects;
        private Random.State _randomState;

        [SetUp]
        public void SetUp()
        {
            _randomState = Random.state;
            var host = Track(new GameObject("ShieldEffectiveness"));
            _stats = host.AddComponent<PlayerStats>();
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness);
            _effects = host.AddComponent<StatusEffectController>();
            _runner = host.AddComponent<SkillStepRunner>();
            typeof(SkillBehaviour).GetField("_ownerStats", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_runner, _stats);
            NewContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context.Cleanup();
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            Random.state = _randomState;
        }

        [Test]
        public void DefaultOneAndOrdinaryModifierLayers()
        {
            var stat = _stats.GetStat(StatType.MysticShieldSkillEffectiveness);
            Assert.That(stat.BaseValue, Is.EqualTo(1f));
            Assert.That(MysticShieldSkillEffectiveness.Resolve(null), Is.EqualTo(1f));
            stat.AddModifier(new StatModifier(1f, StatModType.Flat));
            stat.AddModifier(new StatModifier(100f, StatModType.PercentAdd));
            stat.AddModifier(new StatModifier(50f, StatModType.PercentMult));
            Assert.That(MysticShieldSkillEffectiveness.Resolve(_stats), Is.EqualTo(6f));
        }

        [Test]
        public void ActualConsumptionEnablesScalingAndFailedConsumptionDoesNot()
        {
            DoubleEffectiveness();
            _stats.GetStat(StatType.MaxMysticShield).BaseValue = 1f;
            MysticShieldController.TryResolve(_stats.transform, out var shield);
            shield.FillCharges();
            var damage = Step("DealDamageCircle");
            damage.SetOverrideFloat("DamageMultiplier", 2f);
            Assert.That(Damage(damage), Is.EqualTo(2f));
            Invoke("ExecuteConsumeMysticShield", Step("ConsumeMysticShield"));
            Assert.That(_context.MysticShieldsConsumed, Is.EqualTo(1));
            Assert.That(Damage(damage), Is.EqualTo(4f));
            NewContext();
            Invoke("ExecuteConsumeMysticShield", Step("ConsumeMysticShield"));
            Assert.That(_context.MysticShieldsConsumed, Is.Zero);
            Assert.That(Damage(damage), Is.EqualTo(2f));
        }

        [Test]
        public void SuccessfulGenerationEnablesScalingButFullShieldDoesNot()
        {
            DoubleEffectiveness();
            _stats.GetStat(StatType.MaxMysticShield).BaseValue = 2f;
            MysticShieldController.TryResolve(_stats.transform, out var shield);
            shield.FillCharges();
            Invoke("ExecuteGenerateMysticShield", Step("GenerateMysticShield"));
            Assert.That(Damage(Step("DealDamageCircle")), Is.EqualTo(1f));
            shield.TryConsumeCharges(1, out _);
            Invoke("ExecuteGenerateMysticShield", Step("GenerateMysticShield"));
            Assert.That(_context.MysticShieldsGenerated, Is.EqualTo(1));
            Assert.That(Damage(Step("DealDamageCircle")), Is.EqualTo(2f));
            Assert.That(shield.CurrentCharges, Is.EqualTo(2));
        }

        [Test]
        public void FirstSuccessfulUseSnapshotsEffectivenessWithoutChargeCompounding()
        {
            DoubleEffectiveness();
            _context.RegisterMysticShieldConsumption(0);
            Assert.That(_context.MysticShieldEffectivenessMultiplier, Is.EqualTo(1f));
            _context.RegisterMysticShieldConsumption(2);
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness).AddModifier(new StatModifier(100f, StatModType.PercentAdd));
            _context.RegisterMysticShieldGeneration(1);
            Assert.That(_context.MysticShieldEffectivenessMultiplier, Is.EqualTo(2f));
        }

        [TestCase(1, 2f, 4f)]
        [TestCase(2, 3f, 6f)]
        public void ExplicitDamageBonusScalesOnlyBonusAndCombinesWithBaseScaling(int charges, float shieldMultiplier, float total)
        {
            DoubleEffectiveness();
            _context.RegisterMysticShieldConsumption(charges);
            Invoke("ExecuteMysticShieldDamageBoost", Step("MysticShieldDamageBoost"));
            Assert.That(_context.MysticShieldDamageMultiplier, Is.EqualTo(shieldMultiplier));
            Assert.That(Damage(Step("DealDamageCircle")), Is.EqualTo(total));
        }

        [TestCase(0f, 50f, 0f)]
        [TestCase(5f, 100f, 10f)]
        public void QuickBuffUsesRuntimeScalingButInstantBuffRetainsAuthoredValue(float duration, float value, float finalDuration)
        {
            DoubleEffectiveness();
            _context.RegisterMysticShieldConsumption(1);
            var step = Step("ApplyQuickStatusSelfPerConsumedMysticShield");
            step.SetOverrideInt("QuickStatusStat", (int)StatType.SpellDamage);
            step.SetOverrideInt("QuickStatusModType", (int)StatModType.PercentMult);
            step.SetOverrideFloat("QuickStatusValue", 50f);
            step.SetOverrideFloat("QuickStatusDuration", duration);
            Invoke("ApplyQuickStatusToController", _effects, step, 1, "Test");
            Assert.That(_stats.GetStat(StatType.SpellDamage).Modifiers[0].Value, Is.EqualTo(value));
            if (duration > 0f)
                Assert.That(_effects.ActiveEffects[0].DurationSeconds, Is.EqualTo(finalDuration));
            else
            {
                Assert.That(_effects.ActiveEffects.Count, Is.Zero);
                _context.Cleanup();
                Assert.That(_stats.GetStat(StatType.SpellDamage).Modifiers.Count, Is.Zero);
            }
            Assert.That(step.GetFloat("QuickStatusValue"), Is.EqualTo(50f));
        }

        [Test]
        public void TimedBuffAndDurationComposeWithoutMutatingAuthoredData()
        {
            var effect = Effect(5f);
            effect.Modifiers.Add(new SerializableStatModifier { Stat = StatType.MoveSpeed, Value = 20f, Type = StatModType.PercentAdd });
            _stats.GetStat(StatType.EffectDuration).AddModifier(new StatModifier(50f, StatModType.PercentAdd));
            _effects.ApplyStatusEffect(effect, _stats, _stats, 2f);
            Assert.That(_effects.ActiveEffects[0].DurationSeconds, Is.EqualTo(15f));
            Assert.That(_stats.GetStat(StatType.MoveSpeed).Modifiers[0].Value, Is.EqualTo(40f));
            _effects.ApplyStatusEffect(effect, _stats, _stats, 2f);
            Assert.That(_stats.GetStat(StatType.MoveSpeed).Modifiers.Count, Is.EqualTo(1));
            Assert.That(effect.DurationSeconds, Is.EqualTo(5f));
            Assert.That(effect.Modifiers[0].Value, Is.EqualTo(20f));
        }

        [Test]
        public void DerivedBuffScalesResultAndSeparateEffectsRetainTheirSnapshots()
        {
            _stats.GetStat(StatType.Armor).BaseValue = 100f;
            var first = Effect(5f);
            first.DerivedModifiers.Add(new DerivedStatModifier { SourceStat = StatType.Armor, SourcePercent = 20f, TargetStat = StatType.HealthRegen, TargetModifierType = StatModType.Flat });
            var second = Effect(3f);
            _effects.ApplyStatusEffect(first, _stats, _stats, 2f);
            _effects.ApplyStatusEffect(second, _stats, _stats, 3f);
            Assert.That(_stats.GetValue(StatType.HealthRegen), Is.EqualTo(40f));
            Assert.That(_effects.ActiveEffects[0].DurationSeconds, Is.EqualTo(10f));
            Assert.That(_effects.ActiveEffects[1].DurationSeconds, Is.EqualTo(9f));
            Assert.That(first.DerivedModifiers[0].SourcePercent, Is.EqualTo(20f));
        }

        [TestCase("en")]
        [TestCase("ru")]
        public void PreviewShowsScaledTimedValuesAndUnscaledInstantValues(string locale)
        {
            DoubleEffectiveness();
            var skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            skill.Recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            skill.Recipe.Steps.Add(Step("ConsumeMysticShield"));
            var timed = Step("ApplyQuickStatusSelf");
            timed.SetOverrideFloat("QuickStatusValue", 20f);
            timed.SetOverrideFloat("QuickStatusDuration", 5f);
            skill.Recipe.Steps.Add(timed);
            var instant = Step("ApplyQuickStatusSelf");
            instant.SetOverrideFloat("QuickStatusValue", 50f);
            skill.Recipe.Steps.Add(instant);
            var damage = Step("DealDamageCircle");
            damage.SetOverrideFloat("DamageMultiplier", 2f);
            skill.Recipe.Steps.Add(damage);
            string text = SkillDescriptionGenerator.BuildAutomatic(skill, locale, durationStats: _stats);
            Assert.That(text, Does.Contain("+40%"));
            Assert.That(text, Does.Contain("10"));
            Assert.That(text, Does.Contain("+50%"));
            Assert.That(text, Does.Contain("400%"));
        }

        [Test]
        public void LinkedEffectPreviewCarriesPotencyAndDuration()
        {
            DoubleEffectiveness();
            var skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            skill.Recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            skill.Recipe.Steps.Add(Step("GenerateMysticShield"));
            var effect = Effect(5f);
            effect.NameEn = "Speed";
            effect.Modifiers.Add(new SerializableStatModifier { Stat = StatType.MoveSpeed, Value = 20f, Type = StatModType.PercentAdd });
            var step = Step("ApplyStatusSelf");
            step.SetOverrideObject("StatusEffect", effect);
            skill.Recipe.Steps.Add(step);
            var line = SkillDescriptionGenerator.BuildAutomaticLines(skill, "en", durationStats: _stats).Find(x => x.HasLink);
            string text = SkillDescriptionGenerator.BuildStatusEffectTooltip(effect, "en", durationStats: _stats, effectiveness: line.Effectiveness);
            Assert.That(text, Does.Contain("+40%"));
            Assert.That(text, Does.Contain("10s"));
        }

        [Test]
        public void ZeroEffectivenessRemovesTimedOutputsButPreservesInstantEffects()
        {
            _stats.GetStat(StatType.MysticShieldSkillEffectiveness).BaseValue = 0f;
            _context.RegisterMysticShieldConsumption(1);
            Assert.That(Damage(Step("DealDamageCircle")), Is.Zero);
            var mods = new[] { new SerializableStatModifier { Stat = StatType.Armor, Value = 20f, Type = StatModType.Flat } };
            Assert.That(_effects.ApplyRuntimeStatusEffect(mods, 5f, StatusEffectKind.Buff, effectiveness: 0f), Is.Null);
            using (_effects.ApplyRuntimeStatusEffect(mods, 0f, StatusEffectKind.Buff, effectiveness: 0f))
                Assert.That(_stats.GetValue(StatType.Armor), Is.EqualTo(20f));
        }

        [TestCase("Skills/Helmet/Mystic/Recipe_Mystic_Impulse_Skill", StatType.SpellDamage, 50f, 0f)]
        [TestCase("Skills/Boots/Mystic/Recipe_Mystic_Haste_Skill", StatType.MoveSpeed, 60f, 40f)]
        [TestCase("Skills/Gloves/Mystic/Recipe_Mystic_Consume_Skill", StatType.SpellDamage, 60f, 10f)]
        public void ExistingMysticRecipesRetainInstantBuffAndScaleTimedBuff(string path, StatType stat, float value, float duration)
        {
            DoubleEffectiveness();
            _context.RegisterMysticShieldConsumption(1);
            var recipe = Resources.Load<SkillRecipeSO>(path);
            Assert.That(recipe, Is.Not.Null, path);
            var step = recipe.Steps.Find(x => x.StepDefinition.Id == "ApplyQuickStatusSelfPerConsumedMysticShield"
                && x.GetInt("QuickStatusStat", (int)StatType.MoveSpeed) == (int)stat);
            Assert.That(step, Is.Not.Null);
            Invoke("ApplyQuickStatusToController", _effects, step, 1, path);
            Assert.That(_stats.GetStat(stat).Modifiers[0].Value, Is.EqualTo(value));
            if (duration > 0f)
                Assert.That(_effects.ActiveEffects[0].DurationSeconds, Is.EqualTo(duration));
            else
            {
                var damage = recipe.Steps.Find(x => x.StepDefinition.Id == "DealDamageCircle");
                Assert.That(Damage(damage), Is.EqualTo(4f));
            }
        }

        [Test]
        public void FractionalEffectivenessIsDisplayedAsMultiplier()
        {
            Assert.That(StatPresentation.FormatScalarValue(null, StatType.MysticShieldSkillEffectiveness, 1.25f), Is.EqualTo("\u00d71.25"));
        }

        private void NewContext()
        {
            _context?.Cleanup();
            _context = new SkillStepContext { OwnerStats = _stats };
            typeof(SkillStepRunner).GetField("_ctx", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_runner, _context);
        }
        private void DoubleEffectiveness() => _stats.GetStat(StatType.MysticShieldSkillEffectiveness).AddModifier(new StatModifier(100f, StatModType.PercentAdd));
        private float Damage(StepEntry step) => (float)Invoke("ResolveDamageMultiplier", step);
        private object Invoke(string name, params object[] args) => typeof(SkillStepRunner).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_runner, args);
        private StepEntry Step(string id)
        {
            var definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            definition.Id = id;
            return new StepEntry { StepDefinition = definition };
        }
        private StatusEffectSO Effect(float duration)
        {
            var effect = Track(ScriptableObject.CreateInstance<StatusEffectSO>());
            effect.BaseDurationSeconds = duration;
            return effect;
        }
        private T Track<T>(T value) where T : Object { _created.Add(value); return value; }
    }
}
