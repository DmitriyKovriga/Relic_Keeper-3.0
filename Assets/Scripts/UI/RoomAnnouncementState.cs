using System;
using UnityEngine;

namespace Scripts.UI
{
    [Flags]
    public enum RoomNotice { None = 0, PortalUnlocked = 1, Cleared = 2, LevelUp = 4 }
    public enum ExitHintDirection { None, Left, Right, Above, Below }

    /// <summary>One merged announcement, extended only by genuinely new information.</summary>
    public sealed class RoomAnnouncementState
    {
        public const float FadeIn = .28f, Hold = 1.6f, FadeOut = .28f;
        public RoomNotice Current { get; private set; }
        public float StartedAt { get; private set; }
        public float ExpiresAt { get; private set; }
        public bool IsVisible(float now) => Current != RoomNotice.None && now < ExpiresAt;

        public bool Add(RoomNotice notice, float now)
        {
            if (!IsVisible(now)) Clear();
            if (notice == RoomNotice.None || (notice & ~Current) == RoomNotice.None) return false;
            if (Current == RoomNotice.None) StartedAt = now;
            Current |= notice; ExpiresAt = now + FadeIn + Hold + FadeOut;
            return true;
        }
        public float Opacity(float now) => !IsVisible(now) ? 0 : Mathf.Min(
            Mathf.Clamp01((now - StartedAt) / FadeIn), Mathf.Clamp01((ExpiresAt - now) / FadeOut));
        public void Clear() { Current = RoomNotice.None; StartedAt = ExpiresAt = 0; }

        public static ExitHintDirection ExitDirection(Vector3 viewport)
        {
            if (viewport.z <= 0 || (viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1)) return ExitHintDirection.None;
            Vector2 delta = new Vector2(viewport.x - .5f, viewport.y - .5f);
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) return delta.x < 0 ? ExitHintDirection.Left : ExitHintDirection.Right;
            return delta.y < 0 ? ExitHintDirection.Below : ExitHintDirection.Above;
        }
    }
}
