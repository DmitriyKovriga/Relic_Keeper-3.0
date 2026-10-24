using System.IO;
using System.Linq;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RelicKeeper.Editor.Visuals
{
    // Source-art comparisons and an in-game capture; no gameplay or player-save changes.
    public static class WorldColorProfileCapture
    {
        private const string Output = "Logs/ColorProfiles";

        [MenuItem("Tools/Relic Keeper/Visuals/Capture Color Profile Examples")]
        public static void CaptureExamples()
        {
            CaptureExample("hub", "Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab", "HubDay", false);
            CaptureExample("dungeon", "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab", "Mortfall", true);
        }

        private static void CaptureExample(string name, string prefabPath, string profileName, bool dungeon)
        {
            using (var stage = WorldColorPreviewStage.Create(dungeon, prefabPath, null))
            {
                stage.Controller.PreviewColorProfile = AssetDatabase.LoadAssetAtPath<WorldColorProfileSO>("Assets/Visuals/ColorProfiles/" + profileName + ".asset");
                stage.Controller.PreviewNeutralColors = true; Capture(stage.Camera, name + "-neutral.png");
                stage.Controller.PreviewNeutralColors = false; Capture(stage.Camera, name + "-profile.png");
            }
        }

        public static void CaptureSelectedPreview()
        {
            var window = Resources.FindObjectsOfTypeAll<WorldColorProfilesWindow>().FirstOrDefault();
            if (window == null || window.ActivePreviewCamera == null) throw new System.InvalidOperationException("Open a color profile preview first.");
            Capture(window.ActivePreviewCamera, "selected-preview.png");
        }

        public static void CapturePlayMode()
        {
            if (!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
            var controller = Camera.main != null ? Camera.main.GetComponent<AtmosphereController>() : null;
            if (controller == null) throw new System.InvalidOperationException("No active gameplay atmosphere camera.");
            var camera = controller.GetComponent<Camera>();
            var previous = controller.PreviewColorProfile; bool neutral = controller.PreviewNeutralColors;
            try
            {
                controller.RefreshColorGroups();
                controller.PreviewNeutralColors = true; Capture(camera, "play-hub-neutral.png");
                controller.PreviewNeutralColors = false; Capture(camera, "play-hub-profile.png");
                controller.PreviewColorProfile = AssetDatabase.LoadAssetAtPath<WorldColorProfileSO>("Assets/Visuals/ColorProfiles/Mortfall.asset");
                Capture(camera, "play-profile-switch.png");
                Debug.Log("[WorldColorProfiles] Live camera captured neutral, assigned, and switched profiles; restoring original preview state.");
            }
            finally { controller.PreviewColorProfile = previous; controller.PreviewNeutralColors = neutral; }
        }

        // Capture exactly what the camera sees, without first refreshing/repairing bindings.
        public static void CaptureCurrentWorld()
        {
            var controller = Camera.main != null ? Camera.main.GetComponent<AtmosphereController>() : null;
            if (controller == null) throw new System.InvalidOperationException("No active atmosphere camera.");
            Capture(controller.GetComponent<Camera>(), "transition-current.png");
            var report = new System.Text.StringBuilder();
            report.AppendLine("Playing=" + EditorApplication.isPlaying + "; bindings=" + WorldColorGrading.BoundRendererCount);
            foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != controller.gameObject.scene) continue;
                Material material = renderer.sharedMaterial;
                report.AppendLine(renderer.name + " | " + (material != null ? material.name : "MISSING") + " | " +
                    (material != null && material.shader != null ? material.shader.name : "MISSING SHADER") + " | " +
                    (material != null ? AssetDatabase.GetAssetPath(material) : ""));
            }
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "transition-materials.txt"), report.ToString());
        }

        private static void Capture(Camera camera, string file)
        {
            var target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Path.Combine(Output, file), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(target); Object.DestroyImmediate(image); }
        }
    }
}
