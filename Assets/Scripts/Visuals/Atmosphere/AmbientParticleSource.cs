using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Visuals.Atmosphere
{
    public enum AmbientParticleKind { ForgeEmbers, DriftingLeaves, TorchDust }

    // Fixed renderer slots, deterministic motion, no physics or per-frame spawning.
    // Bounds describe a hand-placed clear pocket of air, not the camera viewport.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AmbientParticleSource : MonoBehaviour
    {
        [SerializeField] private AmbientParticleKind _kind;
        [SerializeField] private Sprite[] _frames;
        [SerializeField] private Vector2 _extent = new Vector2(0.5f, 1.3f);
        [SerializeField] private int _seed = 17;
        private SpriteRenderer[] _slots;
        private LocalLight2DSource _torch;
        public AmbientParticleKind Kind => _kind;
        public Sprite[] Frames => _frames;
        public Vector2 Extent => _extent;
        public float PreviewTime { get; set; } = -1;
        public static int Capacity(AmbientParticleKind kind) => kind == AmbientParticleKind.DriftingLeaves ? 1 : kind == AmbientParticleKind.ForgeEmbers ? 3 : 4;
        public readonly struct State
        {
            public readonly bool visible;
            public readonly Vector2 offset;
            public readonly float opacity;
            public readonly int frame;
            public State(Vector2 offset, float opacity, int frame)
            { visible = opacity > 0; this.offset = offset; this.opacity = opacity; this.frame = frame; }
        }

        public void Configure(AmbientParticleKind kind, Sprite[] frames, Vector2 extent, int seed)
        { _kind = kind; _frames = frames; _extent = extent; _seed = seed; FindSlots(); Restore(); }
        private void FindSlots()
        {
            if (_slots == null) _slots = GetComponentsInChildren<SpriteRenderer>(true);
            if (_kind == AmbientParticleKind.TorchDust && _torch == null) _torch = GetComponentInParent<LocalLight2DSource>();
        }
        private void OnEnable()
        {
            FindSlots(); Restore();
            foreach (var slot in _slots) WorldColorGrading.Bind(slot, RenderDepthCategory.Environment);
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
        private static float Hash(int seed, int slot, int cycle)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)(slot * 374761393) ^ (uint)(cycle * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 65535) / 65535f;
            }
        }
        public static State Sample(AmbientParticleKind kind, float seconds, int slot, int seed, Vector2 extent, float density)
        {
            density = Mathf.Clamp01(density);
            int capacity = Capacity(kind);
            if (density <= 0 || slot < 0 || slot >= Mathf.CeilToInt(capacity * density)) return default;
            float variation = Hash(seed, slot, 0);
            float life = kind == AmbientParticleKind.DriftingLeaves ? 3.8f : kind == AmbientParticleKind.ForgeEmbers ? 2.2f : 6.5f;
            float quiet = kind == AmbientParticleKind.DriftingLeaves ? 6f / density : kind == AmbientParticleKind.ForgeEmbers ? 0.7f / density : 0;
            float period = life + quiet;
            float clock = Mathf.Max(0, seconds) + variation * period + slot * period / capacity;
            int cycle = Mathf.FloorToInt(clock / period);
            float age = Mathf.Repeat(clock, period);
            if (age >= life) return default;
            float t = age / life, phase = Hash(seed, slot, cycle) * Mathf.PI * 2;
            float width = Mathf.Max(0, extent.x), height = Mathf.Max(0, extent.y);
            Vector2 offset;
            if (kind == AmbientParticleKind.DriftingLeaves)
                offset = new Vector2(Mathf.Sin(t * 6 + phase) * width * 0.38f, -t * height);
            else if (kind == AmbientParticleKind.ForgeEmbers)
                offset = new Vector2(Mathf.Sin(t * 4 + phase) * width * 0.45f, t * height);
            else
                offset = new Vector2(Mathf.Sin(t * 2 + phase) * width * 0.45f, Mathf.Cos(t * 2.5f + phase) * height * 0.45f);
            offset = new Vector2(Mathf.Round(offset.x * 24) / 24, Mathf.Round(offset.y * 24) / 24);
            float opacity = Mathf.SmoothStep(0, 1, Mathf.Min(t * 8, (1 - t) * 5));
            opacity *= kind == AmbientParticleKind.TorchDust ? 0.5f : 0.9f;
            return new State(offset, opacity, Mathf.FloorToInt(age * 8));
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            FindSlots(); Restore();
            AtmosphereController owner = null;
            if (camera.cameraType == CameraType.Game && camera.gameObject.scene == gameObject.scene) camera.TryGetComponent(out owner);
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView && Camera.main != null && Camera.main.gameObject.scene == gameObject.scene)
                owner = Camera.main.GetComponent<AtmosphereController>();
#endif
            if (WorldColorGrading.BindingsSuspended || owner == null || !owner.isActiveAndEnabled || _frames == null || _frames.Length == 0) return;
            var profile = owner.ActiveDepthMistProfile;
            if (profile == null || !profile.ambientParticles || profile.ambientParticleOpacity <= 0) return;
            float seconds = Time.realtimeSinceStartup;
#if UNITY_EDITOR
            if (!Application.isPlaying) seconds = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
            if (PreviewTime >= 0) seconds = PreviewTime;
            for (int i = 0; i < _slots.Length && i < Capacity(_kind); i++)
            {
                var state = Sample(_kind, seconds, i, _seed, _extent, profile.ambientParticleDensity);
                if (!state.visible) continue;
                float illumination = _kind != AmbientParticleKind.TorchDust ? 1 :
                    _torch != null ? _torch.ParticleIllumination(profile, transform.TransformPoint(state.offset)) : 0;
                float opacity = state.opacity * profile.ambientParticleOpacity * illumination;
                if (opacity <= 0) continue;
                var slot = _slots[i]; slot.sprite = _frames[state.frame % _frames.Length];
                slot.transform.localPosition = state.offset;
                slot.color = new Color(1, 1, 1, opacity); slot.enabled = true;
            }
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();
        private void Restore()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
            {
                if (slot == null) continue;
                slot.enabled = false; slot.transform.localPosition = Vector3.zero; slot.color = Color.white;
                if (_frames != null && _frames.Length > 0) slot.sprite = _frames[0];
            }
        }
    }
}
