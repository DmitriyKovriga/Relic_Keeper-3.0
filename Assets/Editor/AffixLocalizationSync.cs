using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;
using Scripts.Items.Affixes;
using Scripts.Editor.Affixes;

public class AffixLocalizationSync : EditorWindow
{
    private const string AffixPath = "Assets/Resources/Affixes/Generated";

    [SerializeField] private StringTableCollection _menuLabels;
    [SerializeField] private StringTableCollection _affixesLabels;

    [MenuItem("Tools/RPG/2. Sync Affix Text (Recursive)")]
    public static void ShowWindow()
    {
        GetWindow<AffixLocalizationSync>("Sync Affixes");
    }

    private void OnEnable()
    {
        if (_menuLabels == null)
            _menuLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/Localization/LocalizationTables/MenuLabels.asset");
        if (_affixesLabels == null)
            _affixesLabels = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/Localization/LocalizationTables/AffixesLabels.asset");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Affix Localization Sync", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool now uses the same production pipeline as the Affix Editor and the stats upgrade. " +
            "It regenerates auto-managed affix localization through AffixSetGenerator and no longer keeps its own legacy templates.",
            MessageType.Info);

        _menuLabels = (StringTableCollection)EditorGUILayout.ObjectField("Menu Labels", _menuLabels, typeof(StringTableCollection), false);
        _affixesLabels = (StringTableCollection)EditorGUILayout.ObjectField("Affixes Labels", _affixesLabels, typeof(StringTableCollection), false);

        using (new EditorGUI.DisabledScope(_menuLabels == null || _affixesLabels == null))
        {
            if (GUILayout.Button("Regenerate Auto Affix Localization"))
                Sync();
        }
    }

    private void Sync()
    {
        if (_menuLabels == null || _affixesLabels == null)
        {
            Debug.LogError("AffixLocalizationSync: MenuLabels and AffixesLabels must both be assigned.");
            return;
        }

        AffixSetGenerator.EnsureValueUnitLocalizations(_menuLabels);

        string[] guids = AssetDatabase.FindAssets("t:ItemAffixSO", new[] { AffixPath });
        int regenerated = 0;
        int skippedLocked = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var affix = AssetDatabase.LoadAssetAtPath<ItemAffixSO>(path);
            if (affix == null)
                continue;

            if (affix.LockAutoLocalization)
            {
                skippedLocked++;
                continue;
            }

            AffixSetGenerator.RegenerateLocalizationFromStat(affix, _menuLabels, _affixesLabels);
            EditorUtility.SetDirty(affix);
            regenerated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"AffixLocalizationSync: regenerated {regenerated} auto-managed affixes, skipped {skippedLocked} locked affixes.");
    }
}
