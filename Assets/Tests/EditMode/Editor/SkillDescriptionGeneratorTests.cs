using System.Collections.Generic;
using NUnit.Framework;
using Scripts.GameplayEvents;
using Scripts.Skills;
using Scripts.Skills.Steps;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class SkillDescriptionGeneratorTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _created)
                if (value != null) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void VenomStrike_UsesCurrentRecipeValuesAndNotLegacyText()
        {
            SkillDataSO skill = Resources.Load<SkillDataSO>("Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill");

            string en = SkillDescriptionGenerator.Build(skill, "en", "OUTDATED LEGACY DESCRIPTION");
            string ru = SkillDescriptionGenerator.Build(skill, "ru", "УСТАРЕВШЕЕ ОПИСАНИЕ");

            Assert.That(en, Does.Contain("15"));
            Assert.That(en, Does.Contain("Poison stack"));
            Assert.That(en, Does.Contain("50%"));
            Assert.That(en, Does.Not.Contain("OUTDATED"));
            Assert.That(ru, Does.Contain("15"));
            Assert.That(ru, Does.Contain("яда"));
            Assert.That(ru, Does.Not.Contain("УСТАРЕВШЕЕ"));
        }

        [Test]
        public void Description_RebuildsWhenRecipeMechanicChanges()
        {
            SkillDataSO skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            SkillRecipeSO recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            StepDefinitionSO definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            definition.Id = "DealDamageRectangle";
            var rule = new TargetAilmentStackStatModifierRule
            {
                Ailment = AilmentType.Poison,
                Stat = StatType.DamagePhysical,
                Type = StatModType.Flat,
                ValuePerStack = 10f
            };
            recipe.Steps.Add(new StepEntry
            {
                StepDefinition = definition,
                TargetAilmentStackModifiers = new List<TargetAilmentStackStatModifierRule> { rule }
            });
            skill.Recipe = recipe;

            Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, "en"), Does.Contain("+10"));
            rule.ValuePerStack = 25f;
            Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, "en"), Does.Contain("+25"));
        }

        [Test]
        public void LegacyModes_PreserveOptionalAuthoredText()
        {
            SkillDataSO skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            SkillRecipeSO recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            StepDefinitionSO definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            definition.Id = "DealDamageCircle";
            recipe.Steps.Add(new StepEntry { StepDefinition = definition });
            skill.Recipe = recipe;
            skill.Description = "Legacy text";

            skill.DescriptionMode = SkillDescriptionMode.AutomaticWithLegacy;
            Assert.That(SkillDescriptionGenerator.Build(skill, "en"), Does.Contain("Legacy text"));
            Assert.That(SkillDescriptionGenerator.Build(skill, "en"), Does.Contain("weapon damage"));

            skill.DescriptionMode = SkillDescriptionMode.LegacyOnly;
            Assert.That(SkillDescriptionGenerator.Build(skill, "en"), Is.EqualTo("Legacy text"));
        }

        [Test]
        public void StatusWithoutDirectModifiers_StillDescribesEventReactions()
        {
            SkillDataSO skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            SkillRecipeSO recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            StepDefinitionSO definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            StatusEffectSO effect = Track(ScriptableObject.CreateInstance<StatusEffectSO>());
            definition.Id = "ApplyStatusSelf";
            effect.NameEn = "Focus";
            effect.NameRu = "Концентрация";
            effect.EventReactions.Add(new StatusEventReaction
            {
                EventType = GameplayEventType.DamageTaken,
                Action = StatusEventReactionAction.EndCurrentEffect
            });
            var step = new StepEntry { StepDefinition = definition };
            step.SetOverrideObject("StatusEffect", effect);
            recipe.Steps.Add(step);
            skill.Recipe = recipe;

            string en = SkillDescriptionGenerator.BuildAutomatic(skill, "en");
            string ru = SkillDescriptionGenerator.BuildAutomatic(skill, "ru");

            Assert.That(en, Does.Contain("ends when taking damage"));
            Assert.That(ru, Does.Contain("получение урона"));
        }

        [Test]
        public void EnabledPushback_AppearsInAutomaticDescription()
        {
            SkillDataSO skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            SkillRecipeSO recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            StepDefinitionSO definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            definition.Id = "DealDamageRectangle";
            recipe.Steps.Add(new StepEntry { StepDefinition = definition });
            skill.Recipe = recipe;
            skill.EnablePushback = true;
            skill.PushbackRating = 200f;

            string en = SkillDescriptionGenerator.BuildAutomatic(skill, "en");
            string ru = SkillDescriptionGenerator.BuildAutomatic(skill, "ru");

            Assert.That(en, Does.Contain("Knocks enemies back"));
            Assert.That(en, Does.Contain("200"));
            Assert.That(ru, Does.Contain("Отталкивает"));
        }

        [Test]
        public void StatusAuthoredDescription_AppearsInSkillTextAndTooltip()
        {
            SkillDataSO skill = Track(ScriptableObject.CreateInstance<SkillDataSO>());
            SkillRecipeSO recipe = Track(ScriptableObject.CreateInstance<SkillRecipeSO>());
            StepDefinitionSO definition = Track(ScriptableObject.CreateInstance<StepDefinitionSO>());
            StatusEffectSO effect = Track(ScriptableObject.CreateInstance<StatusEffectSO>());
            definition.Id = "ApplyStatusSelf";
            effect.NameEn = "Warrior Step";
            effect.NameRu = "Шаг Воина";
            effect.DescriptionEn = "Increase movement speed and armor by 30%";
            effect.DescriptionRu = "Увеличивает скорость передвижения на 30%";
            effect.BaseDurationSeconds = 10f;
            var step = new StepEntry { StepDefinition = definition };
            step.SetOverrideObject("StatusEffect", effect);
            recipe.Steps.Add(step);
            skill.Recipe = recipe;

            string en = SkillDescriptionGenerator.BuildAutomatic(skill, "en");
            string ru = SkillDescriptionGenerator.BuildAutomatic(skill, "ru");
            Assert.That(en, Does.Contain("Warrior Step"));
            Assert.That(en, Does.Contain("Increase movement speed and armor by 30%"));
            Assert.That(ru, Does.Contain("Шаг Воина"));
            Assert.That(ru, Does.Contain("скорость передвижения"));

            List<SkillDescriptionLine> lines = SkillDescriptionGenerator.BuildAutomaticLines(skill, "en");
            Assert.That(lines.Exists(line => line.HasLink && line.LinkedEffect == effect), Is.True);

            string tooltip = SkillDescriptionGenerator.BuildStatusEffectTooltip(effect, "en");
            Assert.That(tooltip, Does.Contain("Increase movement speed and armor by 30%"));
        }

        [Test]
        public void EveryCurrentActiveSkillRecipe_HasAutomaticDescriptionInBothLocales()
        {
            SkillDataSO[] skills = Resources.LoadAll<SkillDataSO>("Skills");
            Assert.That(skills, Is.Not.Empty);
            foreach (SkillDataSO skill in skills)
            {
                if (skill == null || !skill.IsActive || skill.Recipe == null) continue;
                Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, "en"), Is.Not.Empty, $"{skill.name} EN");
                Assert.That(SkillDescriptionGenerator.BuildAutomatic(skill, "ru"), Is.Not.Empty, $"{skill.name} RU");
            }
        }

        [Test]
        public void EveryStepDefinition_IsExplicitlyHandledOrIgnored()
        {
            StepDefinitionSO[] definitions = Resources.LoadAll<StepDefinitionSO>("Skills/StepDefinitions");
            Assert.That(definitions, Is.Not.Empty);
            foreach (StepDefinitionSO definition in definitions)
                Assert.That(
                    SkillDescriptionGenerator.IsKnownStepId(definition.Id),
                    Is.True,
                    $"Step '{definition.Id}' must be described or explicitly classified as presentation/timing only.");
        }

        private T Track<T>(T value) where T : Object
        {
            _created.Add(value);
            return value;
        }
    }
}
