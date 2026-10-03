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
    public enum SkillDescriptionSection
    {
        Attack = 0,
        Other = 1
    }

    public readonly struct SkillDescriptionLine
    {
        public string Prefix { get; }
        public string LinkedName { get; }
        public StatusEffectSO LinkedEffect { get; }
        public string Suffix { get; }
        public SkillDescriptionSection Section { get; }

        public bool HasLink => LinkedEffect != null && !string.IsNullOrEmpty(LinkedName);

        public string Text => HasLink ? Prefix + LinkedName + Suffix : Prefix ?? string.Empty;

        public SkillDescriptionLine(string prefix, string linkedName, StatusEffectSO linkedEffect, string suffix, SkillDescriptionSection section)
        {
            Prefix = prefix ?? string.Empty;
            LinkedName = linkedName;
            LinkedEffect = linkedEffect;
            Suffix = suffix ?? string.Empty;
            Section = section;
        }

        public static SkillDescriptionLine Plain(string text, SkillDescriptionSection section = SkillDescriptionSection.Other)
        {
            return new SkillDescriptionLine(text, null, null, null, section);
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
            var quickNotes = new List<QuickStatusNote>();
            if (skill.EnablePushback)
            {
                if (skill.PushbackRating > 0.001f)
                    Add(lines, unique, ru
                        ? $"Отталкивает врагов (pushback {N(skill.PushbackRating)})."
                        : $"Knocks enemies back (pushback {N(skill.PushbackRating)}).", SkillDescriptionSection.Attack);
                else
                    Add(lines, unique, ru ? "Отталкивает врагов." : "Knocks enemies back.", SkillDescriptionSection.Attack);
            }

            foreach (StepEntry step in skill.Recipe.Steps)
                AppendStep(step, ru, statNameResolver, lines, unique, quickNotes);

            FlushQuickNotes(quickNotes, ru, statNameResolver, lines, unique);

            if (skill.Recipe.IsChanneling)
                Add(lines, unique, ru
                    ? $"Поддерживаемый навык, максимум {N(skill.Recipe.ChannelMaxDuration)} с."
                    : $"Channelled skill, up to {N(skill.Recipe.ChannelMaxDuration)}s.");

            OrderSections(lines);
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
            bool hasStatLines = HasStatLines(effect);
            string authored = effect.GetDescription(ru);
            if (!hasStatLines && !string.IsNullOrWhiteSpace(authored))
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
            HashSet<string> unique,
            List<QuickStatusNote> quickNotes)
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
                        : $"Each chained target takes {P(step.GetFloat("DamageMultiplier", 1f) * 100f)} weapon damage.", SkillDescriptionSection.Attack);
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
                    RecordQuickStatus(step, id, quickNotes);
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
                        ? $"+{N(step.GetFloat("BonusPercentPerConsumedShield", 50f))}% урона за заряд щита."
                        : $"+{N(step.GetFloat("BonusPercentPerConsumedShield", 50f))}% damage per shield charge.", SkillDescriptionSection.Attack);
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
                AppendStep(subStep, ru, resolver, lines, unique, quickNotes);
        }

        private static void AddDamage(StepEntry step, bool ru, bool nearby, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            string damage = P(step.GetFloat("DamageMultiplier", 1f) * 100f);
            Add(lines, unique, ru
                ? (nearby ? $"Наносит ближайшим врагам {damage} урона оружия." : $"Наносит врагам перед персонажем {damage} урона оружия.")
                : (nearby ? $"Deals {damage} weapon damage to nearby enemies." : $"Deals {damage} weapon damage to enemies in front."), SkillDescriptionSection.Attack);
        }

        private static void AddProjectile(StepEntry step, bool ru, bool ground, bool orbit, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            int count = Mathf.Max(1, step.GetInt("BaseProjectileCount", orbit ? 3 : 1));
            string damage = P(step.GetFloat("DamageMultiplier", 1f) * 100f);
            bool usesCountStat = !ground && step.GetBool("UseProjectileCountStat", true);
            string extra = usesCountStat ? (ru ? " плюс дополнительные снаряды" : " plus additional projectiles") : string.Empty;

            if (ground)
                Add(lines, unique, ru ? $"Выпускает наземную волну, наносящую {damage} урона оружия." : $"Releases a ground wave dealing {damage} weapon damage.", SkillDescriptionSection.Attack);
            else if (orbit)
                Add(lines, unique, ru ? $"Создаёт {count} вращающихся снаряда{extra}, каждый наносит {damage} урона оружия." : $"Creates {count} orbiting projectiles{extra}; each deals {damage} weapon damage.", SkillDescriptionSection.Attack);
            else
                Add(lines, unique, ru ? $"Выпускает {count} снаряд{extra}, наносящий {damage} урона оружия." : $"Fires {count} projectile{(count == 1 ? string.Empty : "s")}{extra}, dealing {damage} weapon damage.", SkillDescriptionSection.Attack);

            if (step.GetBool("Homing", false))
                Add(lines, unique, ru ? "Снаряды наводятся на врагов." : "Projectiles home in on enemies.", SkillDescriptionSection.Attack);
            if (step.GetBool("InfinitePierce", false) || (orbit && step.GetBool("PierceTargets", true)))
                Add(lines, unique, ru ? "Снаряды пробивают цели." : "Projectiles pierce targets.", SkillDescriptionSection.Attack);
            int reversals = Mathf.Max(0, step.GetInt("ReversalCount", 0));
            if (reversals > 0)
            {
                Add(lines, unique, ru ? $"Снаряды меняют направление {reversals} раз." : $"Projectiles reverse direction {reversals} time{(reversals == 1 ? string.Empty : "s")}.", SkillDescriptionSection.Attack);
                SkillProjectileReversalMode legacyMode = step.GetBool("ReturnToOwnerOnReverse", true)
                    ? SkillProjectileReversalMode.AimAtOwnerPosition
                    : SkillProjectileReversalMode.ReverseDirection;
                var reversalMode = (SkillProjectileReversalMode)Mathf.Clamp(
                    step.GetInt("ReversalMode", (int)legacyMode),
                    (int)SkillProjectileReversalMode.ReverseDirection,
                    (int)SkillProjectileReversalMode.HomeToOwner);
                if (reversalMode == SkillProjectileReversalMode.HomeToOwner)
                    Add(lines, unique, ru ? "После разворота снаряды наводятся обратно на персонажа." : "After reversing, projectiles home back to the character.", SkillDescriptionSection.Attack);
                else if (reversalMode == SkillProjectileReversalMode.AimAtOwnerPosition)
                    Add(lines, unique, ru ? "При развороте снаряды летят к текущей позиции персонажа." : "When reversing, projectiles aim at the character's current position.", SkillDescriptionSection.Attack);

                string returnDamage = P(step.GetFloat(
                    "ReturnDamagePercent",
                    ReturningProjectileDamageResolver.DefaultReturnDamagePercent));
                Add(lines, unique, ru
                    ? $"На возврате снаряды наносят {returnDamage} обычного урона."
                    : $"Returning projectiles deal {returnDamage} of their normal damage.", SkillDescriptionSection.Attack);
            }
        }

        private static void AddChain(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            int targets = 1 + Mathf.Max(0, step.GetInt("BaseExtraChains", 3));
            bool scales = step.GetBool("UseProjectileChainStat", true);
            Add(lines, unique, ru
                ? $"Цепь поражает до {targets} целей{(scales ? " плюс дополнительные цели от цепи снарядов" : string.Empty)}."
                : $"Chains through up to {targets} targets{(scales ? " plus additional Projectile Chain targets" : string.Empty)}.", SkillDescriptionSection.Attack);
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
            bool self = id.Contains("Self");
            string scaling = id.Contains("PerConsumedMysticShield")
                ? (ru ? " за заряд щита" : " per shield charge")
                : string.Empty;
            int minConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1));
            if (id == "ApplyStatusSelfIfMysticShieldConsumed" || (id.Contains("PerConsumedMysticShield") && minConsumed > 1))
                scaling += ru ? $", от {minConsumed} зарядов" : $", from {minConsumed} charges";
            string duration = $"{N(effect.DurationSeconds)}{(ru ? " с" : "s")}";
            string suffix = self
                ? $" {duration}{scaling}."
                : (ru ? $" на врагов, {duration}{scaling}." : $" on enemies, {duration}{scaling}.");
            AddLinked(lines, unique, string.Empty, name, effect, suffix, StatusSection(effect));
        }

        private static void RecordQuickStatus(StepEntry step, string id, List<QuickStatusNote> notes)
        {
            if (notes == null)
                return;

            notes.Add(new QuickStatusNote
            {
                Stat = ResolveStat(step.GetInt("QuickStatusStat", (int)StatType.MoveSpeed), StatType.MoveSpeed),
                Type = (StatModType)step.GetInt("QuickStatusModType", (int)StatModType.PercentAdd),
                Value = step.GetFloat("QuickStatusValue", 0f),
                Duration = Mathf.Max(0f, step.GetFloat("QuickStatusDuration", 0f)),
                PerCharge = id.Contains("PerConsumedMysticShield"),
                Self = id.Contains("Self"),
                MinConsumed = Mathf.Max(1, step.GetInt("MinConsumed", 1))
            });
        }

        private static void FlushQuickNotes(
            List<QuickStatusNote> notes,
            bool ru,
            Func<StatType, string> resolver,
            List<SkillDescriptionLine> lines,
            HashSet<string> unique)
        {
            if (notes == null || notes.Count == 0)
                return;

            int index = 0;
            while (index < notes.Count)
            {
                QuickStatusNote first = notes[index];
                int end = index + 1;
                while (end < notes.Count && SameQuickGroup(first, notes[end]))
                    end++;

                var group = notes.GetRange(index, end - index);
                bool offensive = true;
                for (int i = 0; i < group.Count; i++)
                    offensive &= IsOffensiveStat(group[i].Stat);
                SkillDescriptionSection section = offensive ? SkillDescriptionSection.Attack : SkillDescriptionSection.Other;

                if (group.Count == 1)
                {
                    Add(lines, unique, SingleQuickLine(group[0], ru, resolver), section);
                }
                else
                {
                    Add(lines, unique, QuickHeader(first, ru), section);
                    for (int i = 0; i < group.Count; i++)
                        Add(lines, unique, StatChip(group[i].Stat, group[i].Value, group[i].Type, ru, resolver), section);
                }

                index = end;
            }
        }

        private static bool SameQuickGroup(QuickStatusNote a, QuickStatusNote b)
        {
            return a.PerCharge == b.PerCharge
                && a.Self == b.Self
                && a.MinConsumed == b.MinConsumed
                && Mathf.Abs(a.Duration - b.Duration) < 0.01f
                && IsOffensiveStat(a.Stat) == IsOffensiveStat(b.Stat);
        }

        private static string SingleQuickLine(QuickStatusNote note, bool ru, Func<StatType, string> resolver)
        {
            string chip = StatChip(note.Stat, note.Value, note.Type, ru, resolver);
            string tail = QuickTail(note, ru);
            return string.IsNullOrEmpty(tail) ? chip + "." : $"{chip}, {tail}.";
        }

        private static string QuickHeader(QuickStatusNote note, bool ru)
        {
            string tail = QuickTail(note, ru);
            if (string.IsNullOrEmpty(tail))
                return ru ? "Эффект:" : "Effect:";
            return char.ToUpper(tail[0]) + tail.Substring(1) + ":";
        }

        private static string QuickTail(QuickStatusNote note, bool ru)
        {
            string duration = note.Duration > 0f ? $"{N(note.Duration)}{(ru ? " с" : "s")}" : string.Empty;
            string charge = note.PerCharge ? (ru ? "за заряд щита" : "per shield charge") : string.Empty;
            string minimum = note.PerCharge && note.MinConsumed > 1
                ? (ru ? $"от {note.MinConsumed}" : $"from {note.MinConsumed}")
                : string.Empty;
            if (duration.Length == 0 && charge.Length == 0)
                return string.Empty;
            if (duration.Length == 0)
                return string.IsNullOrEmpty(minimum) ? charge : $"{charge}, {minimum}";
            if (charge.Length == 0)
                return duration;
            string combined = string.IsNullOrEmpty(minimum) ? $"{charge}, {duration}" : $"{charge}, {minimum}, {duration}";
            return combined;
        }

        private static string StatChip(StatType stat, float value, StatModType type, bool ru, Func<StatType, string> resolver)
        {
            string name = StatName(stat, resolver);
            float magnitude = Mathf.Abs(value);
            return type switch
            {
                StatModType.PercentAdd => $"+{N(magnitude)}% {name}",
                StatModType.PercentSub => $"-{N(magnitude)}% {name}",
                StatModType.PercentMult => ru ? $"{N(magnitude)}% Больше {name}" : $"{N(magnitude)}% more {name}",
                StatModType.PercentLess => ru ? $"{N(magnitude)}% Меньше {name}" : $"{N(magnitude)}% less {name}",
                _ => $"{(value >= 0f ? "+" : string.Empty)}{N(value)}{(StatsDatabaseSO.DefaultDisplayAsPercentWhenFlat(stat) ? "%" : string.Empty)} {name}"
            };
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
                string durationText = duration > 0f ? (ru ? $", {N(duration)} с" : $", {N(duration)}s") : string.Empty;
                Add(lines, unique, ru
                    ? $"{P(percent)} от «{sourceName}» к «{StatName(target, resolver)}»{durationText}."
                    : $"{P(percent)} of {sourceName} as {StatName(target, resolver)}{durationText}.",
                    IsOffensiveStat(target) ? SkillDescriptionSection.Attack : SkillDescriptionSection.Other);
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
            if (step.DamageConversions == null || step.DamageConversions.Count == 0)
                return;

            var pending = new List<DamageConversionRule>();
            foreach (DamageConversionRule rule in step.DamageConversions)
            {
                if (!rule.IsValid)
                    continue;
                if (pending.Count > 0 && (Mathf.Abs(pending[0].Percent - rule.Percent) > 0.01f || pending[0].Target != rule.Target))
                {
                    AddConversionGroup(pending, ru, lines, unique);
                    pending.Clear();
                }
                pending.Add(rule);
            }

            AddConversionGroup(pending, ru, lines, unique);
        }

        private static void AddConversionGroup(List<DamageConversionRule> rules, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (rules == null || rules.Count == 0)
                return;

            if (rules.Count == 1)
            {
                DamageConversionRule rule = rules[0];
                Add(lines, unique, ru
                    ? $"Конвертирует {P(rule.Percent)} урона: {DamageName(rule.Source, true)} → {DamageName(rule.Target, true)}."
                    : $"Converts {P(rule.Percent)} of {DamageName(rule.Source, false)} Damage to {DamageName(rule.Target, false)} Damage.", SkillDescriptionSection.Attack);
                return;
            }

            var sources = new List<string>();
            for (int i = 0; i < rules.Count; i++)
                sources.Add(DamageName(rules[i].Source, ru));
            string joined = JoinNames(sources, ru);
            Add(lines, unique, ru
                ? $"Конвертирует {P(rules[0].Percent)} урона в {DamageName(rules[0].Target, true, accusative: true)}: {joined}."
                : $"Converts {P(rules[0].Percent)} of {joined} Damage to {DamageName(rules[0].Target, false)}.", SkillDescriptionSection.Attack);
        }

        private static string JoinNames(List<string> names, bool ru)
        {
            if (names.Count == 1)
                return names[0];
            if (names.Count == 2)
                return ru ? $"{names[0]} и {names[1]}" : $"{names[0]} and {names[1]}";
            var head = new List<string>();
            for (int i = 0; i < names.Count - 1; i++)
                head.Add(names[i]);
            string last = names[names.Count - 1];
            return ru ? $"{string.Join(", ", head)} и {last}" : $"{string.Join(", ", head)} and {last}";
        }

        private static void AddScopedModifiers(StepEntry step, bool ru, Func<StatType, string> resolver, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.ScopedStatModifiers == null) return;
            foreach (SerializableStatModifier modifier in step.ScopedStatModifiers)
            {
                SkillDescriptionSection section = IsOffensiveStat(modifier.Stat) ? SkillDescriptionSection.Attack : SkillDescriptionSection.Other;
                Add(lines, unique, StatChip(modifier.Stat, modifier.Value, modifier.Type, ru, resolver) + ".", section);
            }
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
                    ? $"{StatChip(rule.Stat, rule.ValuePerStack, rule.Type, true, resolver)} за стак {AilmentName(rule.Ailment, true, true)}{cap}."
                    : $"{StatChip(rule.Stat, rule.ValuePerStack, rule.Type, false, resolver)} per {AilmentName(rule.Ailment, false, false)} stack{cap}.",
                    IsOffensiveStat(rule.Stat) ? SkillDescriptionSection.Attack : SkillDescriptionSection.Other);
            }
        }

        private static void AddOnHitEffects(StepEntry step, bool ru, List<SkillDescriptionLine> lines, HashSet<string> unique)
        {
            if (step.OnHitEffects == null) return;
            foreach (SkillOnHitEffectRule rule in step.OnHitEffects)
                Add(lines, unique, ru
                    ? $"При попадании: область, {P(rule.DamageMultiplier * 100f)} урона оружия."
                    : $"On hit: an area for {P(rule.DamageMultiplier * 100f)} weapon damage.", SkillDescriptionSection.Attack);
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
                        Add(lines, unique, ru ? $"Заканчивается при {trigger}." : $"Ends when {trigger}.");
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
                                ru ? $"При {trigger}: " : $"On {trigger}: ",
                                nested.GetDisplayName(ru),
                                nested,
                                ".",
                                StatusSection(nested));
                            if (!HasStatLines(nested))
                            {
                                string nestedDesc = nested.GetDescription(ru);
                                if (!string.IsNullOrWhiteSpace(nestedDesc))
                                    Add(lines, unique, nestedDesc);
                            }
                        }
                        break;
                    case StatusEventReactionAction.ApplyQuickEffect:
                        string chips = QuickReactionChips(reaction, ru, resolver);
                        if (string.IsNullOrEmpty(chips))
                            break;
                        string duration = reaction.QuickEffectDurationSeconds > 0f
                            ? (ru ? $", {N(reaction.QuickEffectDurationSeconds)} с" : $", {N(reaction.QuickEffectDurationSeconds)}s")
                            : string.Empty;
                        Add(lines, unique, ru
                            ? $"При {trigger}{duration}: {chips}."
                            : $"On {trigger}{duration}: {chips}.");
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
                StatModType.PercentMult => ru ? $"на {N(magnitude)}% Больше" : $"{N(magnitude)}% more",
                StatModType.PercentLess => ru ? $"на {N(magnitude)}% Меньше" : $"{N(magnitude)}% less",
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

        private static string DamageName(DamageChannel channel, bool ru, bool accusative = false) => channel switch
        {
            DamageChannel.Fire => ru ? "огонь" : "Fire",
            DamageChannel.Cold => ru ? "холод" : "Cold",
            DamageChannel.Lightning => ru ? (accusative ? "молнию" : "молния") : "Lightning",
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
                GameplayEventType.DamageTaken => "получении урона",
                GameplayEventType.Evaded => "уклонении от атаки",
                GameplayEventType.DamageDealt => "нанесении урона",
                GameplayEventType.Landed => "приземлении",
                GameplayEventType.Jumped => "прыжке",
                GameplayEventType.Dodged => "рывке",
                GameplayEventType.EnemyKilled => "убийстве врага",
                GameplayEventType.MysticShieldConsumed => "поглощении Мистического щита",
                _ => type.ToString()
            };
        }

        private static string Duration(float seconds, bool ru) =>
            seconds > 0f ? (ru ? $" на {N(seconds)} с" : $" for {N(seconds)}s") : string.Empty;
        private static string P(float value) => $"{N(value)}%";
        private static string N(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
        private static bool IsRussian(string code) =>
            !string.IsNullOrWhiteSpace(code) && code.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        private static void Add(List<SkillDescriptionLine> lines, HashSet<string> unique, string line, SkillDescriptionSection section = SkillDescriptionSection.Other)
        {
            if (string.IsNullOrWhiteSpace(line) || !unique.Add(line))
                return;
            lines.Add(SkillDescriptionLine.Plain(line, section));
        }

        private static void AddLinked(
            List<SkillDescriptionLine> lines,
            HashSet<string> unique,
            string prefix,
            string name,
            StatusEffectSO effect,
            string suffix,
            SkillDescriptionSection section)
        {
            if (effect == null || string.IsNullOrWhiteSpace(name))
                return;

            var line = new SkillDescriptionLine(prefix, name, effect, suffix, section);
            if (!unique.Add(line.Text))
                return;
            lines.Add(line);
        }

        private struct QuickStatusNote
        {
            public StatType Stat;
            public StatModType Type;
            public float Value;
            public float Duration;
            public bool PerCharge;
            public bool Self;
            public int MinConsumed;
        }

        private static bool HasStatLines(StatusEffectSO effect)
        {
            if (effect == null)
                return false;
            if (effect.Modifiers != null && effect.Modifiers.Count > 0)
                return true;
            if (effect.DerivedModifiers != null && effect.DerivedModifiers.Count > 0)
                return true;
            if (effect.EventReactions == null)
                return false;
            for (int i = 0; i < effect.EventReactions.Count; i++)
            {
                StatusEventReaction reaction = effect.EventReactions[i];
                if (reaction == null)
                    continue;
                if (reaction.QuickModifiers != null && reaction.QuickModifiers.Count > 0)
                    return true;
                if (reaction.QuickDerivedModifiers != null && reaction.QuickDerivedModifiers.Count > 0)
                    return true;
            }

            return false;
        }

        private static SkillDescriptionSection StatusSection(StatusEffectSO effect)
        {
            if (effect?.Modifiers == null)
                return SkillDescriptionSection.Other;
            for (int i = 0; i < effect.Modifiers.Count; i++)
            {
                if (IsOffensiveStat(effect.Modifiers[i].Stat))
                    return SkillDescriptionSection.Attack;
            }

            return SkillDescriptionSection.Other;
        }

        private static bool IsOffensiveStat(StatType stat)
        {
            switch (stat)
            {
                case StatType.DamagePhysical:
                case StatType.DamageFire:
                case StatType.DamageCold:
                case StatType.DamageLightning:
                case StatType.MeleeDamage:
                case StatType.SpellDamage:
                case StatType.AttackSpeed:
                case StatType.CastSpeed:
                case StatType.CritChance:
                case StatType.CritMultiplier:
                case StatType.PenetrationPhysical:
                case StatType.PenetrationFire:
                case StatType.PenetrationCold:
                case StatType.PenetrationLightning:
                case StatType.AreaOfEffect:
                case StatType.ProjectileSpeed:
                case StatType.ProjectileCount:
                case StatType.ProjectileFork:
                case StatType.ProjectileChain:
                case StatType.ProjectilePierce:
                case StatType.ReturningProjectileDamage:
                case StatType.ExtraTargetsForMeleeHits:
                case StatType.BleedChance:
                case StatType.BleedDamage:
                case StatType.BleedDamageMult:
                case StatType.PoisonChance:
                case StatType.PoisonDamage:
                case StatType.PoisonDamageMult:
                case StatType.IgniteChance:
                case StatType.IgniteDamage:
                case StatType.IgniteDamageMult:
                case StatType.FreezeChance:
                case StatType.ShockChance:
                case StatType.StunBuildUp:
                    return true;
                default:
                    return false;
            }
        }

        private static string QuickReactionChips(StatusEventReaction reaction, bool ru, Func<StatType, string> resolver)
        {
            if (reaction?.QuickModifiers == null || reaction.QuickModifiers.Count == 0)
                return string.Empty;
            var chips = new List<string>();
            foreach (SerializableStatModifier modifier in reaction.QuickModifiers)
                chips.Add(StatChip(modifier.Stat, modifier.Value, modifier.Type, ru, resolver));
            return string.Join(", ", chips);
        }

        private static void OrderSections(List<SkillDescriptionLine> lines)
        {
            if (lines == null || lines.Count < 2)
                return;
            var attack = new List<SkillDescriptionLine>();
            var other = new List<SkillDescriptionLine>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Section == SkillDescriptionSection.Attack)
                    attack.Add(lines[i]);
                else
                    other.Add(lines[i]);
            }

            lines.Clear();
            lines.AddRange(attack);
            lines.AddRange(other);
        }
    }
}
