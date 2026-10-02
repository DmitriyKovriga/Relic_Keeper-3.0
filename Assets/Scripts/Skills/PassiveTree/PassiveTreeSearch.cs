using System;
using System.Collections.Generic;
using System.Text;
using Scripts.Stats;
using Scripts.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Scripts.Skills.PassiveTree
{
    /// <summary>
    /// Path-of-Exile style passive search: every word must appear in the node name,
    /// its description, a stat it changes, or the wording of that stat in either language.
    /// </summary>
    public static class PassiveTreeSearch
    {
        private static readonly string[][] AliasGroups =
        {
            new[] { "physical", "phys", "физическ", "физ" },
            new[] { "fire", "огонь", "огня", "огнен" },
            new[] { "cold", "холод", "холода", "лед", "льда" },
            new[] { "lightning", "light", "молния", "молнии" },
            new[] { "health", "life", "hp", "здоровье", "здоровья" },
            new[] { "mana", "мана", "маны", "mp" },
            new[] { "armor", "armour", "броня", "брони" },
            new[] { "evasion", "уклонение", "уклонения" },
            new[] { "resist", "resistance", "res", "сопротивление", "сопротивления" },
            new[] { "damage", "урон", "урона" },
            new[] { "critical", "crit", "крит", "критический" },
            new[] { "attack", "атака", "атаки" },
            new[] { "spell", "заклинание", "заклинания" },
            new[] { "speed", "скорость", "скорости" },
            new[] { "block", "блок", "блока" },
            new[] { "projectile", "proj", "снаряд", "снаряда" },
            new[] { "ailment", "недуг", "недуга" },
            new[] { "notable", "заметн" },
            new[] { "keystone", "краеугольн" }
        };

        public static bool Matches(string haystack, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            if (string.IsNullOrEmpty(haystack))
                return false;

            string[] tokens = Normalize(query).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        public static string BuildSearchText(PassiveNodeDefinition node)
        {
            if (node == null)
                return string.Empty;

            var builder = new StringBuilder();
            Append(builder, node.GetDisplayName());
            Append(builder, node.GetDisplayDescription());
            Append(builder, node.NodeType.ToString());
            if (node.NodeType == PassiveNodeType.Start)
            {
                Append(builder, "start");
                Append(builder, "старт");
            }

            if (node.Template != null)
            {
                Append(builder, node.Template.Name);
                Append(builder, node.Template.Description);
                Append(builder, node.Template.name);
                Append(builder, Localized($"passive.node.{node.Template.name}.name", "en"));
                Append(builder, Localized($"passive.node.{node.Template.name}.name", "ru"));
                Append(builder, Localized($"passive.node.{node.Template.name}.description", "en"));
                Append(builder, Localized($"passive.node.{node.Template.name}.description", "ru"));

                var lines = new List<string>();
                PassiveEffectDescriber.AppendAll(node.Template, false, null, lines);
                PassiveEffectDescriber.AppendAll(node.Template, true, null, lines);
                for (int i = 0; i < lines.Count; i++)
                    Append(builder, lines[i]);
            }

            List<SerializableStatModifier> modifiers = node.GetFinalModifiers();
            for (int i = 0; i < modifiers.Count; i++)
                AppendStat(builder, modifiers[i].Stat);

            List<PassiveStatScalingRule> rules = node.GetFinalStatScalingRules();
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] == null)
                    continue;
                AppendStat(builder, rules[i].SourceStat);
                AppendStat(builder, rules[i].TargetStat);
            }

            string text = ExpandAliases(Normalize(builder.ToString()));
            return text;
        }

        private static void AppendStat(StringBuilder builder, StatType stat)
        {
            string raw = stat.ToString();
            Append(builder, raw);
            Append(builder, SplitCamel(raw));
            Append(builder, Localized("stats." + raw, "en"));
            Append(builder, Localized("stats." + raw, "ru"));
        }

        private static void Append(StringBuilder builder, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                builder.Append(' ').Append(value);
        }

        private static string SplitCamel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
                    builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            bool pendingSpace = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (c == 'ё')
                    c = 'е';
                if (char.IsLetterOrDigit(c))
                {
                    if (pendingSpace && builder.Length > 0)
                        builder.Append(' ');
                    pendingSpace = false;
                    builder.Append(c);
                }
                else
                    pendingSpace = true;
            }

            return builder.ToString();
        }

        private static string ExpandAliases(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var extra = new StringBuilder();
            for (int g = 0; g < AliasGroups.Length; g++)
            {
                string[] group = AliasGroups[g];
                bool hit = false;
                for (int i = 0; i < group.Length; i++)
                {
                    if (text.IndexOf(Normalize(group[i]), StringComparison.Ordinal) >= 0)
                    {
                        hit = true;
                        break;
                    }
                }

                if (!hit)
                    continue;

                for (int i = 0; i < group.Length; i++)
                    extra.Append(' ').Append(Normalize(group[i]));
            }

            return extra.Length == 0 ? text : text + extra;
        }

        private static string Localized(string key, string languageCode)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            try
            {
                Locale locale = LocalizationSettings.AvailableLocales?.GetLocale(languageCode);
                if (locale == null || LocalizationSettings.StringDatabase == null)
                    return string.Empty;

                var table = LocalizationSettings.StringDatabase.GetTable(RuntimeLocalization.MenuLabelsTable, locale);
                string text = table?.GetEntry(key)?.GetLocalizedString();
                if (string.IsNullOrWhiteSpace(text) || text.IndexOf("translation found", StringComparison.OrdinalIgnoreCase) >= 0)
                    return string.Empty;
                return text;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
