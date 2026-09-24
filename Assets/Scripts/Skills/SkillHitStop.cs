using System.Collections;
using UnityEngine;

namespace Scripts.Skills
{
    /// <summary>One-shot gate shared by every hit produced by one skill cast.</summary>
    public sealed class SkillHitStopGate
    {
        private readonly int _frames;
        private bool _consumed;

        public int Frames => _frames;
        public bool IsConsumed => _consumed;

        public SkillHitStopGate(int frames)
        {
            _frames = HitStopService.ClampFrames(frames);
        }

        public bool TryTrigger()
        {
            if (_consumed)
                return false;

            _consumed = true;
            HitStopService.Request(_frames);
            return true;
        }
    }

    /// <summary>Freezes scaled gameplay for a small number of rendered frames.</summary>
    public static class HitStopService
    {
        public const int MinFrames = 1;
        public const int MaxFrames = 5;

        private static HitStopRunner _runner;

        public static int ClampFrames(int frames) => Mathf.Clamp(frames, MinFrames, MaxFrames);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _runner = null;
        }

        public static void Request(int frames)
        {
            if (!Application.isPlaying || GamePauseService.IsPaused)
                return;

            EnsureRunner().Request(ClampFrames(frames));
        }

        private static HitStopRunner EnsureRunner()
        {
            if (_runner != null)
                return _runner;

            var host = new GameObject("[Hit Stop]");
            host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<HitStopRunner>();
            return _runner;
        }

        private sealed class HitStopRunner : MonoBehaviour
        {
            private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();
            private int _framesRemaining;
            private float _restoreTimeScale = 1f;
            private Coroutine _freezeRoutine;

            public void Request(int frames)
            {
                if (_framesRemaining <= 0)
                {
                    if (Time.timeScale <= 0f)
                        return;

                    _restoreTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }

                _framesRemaining = Mathf.Max(_framesRemaining, frames);
                if (_freezeRoutine == null)
                    _freezeRoutine = StartCoroutine(FreezeForRenderedFrames());
            }

            private IEnumerator FreezeForRenderedFrames()
            {
                while (_framesRemaining > 0)
                {
                    yield return EndOfFrame;
                    _framesRemaining--;
                }

                _freezeRoutine = null;
                RestoreTimeScale();
            }

            private void OnDestroy()
            {
                RestoreTimeScale();
            }

            private void RestoreTimeScale()
            {
                _framesRemaining = 0;
                if (_freezeRoutine != null)
                {
                    StopCoroutine(_freezeRoutine);
                    _freezeRoutine = null;
                }
                if (!GamePauseService.IsPaused && Mathf.Approximately(Time.timeScale, 0f))
                    Time.timeScale = _restoreTimeScale > 0f ? _restoreTimeScale : 1f;
                _restoreTimeScale = 1f;
            }
        }
    }
}
