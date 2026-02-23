using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Scripts.Stats;

public partial class TavernUI
{
    private void SetLocalizedLabel(Label label, string key, string fallback)
    {
        if (label == null) return;
        label.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        op.Completed += _ => { if (label != null && label.panel != null) label.text = !string.IsNullOrEmpty(op.Result) ? op.Result : fallback; };
    }

    private void SetLocalizedButton(Button btn, string key, string fallback)
    {
        if (btn == null) return;
        btn.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        op.Completed += _ => { if (btn != null && btn.panel != null) btn.text = !string.IsNullOrEmpty(op.Result) ? op.Result : fallback; };
    }

    private static string FormatStatName(StatType type)
    {
        var s = type.ToString();
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]))
                sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private IEnumerable<string> FormatStartingStatsLines(CharacterDataSO ch)
    {
        if (ch.StartingStats == null || ch.StartingStats.Count == 0) yield break;
        foreach (var s in ch.StartingStats)
        {
            string name = FormatStatName(s.Type);
            yield return $"{name}: {s.Value}";
        }
    }

    private string GetLocalizedName(CharacterDataSO ch)
    {
        if (string.IsNullOrEmpty(ch.NameKey)) return ch.DisplayName;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ch.NameKey);
        return op.IsDone ? op.Result : ch.DisplayName;
    }

    private string GetLocalizedDescription(CharacterDataSO ch)
    {
        if (string.IsNullOrEmpty(ch.DescriptionKey)) return ch.DescriptionFallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, ch.DescriptionKey);
        return op.IsDone ? op.Result : ch.DescriptionFallback;
    }
}
