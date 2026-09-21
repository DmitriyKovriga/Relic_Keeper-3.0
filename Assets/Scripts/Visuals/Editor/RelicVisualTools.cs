using System;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using Scripts.Visuals.SpriteFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RelicKeeper.Editor.Visuals
{
    public static class RelicVisualTools
    {
        public const string Root = "Assets/Visuals/RelicFX";
        public const string MaterialPath = Root + "/RelicSpriteFX.mat";
        public const string ProfilePath = Root + "/AtmosphereNeutral.asset";
        public const string PreviewPath = Root + "/Preview/RelicFXPreview.prefab";
        public const string SpriteShaderName = "RelicKeeper/Sprites/Relic Sprite FX";

        [MenuItem("Tools/Relic Keeper/Visual FX/Install neutral assets and Renderer Feature")]
        public static void Install()
        {
            EnsureAssets();
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("An active URP asset is required.");
            var serialized = new SerializedObject(pipeline);
            int index = serialized.FindProperty("m_DefaultRendererIndex").intValue;
            var renderer = serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(index).objectReferenceValue as Renderer2DData;
            if (renderer == null) throw new InvalidOperationException("The default renderer must be Renderer2D.");
            InstallFeature(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
            Debug.Log("Relic FX ready. Atmosphere stays off until a camera has an enabled controller and a non-neutral profile.");
        }

        public static RelicAtmosphereFeature InstallFeature(ScriptableRendererData renderer)
        {
            var existing = renderer.rendererFeatures.OfType<RelicAtmosphereFeature>().FirstOrDefault();
            if (existing != null) return existing;
            Undo.RecordObject(renderer, "Install Relic Atmosphere");
            var feature = ScriptableObject.CreateInstance<RelicAtmosphereFeature>();
            feature.name = "Relic Atmosphere";
            feature.SetShader(Shader.Find("Hidden/RelicKeeper/Atmosphere"));
            Undo.RegisterCreatedObjectUndo(feature, "Install Relic Atmosphere");
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            // Unity keeps a parallel local-file-ID map to recover feature references after reload.
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
            return feature;
        }

        public static void EnsureAssets()
        {
            EnsureFolder(Root);
            if (AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(ProfilePath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AtmosphereProfileSO>(), ProfilePath);
            string noisePath = Root + "/DissolveNoise.asset";
            var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath);
            if (noise == null)
            {
                noise = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
                { name = "Relic Dissolve Noise", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
                var pixels = new Color32[32 * 32];
                uint state = 0x52454c49;
                for (int i = 0; i < pixels.Length; i++)
                {
                    state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                    byte v = (byte)(1 + state % 254);
                    pixels[i] = new Color32(v, v, v, 255);
                }
                noise.SetPixels32(pixels); noise.Apply(false, true);
                AssetDatabase.CreateAsset(noise, noisePath);
            }
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            {
                var material = new Material(Shader.Find(SpriteShaderName)) { name = "Relic Sprite FX" };
                material.SetTexture("_FxNoiseTex", noise);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
        }

        [MenuItem("Tools/Relic Keeper/Visual FX/Apply shared Sprite FX to selected sprites")]
        public static void ApplySelected()
        {
            EnsureAssets();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            foreach (var renderer in Selection.GetFiltered<SpriteRenderer>(SelectionMode.Editable | SelectionMode.Deep))
            {
                Undo.RecordObject(renderer, "Apply Relic Sprite FX");
                renderer.sharedMaterial = material;
                if (renderer.GetComponent<SpriteFxController>() == null) Undo.AddComponent<SpriteFxController>(renderer.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
        }

        [MenuItem("Tools/Relic Keeper/Visual FX/Add atmosphere to selected camera")]
        public static void AddToCamera()
        {
            var camera = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Camera>() : null;
            if (camera == null || !camera.orthographic) throw new InvalidOperationException("Select an orthographic Camera.");
            if (FindUnsupportedCanvas(camera) != null)
                throw new InvalidOperationException("This camera sees a Camera/World Space Canvas. Move that UI to Screen Space Overlay before enabling atmosphere.");
            Install();
            var controller = camera.GetComponent<AtmosphereController>();
            if (controller == null) controller = Undo.AddComponent<AtmosphereController>(camera.gameObject);
            Undo.RecordObject(controller, "Set neutral atmosphere");
            controller.Profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(ProfilePath);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        public static Canvas FindUnsupportedCanvas(Camera camera)
        {
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || (camera.cullingMask & (1 << canvas.gameObject.layer)) == 0) continue;
                if ((canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == camera) ||
                    canvas.renderMode == RenderMode.WorldSpace) return canvas;
            }
            return null;
        }

        [MenuItem("Tools/Relic Keeper/Visual FX/Create preview assets")]
        public static void CreatePreview()
        {
            Install();
            EnsureFolder(Root + "/Preview");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) != null)
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath));
                return;
            }
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Relic FX Preview");
                SceneManager.MoveGameObjectToScene(root, preview);
                var camera = new GameObject("Preview Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, preview);
                camera.transform.SetParent(root.transform, false);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true; camera.orthographicSize = 5.625f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.16f, 0.19f, 0.23f);
                camera.allowMSAA = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var pixel = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>();
                pixel.assetsPPU = 24; pixel.refResolutionX = 480; pixel.refResolutionY = 270;
                pixel.gridSnapping = UnityEngine.Rendering.Universal.PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
                var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
                profile.darkness = 0.35f; profile.fogOpacity = 0.3f; profile.vignette = 0.2f;
                profile.revealRadius = 1.2f; profile.revealSoftness = 2.5f;
                AssetDatabase.CreateAsset(profile, Root + "/Preview/AtmospherePreview.asset");
                var atmosphere = camera.gameObject.AddComponent<AtmosphereController>(); atmosphere.Profile = profile;

                Sprite sprite = CreatePreviewSprite();
                string[] names = { "Neutral", "Flash", "Outline", "Inner shadow", "Emission", "Dissolve" };
                SpriteRenderer player = null;
                for (int i = 0; i < names.Length; i++)
                {
                    var renderer = new GameObject(names[i]).AddComponent<SpriteRenderer>();
                    SceneManager.MoveGameObjectToScene(renderer.gameObject, preview);
                    renderer.transform.SetParent(root.transform, false);
                    renderer.transform.position = new Vector3(-6.25f + i * 2.5f, 0, 0);
                    renderer.transform.localScale = Vector3.one * 2f;
                    renderer.sprite = sprite;
                    var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
                    material.name = names[i];
                    if (i == 1) material.SetFloat("_FxFlash", 0.8f);
                    if (i == 2) { material.SetFloat("_FxOutline", 1); material.SetFloat("_FxOutlineWidth", 2); }
                    if (i == 3) material.SetFloat("_FxInnerShadow", 0.9f);
                    if (i == 4) { material.SetFloat("_FxEmission", 0.55f); material.SetFloat("_FxOutline", 0.7f); player = renderer; }
                    if (i == 5) material.SetFloat("_FxDissolve", 0.45f);
                    AssetDatabase.CreateAsset(material, Root + "/Preview/" + names[i].Replace(" ", "") + ".mat");
                    renderer.sharedMaterial = material;
                    renderer.gameObject.AddComponent<SpriteFxController>();
                }
                atmosphere.BindPlayer(player.transform);
                CreatePreviewHud(preview, root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            Debug.Log("Relic FX preview prefab saved separately at " + PreviewPath);
        }

        private static Sprite CreatePreviewSprite()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "Original relic silhouette", filterMode = FilterMode.Point };
            var colors = new Color32[1024];
            for (int y = 5; y < 27; y++)
                for (int x = 5; x < 27; x++)
                    if (Mathf.Abs(x - 15) + Mathf.Abs(y - 16) < 13)
                        colors[y * 32 + x] = x < 15 ? new Color32(88, 156, 176, 255) : new Color32(171, 213, 185, 255);
            colors[18 * 32 + 12] = new Color32(255, 216, 110, 255);
            texture.SetPixels32(colors); texture.Apply();
            AssetDatabase.CreateAsset(texture, Root + "/Preview/RelicSilhouette.asset");
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
            sprite.name = "Padded relic";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.SaveAssetIfDirty(texture);
            return sprite;
        }

        private static void CreatePreviewHud(Scene scene, Transform parent)
        {
            var canvas = new GameObject("Overlay UI — unaffected", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            SceneManager.MoveGameObjectToScene(canvas.gameObject, scene);
            canvas.transform.SetParent(parent, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(480, 270);
            var label = new GameObject("Guide", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(canvas.transform, false);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 12;
            label.text = "RELIC FX  /  OVERLAY UI\n\n1 Neutral   2 Flash   3 Outline   4 Shadow   5 Aura   6 Dissolve\n\nReveal follows Aura. Move it or tune AtmospherePreview.";
            label.color = Color.white;
            var rect = label.rectTransform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(-24, 90); rect.anchoredPosition = new Vector2(0, -12);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }

    [CustomEditor(typeof(AtmosphereController))]
    public sealed class AtmosphereControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (AtmosphereController)target;
            if (RelicVisualTools.FindUnsupportedCanvas(controller.GetComponent<Camera>()) != null)
                EditorGUILayout.HelpBox("Camera/World Space UI is part of the world buffer. Switch it to Screen Space Overlay before enabling atmosphere.", MessageType.Error);
            if (controller.Player == null)
                EditorGUILayout.HelpBox("No Player: fog still works, but reveal is disabled. Bind the spawned player's Transform.", MessageType.Info);
        }
    }
}
