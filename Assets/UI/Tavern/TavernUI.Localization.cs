using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Scripts.Stats;

public partial class TavernUI
{
    private static bool IsMissingLocalization(string value)
    {
        return string.IsNullOrEmpty(value) ||
               value.IndexOf("translation found", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetLocalizedLabel(Label label, string key, string fallback)
    {
        if (label == null) return;
        label.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        op.Completed += _ =>
        {
            if (label != null && label.panel != null)
                label.text = !IsMissingLocalization(op.Result) ? op.Result : fallback;
        };
    }

    private void SetLocalizedButton(Button btn, string key, string fallback)
    {
        if (btn == null) return;
        btn.text = fallback;
        var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(MenuLabelsTable, key);
        op.Completed += _ =>
        {
            if (btn != null && btn.panel != null)
                btn.text = !IsMissingLocalization(op.Result) ? op.Result : fallback;
        };
    }

    private IEnumerable<string> FormatStartingStatsLines(CharacterDataSO ch)
    {
        GlobalBaseStatsSO bases = Resources.Load<GlobalBaseStatsSO>(GlobalBaseStatsSO.DefaultResourcesPath);
        return FormatStartingStatDeltaLines(ch != null ? ch.StartingStats : null, bases);
    }

    public static IEnumerable<string> FormatStartingStatDeltaLines(
        IReadOnlyList<CharacterDataSO.StatConfig> starting,
        GlobalBaseStatsSO bases)
    {
        if (starting == null)
            yield break;

        foreach (CharacterDataSO.StatConfig stat in starting)
        {
            float baseValue = 0f;
            if (bases != null)
                bases.TryGetValue(stat.Type, out baseValue);

            float delta = stat.Value - baseValue;
            if (Mathf.Abs(delta) < 0.001f)
                continue;

            string valueText = StatPresentation.FormatModifierValue(null, stat.Type, delta, StatModType.Flat);
            string name = CharacterWindowLoc.StatName(stat.Type);
            yield return $"{valueText} {name}";
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
