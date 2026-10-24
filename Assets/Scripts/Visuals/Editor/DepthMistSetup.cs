using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Scripts.Dungeon;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    public static class DepthMistSetup
    {
        public const string HubPath = "Assets/Visuals/RelicFX/HubDepthMist.asset";
        public const string DungeonPath = "Assets/Visuals/RelicFX/MortfallDepthMist.asset";

        [MenuItem("Tools/Relic Keeper/Developer/Visuals/Set Up Depth Mist")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var hub = Profile(HubPath, false); var dungeon = Profile(DungeonPath, true);
            Shader shader = Shader.Find("RelicKeeper/Atmosphere/Depth Mist");
            if (shader == null) throw new InvalidOperationException("Depth mist shader is unavailable.");
            string scenePath = "Assets/Scenes/HubScene.unity";
            string text = File.ReadAllText(scenePath);
            var blocks = Regex.Matches(text, @"(?ms)^--- !u!114 &\d+.*?(?=^--- !u!|\z)");
            var block = blocks.Cast<Match>().Single(x => x.Value.Contains("guid: 334b7dde2a59dfc4e9a483859056d0a3"));
            string updated = block.Value;
            foreach (var item in new[] { ("_hubDepthMistProfile", (Object)hub), ("_defaultDungeonDepthMistProfile", (Object)dungeon), ("_depthMistShader", (Object)shader) })
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.Item2, out string guid, out long id);
                string field = "  " + item.Item1 + ": {fileID: " + id + ", guid: " + guid + ", type: " + (item.Item2 is Shader ? "3" : "2") + "}";
                string pattern = @"(?m)^  " + item.Item1 + @":.*$";
                updated = Regex.IsMatch(updated, pattern) ? Regex.Replace(updated, pattern, field) : updated.TrimEnd() + "\n" + field + "\n";
            }
            File.WriteAllText(scenePath, text.Substring(0, block.Index) + updated + text.Substring(block.Index + block.Length));
            var data = AssetDatabase.LoadAssetAtPath<DungeonDataSO>(WorldColorPreviewStage.MortfallDataPath);
            var fields = new SerializedObject(data); fields.FindProperty("_depthMistProfile").objectReferenceValue = dungeon;
            fields.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            // Update an open hub camera without serializing unrelated unsaved scene edits.
            foreach (var controller in Object.FindObjectsByType<AtmosphereController>(FindObjectsSortMode.None))
            {
                if (controller.gameObject.scene.path != scenePath) continue;
                var cameraFields = new SerializedObject(controller);
                cameraFields.FindProperty("_hubDepthMistProfile").objectReferenceValue = hub;
                cameraFields.FindProperty("_defaultDungeonDepthMistProfile").objectReferenceValue = dungeon;
                cameraFields.FindProperty("_depthMistShader").objectReferenceValue = shader;
                cameraFields.ApplyModifiedPropertiesWithoutUndo();
            }
            UpdateHubLayering();
        }

        public static void UpdateHubLayering()
        {
            const string path = "Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "Forest").sortingOrder = 6;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static AtmosphereProfileSO Profile(string path, bool dungeon)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>(path);
            if (existing != null) return existing;
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            profile.backgroundMist = dungeon ? 0.17f : 0.1f;
            profile.backgroundTint = dungeon ? new Color(0.31f, 0.43f, 0.47f) : new Color(0.4f, 0.52f, 0.56f);
            profile.wispStrength = dungeon ? 0.8f : 0.7f;
            profile.depthMistCenterY = dungeon ? 2f : 3.4f;
            profile.depthMistHeight = dungeon ? 12f : 7f;
            profile.depthMistFeather = dungeon ? 3f : 2.2f;
            profile.noiseScale = dungeon ? 0.16f : 0.22f;
            profile.driftSpeed = dungeon ? new Vector2(0.009f, -0.002f) : new Vector2(0.012f, 0.003f);
            AssetDatabase.CreateAsset(profile, path); AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        [MenuItem("Tools/Relic Keeper/Developer/Visuals/Capture Depth Mist")]
        public static void Capture()
        {
            foreach (var view in new[] { (dungeon: false, room: "001", name: "Hub"),
                (dungeon: true, room: "001", name: "Mortfall"), (dungeon: true, room: "003", name: "Mortfall-lower-floor") })
            using (var stage = WorldColorPreviewStage.Create(view.dungeon, "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_" + view.room + ".prefab", null))
            {
                if (!view.dungeon) stage.Camera.transform.position = RelicKeeper.Editor.Hub.Hub20Builder.PixelToWorld(410, 190) + Vector3.back * 10;
                if (view.room == "003")
                {
                    stage.SetView(true, true);
                    stage.Camera.orthographicSize = 7.5f;
                    stage.Camera.transform.position = new Vector3(3f, 0f, -10f);
                }
                var profile = stage.Controller.ActiveDepthMistProfile;
                var draft = Object.Instantiate(profile); draft.driftSpeed = Vector2.zero;
                var settings = new SerializedObject(stage.Controller);
                settings.FindProperty("_hubDepthMistProfile").objectReferenceValue = draft; settings.ApplyModifiedPropertiesWithoutUndo();
                var target = new RenderTexture(480, 270, 24);
                var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    Directory.CreateDirectory("Logs/DepthMist"); target.Create();
                    foreach (bool active in new[] { false, true })
                    {
                        draft.backgroundMist = active ? profile.backgroundMist : 0;
                        RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                        File.WriteAllBytes("Logs/DepthMist/" + view.name + (active ? "-after.png" : "-before.png"), image.EncodeToPNG());
                    }
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(draft); }
            }
        }

    }
}
