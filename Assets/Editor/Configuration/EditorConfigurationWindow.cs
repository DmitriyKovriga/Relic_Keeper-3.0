using Scripts.Configuration;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.EditorTools
{
    public sealed class EditorConfigurationWindow : EditorWindow
    {
        private string _buildVersion;

        [MenuItem("Tools/Relic Keeper/Editor Configuration")]
        private static void Open()
        {
            var window = GetWindow<EditorConfigurationWindow>("Editor Configuration");
            window.minSize = new Vector2(420f, 210f);
            window.Show();
        }

        private void OnEnable()
        {
            _buildVersion = PlayerSettings.bundleVersion;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Version is written to Player Settings and displayed in the lower-left corner of a built game.",
                MessageType.Info);

            string version = EditorGUILayout.TextField("Build Version", _buildVersion);
            if (version != _buildVersion)
                _buildVersion = version;

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_buildVersion) || _buildVersion == PlayerSettings.bundleVersion))
            {
                if (GUILayout.Button("Apply Build Version", GUILayout.Height(24f)))
                {
                    PlayerSettings.bundleVersion = _buildVersion.Trim();
                    AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.LabelField("Editor Play Mode", EditorStyles.boldLabel);

            bool autoSave = EditorGUILayout.ToggleLeft(
                "Enable autosave in Editor",
                PlaytestConfiguration.AutoSaveEnabled);
            if (autoSave != PlaytestConfiguration.AutoSaveEnabled)
                PlaytestConfiguration.SetEditorAutoSave(autoSave);

            bool immortal = EditorGUILayout.ToggleLeft(
                "Player is immortal in Editor",
                PlaytestConfiguration.PlayerImmortal);
            if (immortal != PlaytestConfiguration.PlayerImmortal)
                PlaytestConfiguration.SetEditorPlayerImmortal(immortal);

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                "Player build: autosave is always ON and immortality is always OFF.",
                MessageType.None);
        }
    }
}
