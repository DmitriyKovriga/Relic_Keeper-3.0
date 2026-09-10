using System;
using System.Collections.Generic;
using UnityEngine;

public enum GamePauseReason
{
    PauseMenu,
    Inventory,
    DungeonChoice
}

/// <summary>
/// Central pause state. Every UI that pauses gameplay owns an independent handle,
/// so closing one window cannot resume the game while another pause source is active.
/// </summary>
public static class GamePauseService
{
    private static readonly Dictionary<int, GamePauseReason> ActiveRequests =
        new Dictionary<int, GamePauseReason>();

    private static int _nextRequestId;
    private static int _generation;
    private static float _timeScaleBeforePause = 1f;

    public static bool IsPaused => ActiveRequests.Count > 0;
    public static event Action<bool> PauseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ActiveRequests.Clear();
        _nextRequestId = 0;
        _generation++;
        _timeScaleBeforePause = 1f;
        PauseChanged = null;
        Time.timeScale = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateLifetimeGuard()
    {
        var host = new GameObject("[Game Pause Service]");
        host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<GamePauseLifetimeGuard>();
    }

    public static PauseHandle Acquire(GamePauseReason reason)
    {
        int requestId = ++_nextRequestId;
        int requestGeneration = _generation;
        bool wasPaused = IsPaused;

        ActiveRequests.Add(requestId, reason);
        if (!wasPaused)
        {
            _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            PauseChanged?.Invoke(true);
        }

        return new PauseHandle(requestId, requestGeneration);
    }

    /// <summary>
    /// Emergency reset for scene transitions, player death and shutdown.
    /// Existing handles become harmless after this call.
    /// </summary>
    public static void ResumeAll()
    {
        bool wasPaused = IsPaused;
        ActiveRequests.Clear();
        _generation++;

        float restoreScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        _timeScaleBeforePause = 1f;
        Time.timeScale = restoreScale;

        if (wasPaused)
            PauseChanged?.Invoke(false);
    }

    private static void Release(int requestId, int requestGeneration)
    {
        if (requestGeneration != _generation || !ActiveRequests.Remove(requestId) || IsPaused)
            return;

        float restoreScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;
        _timeScaleBeforePause = 1f;
        Time.timeScale = restoreScale;
        PauseChanged?.Invoke(false);
    }

    public sealed class PauseHandle : IDisposable
    {
        private readonly int _requestId;
        private readonly int _requestGeneration;
        private bool _disposed;

        internal PauseHandle(int requestId, int requestGeneration)
        {
            _requestId = requestId;
            _requestGeneration = requestGeneration;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Release(_requestId, _requestGeneration);
        }
    }

}
