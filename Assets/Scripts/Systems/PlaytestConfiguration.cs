namespace Scripts.Configuration
{
    /// <summary>
    /// Editor-only playtest switches. Player builds intentionally ignore these values:
    /// autosave is always enabled, player immortality and loot diagnostics are always disabled,
    /// and the stash and the tavern can only be reached through their hub NPCs.
    /// </summary>
    public static class PlaytestConfiguration
    {
        public const bool DefaultEditorAutoSave = false;
        public const bool DefaultEditorPlayerImmortal = true;
        public const bool DefaultEditorStashAlwaysAvailable = true;
        public const bool DefaultEditorTavernAlwaysAvailable = true;
        public const bool DefaultEditorShowLootChanceInfo = false;

#if UNITY_EDITOR
        private const string AutoSaveEditorPref = "RelicKeeper.EditorConfiguration.AutoSave";
        private const string PlayerImmortalEditorPref = "RelicKeeper.EditorConfiguration.PlayerImmortal";
        private const string StashAlwaysAvailableEditorPref = "RelicKeeper.EditorConfiguration.StashAlwaysAvailable";
        private const string TavernAlwaysAvailableEditorPref = "RelicKeeper.EditorConfiguration.TavernAlwaysAvailable";
        private const string ShowLootChanceInfoEditorPref = "RelicKeeper.EditorConfiguration.ShowLootChanceInfo";
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

        /// <summary>Можно ли открыть склад хоткеем в любой точке мира. В билде — только через NPC в хабе.</summary>
        public static bool StashAlwaysAvailable
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(StashAlwaysAvailableEditorPref, DefaultEditorStashAlwaysAvailable);
#else
                return false;
#endif
            }
        }

        /// <summary>Можно ли открыть трактир хоткеем в любой точке мира. В билде — только через NPC в хабе.</summary>
        public static bool TavernAlwaysAvailable
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(TavernAlwaysAvailableEditorPref, DefaultEditorTavernAlwaysAvailable);
#else
                return false;
#endif
            }
        }

        /// <summary>Показывать ли editor-only панель с эффективными шансами лута.</summary>
        public static bool ShowLootChanceInfo
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(ShowLootChanceInfoEditorPref, DefaultEditorShowLootChanceInfo);
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

        public static void SetEditorStashAlwaysAvailable(bool enabled) =>
            UnityEditor.EditorPrefs.SetBool(StashAlwaysAvailableEditorPref, enabled);

        public static void SetEditorTavernAlwaysAvailable(bool enabled) =>
            UnityEditor.EditorPrefs.SetBool(TavernAlwaysAvailableEditorPref, enabled);

        public static void SetEditorShowLootChanceInfo(bool enabled) =>
            UnityEditor.EditorPrefs.SetBool(ShowLootChanceInfoEditorPref, enabled);
#endif
    }
}
