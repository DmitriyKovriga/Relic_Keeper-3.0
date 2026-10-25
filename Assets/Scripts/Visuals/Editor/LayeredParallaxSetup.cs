using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Editor.Visuals
{
    public static class LayeredParallaxSetup
    {
        internal const string ColumnPath = "Assets/Visuals/Environment/Parallax/MortfallDistantColumn.png";
        public static void Apply()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(ColumnPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 24; importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteBorder = new Vector4(0, 32, 0, 32);
            importer.SetTextureSettings(settings); importer.SaveAndReimport();
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDataSO>(WorldColorPreviewStage.MortfallDataPath);
            var fields = new SerializedObject(dungeon);
            fields.FindProperty("_parallaxSilhouette").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ColumnPath);
            fields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(dungeon);
            foreach (string path in new[] { DepthMistSetup.HubPath, DepthMistSetup.DungeonPath })
            {
                var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
                profile.layeredParallax = true;
                profile.parallaxVertical = path == DepthMistSetup.DungeonPath ? 0.75f : 0f;
                EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            }
            const string prefabPath = "Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try { LayeredParallaxGroup.ConfigureHub(root); PrefabUtility.SaveAsPrefabAsset(root, prefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ShowHubMovement()
        {
            var window = EditorWindow.GetWindow<WorldColorProfilesWindow>("World Appearance");
            window.SetPreviewEnvironment(WorldColorProfilesWindow.PreviewEnvironment.Hub);
            var field = typeof(WorldColorProfilesWindow).GetField("_appearanceTab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(window, 2); window.Show(); window.Focus();
        }

        public static void Capture()
        {
            System.IO.Directory.CreateDirectory("Logs/Parallax");
            foreach (bool dungeon in new[] { false, true })
            using (var stage = WorldColorPreviewStage.Create(dungeon, "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_003.prefab", Camera.main?.GetComponent<AtmosphereController>()))
            {
                var target = new RenderTexture(480, 270, 24); target.Create();
                var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    foreach (float offset in new[] { -15f, 0f, 15f })
                    {
                        stage.SetCameraOffset(offset);
                        UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                            new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        System.IO.File.WriteAllBytes($"Logs/Parallax/{(dungeon ? "Mortfall" : "Hub")}-{offset}.png", image.EncodeToPNG());
                    }
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(image); Object.DestroyImmediate(target); }
            }
        }
    }
}
