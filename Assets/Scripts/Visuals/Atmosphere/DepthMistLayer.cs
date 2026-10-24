using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Scripts.Visuals.Atmosphere
{
    // A camera-owned, sorted background plane. Geometry follows the viewport;
    // noise and the optional vertical belt remain anchored in world/room coordinates.
    internal sealed class DepthMistLayer : IDisposable
    {
        internal const string ObjectName = "DepthMist (temporary)";
        private readonly AtmosphereController _owner;
        private readonly Camera _camera;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private GameObject _object;
        private MeshRenderer _renderer;
        private Mesh _mesh;
        private Material _material;
        private static readonly int ColorId = Shader.PropertyToID("_MistColor");
        private static readonly int ParametersId = Shader.PropertyToID("_MistParameters");
        private static readonly int DriftId = Shader.PropertyToID("_MistDrift");
        private static readonly int BandId = Shader.PropertyToID("_MistBand");
        private static readonly int OriginId = Shader.PropertyToID("_MistOrigin");

        internal DepthMistLayer(AtmosphereController owner)
        {
            _owner = owner; _camera = owner.GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_renderer != null) _renderer.enabled = false;
            var profile = _owner.ActiveDepthMistProfile;
            if (camera != _camera || !_owner.isActiveAndEnabled || !camera.orthographic ||
                camera.cameraType != CameraType.Game || profile == null || !profile.HasDepthLayer ||
                WorldColorGrading.BindingsSuspended || Mathf.Abs(camera.transform.forward.z) < 0.99f) return;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base) return;
            if (!EnsurePlane()) return;

            float ppu = Mathf.Max(1f, profile.pixelsPerUnit);
            float height = camera.orthographicSize * 2f + 4f / ppu;
            Vector2 center = AtmosphereProfileSO.SnapToPixel(camera.transform.position, ppu);
            _object.transform.SetPositionAndRotation(new Vector3(center.x, center.y, _owner.WorldPlaneZ), Quaternion.identity);
            _object.transform.localScale = Vector3.one;
            // Compensate parent scale without changing the hierarchy every render.
            Vector3 parentScale = _object.transform.lossyScale;
            _object.transform.localScale = new Vector3(height * camera.aspect / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                height / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
            _object.layer = _camera.gameObject.layer;
            _renderer.sortingLayerName = WorldRenderSorting.LayerBackground;
            _renderer.sortingOrder = 7; // Hub Fog=5, Forest=6, ground=8; dungeon terrain=Default.
            Color tint = QualitySettings.activeColorSpace == ColorSpace.Linear ? profile.backgroundTint.linear : profile.backgroundTint;
            _properties.SetColor(ColorId, tint);
            _properties.SetVector(ParametersId, new Vector4(Mathf.Clamp01(profile.backgroundMist), Mathf.Clamp01(profile.shafts),
                Mathf.Max(0.001f, profile.noiseScale), (float)profile.quality));
            Vector2 drift = profile.driftSpeed * _owner.DepthMistTime;
            _properties.SetVector(DriftId, new Vector4(drift.x, drift.y, ppu, Mathf.Clamp01(profile.intensity)));
            _properties.SetVector(BandId, new Vector4(_owner.DepthMistAnchor.y + profile.depthMistCenterY,
                Mathf.Max(0.05f, profile.depthMistHeight * 0.5f), Mathf.Max(0.01f, profile.depthMistFeather), Mathf.Clamp01(profile.wispStrength)));
            _properties.SetVector(OriginId, new Vector4(_owner.DepthMistAnchor.x, _owner.DepthMistAnchor.y,
                profile.limitDepthMistHeight ? 1f : 0f, 0f));
            _renderer.SetPropertyBlock(_properties);
            _renderer.enabled = true;
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private bool EnsurePlane()
        {
            if (_renderer != null) return true;
            Shader shader = _owner.DepthMistShader != null ? _owner.DepthMistShader : Shader.Find("RelicKeeper/Atmosphere/Depth Mist");
            if (shader == null || !shader.isSupported) return false;
            _object = new GameObject(ObjectName) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(_object, _camera.gameObject.scene);
            _object.transform.SetParent(_camera.transform, false);
            _mesh = new Mesh { name = "Depth mist quad", hideFlags = HideFlags.HideAndDontSave };
            _mesh.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; _mesh.RecalculateBounds();
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _material = new Material(shader) { name = "Depth mist (owned)", hideFlags = HideFlags.HideAndDontSave };
            _renderer = _object.AddComponent<MeshRenderer>(); _renderer.enabled = false;
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off; _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off; _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (_renderer != null) _renderer.enabled = false;
            Destroy(_object); Destroy(_material); Destroy(_mesh);
            _renderer = null; _object = null; _material = null; _mesh = null;
        }

        private static void Destroy(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
    }
}
