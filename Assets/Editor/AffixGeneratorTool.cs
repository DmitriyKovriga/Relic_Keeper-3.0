using UnityEditor;
using UnityEngine;
using Scripts.Editor.Affixes;

public static class AffixGeneratorTool
{
    [MenuItem("Tools/RPG/Generate Affixes (No Filters + Report)", false, 50)]
    public static void GenerateAffixes()
    {
        Debug.LogWarning("AffixGeneratorTool is deprecated. Opening Affix Editor, which uses the current stats metadata and localization pipeline.");
        AffixEditorWindow.OpenWindow();
    }
}
