public static class InventoryCompanionLayout
{
    // Fixed HUD-space contract for inventory + companion windows on 480x270.
    public const float ScreenWidth = 480f;
    public const float ScreenHeight = 270f;
    public const float InventoryDockWidth = 275f;
    public const float CompanionGap = 3f;

    public static float LeftCompanionWidth => ScreenWidth - InventoryDockWidth - CompanionGap; // 202
}
