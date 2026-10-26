using System;
using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class StoneReliefSetup
    {
        public const string SheetPath = "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-ground-platforms.png";
        public const string NormalPath = "Assets/Visuals/Normals/MortfallStoneNormal.png";
        public const string WallSheetPath = "Assets/Resources/Sprites/WallAndGrounds/MortfallDungeon/MortFallAssets/Mortfall-Walls-tiled.png";
        public const string WallNormalPath = "Assets/Visuals/Normals/MortfallWallNormal.png";
        public const string MaterialPath = "Assets/Visuals/Normals/MortfallStoneRelief.mat";
        private static WorldColorPreviewStage _capture;

        public static void PrepareCapture()
        {
            _capture?.Dispose();
            _capture = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, Camera.main?.GetComponent<AtmosphereController>());
            _capture.FocusLighting(); EditorApplication.QueuePlayerLoopUpdate();
        }

        public static void Capture()
        {
            if (_capture == null) throw new InvalidOperationException("Prepare the stone-relief capture first.");
            var stage = _capture;
            var lights = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).ToArray();
            var source = lights.Where(x => !x.IsRoomFill).OrderBy(x => x.transform.position.x).First();
            foreach (var light in lights)
            {
                var fields = new SerializedObject(light); fields.FindProperty("_flame").boolValue = false; fields.ApplyModifiedPropertiesWithoutUndo();
            }
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile); stage.Controller.PreviewDepthMistProfile = profile;
            profile.ambientAnimations = false; profile.ambientParticles = false; profile.sourceGlow = false;
            var target = new RenderTexture(480, 270, 24); target.Create(); var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Directory.CreateDirectory("Logs/StoneRelief");
                stage.Camera.orthographicSize = 3.5f; stage.Camera.transform.position = source.transform.position + new Vector3(-0.75f, 0.25f, -10);
                var baseline = Array.Empty<Color32>(); var report = new System.Text.StringBuilder();
                foreach (var value in new[] { 0f, 0.4f, 1f })
                {
                    profile.stoneReliefStrength = value;
                    RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/StoneRelief/Strength-{value:0.0}.png", image.EncodeToPNG());
                    var data = image.GetPixels32(); int changed = 0, peak = 0;
                    if (baseline.Length == 0) baseline = data;
                    for (int i = 0; i < data.Length; i++)
                    {
                        int difference = Mathf.Abs(data[i].r - baseline[i].r) + Mathf.Abs(data[i].g - baseline[i].g) + Mathf.Abs(data[i].b - baseline[i].b);
                        if (difference > 2) changed++; peak = Mathf.Max(peak, difference);
                    }
                    report.AppendLine($"Strength {value}: changed pixels {changed}, peak RGB-sum delta {peak}");
                }
                profile.stoneReliefStrength = 0.4f;
                var cameraStart = stage.Camera.transform.position;
                for (int i = 0; i <= 20; i++)
                {
                    stage.Camera.transform.position = cameraStart + Vector3.right * ((i - 10) / 24f);
                    RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/StoneRelief/Move-{i:00}.png", image.EncodeToPNG());
                }
                File.WriteAllText("Logs/StoneRelief/Report.txt", report.ToString());
            }
            finally
            {
                RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                stage.Dispose(); _capture = null;
            }
        }
        public static void Inspect()
        {
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(LocalLightingSetup.RoomPath);
            var report = new System.Text.StringBuilder();
            foreach (var light in room.GetComponentsInChildren<LocalLight2DSource>(true))
                report.AppendLine($"Light {light.name}: {light.transform.position}, normals {light.Light.normalMapQuality}");
            foreach (var map in room.GetComponentsInChildren<Tilemap>(true))
            {
                var textures = new System.Collections.Generic.HashSet<string>();
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var sprite = map.GetSprite(cell);
                    if (sprite != null) textures.Add(AssetDatabase.GetAssetPath(sprite.texture));
                }
                report.AppendLine($"Map {map.name}: {map.cellBounds}, material {map.GetComponent<Renderer>().sharedMaterial.name}, textures {string.Join(",", textures)}");
            }
            Directory.CreateDirectory("Logs/StoneRelief"); File.WriteAllText("Logs/StoneRelief/Inspection.txt", report.ToString());
        }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before saving the pilot.");
            ImportNormal(SheetPath, NormalPath); ImportNormal(WallSheetPath, WallNormalPath);
            var shader = Shader.Find("RelicKeeper/Sprites/Stone Relief");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("The stone-relief shader must compile first.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(shader) { name = "Mortfall stone relief" }; AssetDatabase.CreateAsset(material, MaterialPath); }
            ApplyRoom(material);
            var profile = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/MortfallDepthMist.asset");
            var settings = new SerializedObject(profile); settings.FindProperty("stoneReliefStrength").floatValue = 0.4f;
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
        }

        private static void ImportNormal(string sheetPath, string normalPath)
        {
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            if (normalImporter == null) throw new InvalidOperationException("Download the PixelLab normal map first.");
            normalImporter.textureType = TextureImporterType.NormalMap; normalImporter.sRGBTexture = false;
            normalImporter.convertToNormalmap = false; normalImporter.mipmapEnabled = false; normalImporter.filterMode = FilterMode.Point;
            normalImporter.wrapMode = TextureWrapMode.Clamp; normalImporter.npotScale = TextureImporterNPOTScale.None;
            normalImporter.textureCompression = TextureImporterCompression.Uncompressed; normalImporter.SaveAndReimport();
            var sheet = (TextureImporter)AssetImporter.GetAtPath(sheetPath);
            var secondary = sheet.secondarySpriteTextures.Where(x => x.name != "_NormalMap").ToList();
            secondary.Add(new SecondarySpriteTexture { name = "_NormalMap", texture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) });
            sheet.secondarySpriteTextures = secondary.ToArray(); sheet.SaveAndReimport();
        }

        private static void ApplyRoom(Material material)
        {
            WorldColorGrading.SuspendBindings();
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var light = room.GetComponentsInChildren<LocalLight2DSource>(true).Where(x => !x.IsRoomFill)
                    .OrderBy(x => x.transform.position.x).First();
                var fields = new SerializedObject(light.Light);
                fields.FindProperty("m_NormalMapQuality").intValue = (int)Light2D.NormalMapQuality.Accurate;
                fields.FindProperty("m_NormalMapDistance").floatValue = 0.85f;
                fields.FindProperty("m_UseNormalMap").boolValue = true;
                fields.ApplyModifiedPropertiesWithoutUndo();
                Vector2 center = (Vector2)light.transform.position + new Vector2(-1, 0.75f);
                foreach (var map in room.GetComponentsInChildren<Tilemap>(true))
                {
                    if (map.name != "Ground" && map.name != "Background" && map.name != "Platforms") continue;
                    var surface = map.GetComponent<StoneReliefSurface>() ?? map.gameObject.AddComponent<StoneReliefSurface>();
                    Vector2 min = map.transform.InverseTransformPoint(center - new Vector2(3, 3));
                    Vector2 max = map.transform.InverseTransformPoint(center + new Vector2(3, 3));
                    surface.Configure(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
                    map.GetComponent<Renderer>().sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(room, LocalLightingSetup.RoomPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); WorldColorGrading.RefreshRegisteredScenes(); }
        }
    }
}
