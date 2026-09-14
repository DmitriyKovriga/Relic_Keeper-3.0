/// <summary>
/// Describes why game progress is being written. Automatic reasons obey the
/// playtest autosave switch; Manual is always allowed.
/// </summary>
public enum GameSaveReason
{
    Manual,
    LegacyAutomatic,
    LocationTransition,
    CharacterDeath,
    TavernPartyChanged,
    StarterGearGranted
}

public static class GameSavePolicy
{
    public static bool IsAllowed(GameSaveReason reason, bool autoSaveEnabled)
    {
        return reason == GameSaveReason.Manual || autoSaveEnabled;
    }
}
