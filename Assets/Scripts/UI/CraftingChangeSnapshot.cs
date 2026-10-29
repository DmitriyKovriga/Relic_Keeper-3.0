using System;
using System.Collections.Generic;
using System.Globalization;
using Scripts.Inventory;
using Scripts.Items;
using Scripts.Items.Affixes;
using Scripts.Stats;
using Scripts.Skills;

namespace Scripts.UI
{
    /// <summary>Immutable rolled-affix presentation, captured around one committed craft.</summary>
    public sealed class CraftingChangeSnapshot
    {
        public sealed class Roll
        {
            public readonly StatType Stat;
            public readonly StatScope Scope;
            public readonly StatModType Type;
            public readonly float Min, Max;
            public readonly bool HasRange;
            public Roll(AffixModifierInstance mod)
            {
                Stat = mod.Type; Scope = mod.Scope; Type = mod.PrimaryMod.Type;
                mod.GetRolledRange(out Min, out Max); HasRange = mod.HasRange;
            }
        }

        public sealed class Affix
        {
            public readonly ItemAffixSO Data;
            public readonly int Tier;
            public readonly IReadOnlyList<Roll> Rolls;
            internal Affix(AffixInstance affix)
            {
                Data = affix.Data; Tier = affix.Tier;
                var rolls = new List<Roll>();
                foreach (var mod in affix.Modifiers)
                    if (mod?.PrimaryMod != null) rolls.Add(new Roll(mod));
                Rolls = rolls.AsReadOnly();
            }
            public bool SameValues(Affix other)
            {
                if (other == null || Data != other.Data || Tier != other.Tier || Rolls.Count != other.Rolls.Count) return false;
                for (int i = 0; i < Rolls.Count; i++)
                {
                    var a = Rolls[i]; var b = other.Rolls[i];
                    if (a.Stat != b.Stat || a.Scope != b.Scope || a.Type != b.Type || a.Min != b.Min || a.Max != b.Max || a.HasRange != b.HasRange) return false;
                }
                return true;
            }
            public string Format(StatsDatabaseSO database)
            {
                var lines = new List<string>();
                foreach (var roll in Rolls)
                {
                    string name = RuntimeLocalization.Resolve("stats." + roll.Stat, SkillDescriptionGenerator.Humanize(roll.Stat.ToString()), SkillDescriptionGenerator.Humanize(roll.Stat.ToString()));
                    string line = StatPresentation.FormatModifierLine(database, roll.Stat, name, roll.Min, roll.Type, StatPresentation.ModifierLineStyle.StatThenValue);
                    if (roll.HasRange) line += " / " + StatPresentation.FormatModifierValue(database, roll.Stat, roll.Max, roll.Type);
                    if (roll.Scope == StatScope.Local) line += RuntimeLocalization.Resolve("transaction.local", " (local)", " (лок.)");
                    lines.Add(line);
                }
                // Single-stat affixes use the same authored translations as the normal tooltip.
                if (Rolls.Count == 1 && Data != null)
                {
                    string template = RuntimeLocalization.Resolve("AffixesLabels", Data.GetResolvedTranslationKey(), "", "");
                    if (!string.IsNullOrEmpty(template))
                        try { return string.Format(CultureInfo.CurrentCulture, template, Rolls[0].Min, Rolls[0].Max); }
                        catch (FormatException) { }
                }
                return string.Join(", ", lines);
            }
        }

        public sealed class Change
        {
            public readonly Affix Before, After;
            public bool Changed => Before == null || After == null || !Before.SameValues(After);
            public Change(Affix before, Affix after) { Before = before; After = after; }
        }

        public readonly IReadOnlyList<Affix> Affixes;
        private CraftingChangeSnapshot(InventoryItem item)
        {
            var affixes = new List<Affix>();
            if (item?.Affixes != null)
                foreach (var affix in item.Affixes)
                    if (affix?.Data != null) affixes.Add(new Affix(affix));
            Affixes = affixes.AsReadOnly();
        }
        public static CraftingChangeSnapshot Capture(InventoryItem item) => new CraftingChangeSnapshot(item);
        public IReadOnlyList<Change> CompareTo(CraftingChangeSnapshot after)
        {
            var result = new List<Change>(); var used = new HashSet<int>();
            foreach (var current in after.Affixes)
            {
                Affix previous = null;
                for (int i = 0; i < Affixes.Count; i++)
                    if (!used.Contains(i) && Affixes[i].Data == current.Data) { previous = Affixes[i]; used.Add(i); break; }
                result.Add(new Change(previous, current));
            }
            for (int i = 0; i < Affixes.Count; i++) if (!used.Contains(i)) result.Add(new Change(Affixes[i], null));
            return result.AsReadOnly();
        }
    }
}
