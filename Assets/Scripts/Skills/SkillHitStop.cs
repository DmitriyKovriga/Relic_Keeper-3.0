using Scripts.Visuals;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>One attempt shared by every target, projectile clone and return pass of one cast.</summary>
    public sealed class SkillHitStopGate
    {
        private readonly int _frames;
        private bool _consumed;
        public int Frames => _frames;
        public bool IsConsumed => _consumed;
        public SkillHitStopGate(int frames) => _frames = HitStopService.ClampFrames(frames);

        public bool TryTrigger()
        {
            if (_consumed) return false;
            _consumed = true;
            if (_frames == 0) return false;
            HitStopService.Request(_frames);
            return true; // Cast attempt, not a promise that the shared interval granted a freeze.
        }
    }

    /// <summary>Pure real-time budget. Requests cannot extend an accepted deadline, even across casts.</summary>
    public sealed class HitStopBudget
    {
        private bool _requested;
        private double _next;

        public bool TrySchedule(double now, int frames, float interval, out double deadline)
        {
            deadline = now;
            if (double.IsNaN(now) || double.IsInfinity(now) || float.IsNaN(interval) || float.IsInfinity(interval) ||
                HitStopService.ClampFrames(frames) == 0 || _requested && now < _next) return false;
            double duration = HitStopService.DurationSeconds(frames);
            deadline = now + duration;
            _next = now + System.Math.Max(System.Math.Max(0, interval), duration + .05);
            _requested = true; return true;
        }

        public void Clear() { _requested = false; _next = 0; }
    }

    /// <summary>Short unscaled-time freeze; recipe frame values retain their 60 Hz authored duration.</summary>
    public static class HitStopService
    {
        public const int MinFrames = 0, MaxFrames = 5;
        public const float ReferenceFramesPerSecond = 60f;
        private static HitStopRunner _runner;
        private static CombatPoseProfile _profile;
        public static bool IsActive => _runner != null && _runner.OwnsFreeze;
        public static int ClampFrames(int frames) => Mathf.Clamp(frames, MinFrames, MaxFrames);
        public static float DurationSeconds(int frames) => ClampFrames(frames) / ReferenceFramesPerSecond;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { _runner = null; _profile = null; }

        public static void Request(int frames)
        {
            if (!Application.isPlaying || GamePauseService.IsPaused || Time.timeScale <= 0 || ClampFrames(frames) == 0) return;
            if (_profile == null) _profile = CombatPoseProfile.LoadDefault();
            if (_profile == null || !_profile.EnableHitStop) return;
            EnsureRunner().Request(frames, Time.unscaledTimeAsDouble, _profile);
        }

        /// <summary>Pause/scene ownership takes priority. Skill cancellation need not own this independent timer.</summary>
        public static void CancelCurrent() { if (_runner != null) _runner.Cancel(); }

        private static HitStopRunner EnsureRunner()
        {
            if (_runner != null) return _runner;
            var host = new GameObject("[Hit Stop]") { hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
            if (Application.isPlaying) Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<HitStopRunner>(); return _runner;
        }

        [DefaultExecutionOrder(-10000)]
        private sealed class HitStopRunner : MonoBehaviour
        {
            private readonly HitStopBudget _budget = new HitStopBudget();
            private double _deadline;
            private float _restoreTimeScale = 1;
            private CombatPoseProfile _settings;
            public bool OwnsFreeze { get; private set; }

            public bool Request(int frames, double now, CombatPoseProfile settings)
            {
                if (!isActiveAndEnabled || OwnsFreeze || Time.timeScale <= 0 || GamePauseService.IsPaused || settings == null || !settings.EnableHitStop ||
                    !_budget.TrySchedule(now, frames, settings.HitStopInterval, out double deadline)) return false;
                _settings = settings; _deadline = deadline; _restoreTimeScale = Time.timeScale;
                OwnsFreeze = true; Time.timeScale = 0; return true;
            }

            private void Update() => Sample(Time.unscaledTimeAsDouble);
            public void Sample(double now)
            {
                if (OwnsFreeze && (now >= _deadline || _settings == null || !_settings.EnableHitStop)) Release();
            }

            public void Cancel() { Release(); _budget.Clear(); }
            private void OnDisable() => Cancel();
            private void OnDestroy() => Cancel();

            private void Release()
            {
                // Restore only our own zero; never overwrite a menu pause or another clock owner.
                if (OwnsFreeze && !GamePauseService.IsPaused && Mathf.Approximately(Time.timeScale, 0))
                    Time.timeScale = _restoreTimeScale > 0 ? _restoreTimeScale : 1;
                OwnsFreeze = false; _restoreTimeScale = 1;
            }
        }
    }
}
