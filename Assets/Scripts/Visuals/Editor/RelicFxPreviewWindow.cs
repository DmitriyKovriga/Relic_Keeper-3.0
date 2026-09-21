using System.IO;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RelicKeeper.Editor.Visuals
{
    public sealed class RelicFxPreviewWindow : EditorWindow
    {
        private Scene _scene;
        private GameObject _root;
        private Camera _camera;
        private AtmosphereController _controller;
        private AtmosphereProfileSO _profile;
        private RenderTexture _target;
        private double _lastRepaint;

        [MenuItem("Tools/Relic Keeper/Visual FX/Open safe preview")]
        public static void Open() => GetWindow<RelicFxPreviewWindow>("Relic FX Preview");

        private void OnEnable()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RelicVisualTools.PreviewPath) == null) RelicVisualTools.CreatePreview();
            _scene = EditorSceneManager.NewPreviewScene();
            _root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RelicVisualTools.PreviewPath), _scene);
            _camera = _root.GetComponentInChildren<Camera>(); _camera.enabled = false; _camera.scene = _scene;
            _controller = _camera.GetComponent<AtmosphereController>();
            _profile = Instantiate(_controller.Profile); _profile.hideFlags = HideFlags.HideAndDontSave; _controller.Profile = _profile;
            _target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            _target.Create();
            EditorApplication.update += Tick;
        }

        private void Tick()
        {
            if (EditorApplication.timeSinceStartup - _lastRepaint < 1.0 / 12.0) return;
            _lastRepaint = EditorApplication.timeSinceStartup; Repaint();
        }

        private void OnGUI()
        {
            if (_profile == null || _camera == null) return;
            EditorGUILayout.LabelField("Isolated preview — gameplay scenes and profile assets are untouched.");
            _profile.darkness = EditorGUILayout.Slider("Darkness", _profile.darkness, 0, 1);
            _profile.fogOpacity = EditorGUILayout.Slider("Fog", _profile.fogOpacity, 0, 1);
            _profile.revealRadius = EditorGUILayout.Slider("Reveal radius", _profile.revealRadius, 0, 5);
            Vector3 position = _controller.Player.position;
            position.x = EditorGUILayout.Slider("Aura / reveal X", position.x, -8, 8);
            _controller.Player.position = position;
            _controller.enabled = EditorGUILayout.Toggle("Atmosphere", _controller.enabled);
            Rect area = GUILayoutUtility.GetAspectRect(480f / 270f);
            if (Event.current.type == EventType.Repaint)
            {
                RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = _target });
                GUI.DrawTexture(area, _target, ScaleMode.ScaleToFit, false);
            }
            EditorGUILayout.LabelField("Neutral  •  Flash  •  Outline  •  Inner shadow  •  Emission  •  Dissolve");
        }

        // Bridge-callable capture for visual QA; opens no scenes/windows.
        public static void Capture()
        {
            var window = CreateInstance<RelicFxPreviewWindow>();
            try
            {
                RenderPipeline.SubmitRenderRequest(window._camera, new RenderPipeline.StandardRequest { destination = window._target });
                var texture = new Texture2D(480, 270, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = window._target;
                    texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); texture.Apply();
                    Directory.CreateDirectory("Logs/RelicFX");
                    File.WriteAllBytes("Logs/RelicFX/preview-480x270.png", texture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; DestroyImmediate(texture); }
            }
            finally { DestroyImmediate(window); }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            if (_target != null) DestroyImmediate(_target);
            if (_profile != null) DestroyImmediate(_profile);
        }
    }
}
