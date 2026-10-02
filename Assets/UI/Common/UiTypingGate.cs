/// <summary>
/// Window shortcuts stay live while a modal pauses the Player map.
/// Set this while a text field has focus so those shortcuts do not fire mid-typing.
/// </summary>
public static class UiTypingGate
{
    public static bool IsTyping { get; set; }
}
