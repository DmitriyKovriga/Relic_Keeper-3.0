using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Visuals.Atmosphere
{
    public enum EnvironmentAnimationKind { ChimneySmoke, WaterDrip }

    // One persistent renderer per source: no particle spawning, gameplay physics,
    // material instances, or changes to the emitter's anchored transform.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class EnvironmentAnimation : MonoBehaviour
    {
        [SerializeField] private EnvironmentAnimationKind _kind;
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private float _phase;
        [SerializeField, Min(0.1f)] private float _fallDistance = 3f;
        private SpriteRenderer _visual;
        public EnvironmentAnimationKind Kind => _kind;
        public Sprite[] Frames => _frames;
        public float FallDistance => _fallDistance;
        public float PreviewTime { get; set; } = -1f;

        public readonly struct State
        {
            public readonly bool visible;
            public readonly int frame;
            public readonly float offsetY, opacity;
            public State(bool visible, int frame, float offsetY, float opacity)
            { this.visible = visible; this.frame = frame; this.offsetY = offsetY; this.opacity = opacity; }
        }
        public void Configure(EnvironmentAnimationKind kind, Sprite[] frames, float phase, float fallDistance = 3f)
        { _kind = kind; _frames = frames; _phase = phase; _fallDistance = fallDistance; FindVisual(); Restore(); }
        private void FindVisual() { if (_visual == null) _visual = transform.Find("Visual")?.GetComponent<SpriteRenderer>(); }
        private void OnEnable()
        {
            FindVisual(); Restore();
            if (_visual != null) WorldColorGrading.Bind(_visual, RenderDepthCategory.Environment);
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Restore();
        }
        public static State SampleDrip(float seconds, float phase, float distance, float frequency)
        {
            if (frequency <= 0) return default;
            distance = Mathf.Max(0.1f, distance);
            float quiet = 6f / frequency, fall = Mathf.Sqrt(2f * distance / 9f);
            float time = Mathf.Repeat(seconds + phase, quiet + 0.25f + fall + 0.3f);
            if (time < quiet) return default;
            time -= quiet;
            if (time < 0.25f) return new State(true, Mathf.Min(1, (int)(time * 8)), 0, 0.8f);
            time -= 0.25f;
            if (time < fall) return new State(true, 2 + ((int)(time * 12) & 1), -Mathf.Min(distance, 4.5f * time * time), 0.8f);
            return new State(true, 4 + Mathf.Clamp((int)((time - fall) / 0.075f), 0, 3), -distance, 0.8f);
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            FindVisual(); if (_visual == null) return;
            Restore();
            AtmosphereController owner = null;
            if (camera.cameraType == CameraType.Game && camera.gameObject.scene == gameObject.scene) camera.TryGetComponent(out owner);
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView && Camera.main != null && Camera.main.gameObject.scene == gameObject.scene)
                owner = Camera.main.GetComponent<AtmosphereController>();
#endif
            if (WorldColorGrading.BindingsSuspended || owner == null || !owner.isActiveAndEnabled || _frames == null || _frames.Length == 0) return;
            var profile = owner.ActiveDepthMistProfile;
            if (profile == null || !profile.ambientAnimations) return;
            float seconds = Time.realtimeSinceStartup;
#if UNITY_EDITOR
            if (!Application.isPlaying) seconds = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            if (PreviewTime >= 0) seconds = PreviewTime;
            State state = _kind == EnvironmentAnimationKind.ChimneySmoke
                ? new State(profile.chimneySmokeOpacity > 0, (int)Mathf.Repeat((seconds + _phase) * 8f, _frames.Length), 0, profile.chimneySmokeOpacity)
                : SampleDrip(seconds, _phase, _fallDistance, profile.dripFrequency);
            if (!state.visible) return;
            _visual.sprite = _frames[Mathf.Clamp(state.frame, 0, _frames.Length - 1)];
            _visual.transform.localPosition = Vector3.up * (Mathf.Round(state.offsetY * 24f) / 24f);
            _visual.color = new Color(1, 1, 1, state.opacity);
            _visual.enabled = true;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();
        private void Restore()
        {
            if (_visual == null) return;
            _visual.enabled = false; _visual.transform.localPosition = Vector3.zero; _visual.color = Color.white;
            if (_frames != null && _frames.Length > 0) _visual.sprite = _frames[0];
        }
    }
}
