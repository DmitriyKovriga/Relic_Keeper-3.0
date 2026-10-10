using System.Collections.Generic;
using Scripts.GameplayEvents;
using Scripts.Skills;
using Scripts.Skills.PassiveTree;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEditor;

namespace Scripts.Editor.PassiveTree
{
    /// <summary>Compact one-line summaries shown in collapsed effect headers.</summary>
    internal static class PassiveEffectEditorText
    {
        private static string N(float value)
        {
            return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string Summarize(PassiveConditionalModifiers group)
        {
            return group == null ? string.Empty : $"{Condition(group.Condition)}  →  {(group.Destination == PassiveConditionalDestination.EnemyDamageTaken ? "enemy: " : string.Empty)}{Modifiers(group.Modifiers)}";
        }

        internal static string Summarize(PassiveTriggeredEffect effect)
        {
            if (effect == null)
                return string.Empty;

            string summary = $"On {Event(effect.Trigger)}  →  {Action(effect)}";
            if (effect.ChancePercent < 100f)
                summary += $"  ·  {N(effect.ChancePercent)}%";
            if (effect.InternalCooldownSeconds > 0f)
                summary += $"  ·  every {N(effect.InternalCooldownSeconds)} s max";
            return summary;
        }

        internal static string Summarize(PassiveHitScalingRule rule)
        {
            if (rule == null)
                return string.Empty;

            string source = rule.Source == PassiveHitScalingSource.ProjectileFlightDistance
                ? $"{N(rule.SourcePerStep)} units flown"
                : $"{N(rule.SourcePerStep)} s of flight";
            string cap = rule.MaxSteps > 0f ? $" (max {N(rule.MaxSteps)})" : string.Empty;
            return $"Per {source}{cap}  →  {Modifiers(rule.ModifiersPerStep)}";
        }

        internal static string Condition(PassiveCondition condition)
        {
            if (condition == null)
                return "No condition";

            if (condition.Kind == PassiveConditionKind.TargetDistance)
                return $"Enemy distance {(condition.Comparison == PassiveComparison.AtLeast ? "≥" : "≤")} {N(condition.DistanceUnits)} cells (24 px)";

            if (condition.Kind == PassiveConditionKind.ResourceThreshold)
            {
                string sign = condition.Comparison == PassiveComparison.AtLeast ? "≥" : "≤";
                return $"{condition.Resource} {sign} {N(condition.ThresholdPercent)}%";
            }

            string noun = Event(condition.Event);
            return condition.Recency == PassiveRecencyMode.NotRecently
                ? $"No {noun} for {N(condition.WindowSeconds)} s"
                : $"{Capitalize(noun)} within {N(condition.WindowSeconds)} s";
        }

        internal static string Event(PassiveEventFilter filter)
        {
            if (filter == null)
                return "event";

            var qualifiers = new List<string>();
            if (PassiveEffectEventInfo.IsDamageEvent(filter.Event))
            {
                if (filter.DirectHit == PassiveTriState.Yes) qualifiers.Add("hit");
                if (filter.DirectHit == PassiveTriState.No) qualifiers.Add("over time");
                if (filter.Crit == PassiveTriState.Yes) qualifiers.Add("crit");
                if (filter.Crit == PassiveTriState.No) qualifiers.Add("non-crit");
            }

            if (filter.RequireOtherAilment && PassiveEffectEventInfo.HasOtherParticipant(filter.Event))
            {
                string who = filter.Subject == StatusEventSubject.CarrierAsTarget ? "attacker" : "target";
                string stacks = filter.MinAilmentStacks > 1 ? $" ×{filter.MinAilmentStacks}" : string.Empty;
                qualifiers.Add($"{who} {PassiveEffectEventInfo.AilmentAdjective(filter.OtherAilment)}{stacks}");
            }

            if (filter.Subject != PassiveEffectEventInfo.NaturalSubject(filter.Event) && PassiveEffectEventInfo.HasOtherParticipant(filter.Event))
                qualifiers.Add(PassiveEffectEventInfo.RoleShort(filter.Subject));

            string noun = PassiveEffectEventInfo.Noun(filter.Event);
            return qualifiers.Count > 0 ? $"{noun} ({string.Join(", ", qualifiers)})" : noun;
        }

        private static string Action(PassiveTriggeredEffect effect)
        {
            switch (effect.Action)
            {
                case PassiveTriggerAction.ReduceSkillCooldown:
                    string skill = effect.CooldownTarget switch
                    {
                        PassiveCooldownTarget.AllSkills => "all cooldowns",
                        PassiveCooldownTarget.Slot => $"{PassiveEffectEventInfo.SlotName(effect.SkillSlot)} cooldown",
                        _ => "random cooldown"
                    };
                    return $"{skill} −{N(effect.CooldownSeconds)} s";
                case PassiveTriggerAction.ApplyBuff:
                    if (effect.StatusEffect != null)
                        return $"apply {effect.StatusEffect.GetDisplayName(false)}";
                    string stacks = effect.Stacking == PassiveBuffStacking.IndependentStacks && effect.MaxStacks > 1 ? $", ×{effect.MaxStacks}" : string.Empty;
                    return $"{N(effect.BuffDurationSeconds)} s buff{stacks}: {Modifiers(effect.BuffModifiers)}";
                case PassiveTriggerAction.RestoreResource:
                    string amount = effect.RestoreAsPercentOfMax ? $"{N(effect.RestoreAmount)}% max" : $"{N(effect.RestoreAmount)}";
                    return $"+{amount} {effect.Resource}";
                default:
                    return effect.Action.ToString();
            }
        }

        private static string Modifiers(List<SerializableStatModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
                return "no modifiers";

            string first = StatPresentation.FormatModifierLine(null, modifiers[0].Stat, StatName(modifiers[0].Stat), modifiers[0].Value,
                modifiers[0].Type, StatPresentation.ModifierLineStyle.ValueThenStat);
            return modifiers.Count > 1 ? $"{first} +{modifiers.Count - 1}" : first;
        }

        internal static string StatName(StatType stat)
        {
            return ObjectNames.NicifyVariableName(stat.ToString());
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }

    internal static class PassiveEffectEventInfo
    {
        internal static bool IsDamageEvent(GameplayEventType type)
        {
            return type == GameplayEventType.DamageDealt || type == GameplayEventType.DamageTaken;
        }

        internal static bool HasOtherParticipant(GameplayEventType type)
        {
            return type == GameplayEventType.DamageDealt || type == GameplayEventType.DamageTaken ||
                   type == GameplayEventType.Evaded || type == GameplayEventType.EnemyKilled;
        }

        internal static StatusEventSubject NaturalSubject(GameplayEventType type)
        {
            return type == GameplayEventType.DamageTaken || type == GameplayEventType.Evaded
                ? StatusEventSubject.CarrierAsTarget
                : StatusEventSubject.CarrierAsSource;
        }

        internal static string Noun(GameplayEventType type)
        {
            return type switch
            {
                GameplayEventType.DamageDealt => "damage dealt",
                GameplayEventType.DamageTaken => "damage taken",
                GameplayEventType.Evaded => "evade",
                GameplayEventType.EnemyKilled => "kill",
                GameplayEventType.Dodged => "dodge",
                GameplayEventType.Jumped => "jump",
                GameplayEventType.Landed => "landing",
                GameplayEventType.MysticShieldConsumed => "Mystic Shield charge lost",
                _ => ObjectNames.NicifyVariableName(type.ToString()).ToLowerInvariant()
            };
        }

        internal static string RoleShort(StatusEventSubject subject)
        {
            return subject switch
            {
                StatusEventSubject.CarrierAsTarget => "you as target",
                StatusEventSubject.CarrierAsSource => "you as source",
                StatusEventSubject.CarrierAsSourceOrTarget => "either side",
                _ => "anyone"
            };
        }

        internal static string AilmentAdjective(AilmentType ailment)
        {
            return ailment switch
            {
                AilmentType.Poison => "poisoned",
                AilmentType.Bleed => "bleeding",
                AilmentType.Ignite => "ignited",
                AilmentType.Freeze => "frozen",
                AilmentType.Shock => "shocked",
                _ => ailment.ToString().ToLowerInvariant()
            };
        }

        internal static string SlotName(int slot)
        {
            return slot switch
            {
                SkillCooldownRecovery.MainSkillSlot => "main hand",
                SkillCooldownRecovery.SpecialSkillSlot => "off hand",
                SkillCooldownRecovery.HelmetSkillSlot => "helmet",
                SkillCooldownRecovery.BodyArmorSkillSlot => "body armour",
                SkillCooldownRecovery.GlovesSkillSlot => "gloves",
                SkillCooldownRecovery.BootsSkillSlot => "boots",
                _ => $"slot {slot + 1}"
            };
        }
    }

    /// <summary>Authoring mistakes shown inside the element that has them.</summary>
    internal static class PassiveEffectValidation
    {
        internal static void Validate(PassiveConditionalModifiers group, List<string> issues)
        {
            if (group == null)
                return;
            if (group.Modifiers == null || group.Modifiers.Count == 0)
                issues.Add("Add at least one modifier, otherwise the condition changes nothing.");
            if (group.Condition?.Kind == PassiveConditionKind.TargetDistance)
            {
                if (group.Destination != PassiveConditionalDestination.EnemyDamageTaken)
                    issues.Add("Distance to enemy requires Apply to: Enemy.");
                if (group.Condition.DistanceUnits < 0f || float.IsNaN(group.Condition.DistanceUnits) || float.IsInfinity(group.Condition.DistanceUnits))
                    issues.Add("Distance must be a finite non-negative number of 24-pixel cells.");
            }
            if (group.Destination == PassiveConditionalDestination.EnemyDamageTaken && group.Modifiers != null)
                foreach (SerializableStatModifier modifier in group.Modifiers)
                    if (modifier.Stat != StatType.DamageTaken)
                        issues.Add("Only DamageTaken modifiers are currently supported for Enemy.");
            if (group.Condition != null && group.Condition.Kind == PassiveConditionKind.EventRecency)
                ValidateFilter(group.Condition.Event, issues);
        }

        internal static void Validate(PassiveTriggeredEffect effect, List<string> issues)
        {
            if (effect == null)
                return;

            ValidateFilter(effect.Trigger, issues);
            if (effect.ChancePercent <= 0f)
                issues.Add("Chance is 0%: the effect never fires.");

            switch (effect.Action)
            {
                case PassiveTriggerAction.ReduceSkillCooldown when effect.CooldownSeconds <= 0f:
                    issues.Add("Cooldown reduction is 0 s.");
                    break;
                case PassiveTriggerAction.ApplyBuff when effect.StatusEffect == null && (effect.BuffModifiers == null || effect.BuffModifiers.Count == 0):
                    issues.Add("The buff has no modifiers. Add modifiers or assign a Status Effect.");
                    break;
                case PassiveTriggerAction.ApplyBuff when effect.StatusEffect != null && effect.Stacking == PassiveBuffStacking.IndependentStacks:
                    issues.Add("An assigned Status Effect refreshes instead of stacking. Use inline modifiers for stacks.");
                    break;
                case PassiveTriggerAction.RestoreResource when effect.RestoreAmount <= 0f:
                    issues.Add("Restore amount is 0.");
                    break;
            }
        }

        internal static void Validate(PassiveHitScalingRule rule, List<string> issues)
        {
            if (rule == null)
                return;
            if (rule.ModifiersPerStep == null || rule.ModifiersPerStep.Count == 0)
                issues.Add("Add at least one modifier per step.");
            if (rule.MaxSteps <= 0f)
                issues.Add("No step cap: long-lived projectiles keep scaling without limit.");
        }

        private static void ValidateFilter(PassiveEventFilter filter, List<string> issues)
        {
            if (filter == null || !PassiveEffectEventInfo.HasOtherParticipant(filter.Event))
                return;

            if (filter.Subject == StatusEventSubject.Any)
                issues.Add("Role \"Anyone\" also reacts to events between other characters.");
            else if (filter.Subject != StatusEventSubject.CarrierAsSourceOrTarget && filter.Subject != PassiveEffectEventInfo.NaturalSubject(filter.Event))
                issues.Add($"\"{PassiveEffectEventInfo.RoleShort(filter.Subject)}\" flips {PassiveEffectEventInfo.Noun(filter.Event)} onto the other side. Usually it is \"{PassiveEffectEventInfo.RoleShort(PassiveEffectEventInfo.NaturalSubject(filter.Event))}\".");
        }
    }
}
