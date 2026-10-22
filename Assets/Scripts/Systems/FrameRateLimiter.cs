using UnityEngine;

/// <summary>Synchronizes presentation to the display, with a 60 FPS fallback.</summary>
public static class FrameRateLimiter
{
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Apply()
    {
        // Desktop presentation must wait for a complete refresh to avoid screen tearing.
        QualitySettings.vSyncCount = 1;
        // Platforms that ignore VSync still use the software frame-rate limit.
        Application.targetFrameRate = TargetFrameRate;
    }
}
