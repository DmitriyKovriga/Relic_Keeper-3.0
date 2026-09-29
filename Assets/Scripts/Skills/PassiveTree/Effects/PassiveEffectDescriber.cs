using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Scripts.GameplayEvents;
using Scripts.Stats;
using Scripts.StatusEffects;

namespace Scripts.Skills.PassiveTree
{
    /// <summary>Player-facing EN/RU text for conditional, triggered and hit-scaling node effects.</summary>
    public static class PassiveEffectDescriber
    {
        private const string Indent = "  ";
        private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        private static string N(float value, bool ru)
        {
            return value.ToString("0.##", ru ? Russian : CultureInfo.InvariantCulture);
        }

        public static Func<SerializableStatModifier, string> DefaultModifierFormatter(Func<StatType, string> statName = null)
        {
            return modifier => StatPresentation.FormatModifierLine(
                null,
                modifier.Stat,
                statName != null ? statName(modifier.Stat) : modifier.Stat.ToString(),
                modifier.Value,
                modifier.Type,
                StatPresentation.ModifierLineStyle.StatThenValue);
        }

        public static void AppendAll(PassiveNodeTemplateSO template, bool ru, Func<SerializableStatModifier, string> formatModifier, List<string> lines)
        {
            if (template == null || lines == null)
                return;

            formatModifier ??= DefaultModifierFormatter();
            if (template.ConditionalModifiers != null)
                foreach (PassiveConditionalModifiers group in template.ConditionalModifiers)
                    if (group != null) lines.Add(Describe(group, ru, formatModifier));

            if (template.TriggeredEffects != null)
                foreach (PassiveTriggeredEffect effect in template.TriggeredEffects)
                    if (effect != null) lines.Add(Describe(effect, ru, formatModifier));

            if (template.HitScalingRules != null)
                foreach (PassiveHitScalingRule rule in template.HitScalingRules)
                    if (rule != null) lines.Add(Describe(rule, ru, formatModifier));
        }

        public static string Describe(PassiveConditionalModifiers group, bool ru, Func<SerializableStatModifier, string> formatModifier)
        {
            string header = Capitalize(DescribeCondition(group.Condition, ru)) + ":";
            return header + FormatModifierBlock(group.Modifiers, formatModifier);
        }

        public static string Describe(PassiveTriggeredEffect effect, bool ru, Func<SerializableStatModifier, string> formatModifier)
        {
            string when = ru
                ? $"Когда {DescribeEvent(effect.Trigger, ru, past: false)}"
                : $"When {DescribeEvent(effect.Trigger, ru, past: false)}";

            var sb = new StringBuilder(when);
            switch (effect.Action)
            {
                case PassiveTriggerAction.ReduceSkillCooldown:
                    sb.Append(", ").Append(DescribeCooldownReduction(effect, ru)).Append('.');
                    break;
                case PassiveTriggerAction.RestoreResource:
                    sb.Append(", ").Append(DescribeRestore(effect, ru)).Append('.');
                    break;
                case PassiveTriggerAction.ApplyBuff:
                    sb.Append(", ").Append(DescribeBuffHeader(effect, ru)).Append(':');
                    if (effect.StatusEffect == null)
                        sb.Append(FormatModifierBlock(effect.BuffModifiers, formatModifier));
                    else
                        sb.Append('\n').Append(Indent).Append('«').Append(effect.StatusEffect.GetDisplayName(ru)).Append('»');
                    break;
            }

            AppendChanceAndCooldown(sb, effect, ru);
            return sb.ToString();
        }

        public static string Describe(PassiveHitScalingRule rule, bool ru, Func<SerializableStatModifier, string> formatModifier)
        {
            string step = $"{N(rule.SourcePerStep, ru)}";
            string per = rule.Source switch
            {
                PassiveHitScalingSource.ProjectileFlightDistance => ru ? $"за каждые {step} ед. пройденного пути" : $"per {step} units travelled",
                _ => ru ? $"за каждые {step} с полёта" : $"per {step} s of flight"
            };
            var sb = new StringBuilder(ru ? $"Снаряд при попадании получает {per}" : $"Projectile hits gain {per}");
            if (rule.MaxSteps > 0f)
                sb.Append(ru ? $" (не более {N(rule.MaxSteps, ru)} раз)" : $" (up to {N(rule.MaxSteps, ru)} times)");
            sb.Append(':');
            sb.Append(FormatModifierBlock(rule.ModifiersPerStep, formatModifier));
            return sb.ToString();
        }

        public static string DescribeCondition(PassiveCondition condition, bool ru)
        {
            if (condition == null)
                return string.Empty;

            if (condition.Kind == PassiveConditionKind.ResourceThreshold)
            {
                string resource = condition.Resource == PassiveResource.Mana
                    ? (ru ? "мана" : "Mana")
                    : (ru ? "здоровье" : "Health");
                string comparison = condition.Comparison == PassiveComparison.AtLeast
                    ? (ru ? "не ниже" : "at least")
                    : (ru ? "не выше" : "at most");
                return ru
                    ? $"пока {resource} {comparison} {N(condition.ThresholdPercent, ru)}%"
                    : $"while your {resource} is {comparison} {N(condition.ThresholdPercent, ru)}%";
            }

            string eventText = DescribeEvent(condition.Event, ru, past: true);
            string window = $"{N(condition.WindowSeconds, ru)}";
            if (condition.Recency == PassiveRecencyMode.NotRecently)
                return ru ? $"если вы не {eventText} последние {window} с" : $"if you have not {eventText} in the last {window} s";
            return ru ? $"если вы {eventText} за последние {window} с" : $"if you have {eventText} in the last {window} s";
        }

        /// <summary>
        /// Present tense ("you deal a critical hit to a poisoned enemy") or past for recency
        /// ("taken damage" / "получали урон"). Russian present phrases already include "вы".
        /// </summary>
        public static string DescribeEvent(PassiveEventFilter filter, bool ru, bool past)
        {
            if (filter == null)
                return string.Empty;

            bool crit = filter.Crit == PassiveTriState.Yes;
            bool hit = filter.DirectHit == PassiveTriState.Yes;
            string target = DescribeTarget(filter, ru);

            switch (filter.Event)
            {
                case GameplayEventType.DamageDealt:
                    if (ru)
                    {
                        string verb = crit ? "критический удар" : hit ? "удар" : "урон";
                        return (past ? $"наносили {verb}" : $"вы наносите {verb}") + (target.Length > 0 ? $" по {target}" : string.Empty);
                    }
                    else
                    {
                        string enemy = target.Length > 0 ? target : "an enemy";
                        if (past)
                            return crit ? $"dealt a critical hit to {enemy}" : hit ? $"hit {enemy}" : $"dealt damage to {enemy}";
                        return crit ? $"you deal a critical hit to {enemy}" : hit ? $"you hit {enemy}" : $"you deal damage to {enemy}";
                    }
                case GameplayEventType.DamageTaken:
                    if (ru)
                    {
                        string noun = crit ? "критический удар" : hit ? "удар" : "урон";
                        return past ? $"получали {noun}" : $"вы получаете {noun}";
                    }
                    return past
                        ? (crit ? "taken a critical hit" : hit ? "been hit" : "taken damage")
                        : (crit ? "you take a critical hit" : hit ? "you are hit" : "you take damage");
                case GameplayEventType.Evaded:
                    return ru ? (past ? "уклонялись от атаки" : "вы уклоняетесь от атаки") : (past ? "evaded an attack" : "you evade an attack");
                case GameplayEventType.Dodged:
                    return ru ? (past ? "делали рывок" : "вы делаете рывок") : (past ? "dodged" : "you dodge");
                case GameplayEventType.Jumped:
                    return ru ? (past ? "прыгали" : "вы прыгаете") : (past ? "jumped" : "you jump");
                case GameplayEventType.Landed:
                    return ru ? (past ? "приземлялись" : "вы приземляетесь") : (past ? "landed" : "you land");
                case GameplayEventType.EnemyKilled:
                    return ru ? (past ? "убивали врагов" : "вы убиваете врага") : (past ? "killed an enemy" : "you kill an enemy");
                case GameplayEventType.MysticShieldConsumed:
                    return ru ? (past ? "теряли заряд Мистического щита" : "вы теряете заряд Мистического щита") : (past ? "lost a Mystic Shield charge" : "you lose a Mystic Shield charge");
                default:
                    return filter.Event.ToString();
            }
        }

        private static string DescribeTarget(PassiveEventFilter filter, bool ru)
        {
            if (!filter.RequireOtherAilment)
                return ru ? "врагу" : string.Empty;

            string adjective = filter.OtherAilment switch
            {
                AilmentType.Poison => ru ? "отравленному" : "poisoned",
                AilmentType.Bleed => ru ? "кровоточащему" : "bleeding",
                AilmentType.Ignite => ru ? "горящему" : "ignited",
                AilmentType.Freeze => ru ? "замороженному" : "frozen",
                AilmentType.Shock => ru ? "шокированному" : "shocked",
                _ => filter.OtherAilment.ToString()
            };
            string stacks = filter.MinAilmentStacks > 1
                ? (ru ? $" (не менее {filter.MinAilmentStacks} стаков)" : $" (at least {filter.MinAilmentStacks} stacks)")
                : string.Empty;
            return ru ? $"{adjective} врагу{stacks}" : $"a {adjective} enemy{stacks}";
        }

        private static string DescribeCooldownReduction(PassiveTriggeredEffect effect, bool ru)
        {
            string seconds = $"{N(effect.CooldownSeconds, ru)}";
            switch (effect.CooldownTarget)
            {
                case PassiveCooldownTarget.AllSkills:
                    return ru ? $"восстановление всех навыков ускоряется на {seconds} с" : $"all skills recover {seconds} s faster";
                case PassiveCooldownTarget.Slot:
                    return ru ? $"восстановление навыка {SlotName(effect.SkillSlot, true)} ускоряется на {seconds} с" : $"the {SlotName(effect.SkillSlot, false)} skill recovers {seconds} s faster";
                default:
                    return ru ? $"восстановление случайного навыка ускоряется на {seconds} с" : $"a random skill on cooldown recovers {seconds} s faster";
            }
        }

        private static string DescribeRestore(PassiveTriggeredEffect effect, bool ru)
        {
            bool mana = effect.Resource == PassiveResource.Mana;
            string amount = $"{N(effect.RestoreAmount, ru)}";
            if (effect.RestoreAsPercentOfMax)
                return ru ? $"восстанавливается {amount}% от максимума {(mana ? "маны" : "здоровья")}" : $"restore {amount}% of maximum {(mana ? "Mana" : "Health")}";
            return ru ? $"восстанавливается {amount} ед. {(mana ? "маны" : "здоровья")}" : $"restore {amount} {(mana ? "Mana" : "Health")}";
        }

        private static string DescribeBuffHeader(PassiveTriggeredEffect effect, bool ru)
        {
            float duration = effect.StatusEffect != null ? effect.StatusEffect.DurationSeconds : effect.BuffDurationSeconds;
            string stacking = effect.Stacking switch
            {
                PassiveBuffStacking.IndependentStacks when effect.StatusEffect == null && effect.MaxStacks > 1 =>
                    ru ? $", до {effect.MaxStacks} раз" : $", up to {effect.MaxStacks} times",
                PassiveBuffStacking.IgnoreWhileActive => ru ? ", не обновляется, пока действует" : ", not refreshed while active",
                _ => ru ? ", не суммируется" : ", does not stack"
            };
            return ru ? $"на {N(duration, ru)} с ({stacking.TrimStart(',', ' ')})" : $"for {N(duration, ru)} s ({stacking.TrimStart(',', ' ')}) you gain";
        }

        private static void AppendChanceAndCooldown(StringBuilder sb, PassiveTriggeredEffect effect, bool ru)
        {
            if (effect.ChancePercent < 100f)
                sb.Append(ru ? $"\n{Indent}Шанс {N(effect.ChancePercent, ru)}%." : $"\n{Indent}{N(effect.ChancePercent, ru)}% chance.");
            if (effect.InternalCooldownSeconds > 0f)
                sb.Append(ru ? $"\n{Indent}Не чаще раза в {N(effect.InternalCooldownSeconds, ru)} с." : $"\n{Indent}At most once every {N(effect.InternalCooldownSeconds, ru)} s.");
        }

        private static string FormatModifierBlock(List<SerializableStatModifier> modifiers, Func<SerializableStatModifier, string> formatModifier)
        {
            if (modifiers == null || modifiers.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (SerializableStatModifier modifier in modifiers)
                sb.Append('\n').Append(Indent).Append(formatModifier(modifier));
            return sb.ToString();
        }

        private static string SlotName(int slot, bool ru)
        {
            return slot switch
            {
                SkillCooldownRecovery.MainSkillSlot => ru ? "основной руки" : "main hand",
                SkillCooldownRecovery.SpecialSkillSlot => ru ? "второй руки" : "off hand",
                SkillCooldownRecovery.HelmetSkillSlot => ru ? "шлема" : "helmet",
                SkillCooldownRecovery.BodyArmorSkillSlot => ru ? "нагрудника" : "body armour",
                SkillCooldownRecovery.GlovesSkillSlot => ru ? "перчаток" : "gloves",
                SkillCooldownRecovery.BootsSkillSlot => ru ? "сапог" : "boots",
                _ => (slot + 1).ToString()
            };
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }
    }
}
