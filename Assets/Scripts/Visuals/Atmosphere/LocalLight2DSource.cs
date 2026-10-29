using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Scripts.Visuals.Atmosphere
{
    // A source-owned URP light, not an overlay glow. Slot 1 adds illumination to
    // compatible surface shaders while preserving their baseline outside the radius.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Light2D))]
    public sealed class LocalLight2DSource : MonoBehaviour
    {
        internal static readonly System.Collections.Generic.HashSet<LocalLight2DSource> ActiveSources = new System.Collections.Generic.HashSet<LocalLight2DSource>();
        public bool CastsPlayerShadow(AtmosphereProfileSO profile) => _strength > 0 && IsAvailable(profile) && ProfileStrength(profile) > 0;
        public float ShadowRadius(AtmosphereProfileSO profile) => _radius * ProfileRadius(profile) * FlameSample.y;
        [SerializeField, Min(0f)] private float _strength = 0.18f;
        [SerializeField, Min(0.1f)] private float _radius = 3.5f;
        [SerializeField] private bool _flame;
        [SerializeField] private float _flamePhase;
        [SerializeField, Range(0, .75f)] private float _breathDepth;
        [SerializeField, Range(.1f, 3f)] private float _breathSpeed = 1;
        [SerializeField] private bool _roomFill;
        [SerializeField] private Color _tint = new Color(0.63f, 0.78f, 1);
        [SerializeField] private bool _overrideTint;
        [SerializeField, Range(0, 0.8f)] private float _innerRadius = 0.1f;
        [SerializeField] private SourceGlowSettings _glow = SourceGlowSettings.Flame;
        private SourceGlowVisual _glowVisual;
        private float _placementPhase;
        private Light2D _light;
        private AtmosphereController _previewOwner;
        public Light2D Light => _light != null ? _light : (_light = GetComponent<Light2D>());
        public void Configure(float strength, float radius) { _strength = strength; _radius = radius; Prepare(); }
        public void SetStrength(float strength) => _strength = Mathf.Max(0f, strength);
        public void ConfigureTint(Color tint) { _tint = tint; _overrideTint = true; }
        public Color SourceTint(AtmosphereProfileSO profile) => _roomFill || _overrideTint ? _tint : profile.localLightTint;
        public void ConfigureFlame(float phase) { _flame = true; _flamePhase = phase; SetPlacementPhase(); EnsureGlow(); }
        public void ConfigureBreathing(float depth, float speed = 1) { _breathDepth = Mathf.Clamp(depth, 0, .75f); _breathSpeed = Mathf.Clamp(speed, .1f, 3); }
        public SourceGlowSettings Glow => _glow;
        public void ConfigureGlow(SourceGlowSettings settings) { _glow = settings; EnsureGlow(); }
        public float GlowMultiplier(AtmosphereProfileSO profile) => isActiveAndEnabled && HasFlameMotion && Light.enabled &&
            _strength > 0 && IsAvailable(profile) ? Mathf.Max(0, ProfileStrength(profile)) * FlameSample.x : 0;
        private void EnsureGlow()
        {
            if (!_flame || !isActiveAndEnabled || _glowVisual?.Renderer != null) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            _glowVisual?.Dispose(); _glowVisual = new SourceGlowVisual(transform, () => _glow, this);
        }
        public bool IsRoomFill => _roomFill;
        public float Radius => _radius;
        public void ConfigureFill(float strength, float radius, Color tint, float innerRadius = 0.1f)
        {
            _glowVisual?.Dispose(); _glowVisual = null;
            _roomFill = true; _flame = false; _strength = Mathf.Max(0, strength); _radius = Mathf.Max(0.1f, radius);
            _tint = tint; _innerRadius = Mathf.Clamp(innerRadius, 0, 0.8f); Prepare();
        }
        public bool IsAvailable(AtmosphereProfileSO profile) => profile != null && (_roomFill ? profile.roomFillLights : profile.localLights);
        private float ProfileStrength(AtmosphereProfileSO profile) => _roomFill ? profile.roomFillStrength : profile.localLightStrength;
        private float ProfileRadius(AtmosphereProfileSO profile) => _roomFill ? profile.roomFillRadius : profile.localLightRadius;
        private void SetPlacementPhase() => _placementPhase = Mathf.Repeat(transform.position.x * 0.173f + transform.position.y * 0.347f, 37f);
        public bool HasFlameMotion => _flame;
        // Particle visibility must not depend on transient Light.intensity: it is
        // deliberately reset between cameras. Use the source and profile instead.
        public float ParticleIllumination(AtmosphereProfileSO profile, Vector3 world)
        {
            if (!isActiveAndEnabled || !Light.enabled || _strength <= 0 || !IsAvailable(profile)) return 0;
            float radius = _radius * ProfileRadius(profile) * FlameSample.y;
            float edge = Mathf.Clamp01(1f - Vector2.Distance(world, transform.position) / Mathf.Max(0.1f, radius));
            return Mathf.Clamp01(ProfileStrength(profile)) * Mathf.SmoothStep(0, 1, edge);
        }
        public static Vector2 SampleFlame(float seconds, float phase)
        {
            float slow = Mathf.Clamp01(Mathf.PerlinNoise(phase, seconds * 0.85f)) * 2f - 1f;
            float flutter = Mathf.Clamp01(Mathf.PerlinNoise(phase + 53.17f, seconds * 3.1f)) * 2f - 1f;
            return new Vector2(1f + (slow * 0.8f + flutter * 0.2f) * 0.12f, 1f + slow * 0.025f);
        }
        public static Vector2 SampleBreathing(float seconds, float phase, float depth, float speed = 1)
        {
            float t = seconds * Mathf.Clamp(speed, .1f, 3);
            float wave = Mathf.Sin(t * 2.4f + phase) * .7f + Mathf.Sin(t * 3.7f + phase * .73f) * .3f;
            float amount = wave * Mathf.Clamp(depth, 0, .75f);
            return new Vector2(1 + amount, 1 + amount * .15f);
        }
        private Vector2 FlameSample
        {
            get
            {
                if (!_flame) return Vector2.one;
                // Render requests can continue while the game loop is unfocused
                // or paused. Flame motion belongs to the presentation clock.
                float seconds = Time.realtimeSinceStartup;
#if UNITY_EDITOR
                if (!Application.isPlaying) seconds = (float)UnityEditor.EditorApplication.timeSinceStartup;
#endif
                float phase = _flamePhase + _placementPhase;
                return Vector2.Scale(SampleFlame(seconds, phase), SampleBreathing(seconds, phase, _breathDepth, _breathSpeed));
            }
        }
        public void BindPreview(AtmosphereController owner) { _previewOwner = owner; Prepare(); EnsureGlow(); }
        private void OnEnable()
        {
            ActiveSources.Add(this);
            SetPlacementPhase();
            Prepare();
            EnsureGlow();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            ActiveSources.Remove(this);
            _glowVisual?.Dispose(); _glowVisual = null;
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            // Light2D owns its reload/teardown registration. Disabling it here can
            // deregister it twice during assembly reload; zero intensity is enough.
            if (_light != null) _light.intensity = 0;
        }
        private void Prepare()
        {
            Light.lightType = Light2D.LightType.Point; Light.blendStyleIndex = 1;
            Light.shadowsEnabled = true; Light.shadowIntensity = 1; Light.shadowSoftness = 0;
            Light.intensity = 0; Light.enabled = isActiveAndEnabled;
            Light.pointLightInnerAngle = 360; Light.pointLightOuterAngle = 360;
            Light.pointLightOuterRadius = _radius; Light.pointLightInnerRadius = _roomFill ? _radius * _innerRadius : 0.35f;
            Light.targetSortingLayers = new[] { SortingLayer.NameToID("Background"), SortingLayer.NameToID("Default"),
                SortingLayer.NameToID("World"), SortingLayer.NameToID("Hero") };
        }
        private void LateUpdate()
        {
            EnsureGlow();
            var owner = _previewOwner != null ? _previewOwner : Camera.main?.GetComponent<AtmosphereController>();
            var profile = owner != null && owner.isActiveAndEnabled ? owner.ActiveDepthMistProfile : null;
            UpdateRadius(profile, FlameSample.y);
            Light.intensity = 0; // Keep its mesh/bounds updated, but illuminate only its own camera below.
        }
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Light.intensity = 0;
            AtmosphereController owner = null;
            if (camera.cameraType == CameraType.Game && camera.gameObject.scene == gameObject.scene)
                camera.TryGetComponent(out owner);
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView && Camera.main != null && Camera.main.gameObject.scene == gameObject.scene)
                owner = Camera.main.GetComponent<AtmosphereController>();
#endif
            var profile = owner != null && owner.isActiveAndEnabled ? owner.ActiveDepthMistProfile : null;
            if (WorldColorGrading.BindingsSuspended || !IsAvailable(profile)) return;
            Vector2 flame = FlameSample;
            UpdateRadius(profile, flame.y);
#if UNITY_EDITOR
            // Isolated preview scenes have no ordinary player loop. Flush Unity's
            // own Light2D mesh/bounds update before their synchronous render request.
            // Saved scenes and player builds use the normal LateUpdate instead.
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene))
                Light.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
#endif
            Light.color = SourceTint(profile);
            Light.intensity = _strength * ProfileStrength(profile) * flame.x;
        }
        private void UpdateRadius(AtmosphereProfileSO profile, float motion)
        {
            Light.pointLightOuterRadius = _radius * (profile != null ? ProfileRadius(profile) : 1f) * motion;
            Light.pointLightInnerRadius = _roomFill ? Light.pointLightOuterRadius * _innerRadius : 0.35f;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) { if (_light != null) _light.intensity = 0; }
    }
}
