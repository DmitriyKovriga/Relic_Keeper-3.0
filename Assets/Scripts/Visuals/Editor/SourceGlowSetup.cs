using System;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    [InitializeOnLoad]
    public static class SourceGlowSetup
    {
        private const string Root = "Assets/Resources/Visuals/SourceGlow/";
        private static WorldColorPreviewStage _capture;
        private static bool _captureHub;
        static SourceGlowSetup() => SourceGlowVisual.EditorPreviewProfile = SceneProfile;

        private static AtmosphereProfileSO SceneProfile(GameObject source, Camera camera)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (camera.cameraType != CameraType.SceneView || stage == null || stage.scene != source.scene ||
                stage.prefabContentsRoot.GetComponent<RoomController>() == null) return null;
            foreach (var window in Resources.FindObjectsOfTypeAll<WorldColorProfilesWindow>())
                if (window.MortfallBeamProfile != null) return window.MortfallBeamProfile;
            return AssetDatabase.LoadAssetAtPath<DungeonDataSO>(WorldColorPreviewStage.MortfallDataPath)?.DepthMistProfile;
        }

        public static void Install()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "Halo.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 24;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true;
            var sprite = new TextureImporterSettings(); importer.ReadTextureSettings(sprite);
            sprite.spriteMeshType = SpriteMeshType.FullRect; sprite.spriteAlignment = (int)SpriteAlignment.Center;
            sprite.spritePivot = new Vector2(0.5f, 0.5f); importer.SetTextureSettings(sprite); importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Halo.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("RelicKeeper/Atmosphere/Source Glow")) { name = "Source halo" };
                AssetDatabase.CreateAsset(material, Root + "Halo.mat");
            }
            AssetDatabase.SaveAssetIfDirty(material);
        }

        public static void PrepareCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusLighting(); EditorApplication.QueuePlayerLoopUpdate();
            _captureHub = false;
        }
        public static void PrepareHubCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(false, null, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusLighting(); EditorApplication.QueuePlayerLoopUpdate(); _captureHub = true;
        }

        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the glow preview first.");
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var draft = Object.Instantiate(_capture.Controller.ActiveDepthMistProfile);
            _capture.Controller.PreviewDepthMistProfile = draft;
            try
            {
                Directory.CreateDirectory("Logs/SourceGlow");
                var glows = _capture.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true))
                    .Where(x => x.name == "Source glow (preview)").ToArray();
                for (int i = 0; i < glows.Length; i++)
                {
                    _capture.Camera.transform.position = glows[i].transform.parent.position + new Vector3(0, 0.5f, -10);
                    foreach (bool enabled in new[] { false, true })
                    {
                        draft.sourceGlow = enabled;
                        RenderPipeline.SubmitRenderRequest(_capture.Camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/SourceGlow/{(_captureHub ? "Hub" : "Mortfall")}-{i}-{enabled}.png", image.EncodeToPNG());
                    }
                }
            }
            finally
            {
                RenderTexture.active = previous; _capture.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(draft); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
                _capture.Dispose(); _capture = null;
            }
        }
        public static void Show() => RoomLightingAuthoring.Show();

        public static void CaptureLive()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            var camera = Camera.main;
            var owner = camera != null ? camera.GetComponent<AtmosphereController>() : null;
            if (owner == null || WorldColorGrading.BindingsSuspended) throw new InvalidOperationException("The game camera is not ready.");
            var previousProfile = owner.PreviewDepthMistProfile;
            var draft = Object.Instantiate(owner.ActiveDepthMistProfile); owner.PreviewDepthMistProfile = draft;
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Vector3 cameraPosition = camera.transform.position;
            try
            {
                Directory.CreateDirectory("Logs/SourceGlow");
                var halos = camera.gameObject.scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true))
                    .Where(x => x.name == "Source glow (preview)" && x.gameObject.activeInHierarchy).ToArray();
                if (halos.Length == 0) throw new InvalidOperationException("No active source halos were created in the game scene.");
                for (int i = 0; i < halos.Length; i++)
                {
                    camera.transform.position = halos[i].transform.parent.position + new Vector3(0, 0.5f, -10);
                    foreach (bool enabled in new[] { false, true })
                    {
                        draft.sourceGlow = enabled;
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes($"Logs/SourceGlow/Live-{i}-{enabled}.png", image.EncodeToPNG());
                    }
                }
                File.WriteAllText("Logs/SourceGlow/Live-sources.txt", string.Join("\n",
                    halos.Select(x => x.transform.parent.name + " " + x.transform.position)));
            }
            finally
            {
                owner.PreviewDepthMistProfile = previousProfile; RenderTexture.active = previous;
                camera.transform.position = cameraPosition;
                Object.DestroyImmediate(draft); Object.DestroyImmediate(image); Object.DestroyImmediate(target);
            }
        }
    }

    [CustomPropertyDrawer(typeof(SourceGlowSettings))]
    public sealed class SourceGlowSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => (EditorGUIUtility.singleLineHeight + 2) * 8;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float step = EditorGUIUtility.singleLineHeight + 2;
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(row, label, EditorStyles.boldLabel); row.y += step;
            void Field(string name, string title)
            { EditorGUI.PropertyField(row, property.FindPropertyRelative(name), new GUIContent(title)); row.y += step; }
            Field("enabled", "Enable glow");
            Field("strength", "Glow strength"); Field("radius", "Radius (world units)"); Field("softness", "Edge softness");
            Field("color", "Glow color"); Field("offset", "Position offset");
            EditorGUI.EndProperty();
        }
    }
}
