using UnityEngine;

namespace Scripts.Items.World
{
    public enum WorldItemInspectSource
    {
        None,
        Cursor,
        Player
    }

    public static class WorldItemInspection
    {
        public const float CombatTooltipDelay = 2f;
        public const float ActiveSkillTooltipBlockDuration = 3f;
        public const float StationaryNearbySkillBlockDuration = 0.8f;
        public const float CursorMoveLinger = 0.12f;
        public const float CursorMovePixels = 1f;
        public const float PlayerMoveSpeedSqr = 0.05f;

        private static float _combatTooltipBlockedUntil = float.NegativeInfinity;
        private static float _lastActiveSkillInputAt = float.NegativeInfinity;
        private static bool _stationaryNearInspectedItem;

        public static bool IsCombatTooltipBlocked =>
            IsCombatTooltipBlockedAt(Time.unscaledTime, _combatTooltipBlockedUntil,
                _lastActiveSkillInputAt, _stationaryNearInspectedItem);

        public static void ExtendCombatTooltipBlock()
        {
            _lastActiveSkillInputAt = Time.unscaledTime;
            _combatTooltipBlockedUntil = ResolveCombatTooltipBlockedUntil(
                _combatTooltipBlockedUntil,
                _lastActiveSkillInputAt,
                ActiveSkillTooltipBlockDuration);
        }

        public static void SetStationaryNearInspectedItem(bool value)
        {
            _stationaryNearInspectedItem = value;
        }

        public static float ResolveCombatTooltipBlockedUntil(float currentBlockedUntil, float now, float duration)
        {
            return Mathf.Max(currentBlockedUntil, now + Mathf.Max(0f, duration));
        }

        public static bool IsCombatTooltipBlockedAt(float now, float blockedUntil)
        {
            return now < blockedUntil;
        }

        public static bool IsCombatTooltipBlockedAt(
            float now, float blockedUntil, float lastActiveSkillInputAt, bool stationaryNearInspectedItem)
        {
            if (stationaryNearInspectedItem)
                blockedUntil = Mathf.Min(blockedUntil,
                    lastActiveSkillInputAt + StationaryNearbySkillBlockDuration);

            return IsCombatTooltipBlockedAt(now, blockedUntil);
        }

        public static float ResolveTooltipDelay(float standardDelay, bool roomCleared)
        {
            return roomCleared ? Mathf.Max(0.01f, standardDelay) : CombatTooltipDelay;
        }

        public static float ResolveTooltipDelay(
            float standardDelay, bool roomCleared, bool stationaryNearInspectedItem)
        {
            float delay = ResolveTooltipDelay(standardDelay, roomCleared);
            return stationaryNearInspectedItem ? Mathf.Min(delay, Mathf.Max(0.01f, standardDelay)) : delay;
        }

        public static WorldItemInspectSource ResolveSource(
            bool cursorMoving,
            bool playerMoving,
            bool cursorHasItem,
            bool playerHasItem)
        {
            if (cursorHasItem && (cursorMoving || !playerMoving || !playerHasItem))
                return WorldItemInspectSource.Cursor;
            if (playerHasItem)
                return WorldItemInspectSource.Player;
            return WorldItemInspectSource.None;
        }

        public static bool IsCursorMoving(float mouseDeltaPixels, float lastMoveUnscaledTime, float nowUnscaledTime)
        {
            if (mouseDeltaPixels >= CursorMovePixels)
                return true;

            return nowUnscaledTime - lastMoveUnscaledTime <= CursorMoveLinger;
        }

        public static bool IsPlayerMoving(Vector2 velocity)
        {
            return velocity.sqrMagnitude > PlayerMoveSpeedSqr;
        }

        public static bool CanPickupWithCursorClick(
            WorldItemInspectSource source,
            WorldDroppedItem inspectedItem,
            WorldDroppedItem clickedItem)
        {
            return source == WorldItemInspectSource.Cursor
                && inspectedItem != null
                && clickedItem != null
                && ReferenceEquals(inspectedItem, clickedItem)
                && clickedItem.CanInteract();
        }
    }
}
