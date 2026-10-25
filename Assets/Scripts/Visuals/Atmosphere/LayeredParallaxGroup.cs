using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scripts.Visuals.Atmosphere
{
    // Only artwork moves, only during its camera's render. Authored transforms are restored
    // afterward, so animation, scene saves, other cameras and hub re-entry keep their anchors.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LayeredParallaxGroup : MonoBehaviour
    {
        public enum Depth { Sky, Far, Middle, Near }
        [Serializable] public struct Layer
        {
            public SpriteRenderer renderer;
            public Depth depth;
            public bool silhouette;
            public Layer(SpriteRenderer renderer, Depth depth, bool silhouette = false)
            { this.renderer = renderer; this.depth = depth; this.silhouette = silhouette; }
        }

        [SerializeField] private Layer[] _layers = Array.Empty<Layer>();
        [SerializeField] private SpriteRenderer _visibilitySource;
        private Vector3 _reference;
        private Vector3[] _positions;
        private Color[] _colors;
        private bool _rendering;
        private ParallaxEffect _legacy;
        private bool _legacyWasEnabled;
        public IReadOnlyList<Layer> Layers => _layers;

        private void OnEnable()
        {
            if (_legacy != null) _legacy.enabled = false;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            Restore();
            SetVisible(false);
            if (_legacy != null) _legacy.enabled = _legacyWasEnabled;
        }
        public void ResetAnchor(Vector3 cameraReference) { Restore(); _reference = cameraReference; }
        public void SetVisible(bool visible)
        {
            foreach (var layer in _layers)
                if (layer.silhouette && layer.renderer != null) layer.renderer.enabled = visible;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            Restore();
            if (camera.cameraType != CameraType.Game || camera.gameObject.scene != gameObject.scene ||
                !camera.TryGetComponent<AtmosphereController>(out var controller) || WorldColorGrading.BindingsSuspended ||
                (_visibilitySource != null && !_visibilitySource.enabled)) return;
            ApplyView(camera.transform.position, controller.ActiveDepthMistProfile);
        }
        private void EndCamera(ScriptableRenderContext context, Camera camera) => Restore();

        public void ApplyView(Vector3 cameraPosition, AtmosphereProfileSO profile)
        {
            Restore();
            if (profile == null) return;
            if (_positions == null || _positions.Length != _layers.Length)
            { _positions = new Vector3[_layers.Length]; _colors = new Color[_layers.Length]; }
            _rendering = true;
            // Snapshot children before moving their parent background.
            for (int i = 0; i < _layers.Length; i++)
            {
                var renderer = _layers[i].renderer; if (renderer == null) continue;
                _positions[i] = renderer.transform.position; _colors[i] = renderer.color;
            }
            for (int i = 0; i < _layers.Length; i++)
            {
                Layer layer = _layers[i];
                if (layer.renderer == null) continue;
                float strength = layer.depth == Depth.Sky ? 1f : layer.depth == Depth.Far ? profile.parallaxFar :
                    layer.depth == Depth.Middle ? profile.parallaxMiddle : profile.parallaxNear;
                if (!profile.layeredParallax) strength = 0;
                layer.renderer.transform.position = ParallaxEffect.EvaluatePosition(_positions[i], _reference, cameraPosition,
                    strength, strength * profile.parallaxVertical, Mathf.Max(1f, profile.pixelsPerUnit));
                if (layer.silhouette)
                {
                    // Distance is authored in the stone palette. Never blend the cave
                    // through a visible pillar; the legacy presence value is now on/off.
                    Color color = _colors[i]; color.a = profile.parallaxSilhouettes > 0f ? 1f : 0f;
                    layer.renderer.color = color;
                }
            }
        }
        public void Restore()
        {
            if (!_rendering) return;
            _rendering = false;
            for (int i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i]; if (layer.renderer == null) continue;
                layer.renderer.transform.position = _positions[i];
                if (layer.silhouette) layer.renderer.color = _colors[i];
            }
        }

        public static LayeredParallaxGroup ConfigureHub(GameObject root)
        {
            var layers = new List<Layer>();
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                switch (renderer.name)
                {
                    case "Sky": layers.Add(new Layer(renderer, Depth.Sky)); break;
                    case "Mountains": case "Castle": layers.Add(new Layer(renderer, Depth.Far)); break;
                    case "DistantForest": layers.Add(new Layer(renderer, Depth.Middle)); break;
                    case "Forest": case "Fog": case "FogBlend": layers.Add(new Layer(renderer, Depth.Near)); break;
                }
            }
            var group = root.GetComponent<LayeredParallaxGroup>() ?? root.AddComponent<LayeredParallaxGroup>();
            group.Restore(); group._layers = layers.ToArray(); group.ResetAnchor(Vector3.zero);
            return group;
        }

        public static LayeredParallaxGroup ConfigureDungeon(SpriteRenderer background, Sprite silhouette)
        {
            var group = background.GetComponent<LayeredParallaxGroup>() ?? background.gameObject.AddComponent<LayeredParallaxGroup>();
            group.Restore();
            if (group._legacy == null && background.TryGetComponent<ParallaxEffect>(out var legacy))
            { group._legacy = legacy; group._legacyWasEnabled = legacy.enabled; }
            group.enabled = true;
            if (group._legacy != null) group._legacy.enabled = false;
            var layers = new List<Layer> { new Layer(background, Depth.Far) };
            for (int i = 0; i < 4; i++)
            {
                string name = "Distant column " + i;
                Transform child = background.transform.Find(name);
                if (child == null)
                {
                    var go = new GameObject(name); go.transform.SetParent(background.transform, false); child = go.transform;
                    go.AddComponent<SpriteRenderer>(); go.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
                }
                child.localPosition = new Vector3(-21f + i * 14f, 0, 0);
                var renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = silhouette; renderer.enabled = silhouette != null;
                renderer.sharedMaterial = background.sharedMaterial;
                renderer.sortingLayerName = "Background"; renderer.sortingOrder = -900;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(silhouette != null ? silhouette.rect.width / silhouette.pixelsPerUnit : 4f, 120f);
                renderer.color = Color.white;
                if (silhouette != null) layers.Add(new Layer(renderer, Depth.Middle, true));
            }
            group._layers = layers.ToArray(); group._visibilitySource = background;
            return group;
        }
    }
}
