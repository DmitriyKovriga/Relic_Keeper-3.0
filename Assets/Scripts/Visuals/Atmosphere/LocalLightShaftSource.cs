using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Visuals.Atmosphere
{
    // A source-anchored volume sprite with source-to-pixel visibility baked in the editor.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LocalLightShaftSource : MonoBehaviour
    {
        [Serializable]
        public struct Appearance
        {
            public Vector2 size;
            public float angle, opacity, softness, endFade, dust;
            public Color tint;
            public bool visible, followEnvironment;
            public float strength, dustOpacity, dustSpeed;
            public bool dustEnabled;
        }
#if UNITY_EDITOR
        public static Func<LocalLightShaftSource, Camera, AtmosphereProfileSO> EditorPreviewProfile;
#endif
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Vector2Int _sourcePixel = new Vector2Int(10, 14);
        [SerializeField] private float[] _clearPixels = Array.Empty<float>();
        [SerializeField, Range(0, 1)] private float _opacity = 0.14f;
        [SerializeField] private Vector2 _size = Vector2.one;
        [SerializeField, Range(-180, 180)] private float _angle;
        [SerializeField] private Color _tint = Color.white;
        [SerializeField, Range(0.5f, 3)] private float _softness = 1;
        [SerializeField, Range(0.05f, 0.8f)] private float _endFade = 0.2f;
        [SerializeField, Range(0, 1)] private float _dust = 1;
        [SerializeField] private bool _visible = true;
        [SerializeField] private bool _followEnvironment = true;
        [SerializeField, Range(0, 4)] private float _intensity = 1;
        [SerializeField] private bool _dustEnabled = true;
        [SerializeField, Range(0, 1)] private float _dustOpacity = 1;
        [SerializeField, Range(0, 3)] private float _dustSpeed = 1;
        [SerializeField, HideInInspector] private byte[] _visibility = Array.Empty<byte>();
        private Texture2D _visibilityTexture;
        private bool _maskDirty = true;
        private SpriteRenderer _beam;
        private MaterialPropertyBlock _properties;
        private bool _configured;
        private bool _rebuild = true;
        public Sprite Artwork => _sprite;
        public Vector2Int SourcePixel => _sourcePixel;
        public float[] ClearPixels => _clearPixels;
        public Vector2 Size => _size;
        public float Angle => _angle;
        public float DustAmount => _dust;
        public float DustOpacity => _dustOpacity;
        public float DustSpeed => _dustSpeed;
        public byte[] Visibility => _visibility;
        public Vector3 EndPosition => transform.TransformPoint(PixelToLocal(new Vector2(44, 145)));
        public Appearance ReadAppearance() => new Appearance { size = _size, angle = _angle, opacity = _opacity,
            softness = _softness, endFade = _endFade, dust = _dust, tint = _tint,
            visible = _visible, followEnvironment = _followEnvironment, strength = _intensity,
            dustEnabled = _dustEnabled, dustOpacity = _dustOpacity, dustSpeed = _dustSpeed };
        public void ApplyAppearance(Appearance value)
        {
            SetShape(value.size, value.angle); _opacity = Mathf.Clamp01(value.opacity); _tint = value.tint; _tint.a = 1;
            _softness = Mathf.Clamp(value.softness, 0.5f, 3); _endFade = Mathf.Clamp(value.endFade, 0.05f, 0.8f); _dust = Mathf.Clamp01(value.dust);
            _visible = value.visible; _followEnvironment = value.followEnvironment;
            _intensity = Mathf.Clamp(value.strength, 0, 4); _dustEnabled = value.dustEnabled;
            _dustOpacity = Mathf.Clamp01(value.dustOpacity); _dustSpeed = Mathf.Clamp(value.dustSpeed, 0, 3);
        }
        public float Strength(AtmosphereProfileSO profile) => !_visible || profile == null || !profile.localShafts
            ? 0 : _followEnvironment ? profile.localShaftStrength : 1;
        public float DustDensity(AtmosphereProfileSO profile) => !_dustEnabled || Strength(profile) <= 0 ? 0
            : _dust * (_followEnvironment ? profile.localShaftDust : 1);
        public static Vector2 Axis(Appearance value) => new Vector2(34 * value.size.x, -131 * value.size.y) / 24;
        public static float Radius(Appearance value) => Axis(value).magnitude;
        public static float Rotation(Appearance value) => Mathf.DeltaAngle(0, value.angle + Mathf.Atan2(Axis(value).x, -Axis(value).y) * Mathf.Rad2Deg);
        public static Appearance Aim(Appearance value, float radius, float rotation)
        {
            value.size *= Mathf.Clamp(radius, 0.25f, 30) / Mathf.Max(0.001f, Radius(value));
            value.angle = Mathf.DeltaAngle(0, rotation - Mathf.Atan2(Axis(value).x, -Axis(value).y) * Mathf.Rad2Deg);
            return value;
        }
        private Vector3 Anchor => new Vector3(_sourcePixel.x / 24f, -_sourcePixel.y / 24f);
        public Vector3 SourcePosition => transform.TransformPoint(new Vector3(_sourcePixel.x / 24f, -_sourcePixel.y / 24f));
        public Vector3 PixelToLocal(Vector2 pixel) => Anchor + Quaternion.Euler(0, 0, _angle) *
            Vector3.Scale(new Vector3(pixel.x / 24f, -pixel.y / 24f) - Anchor, new Vector3(_size.x, _size.y, 1));
        private Vector2 LocalToPixel(Vector3 point)
        {
            var p = Quaternion.Euler(0, 0, -_angle) * (point - Anchor);
            return new Vector2((p.x / _size.x + Anchor.x) * 24, -(p.y / _size.y + Anchor.y) * 24);
        }
        public void SetShape(Vector2 size, float angle)
        { _size = new Vector2(Mathf.Clamp(size.x, 0.01f, 32), Mathf.Clamp(size.y, 0.01f, 32)); _angle = Mathf.DeltaAngle(0, angle); _rebuild = true; }
        public void UsePreset(bool soft)
        {
            SetShape(soft ? new Vector2(2.5f, 0.85f) : Vector2.one, 0);
            _opacity = soft ? 0.08f : 0.14f; _softness = soft ? 2 : 1; _endFade = soft ? 0.5f : 0.2f;
            _tint = Color.white; _dust = soft ? 0.45f : 1;
        }
        public void Configure(Sprite sprite, Vector2Int sourcePixel, float[] clearPixels)
        { _sprite = sprite; _sourcePixel = sourcePixel; _clearPixels = (float[])clearPixels.Clone(); _visibility = Array.Empty<byte>(); _maskDirty = true; _rebuild = true; Build(); }
        public void ConfigureVisibility(byte[] visibility)
        { _visibility = (byte[])visibility.Clone(); _maskDirty = true; _rebuild = true; Build(); }
        private void OnValidate() { SetShape(_size, _angle); _tint.a = 1; _maskDirty = true; }
        private void OnEnable()
        {
            Build(); Restore();
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Restore(); Release(); ReleaseMask();
        }
        private void Release()
        {
            if (_beam != null) _beam.SetPropertyBlock(null);
            _configured = false;
        }
        private void ReleaseMask()
        {
            if (_visibilityTexture != null)
            {
                if (Application.isPlaying) Destroy(_visibilityTexture); else DestroyImmediate(_visibilityTexture);
            }
            _visibilityTexture = null; _maskDirty = true;
        }
        private void Build()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(this)) return;
#endif
            if (_beam == null) _beam = transform.Find("Beam")?.GetComponent<SpriteRenderer>();
            if (_beam == null || _sprite == null || (!_rebuild && _configured)) return;
            Release();
            int columns = Mathf.RoundToInt(_sprite.rect.width);
            if (_clearPixels == null || _clearPixels.Length != 64 || columns != 64) return;
            _beam.sprite = _sprite;
            _beam.transform.localPosition = PixelToLocal(new Vector2(_sprite.pivot.x, _sprite.rect.height - _sprite.pivot.y));
            _beam.transform.localRotation = Quaternion.Euler(0, 0, _angle);
            _beam.transform.localScale = new Vector3(_size.x, _size.y, 1);
            if (_properties == null) _properties = new MaterialPropertyBlock();
            bool masked = _visibility != null && _visibility.Length == columns * (int)_sprite.rect.height;
            if (masked && (_visibilityTexture == null || _maskDirty))
            {
                ReleaseMask();
                _visibilityTexture = new Texture2D(columns, (int)_sprite.rect.height, TextureFormat.R8, false, true)
                    { name = "Shaft visibility (baked)", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _visibilityTexture.LoadRawTextureData(_visibility); _visibilityTexture.Apply(false, true); _maskDirty = false;
            }
            _beam.GetPropertyBlock(_properties);
            _properties.SetFloatArray("_ShaftClearPixels", _clearPixels);
            _properties.SetVector("_ShaftImageSize", new Vector4(columns, _sprite.rect.height, masked ? 1 : 0, _intensity));
            if (masked) _properties.SetTexture("_ShaftVisibility", _visibilityTexture);
            _properties.SetVector("_ShaftShape", new Vector4(_softness, _endFade, 0, 0));
            _beam.SetPropertyBlock(_properties); _rebuild = false; _configured = true;
            WorldColorGrading.Bind(_beam, RenderDepthCategory.Background);
        }
#if UNITY_EDITOR
        // Prefabs store artwork and visibility data. GPU resources are restored when rendered.
        public void PrepareForSave() { Restore(); Release(); ReleaseMask(); _rebuild = true; }
#endif
        public Vector3 DustPosition(Vector2 motion)
        {
            float t = Mathf.Clamp01(motion.y + 0.5f);
            float row = Mathf.Lerp(_sourcePixel.y + 3, 145, t);
            float center = Mathf.Lerp(_sourcePixel.x, 44, t);
            float x = center + Mathf.Clamp(motion.x / 0.45f, -1, 1) * Mathf.Lerp(0.5f, 8, t);
            return PixelToLocal(new Vector2(Mathf.Round(x), Mathf.Round(row)));
        }
        public bool Contains(Vector3 localPoint)
        {
            if (_sprite == null || _clearPixels == null || _clearPixels.Length == 0) return false;
            var pixel = LocalToPixel(localPoint);
            int x = Mathf.FloorToInt(pixel.x); float row = pixel.y;
            if (_visibility != null && _visibility.Length == (int)_sprite.rect.width * (int)_sprite.rect.height)
                return x >= 0 && x < _sprite.rect.width && row >= 0 && row < _sprite.rect.height &&
                    _visibility[((int)_sprite.rect.height - 1 - Mathf.FloorToInt(row)) * (int)_sprite.rect.width + x] > 0;
            return x >= 0 && x < _clearPixels.Length && row >= _sourcePixel.y && row < _clearPixels[x] - 1;
        }
        public float ParticleIllumination(Vector3 localPoint) => Contains(localPoint)
            ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.02f, _endFade, 1 - LocalToPixel(localPoint).y / _sprite.rect.height)) : 0;
        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Build(); Restore();
            AtmosphereController owner = null;
            if (camera.cameraType == CameraType.Game && camera.gameObject.scene == gameObject.scene) camera.TryGetComponent(out owner);
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView && Camera.main != null && Camera.main.gameObject.scene == gameObject.scene)
                owner = Camera.main.GetComponent<AtmosphereController>();
#endif
            if (WorldColorGrading.BindingsSuspended || !_configured) return;
            var profile = owner != null && owner.isActiveAndEnabled ? owner.ActiveDepthMistProfile : null;
#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView) profile = EditorPreviewProfile?.Invoke(this, camera) ?? profile;
#endif
            float strength = Strength(profile);
            if (strength <= 0 || _intensity <= 0 || _opacity <= 0) return;
            _beam.color = new Color(_tint.r, _tint.g, _tint.b, Mathf.Clamp01(_opacity * strength)); _beam.enabled = true;
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();
        private void Restore() { if (_beam != null) { _beam.enabled = false; _beam.color = Color.white; } }
    }
}
