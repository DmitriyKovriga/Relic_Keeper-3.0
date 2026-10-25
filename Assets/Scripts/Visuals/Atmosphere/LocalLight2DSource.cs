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
        [SerializeField, Min(0f)] private float _strength = 0.18f;
        [SerializeField, Min(0.1f)] private float _radius = 3.5f;
        [SerializeField] private bool _flame;
        [SerializeField] private float _flamePhase;
        private float _placementPhase;
        private Light2D _light;
        private AtmosphereController _previewOwner;
        public Light2D Light => _light != null ? _light : (_light = GetComponent<Light2D>());
        public void Configure(float strength, float radius) { _strength = strength; _radius = radius; Prepare(); }
        public void ConfigureFlame(float phase) { _flame = true; _flamePhase = phase; SetPlacementPhase(); }
        private void SetPlacementPhase() => _placementPhase = Mathf.Repeat(transform.position.x * 0.173f + transform.position.y * 0.347f, 37f);
        public bool HasFlameMotion => _flame;
        // Particle visibility must not depend on transient Light.intensity: it is
        // deliberately reset between cameras. Use the source and profile instead.
        public float ParticleIllumination(AtmosphereProfileSO profile, Vector3 world)
        {
            if (!isActiveAndEnabled || !Light.enabled || _strength <= 0 || profile == null || !profile.localLights) return 0;
            float radius = _radius * profile.localLightRadius * FlameSample.y;
            float edge = Mathf.Clamp01(1f - Vector2.Distance(world, transform.position) / Mathf.Max(0.1f, radius));
            return Mathf.Clamp01(profile.localLightStrength) * Mathf.SmoothStep(0, 1, edge);
        }
        public static Vector2 SampleFlame(float seconds, float phase)
        {
            float slow = Mathf.Clamp01(Mathf.PerlinNoise(phase, seconds * 0.85f)) * 2f - 1f;
            float flutter = Mathf.Clamp01(Mathf.PerlinNoise(phase + 53.17f, seconds * 3.1f)) * 2f - 1f;
            return new Vector2(1f + (slow * 0.8f + flutter * 0.2f) * 0.12f, 1f + slow * 0.025f);
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
                return SampleFlame(seconds, _flamePhase + _placementPhase);
            }
        }
        public void BindPreview(AtmosphereController owner) { _previewOwner = owner; Prepare(); }
        private void OnEnable()
        {
            SetPlacementPhase();
            Prepare();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
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
            Light.pointLightOuterRadius = _radius; Light.pointLightInnerRadius = 0.35f;
            Light.targetSortingLayers = new[] { SortingLayer.NameToID("Background"), SortingLayer.NameToID("Default"),
                SortingLayer.NameToID("World"), SortingLayer.NameToID("Hero") };
        }
        private void LateUpdate()
        {
            var owner = _previewOwner != null ? _previewOwner : Camera.main?.GetComponent<AtmosphereController>();
            var profile = owner != null && owner.isActiveAndEnabled ? owner.ActiveDepthMistProfile : null;
            Light.pointLightOuterRadius = _radius * (profile != null ? profile.localLightRadius : 1f) * FlameSample.y;
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
            if (WorldColorGrading.BindingsSuspended || profile == null || !profile.localLights) return;
            Vector2 flame = FlameSample;
            Light.pointLightOuterRadius = _radius * profile.localLightRadius * flame.y;
#if UNITY_EDITOR
            // Isolated preview scenes have no ordinary player loop. Flush Unity's
            // own Light2D mesh/bounds update before their synchronous render request.
            // Saved scenes and player builds use the normal LateUpdate instead.
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene))
                Light.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
#endif
            Light.color = profile.localLightTint;
            Light.intensity = _strength * profile.localLightStrength * flame.x;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) { if (_light != null) _light.intensity = 0; }
    }
}
