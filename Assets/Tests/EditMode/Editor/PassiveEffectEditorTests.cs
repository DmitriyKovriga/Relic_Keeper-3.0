using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Editor.PassiveTree;
using Scripts.GameplayEvents;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;

namespace RelicKeeper.Tests.EditMode
{
    public class PassiveEffectEditorTests
    {
        [Test]
        public void Summary_DescribesConfigurationStructurally()
        {
            var effect = new PassiveTriggeredEffect
            {
                Trigger = new PassiveEventFilter
                {
                    Event = GameplayEventType.DamageDealt,
                    Subject = StatusEventSubject.CarrierAsSource,
                    DirectHit = PassiveTriState.Yes,
                    RequireOtherAilment = true,
                    OtherAilment = AilmentType.Poison
                },
                CooldownSeconds = 1.5f,
                InternalCooldownSeconds = 2f
            };

            Assert.That(PassiveEffectEditorText.Summarize(effect),
                Is.EqualTo("On damage dealt (hit, target poisoned)  →  random cooldown −1.5 s  ·  every 2 s max"));
        }

        [Test]
        public void Summary_UsesInvariantNumbers()
        {
            var rule = new PassiveHitScalingRule
            {
                SourcePerStep = 0.1f,
                MaxSteps = 10f,
                ModifiersPerStep = new List<SerializableStatModifier>
                {
                    new SerializableStatModifier { Stat = StatType.CritChance, Value = 2.5f, Type = StatModType.PercentAdd }
                }
            };

            Assert.That(PassiveEffectEditorText.Summarize(rule), Does.StartWith("Per 0.1 s of flight (max 10)"));
        }

        [Test]
        public void NewElements_ProduceNoValidationIssues_ExceptMissingModifiers()
        {
            var trigger = new List<string>();
            PassiveEffectValidation.Validate(new PassiveTriggeredEffect(), trigger);
            Assert.That(trigger, Is.Empty, "a fresh trigger is a working default");

            var conditional = new List<string>();
            PassiveEffectValidation.Validate(new PassiveConditionalModifiers(), conditional);
            Assert.That(conditional, Has.Count.EqualTo(1));
        }

        [Test]
        public void Validation_FlagsFlippedRoleAndZeroChance()
        {
            var issues = new List<string>();
            PassiveEffectValidation.Validate(new PassiveTriggeredEffect
            {
                Trigger = new PassiveEventFilter { Event = GameplayEventType.DamageTaken, Subject = StatusEventSubject.CarrierAsSource },
                ChancePercent = 0f
            }, issues);

            Assert.That(issues, Has.Count.EqualTo(2));
        }
    }
}
