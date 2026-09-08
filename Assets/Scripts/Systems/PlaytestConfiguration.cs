namespace Scripts.Configuration
{
    /// <summary>
    /// Editor-only playtest switches. Player builds intentionally ignore these values:
    /// autosave is always enabled and player immortality is always disabled.
    /// </summary>
    public static class PlaytestConfiguration
    {
        public const bool DefaultEditorAutoSave = false;
        public const bool DefaultEditorPlayerImmortal = true;

#if UNITY_EDITOR
        private const string AutoSaveEditorPref = "RelicKeeper.EditorConfiguration.AutoSave";
        private const string PlayerImmortalEditorPref = "RelicKeeper.EditorConfiguration.PlayerImmortal";
#endif

        public static bool AutoSaveEnabled
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(AutoSaveEditorPref, DefaultEditorAutoSave);
#else
                return true;
#endif
            }
        }

        public static bool PlayerImmortal
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(PlayerImmortalEditorPref, DefaultEditorPlayerImmortal);
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR
        public static void SetEditorAutoSave(bool enabled) =>
            UnityEditor.EditorPrefs.SetBool(AutoSaveEditorPref, enabled);

        public static void SetEditorPlayerImmortal(bool enabled) =>
            UnityEditor.EditorPrefs.SetBool(PlayerImmortalEditorPref, enabled);
#endif
    }
}
