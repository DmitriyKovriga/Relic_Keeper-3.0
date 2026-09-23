using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Scripts.Combat;
using Scripts.GameplayEvents;
using Scripts.Skills.Steps;
using Scripts.Skills.Projectiles;
using Scripts.Stats;
using Scripts.StatusEffects;
using UnityEngine;

namespace Scripts.Skills
{
    public readonly struct SkillDescriptionLine
    {
        public string Prefix { get; }
        public string LinkedName { get; }
        public StatusEffectSO LinkedEffect { get; }
        public string Suffix { get; }

        public bool HasLink => LinkedEffect != null && !string.IsNullOrEmpty(LinkedName);

        public string Text => HasLink ? Prefix + LinkedName + Suffix : Prefix ?? string.Empty;

        public SkillDescriptionLine(string prefix, string linkedName, StatusEffectSO linkedEffect, string suffix)
        {
            Prefix = prefix ?? string.Empty;
            LinkedName = linkedName;
            LinkedEffect = linkedEffect;
            Suffix = suffix ?? string.Empty;
        }

        public static SkillDescriptionLine Plain(string text)
        {
            return new SkillDescriptionLine(text, null, null, null);
        }
    }

    /// <summary>Creates player-facing text from the same recipe data used by gameplay.</summary>
    public static class SkillDescriptionGenerator
    {
        /// <summary>
        /// Keeps new recipe step types from silently disappearing from automatic descriptions.
        /// Presentation and timing steps are deliberately classified here even though they add no text.
        /// </summary>
        public static bool IsKnownStepId(string id)
        {
            return id switch
            {
                "DealDamageRectangle" or "PersistentDamageRectangle" or
                "DealDamageCircle" or "PersistentDamageCircle" or
                "SpawnProjectile" or "SpawnGroundProjectile" or "SpawnOrbitProjectiles" or
                "BuildChainTargets" or "ChainDamage" or "PlayerImpulse" or
                "ApplyStatusSelf" or "ApplyStatusSelfIfMysticShieldConsumed" or
                "ApplyStatusSelfPerConsumedMysticShield" or "ApplyStatusCircle" or "ApplyStatusRectangle" or
                "ApplyQuickStatusSelf" or "ApplyQuickStatusSelfPerConsumedMysticShield" or
                "ApplyQuickStatusCircle" or "ApplyQuickStatusRectangle" or
                "ApplyStatBasedEffectSelf" or "ConsumeMysticShield" or "GenerateMysticShield" or
                "MysticShieldDamageBoost" or "ModifyCooldown" or
                "ParallelGroup" or "MovementLock" or "MovementUnlock" or
                "SpawnChainVFX" or "SpawnVFX" or "Wait" or
                "WeaponWindup" or "WeaponStrike" or "WeaponRecovery" => true,
                _ => false
            };
        }

        public static string Build(
            SkillDataSO skill,
            string localeCode,
            string localizedLegacyDescription = null,
            Func<StatType, string> statNameResolver = null)
        {
            if (skill == null) return string.Empty;

            string automatic = BuildAutomatic(skill, localeCode, statNameResolver);
            string legacy = string.IsNullOrWhiteSpace(localizedLegacyDescription)
                ? skill.Description?.Trim()
                : localizedLegacyDescription.Trim();

            return skill.DescriptionMode switch
            {
                SkillDescriptionMode.LegacyOnly => legacy ?? string.Empty,
                SkillDescriptionMode.AutomaticWithLegacy when string.IsNullOrWhiteSpace(automatic) => legacy ?? string.Empty,
                SkillDescriptionMode.AutomaticWithLegacy when string.IsNullOrWhiteSpace(legacy) => automatic,
                SkillDescriptionMode.AutomaticWithLegacy => automatic + "\n\n" + legacy,
                _ => automatic
            };
        }

        public static string BuildAutomatic(
            SkillDataSO skill,
            string localeCode,
            Func<StatType, string> statNameResolver = null)
        {
            List<SkillDescriptionLine> lines = BuildAutomaticLines(skill, localeCode, statNameResolver);
            if (lines.Count == 0)
                return string.Empty;

            var texts = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                texts[i] = lines[i].Text;
            return string.Join("\n", texts);
        }

        public static List<SkillDescriptionLine> BuildAutomaticLines(
            SkillDataSO skill,
            string localeCode,
            Func<StatType, string> statNameResolver = null)
        {
            var lines = new List<SkillDescriptionLine>();
            if (skill?.Recipe == null)
                return lines;

            bool ru = IsRussian(localeCode);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            if (skill.EnablePushback)
            {
                if (skill.PushbackRating > 0.001f)
                    Add(lines, unique, ru
                        ? $"Отталкивает врагов (pushback {N(skill.PushbackRating)})."
                        : $"Knocks enemies back (pushback {N(skill.PushbackRating)}).");
                else
                    Add(lines, unique, ru ? "Отталкивает врагов." : "Knocks enemies back.");
            }

            foreach (StepEntry step in skill.Recipe.Steps)
                AppendStep(step, ru, statNameResolver, lines, unique);

            if (skill.Recipe.IsChanneling)
                Add(lines, unique, ru
                    ? $"Поддерживаемый навык, максимум {N(skill.Recipe.ChannelMaxDuration)} с."
                    : $"Channelled skill, up to {N(skill.Recipe.ChannelMaxDuration)}s.");

            return lines;
        }

        public static string BuildStatusEffectTooltip(
            StatusEffectSO effect,
            string localeCode,
            Func<StatType, string> statNameResolver = null)
        {
            if (effect == null)
                return string.Empty;

            bool ru = IsRussian(localeCode);
            var lines = new List<SkillDescriptionLine>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            string authored = effect.GetDescription(ru);
            if (!string.IsNullOrWhiteSpace(authored))
                Add(lines, unique, authored);

            AddEffectModifiers(effect.Modifiers, ru ? "Эффект: " : "Effect: ", ru, statNameResolver, lines, unique);
            AddDerivedModifiers(effect.DerivedModifiers, ru, statNameResolver, lines, unique);
            AddEventReactions(effect.EventReactions, ru, statNameResolver, lines, unique);

            if (lines.Count == 0)
                return string.Empty;

            var texts = new string[lines.Count];
            for (int i = 0; i < lines.Count; i++)
                texts[i] = lines[i].Text;
            return string.Join("\n", texts);
        }

        private static void AppendStep(
            StepEntry step,
            bool ru,
            Func<StatType, string> resolver,
            List<SkillDescriptionLine> lines,
            HashSet<string> unique)
        {
            if (step?.StepDefinition == null) return;
            string id = step.StepDefinition.Id ?? string.Empty;

            switch (id)
            {
                case "DealDamageRectangle":
                case "PersistentDamageRectangle":
                    AddDamage(step, ru, nearby: false, lines, unique);
                    break;
                case "DealDamageCircle":
                case "PersistentDamageCircle":
                    AddDamage(step, ru, nearby: true, lines, unique);
                    break;
                case "SpawnProjectile":
                    AddProjectile(step, ru, ground: false, orbit: false, lines, unique);
                    break;
                case "SpawnGroundProjectile":
                    AddProjectile(step, ru, ground: true, orbit: false, lines, unique);
                    break;
                case "SpawnOrbitProjectiles":
                    AddProjectile(step, ru, ground: false, orbit: true, lines, unique);
                    break;
                case "BuildChainTargets":
                    AddChain(step, ru, lines, unique);
                    break;
                case "ChainDamage":
                    Add(lines, unique, ru
                        ? $"Каждая цель цепи получает {P(step.GetFloat("DamageMultiplier", 1f) * 100f)} урона оружия."
                        : $"Each chained target takes {P(step.GetFloat("DamageMultiplier", 1f) * 100f)} weapon damage.");
                    break;
                case "PlayerImpulse":
                    AddImpulse(step, ru, lines, unique);
                    break;
                case "ApplyStatusSelf":
                case "ApplyStatusSelfIfMysticShieldConsumed":
                case "ApplyStatusSelfPerConsumedMysticShield":
                case "ApplyStatusCircle":
                case "ApplyStatusRectangle":
                    AddStatus(step, id, ru, lines, unique);
                    break;
                case "ApplyQuickStatusSelf":
                case "ApplyQuickStatusSelfPerConsumedMysticShield":
                case "ApplyQuickStatusCircle":
                case "ApplyQuickStatusRectangle":
                    AddQuickStatus(step, id, ru, resolver, lines, unique);
                    break;
                case "ApplyStatBasedEffectSelf":
                    AddStatBased(step, ru, resolver, lines, unique);
                    break;
                case "ConsumeMysticShield":
                    AddConsumeShield(step, ru, lines, unique);
                    break;
                case "GenerateMysticShield":
                    AddGenerateShield(step, ru, lines, unique);
                    break;
                case "MysticShieldDamageBoost":
                    Add(lines, unique, ru
                        ? $"Наносит на {N(step.GetFloat("BonusPercentPerConsumedShield", 50f))}% больше урона за каждый поглощённый заряд Мистического щита."
                        : $"Deals {N(step.GetFloat("BonusPercentPerConsumedShield", 50f))}% more damage per consumed Mystic Shield charge.");
                    break;
                case "ModifyCooldown":
                    AddCooldown(step, ru, lines, unique);
                    break;
            }

            AddConversions(step, ru, lines, unique);
            AddScopedModifiers(step, ru, resolver, lines, unique);
            AddStackModifiers(step, ru, resolver, lines, unique);
            AddOnHitEffects(step, ru, lines, unique);

            if (step.SubSteps == null) return;
            foreach (StepEntry subStep in step.SubSteps)
                AppendStep(subStep, ru, resolver, lines, unique);
        }

        private static void AddDamage(StepEntry step, bool ru, bool nearby, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            string damage = P(step.GetFloat("DamageMultiplier", 1f) * 100f);
            Add(lines, unique, ru
                ? (nearby ? $"Наносит ближайшим врагам {damage} урона оружия." : $"Наносит врагам перед персонажем {damage} урона оружия.")
                : (nearby ? $"Deals {damage} weapon damage to nearby enemies." : $"Deals {damage} weapon damage to enemies in front."));
        }

        private static void AddProjectile(StepEntry step, bool ru, bool ground, bool orbit, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            int count = Mathf.Max(1, step.GetInt("BaseProjectileCount", orbit ? 3 : 1));
            string damage = P(step.GetFloat("DamageMultiplier", 1f) * 100f);
            bool usesCountStat = !ground && step.GetBool("UseProjectileCountStat", true);
            string extra = usesCountStat ? (ru ? " плюс дополнительные снаряды" : " plus additional projectiles") : string.Empty;

            if (ground)
                Add(lines, unique, ru ? $"Выпускает наземную волну, наносящую {damage} урона оружия." : $"Releases a ground wave dealing {damage} weapon damage.");
            else if (orbit)
                Add(lines, unique, ru ? $"Создаёт {count} вращающихся снаряда{extra}, каждый наносит {damage} урона оружия." : $"Creates {count} orbiting projectiles{extra}; each deals {damage} weapon damage.");
            else
                Add(lines, unique, ru ? $"Выпускает {count} снаряд{extra}, наносящий {damage} урона оружия." : $"Fires {count} projectile{(count == 1 ? string.Empty : "s")}{extra}, dealing {damage} weapon damage.");

            if (step.GetBool("Homing", false))
                Add(lines, unique, ru ? "Снаряды наводятся на врагов." : "Projectiles home in on enemies.");
            if (step.GetBool("InfinitePierce", false) || (orbit && step.GetBool("PierceTargets", true)))
                Add(lines, unique, ru ? "Снаряды пробивают цели." : "Projectiles pierce targets.");
            int reversals = Mathf.Max(0, step.GetInt("ReversalCount", 0));
            if (reversals > 0)
            {
                Add(lines, unique, ru ? $"Снаряды меняют направление {reversals} раз." : $"Projectiles reverse direction {reversals} time{(reversals == 1 ? string.Empty : "s")}.");
                SkillProjectileReversalMode legacyMode = step.GetBool("ReturnToOwnerOnReverse", true)
                    ? SkillProjectileReversalMode.AimAtOwnerPosition
                    : SkillProjectileReversalMode.ReverseDirection;
                var reversalMode = (SkillProjectileReversalMode)Mathf.Clamp(
                    step.GetInt("ReversalMode", (int)legacyMode),
                    (int)SkillProjectileReversalMode.ReverseDirection,
                    (int)SkillProjectileReversalMode.HomeToOwner);
                if (reversalMode == SkillProjectileReversalMode.HomeToOwner)
                    Add(lines, unique, ru ? "После разворота снаряды наводятся обратно на персонажа." : "After reversing, projectiles home back to the character.");
                else if (reversalMode == SkillProjectileReversalMode.AimAtOwnerPosition)
                    Add(lines, unique, ru ? "При развороте снаряды летят к текущей позиции персонажа." : "When reversing, projectiles aim at the character's current position.");

                string returnDamage = P(step.GetFloat(
                    "ReturnDamagePercent",
                    ReturningProjectileDamageResolver.DefaultReturnDamagePercent));
                Add(lines, unique, ru
                    ? $"На возврате снаряды наносят {returnDamage} обычного урона."
                    : $"Returning projectiles deal {returnDamage} of their normal damage.");
            }
        }

        private static void AddChain(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            int targets = 1 + Mathf.Max(0, step.GetInt("BaseExtraChains", 3));
            bool scales = step.GetBool("UseProjectileChainStat", true);
            Add(lines, unique, ru
                ? $"Цепь поражает до {targets} целей{(scales ? " плюс дополнительные цели от цепи снарядов" : string.Empty)}."
                : $"Chains through up to {targets} targets{(scales ? " plus additional Projectile Chain targets" : string.Empty)}.");
        }

        private static void AddImpulse(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            bool backwards = step.GetBool("InvertFacing", false) || step.GetFloat("Force", 8f) < 0f;
            Add(lines, unique, ru
                ? (backwards ? "Отбрасывает персонажа назад." : "Перемещает персонажа вперёд.")
                : (backwards ? "Propels the character backward." : "Propels the character forward."));
        }

        private static void AddStatus(StepEntry step, string id, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            StatusEffectSO effect = step.GetObject<StatusEffectSO>("StatusEffect");
            if (effect == null) return;

            string name = effect.GetDisplayName(ru);
            string target = id.Contains("Self") ? (ru ? "на персонажа" : "to the character") : (ru ? "на поражённых врагов" : "to affected enemies");
            string scaling = id.Contains("PerConsumedMysticShield")
                ? (ru ? " за каждый поглощённый заряд Мистического щита" : " per consumed Mystic Shield charge")
                : string.Empty;
            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            string condition = id == "ApplyStatusSelfIfMysticShieldConsumed"
                ? (ru
                    ? $", если поглощено не менее {minConsumed} зарядов Мистического щита"
                    : $" if at least {minConsumed} Mystic Shield charge{(minConsumed == 1 ? string.Empty : "s")} was consumed")
                : id.Contains("PerConsumedMysticShield") && minConsumed > 1
                    ? (ru ? $", начиная с {minConsumed} поглощённых зарядов" : $", requiring at least {minConsumed} consumed charges")
                    : string.Empty;
            AddLinked(
                lines,
                unique,
                ru ? "Накладывает " : "Applies ",
                name,
                effect,
                ru
                    ? $" {target} на {N(effect.DurationSeconds)} с{scaling}{condition}."
                    : $" {target} for {N(effect.DurationSeconds)}s{scaling}{condition}.");
        }

        private static void AddQuickStatus(StepEntry step, string id, bool ru, Func<StatType, string> resolver, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            StatType stat = ResolveStat(step.GetInt("QuickStatusStat", (int)StatType.MoveSpeed), StatType.MoveSpeed);
            StatModType type = (StatModType)step.GetInt("QuickStatusModType", (int)StatModType.PercentAdd);
            float value = step.GetFloat("QuickStatusValue", 0f);
            float duration = Mathf.Max(0f, step.GetFloat("QuickStatusDuration", 0f));
            string target = id.Contains("Self") ? (ru ? "Персонаж" : "The character") : (ru ? "Поражённые враги" : "Affected enemies");
            string scaling = id.Contains("PerConsumedMysticShield")
                ? (ru ? " за каждый поглощённый заряд Мистического щита" : " per consumed Mystic Shield charge")
                : string.Empty;
            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            string condition = id.Contains("PerConsumedMysticShield") && minConsumed > 1
                ? (ru ? $", начиная с {minConsumed} поглощённых зарядов" : $", requiring at least {minConsumed} consumed charges")
                : string.Empty;
            Add(lines, unique, ru
                ? $"{target} получает {ModValue(stat, value, type, true)} к параметру «{StatName(stat, resolver)}» на {N(duration)} с{scaling}{condition}."
                : $"{target} gains {ModValue(stat, value, type, false)} {StatName(stat, resolver)} for {N(duration)}s{scaling}{condition}.");
        }

        private static void AddStatBased(StepEntry step, bool ru, Func<StatType, string> resolver, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            StatType source = ResolveStat(step.GetInt("SourceStat", (int)StatType.Armor), StatType.Armor);
            float percent = step.GetFloat("SourcePercent", 25f);
            var operation = (DerivedStatEffectOperation)step.GetInt("Operation", (int)DerivedStatEffectOperation.AddStatModifier);
            string sourceName = StatName(source, resolver);
            if (operation == DerivedStatEffectOperation.RestoreHealth)
                Add(lines, unique, ru ? $"Восстанавливает здоровье в размере {P(percent)} от параметра «{sourceName}»." : $"Restores Health equal to {P(percent)} of {sourceName}.");
            else if (operation == DerivedStatEffectOperation.RestoreMana)
                Add(lines, unique, ru ? $"Восстанавливает ману в размере {P(percent)} от параметра «{sourceName}»." : $"Restores Mana equal to {P(percent)} of {sourceName}.");
            else
            {
                StatType target = ResolveStat(step.GetInt("TargetStat", (int)StatType.HealthRegen), StatType.HealthRegen);
                float duration = Mathf.Max(0f, step.GetFloat("Duration", 0f));
                Add(lines, unique, ru
                    ? $"Даёт параметр «{StatName(target, resolver)}» в размере {P(percent)} от параметра «{sourceName}»{Duration(duration, true)}."
                    : $"Grants {StatName(target, resolver)} equal to {P(percent)} of {sourceName}{Duration(duration, false)}.");
            }
        }

        private static void AddConsumeShield(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            bool all = step.GetBool("ConsumeAll", false);
            int amount = Mathf.Max(1, step.GetInt("Amount", 1));
            Add(lines, unique, ru
                ? (all ? "Поглощает все заряды Мистического щита." : $"Поглощает {amount} заряд Мистического щита.")
                : (all ? "Consumes all Mystic Shield charges." : $"Consumes {amount} Mystic Shield charge{(amount == 1 ? string.Empty : "s")}."));
        }

        private static void AddGenerateShield(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            bool fill = step.GetBool("FillToMax", false);
            int amount = Mathf.Max(1, step.GetInt("Amount", 1));
            Add(lines, unique, ru
                ? (fill ? "Полностью восстанавливает заряды Мистического щита." : $"Создаёт {amount} заряд Мистического щита.")
                : (fill ? "Restores Mystic Shield charges to maximum." : $"Generates {amount} Mystic Shield charge{(amount == 1 ? string.Empty : "s")}."));
        }

        private static void AddCooldown(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            float seconds = Mathf.Max(0f, step.GetFloat("Seconds", 1f));
            bool perHit = step.GetBool("ScaleByHitCount", false);
            bool add = step.GetBool("AddInsteadOfReduce", false);
            int target = step.GetInt("TargetMode", 0);
            string targetText = target switch
            {
                1 => ru ? "выбранного навыка" : "the selected skill",
                2 => ru ? "остальных навыков" : "other skills",
                3 => ru ? "всех навыков" : "all skills",
                _ => ru ? "этого навыка" : "this skill"
            };
            Add(lines, unique, ru
                ? $"{(add ? "Увеличивает" : "Сокращает")} перезарядку {targetText} на {N(seconds)} с{(perHit ? " за каждого поражённого врага" : string.Empty)}."
                : $"{(add ? "Adds" : "Removes")} {N(seconds)}s {(add ? "to" : "from")} the cooldown of {targetText}{(perHit ? " per enemy hit" : string.Empty)}.");
        }

        private static void AddConversions(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.DamageConversions == null) return;
            foreach (DamageConversionRule rule in step.DamageConversions)
            {
                if (!rule.IsValid) continue;
                Add(lines, unique, ru
                    ? $"Конвертирует {P(rule.Percent)} урона: {DamageName(rule.Source, true)} → {DamageName(rule.Target, true)}."
                    : $"Converts {P(rule.Percent)} of {DamageName(rule.Source, false)} Damage to {DamageName(rule.Target, false)} Damage.");
            }
        }

        private static void AddScopedModifiers(StepEntry step, bool ru, Func<StatType, string> resolver, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.ScopedStatModifiers == null) return;
            foreach (SerializableStatModifier modifier in step.ScopedStatModifiers)
                Add(lines, unique, ru
                    ? $"Модификатор навыка: {ModValue(modifier.Stat, modifier.Value, modifier.Type, true)} к параметру «{StatName(modifier.Stat, resolver)}»."
                    : $"Skill modifier: {ModValue(modifier.Stat, modifier.Value, modifier.Type, false)} {StatName(modifier.Stat, resolver)}.");
        }

        private static void AddStackModifiers(StepEntry step, bool ru, Func<StatType, string> resolver, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.TargetAilmentStackModifiers == null) return;
            foreach (TargetAilmentStackStatModifierRule rule in step.TargetAilmentStackModifiers)
            {
                string cap = rule.MaxStacksCounted > 0
                    ? (ru ? $", максимум {rule.MaxStacksCounted} стаков" : $", up to {rule.MaxStacksCounted} stacks")
                    : string.Empty;
                Add(lines, unique, ru
                    ? $"Даёт {ModValue(rule.Stat, rule.ValuePerStack, rule.Type, true)} к параметру «{StatName(rule.Stat, resolver)}» за каждый стак {AilmentName(rule.Ailment, true, true)} на цели{cap}."
                    : $"Grants {ModValue(rule.Stat, rule.ValuePerStack, rule.Type, false)} {StatName(rule.Stat, resolver)} per {AilmentName(rule.Ailment, false, false)} stack on the target{cap}.");
            }
        }

        private static void AddOnHitEffects(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.OnHitEffects == null) return;
            foreach (SkillOnHitEffectRule rule in step.OnHitEffects)
                Add(lines, unique, ru
                    ? $"При попадании создаёт область, наносящую {P(rule.DamageMultiplier * 100f)} урона оружия."
                    : $"On hit, creates an area dealing {P(rule.DamageMultiplier * 100f)} weapon damage.");
        }

        private static void AddEffectModifiers(
            List<SerializableStatModifier> modifiers,
            string prefix,
            bool ru,
            Func<StatType, string> resolver,
            List<SkillDescriptionLine> lines,
            HashSet<string> unique)
        {
            if (modifiers == null || modifiers.Count == 0) return;
            var values = new List<string>();
            foreach (SerializableStatModifier modifier in modifiers)
                values.Add($"{ModValue(modifier.Stat, modifier.Value, modifier.Type, ru)} {StatName(modifier.Stat, resolver)}");
            Add(lines, unique, prefix + string.Join(", ", values) + ".");
        }

        private static void AddDerivedModifiers(
            List<DerivedStatModifier> modifiers,
            bool ru,
            Func<StatType, string> resolver,
            List<SkillDescriptionLine> lines,
            HashSet<string> unique)
        {
            if (modifiers == null) return;
            foreach (DerivedStatModifier modifier in modifiers)
                Add(lines, unique, ru
                    ? $"Эффект даёт параметр «{StatName(modifier.TargetStat, resolver)}» в размере {P(modifier.SourcePercent)} от параметра «{StatName(modifier.SourceStat, resolver)}»."
                    : $"Effect grants {StatName(modifier.TargetStat, resolver)} equal to {P(modifier.SourcePercent)} of {StatName(modifier.SourceStat, resolver)}.");
        }

        private static void AddEventReactions(
            List<StatusEventReaction> reactions,
            bool ru,
            Func<StatType, string> resolver,
            List<SkillDescriptionLine> lines,
            HashSet<string> unique)
        {
            if (reactions == null) return;
            foreach (StatusEventReaction reaction in reactions)
            {
                string trigger = EventName(reaction.EventType, ru);
                switch (reaction.Action)
                {
                    case StatusEventReactionAction.EndCurrentEffect:
                        Add(lines, unique, ru ? $"Эффект заканчивается при событии «{trigger}»." : $"The effect ends when {trigger}.");
                        break;
                    case StatusEventReactionAction.ExtendCurrentEffect:
                        Add(lines, unique, ru
                            ? $"При событии «{trigger}» длительность эффекта увеличивается на {N(reaction.ExtendSeconds)} с."
                            : $"When {trigger}, the effect is extended by {N(reaction.ExtendSeconds)}s.");
                        break;
                    case StatusEventReactionAction.ApplyStatusEffect:
                        if (reaction.StatusEffectToApply != null)
                        {
                            StatusEffectSO nested = reaction.StatusEffectToApply;
                            AddLinked(
                                lines,
                                unique,
                                ru ? $"При событии «{trigger}» накладывает «" : $"When {trigger}, applies ",
                                nested.GetDisplayName(ru),
                                nested,
                                ru ? "»." : ".");
                            string nestedDesc = nested.GetDescription(ru);
                            if (!string.IsNullOrWhiteSpace(nestedDesc))
                                Add(lines, unique, nestedDesc);
                        }
                        break;
                    case StatusEventReactionAction.ApplyQuickEffect:
                        AddEffectModifiers(
                            reaction.QuickModifiers,
                            ru ? $"При событии «{trigger}» на {N(reaction.QuickEffectDurationSeconds)} с: " : $"When {trigger}, for {N(reaction.QuickEffectDurationSeconds)}s: ",
                            ru,
                            resolver,
                            lines,
                            unique);
                        AddDerivedModifiers(reaction.QuickDerivedModifiers, ru, resolver, lines, unique);
                        break;
                }
            }
        }

        private static string ModValue(StatType stat, float value, StatModType type, bool ru)
        {
            float magnitude = Mathf.Abs(value);
            return type switch
            {
                StatModType.PercentAdd => $"+{N(magnitude)}%",
                StatModType.PercentSub => $"-{N(magnitude)}%",
                StatModType.PercentMult => ru ? $"на {N(magnitude)}% больше" : $"{N(magnitude)}% more",
                StatModType.PercentLess => ru ? $"на {N(magnitude)}% меньше" : $"{N(magnitude)}% less",
                _ => $"{(value >= 0f ? "+" : string.Empty)}{N(value)}{(StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(stat) ? "%" : string.Empty)}"
            };
        }

        private static string StatName(StatType stat, Func<StatType, string> resolver)
        {
            string value = resolver?.Invoke(stat);
            return string.IsNullOrWhiteSpace(value) ? Humanize(stat.ToString()) : value;
        }

        public static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1])) sb.Append(' ');
                sb.Append(current);
            }
            return sb.ToString();
        }

        private static StatType ResolveStat(int raw, StatType fallback) =>
            Enum.IsDefined(typeof(StatType), raw) ? (StatType)raw : fallback;

        private static string DamageName(DamageChannel channel, bool ru) => channel switch
        {
            DamageChannel.Fire => ru ? "огонь" : "Fire",
            DamageChannel.Cold => ru ? "холод" : "Cold",
            DamageChannel.Lightning => ru ? "молния" : "Lightning",
            _ => ru ? "физический" : "Physical"
        };

        private static string AilmentName(AilmentType type, bool ru, bool genitive)
        {
            if (!ru) return type.ToString();
            return type switch
            {
                AilmentType.Poison => genitive ? "яда" : "Яд",
                AilmentType.Bleed => genitive ? "кровотечения" : "Кровотечение",
                AilmentType.Ignite => genitive ? "поджигания" : "Поджигание",
                AilmentType.Freeze => genitive ? "заморозки" : "Заморозка",
                AilmentType.Shock => genitive ? "шока" : "Шок",
                _ => type.ToString()
            };
        }

        private static string EventName(GameplayEventType type, bool ru)
        {
            if (!ru)
            {
                return type switch
                {
                    GameplayEventType.DamageTaken => "taking damage",
                    GameplayEventType.Evaded => "evading an attack",
                    GameplayEventType.DamageDealt => "dealing damage",
                    GameplayEventType.Landed => "landing",
                    GameplayEventType.Jumped => "jumping",
                    GameplayEventType.Dodged => "dodging",
                    GameplayEventType.EnemyKilled => "killing an enemy",
                    GameplayEventType.MysticShieldConsumed => "consuming Mystic Shield",
                    _ => Humanize(type.ToString()).ToLowerInvariant()
                };
            }

            return type switch
            {
                GameplayEventType.DamageTaken => "получение урона",
                GameplayEventType.Evaded => "уклонение от атаки",
                GameplayEventType.DamageDealt => "нанесение урона",
                GameplayEventType.Landed => "приземление",
                GameplayEventType.Jumped => "прыжок",
                GameplayEventType.Dodged => "рывок",
                GameplayEventType.EnemyKilled => "убийство врага",
                GameplayEventType.MysticShieldConsumed => "поглощение Мистического щита",
                _ => type.ToString()
            };
        }

        private static string Duration(float seconds, bool ru) =>
            seconds > 0f ? (ru ? $" на {N(seconds)} с" : $" for {N(seconds)}s") : string.Empty;
        private static string P(float value) => $"{N(value)}%";
        private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static bool IsRussian(string code) =>
            !string.IsNullOrWhiteSpace(code) && code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        private static void Add(List<SkillDescriptionLine> lines, HashSet<string> unique, string line)
        {
            if (string.IsNullOrWhiteSpace(line) || !unique.Add(line))
                return;
            lines.Add(SkillDescriptionLine.Plain(line));
        }

        private static void AddLinked(
            List<SkillDescriptionLine> lines,
            HashSet<string> unique,
            string prefix,
            string name,
            StatusEffectSO effect,
            string suffix)
        {
            if (effect == null || string.IsNullOrWhiteSpace(name))
                return;

            var line = new SkillDescriptionLine(prefix, name, effect, suffix);
            if (!unique.Add(line.Text))
                return;
            lines.Add(line);
        }
    }
}
