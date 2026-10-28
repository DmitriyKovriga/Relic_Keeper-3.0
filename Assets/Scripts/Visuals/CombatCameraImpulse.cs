using Scripts.Combat;
using Scripts.Skills;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scripts.Visuals
{
    /// <summary>A scoped Cinemachine impulse, composed before Body/confiner processing.
    /// The local unscaled clock continues through hit-stop without changing Cinemachine's global clock.
    /// Real menu pause cancels it. No output-camera transform or follow target is modified.</summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class CombatCameraImpulse : CinemachineExtension
    {
        private readonly CombatCameraPulse _pulse = new CombatCameraPulse();
        private CombatPoseProfile _profile;
        private Transform _follow;
        private PlayerStats _player;
        private Collider2D _bounds;
        private CinemachineConfiner2D _confiner;
        private double _clock;
        private float _strength;
        public Vector3 LastOffset { get; private set; }
        public int AcceptedImpulses { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Install();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Install();

        private static void Install()
        {
            foreach (var camera in Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                if (camera.Follow != null && camera.GetComponent<CombatCameraImpulse>() == null)
                    camera.gameObject.AddComponent<CombatCameraImpulse>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _profile = CombatPoseProfile.LoadDefault();
            _confiner = GetComponent<CinemachineConfiner2D>();
            OnSettings();
            RememberScope();
            CombatHitFeedback.HitConfirmed += React;
            GamePauseService.PauseChanged += OnPause;
            GameplayPresentationSettings.Changed += OnSettings;
        }

        private void OnDisable()
        {
            CombatHitFeedback.HitConfirmed -= React;
            GamePauseService.PauseChanged -= OnPause;
            GameplayPresentationSettings.Changed -= OnSettings;
            ResetImpulse();
        }

        private void OnPause(bool paused) { if (paused) ResetImpulse(); }
        private void OnSettings()
        {
            _strength = GameplayPresentationSettings.CameraImpulseStrength;
            if (_strength <= 0) ResetImpulse();
        }
        private void RememberScope()
        {
            _follow = ComponentOwner != null ? ComponentOwner.Follow : null;
            _player = _follow != null ? _follow.GetComponentInParent<PlayerStats>() : null;
            _bounds = _confiner != null ? _confiner.BoundingShape2D : null;
        }

        private void CheckScope()
        {
            if (_follow != ComponentOwner.Follow || _bounds != (_confiner != null ? _confiner.BoundingShape2D : null))
            { ResetImpulse(); RememberScope(); }
        }

        private bool Ready => _profile != null && _profile.EnableCameraImpulse && _strength > 0 &&
            _player != null && _player.isActiveAndEnabled && _player.Health != null && _player.Health.Current > 0 &&
            !GamePauseService.IsPaused && (Time.timeScale > 0 || HitStopService.IsActive);

        private void React(ConfirmedHit hit)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || ComponentOwner == null) return;
            CheckScope();
            var attack = hit.Contact?.Attack;
            if (!Ready || attack == null || hit.Contact.Weight != AttackImpactWeight.Heavy || hit.Source == null ||
                hit.Source.GetComponent<PlayerStats>() == null || _follow == null ||
                (_follow != hit.Source.transform && !_follow.IsChildOf(hit.Source.transform)) || !CinemachineCore.IsLive(ComponentOwner)) return;
            var brain = CinemachineCore.FindPotentialTargetBrain(ComponentOwner);
            if (brain == null || brain.OutputCamera == null || !IsVisible(brain.OutputCamera, hit.Position)) return;
            // Consume even when the shared interval suppresses this cast: a late repeat must not shake later.
            if (!attack.TryConsumeCameraImpulse()) return;
            if (_pulse.Trigger(_clock, hit.Direction, _profile.CameraImpulsePixels,
                _profile.CameraImpulseDuration, _profile.CameraImpulseInterval)) AcceptedImpulses++;
        }

        public static bool IsVisible(Camera camera, Vector3 point)
        {
            Vector3 view = camera.WorldToViewportPoint(point);
            return view.z > 0 && view.x >= 0 && view.x <= 1 && view.y >= 0 && view.y <= 1;
        }

        private void Update()
        {
            if (!Application.isPlaying || ComponentOwner == null) return;
            CheckScope();
            if (!Ready || !CinemachineCore.IsLive(ComponentOwner)) { ResetImpulse(); return; }
            _clock += Time.unscaledDeltaTime;
        }

        public override void PrePipelineMutateCameraStateCallback(CinemachineVirtualCameraBase vcam, ref CameraState state, float deltaTime)
        {
            LastOffset = Vector3.zero;
            if (!Application.isPlaying || !Ready || deltaTime < 0 || !state.Lens.Orthographic) return;
            CheckScope();
            // Reference-pixel translation only. Confiner sees the correction before it constrains the viewport.
            // This avoids fractional camera drift, rotational shimmer and a second camera-follow system.
            LastOffset = PixelOffset(_pulse.Sample(_clock) * _strength, state.Lens.OrthographicSize);
            state.PositionCorrection += LastOffset;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
            ref CameraState state, float deltaTime) { }

        public static Vector3 PixelOffset(Vector3 pixels, float orthoSize)
        {
            pixels = Vector3.ClampMagnitude(pixels, 2f);
            var snapped = new Vector3(Mathf.Round(pixels.x), Mathf.Round(pixels.y), 0);
            if (snapped.sqrMagnitude > 4f)
            {
                if (Mathf.Abs(snapped.x) >= Mathf.Abs(snapped.y)) snapped.y = 0;
                else snapped.x = 0;
            }
            return snapped * (2f * orthoSize / 270f);
        }

        public void ResetImpulse() { _pulse.Clear(); LastOffset = Vector3.zero; }
        public override void OnTargetObjectWarped(CinemachineVirtualCameraBase vcam, Transform target, Vector3 positionDelta) => ResetImpulse();
        public override void ForceCameraPosition(CinemachineVirtualCameraBase vcam, Vector3 pos, Quaternion rot) => ResetImpulse();
    }

    /// <summary>One bounded Cinemachine signal, never an additive noise stack. The owner supplies its pause-aware clock.</summary>
    public sealed class CombatCameraPulse : ISignalSource6D
    {
        private static readonly AnimationCurve Shape = new AnimationCurve(
            new Keyframe(0, 0, 0, 0), new Keyframe(.18f, 1, 0, 0), new Keyframe(1, 0, 0, 0));
        private double _start, _next;
        private bool _started;
        private Vector3 _velocity;
        public float SignalDuration { get; private set; }

        public bool Trigger(double now, Vector2 direction, float pixels, float duration, float interval)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || !Finite(pixels) || !Finite(duration) || !Finite(interval) ||
                !Finite(direction.x) || !Finite(direction.y) || pixels <= 0 || duration <= 0 || direction.sqrMagnitude < .0001f || _started && now < _next) return false;
            _start = now; _started = true; SignalDuration = Mathf.Clamp(duration, .08f, .2f);
            _velocity = direction.normalized * Mathf.Clamp(pixels, 0, 2);
            _next = now + Mathf.Max(.3f, interval, SignalDuration + .15f);
            return true;
        }

        public Vector3 Sample(double now)
        {
            if (!_started || now < _start || now >= _start + SignalDuration) return Vector3.zero;
            GetSignal((float)(now - _start), out var pos, out _); return pos;
        }

        public void GetSignal(float timeSinceSignalStart, out Vector3 pos, out Quaternion rot)
        {
            pos = _started && timeSinceSignalStart >= 0 && timeSinceSignalStart < SignalDuration
                ? _velocity * Shape.Evaluate(timeSinceSignalStart / SignalDuration) : Vector3.zero;
            rot = Quaternion.identity;
        }

        public void Clear() { _started = false; _velocity = Vector3.zero; SignalDuration = 0; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
