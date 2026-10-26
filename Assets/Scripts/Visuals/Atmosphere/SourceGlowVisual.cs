using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Scripts.Visuals.Atmosphere
{
    [Serializable]
    public struct SourceGlowSettings
    {
        public bool enabled;
        [Range(0, 2)] public float strength;
        [Range(0.1f, 3)] public float radius;
        [Range(0.25f, 3)] public float softness;
        public Color color;
        public Vector2 offset;
        public static SourceGlowSettings Flame => new SourceGlowSettings
        { enabled = true, strength = 0.32f, radius = 0.65f, softness = 1, color = Color.white };
        public static SourceGlowSettings Portal => new SourceGlowSettings
        { enabled = true, strength = 0.3f, radius = 1.3f, softness = 1.8f, color = new Color(0.34f, 0.56f, 1) };
    }

    // A small, source-owned additive halo, not a full-screen blur. Only the halo
    // samples the PixelLab density sprite; terrain, actors and HUD stay untouched.
    public sealed class SourceGlowVisual : IDisposable
    {
#if UNITY_EDITOR
        public static Func<GameObject, Camera, AtmosphereProfileSO> EditorPreviewProfile;
#endif
        private readonly Transform _source;
        private readonly Func<SourceGlowSettings> _settings;
        private readonly LocalLight2DSource _flame;
        private readonly SpriteRenderer _portal;
        private SpriteRenderer _halo;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private static readonly int Shape = Shader.PropertyToID("_GlowSoftness");
        public SpriteRenderer Renderer => _halo;

        public SourceGlowVisual(Transform source, Func<SourceGlowSettings> settings,
            LocalLight2DSource flame = null, SpriteRenderer portal = null)
        {
            _source = source; _settings = settings; _flame = flame; _portal = portal;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(source)) return;
#endif
            if (!source.gameObject.scene.IsValid()) return;
            var sprite = Resources.Load<Sprite>("Visuals/SourceGlow/Halo");
            var material = Resources.Load<Material>("Visuals/SourceGlow/Halo");
            if (sprite == null || material == null) return;
            var go = new GameObject("Source glow (preview)") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(go, source.gameObject.scene);
            go.transform.SetParent(source, false); go.layer = source.gameObject.layer;
            _halo = go.AddComponent<SpriteRenderer>(); _halo.sprite = sprite; _halo.sharedMaterial = material;
            // Stone can cover the glow; keep it behind characters and world labels.
            _halo.sortingLayerName = "Default"; _halo.sortingOrder = 2; _halo.enabled = false;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_halo == null || _source == null) return;
            _halo.enabled = false;
            if (WorldColorGrading.BindingsSuspended || !_source.gameObject.activeInHierarchy) return;
            AtmosphereController owner = null;
            if (camera.cameraType == CameraType.Game && camera.gameObject.scene == _source.gameObject.scene)
                camera.TryGetComponent(out owner);
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView && Camera.main != null && Camera.main.gameObject.scene == _source.gameObject.scene)
                owner = Camera.main.GetComponent<AtmosphereController>();
#endif
            var profile = owner != null && owner.isActiveAndEnabled ? owner.ActiveDepthMistProfile : null;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView) profile = EditorPreviewProfile?.Invoke(_source.gameObject, camera) ?? profile;
#endif
            if (profile == null || !profile.sourceGlow) return;
            var look = _settings();
            if (!look.enabled || look.strength <= 0 || profile.sourceGlowStrength <= 0) return;
            float motion = 1;
            Color tint = look.color;
            if (_flame != null)
            {
                motion = _flame.GlowMultiplier(profile);
                tint *= profile.localLightTint;
            }
            if (_portal != null && (!_portal.enabled || _portal.sprite == null)) return;
            if (_portal != null) tint *= _portal.color;
            if (motion <= 0) return;
            Vector3 position = _source.position;
            if (_portal != null) position = _portal.bounds.center;
            position += _source.TransformVector(look.offset);
            _halo.transform.position = position;
            _halo.transform.rotation = Quaternion.identity;
            float diameter = Mathf.Max(0.1f, look.radius) * Mathf.Clamp(profile.sourceGlowRadius, 0.25f, 3) * 2;
            // World-unit radius remains predictable under a scaled portal prefab.
            Vector3 scale = _source.lossyScale;
            _halo.transform.localScale = new Vector3(diameter / _halo.sprite.bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                diameter / _halo.sprite.bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1);
            _properties.SetFloat(Shape, Mathf.Clamp(look.softness * profile.sourceGlowSoftness, 0.25f, 5));
            _halo.SetPropertyBlock(_properties);
            tint.a = Mathf.Clamp01(look.strength * profile.sourceGlowStrength * motion * (_portal != null ? _portal.color.a : 1));
            _halo.color = tint;
            WorldColorGrading.Bind(_halo, RenderDepthCategory.Environment);
            _halo.enabled = true;
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        { if (_halo != null) _halo.enabled = false; }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (_halo == null) return;
            _halo.enabled = false;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_halo.gameObject);
            else UnityEngine.Object.DestroyImmediate(_halo.gameObject);
            _halo = null;
        }
    }
}
