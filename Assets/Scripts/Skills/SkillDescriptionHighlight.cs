using System;
using System.Text;

namespace Scripts.Skills
{
    /// <summary>
    /// Colors action verbs, numbers, and quoted stat names for skill tooltip text.
    /// </summary>
    public static class SkillDescriptionHighlight
    {
        public const string ActionColor = "#73D9D9";
        public const string NumberColor = "#F3D147";
        public const string StatColor = "#8CB2FF";

        private static readonly string[] LeadingPhrases =
        {
            "Поражённые враги получает",
            "Персонаж получает",
            "Модификатор навыка",
            "Поддерживаемый навык",
            "Полностью восстанавливает",
            "Affected enemies",
            "The character",
            "Skill modifier",
            "Channelled skill",
            "При попадании",
            "Восстанавливает",
            "Конвертирует",
            "Увеличивает",
            "Накладывает",
            "Отталкивает",
            "Отбрасывает",
            "Перемещает",
            "Поглощает",
            "Выпускает",
            "On hit",
            "Applies",
            "Creates",
            "Releases",
            "Restores",
            "Consumes",
            "Generates",
            "Converts",
            "Propels",
            "Knocks",
            "Grants",
            "Chains",
            "Deals",
            "Fires",
            "Adds",
            "Removes",
            "Наносит",
            "Создаёт",
            "Сокращает",
            "Даёт",
            "Цепь"
        };

        public static string Colorize(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) >= 0)
                return text;

            return ColorizeQuotesAndNumbers(ColorizeLeadingAction(text));
        }

        public static string Wrap(string value, string hex)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return $"<color={hex}>{value}</color>";
        }

        private static string ColorizeLeadingAction(string text)
        {
            for (int i = 0; i < LeadingPhrases.Length; i++)
            {
                string phrase = LeadingPhrases[i];
                if (phrase.Length > text.Length)
                    continue;
                if (string.Compare(text, 0, phrase, 0, phrase.Length, StringComparison.OrdinalIgnoreCase) != 0)
                    continue;

                if (phrase.Length < text.Length && char.IsLetter(text[phrase.Length]))
                    continue;

                return Wrap(text.Substring(0, phrase.Length), ActionColor) + text.Substring(phrase.Length);
            }

            return text;
        }

        private static string ColorizeQuotesAndNumbers(string text)
        {
            var sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close < 0)
                    {
                        sb.Append(text, i, text.Length - i);
                        break;
                    }

                    sb.Append(text, i, close - i + 1);
                    i = close + 1;
                    continue;
                }

                if (text[i] == '«')
                {
                    int end = text.IndexOf('»', i + 1);
                    if (end > i)
                    {
                        sb.Append('«');
                        sb.Append(Wrap(text.Substring(i + 1, end - i - 1), StatColor));
                        sb.Append('»');
                        i = end + 1;
                        continue;
                    }
                }

                if (TryConsumeNumber(text, i, out int numberEnd))
                {
                    sb.Append(Wrap(text.Substring(i, numberEnd - i), NumberColor));
                    i = numberEnd;
                    continue;
                }

                sb.Append(text[i]);
                i++;
            }

            return sb.ToString();
        }

        private static bool TryConsumeNumber(string text, int index, out int end)
        {
            end = index;
            char current = text[index];
            bool signed = current == '+' || current == '-';
            if (signed)
            {
                if (index + 1 >= text.Length || !char.IsDigit(text[index + 1]))
                    return false;
            }
            else if (!char.IsDigit(current))
            {
                return false;
            }

            if (index > 0 && IsTokenChar(text[index - 1]))
                return false;

            int cursor = index;
            if (signed)
                cursor++;
            while (cursor < text.Length && char.IsDigit(text[cursor]))
                cursor++;

            if (cursor < text.Length && (text[cursor] == '.' || text[cursor] == ',')
                && cursor + 1 < text.Length && char.IsDigit(text[cursor + 1]))
            {
                cursor++;
                while (cursor < text.Length && char.IsDigit(text[cursor]))
                    cursor++;
            }

            if (cursor < text.Length && text[cursor] == '%')
                cursor++;
            else if (cursor < text.Length && (text[cursor] == 's' || text[cursor] == 'S')
                     && (cursor + 1 >= text.Length || !char.IsLetter(text[cursor + 1])))
            {
                cursor++;
            }

            if (cursor == index || (signed && cursor == index + 1))
                return false;

            end = cursor;
            return true;
        }

        private static bool IsTokenChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '%';
        }
    }
}
