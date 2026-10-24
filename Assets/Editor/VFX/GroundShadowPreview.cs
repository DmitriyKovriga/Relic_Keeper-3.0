using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RelicKeeper.Editor.Visuals;
using Scripts.Dungeon;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.VFX
{
    // Temporary Play Mode study; does not move the real player or write scene/save assets.
    public static class GroundShadowPreview
    {
        [MenuItem("Tools/Relic Keeper/Visuals/Capture Contact Shadow Study")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            foreach (string character in new[] { "Warrior", "Rogue", "Mage" }) CaptureCharacter(character);
            CaptureEnvironment(true);
            CaptureEnvironment(false);
        }

        private static void CaptureEnvironment(bool mortfall)
        {
            var scene = SceneManager.GetActiveScene();
            var root = new GameObject("Temporary environment contact study");
            var target = new RenderTexture(480, 270, 24) { filterMode = FilterMode.Point };
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var controllers = new List<GroundingVisualController>();
            RenderTexture previous = RenderTexture.active;
            using (var stage = WorldColorPreviewStage.Create(mortfall, WorldColorPreviewStage.GetMortfallRooms()[0], null))
            {
                try
                {
                    // Reuse the visual-only sample, never instantiate dungeon gameplay.
                    // Place temporary colliders in the shared physics scene for raycasts.
                    var data = AssetDatabase.LoadAssetAtPath<DungeonDataSO>(WorldColorPreviewStage.MortfallDataPath);
                    var source = mortfall ? data.LoadRoomPrefab(WorldColorPreviewStage.GetMortfallRooms()[0])
                        : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab");
                    var groundNames = source.GetComponentsInChildren<TilemapCollider2D>(true).Select(x => x.name).ToHashSet();
                    foreach (var go in stage.Scene.GetRootGameObjects())
                    { SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(root.transform, true); }
                    foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                        if (groundNames.Contains(map.name)) map.gameObject.AddComponent<TilemapCollider2D>();
                    foreach (var box in source.GetComponentsInChildren<BoxCollider2D>(true))
                    {
                        var copy = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == box.name);
                        if (copy != null) EditorUtility.CopySerialized(box, copy.gameObject.AddComponent<BoxCollider2D>());
                    }
                    var actors = root.transform.Find("Static character samples");
                    if (actors.Find("Player sample") == null)
                    {
                        Physics2D.SyncTransforms();
                        var rogue = Resources.Load<CharacterDataSO>("Heroes/RogueResources/Rogue");
                        var body = new GameObject("Rogue contact sample").AddComponent<SpriteRenderer>();
                        body.transform.SetParent(actors, false); body.sprite = rogue.RunAnimation.Frames[0];
                        body.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
                        body.sortingLayerName = WorldRenderSorting.LayerHero;
                        Vector3 spawn = mortfall ? source.GetComponent<RoomController>().PlayerSpawnPosition : new Vector3(7, 0.8f);
                        var hit = Physics2D.Raycast(spawn + Vector3.up, Vector2.down, 20f, 1 << LayerMask.NameToLayer("Ground"));
                        body.transform.position = new Vector3(spawn.x, (hit.collider != null ? hit.point.y : spawn.y) - body.bounds.min.y + 0.02f);
                    }
                    foreach (var body in actors.GetComponentsInChildren<SpriteRenderer>())
                    {
                        var collider = body.gameObject.AddComponent<BoxCollider2D>();
                        collider.size = new Vector2(0.7f, 1.4f);
                        collider.offset = new Vector2(0, body.bounds.min.y - body.transform.position.y + 0.7f);
                        controllers.Add(body.gameObject.AddComponent<GroundingVisualController>());
                    }
                    root.transform.position = Vector3.right * 2000f;
                    Physics2D.SyncTransforms();
                    var update = typeof(GroundingVisualController).GetMethod("UpdateShadowVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var controller in controllers) update.Invoke(controller, null);
                    stage.Camera.scene = scene;
                    target.Create();
                    RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    File.WriteAllBytes("Logs/ContactShadows/" + (mortfall ? "Mortfall" : "Hub") + ".png", image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    RemoveShadows(controllers);
                    Object.DestroyImmediate(root); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
            }
        }

        private static void CaptureCharacter(string character)
        {
            var root = new GameObject("Temporary contact study");
            var target = new RenderTexture(480, 270, 24) { filterMode = FilterMode.Point };
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var controllers = new List<GroundingVisualController>();
            RenderTexture previous = RenderTexture.active;
            try
            {
                var data = Resources.Load<CharacterDataSO>("Heroes/" + character + "Resources/" + character);
                var frames = data.RunAnimation.Frames;
                if (frames == null || frames.Length == 0) throw new InvalidOperationException("Missing character study sprite.");
                // Left to right: contact, low jump, distant floor, ledge, slope.
                for (int i = 0; i < 5; i++)
                {
                    float x = 2000f + i * 3.6f;
                    var floor = new GameObject("Study floor " + i);
                    floor.transform.SetParent(root.transform);
                    floor.layer = LayerMask.NameToLayer("Ground");
                    floor.transform.position = new Vector3(x + (i == 3 ? -1.25f : 0f), -0.5f);
                    floor.transform.rotation = Quaternion.Euler(0, 0, i == 4 ? 15 : 0);
                    floor.AddComponent<BoxCollider2D>().size = new Vector2(2.8f, 1f);
                    var ground = floor.AddComponent<SpriteRenderer>();
                    ground.sprite = pixel; ground.sharedMaterial = material;
                    ground.drawMode = SpriteDrawMode.Sliced; ground.size = new Vector2(2.8f, 1f);
                    ground.color = new Color(0.32f, 0.34f, 0.31f);
                    var actor = new GameObject(character + " contact study " + i);
                    actor.transform.SetParent(root.transform);
                    float height = i == 1 ? 1f : i == 2 ? 3f : 0f;
                    actor.transform.position = new Vector3(x, 0.75f + height);
                    actor.AddComponent<BoxCollider2D>().size = new Vector2(0.7f, 1.4f);
                    var visual = new GameObject("Body").AddComponent<SpriteRenderer>();
                    visual.transform.SetParent(actor.transform, false);
                    visual.sprite = frames[0]; visual.sharedMaterial = material;
                    visual.sortingLayerName = WorldRenderSorting.LayerHero;
                    visual.transform.position += Vector3.up * (0.05f + height - visual.bounds.min.y);
                    controllers.Add(actor.AddComponent<GroundingVisualController>());
                }
                Physics2D.SyncTransforms();
                var update = typeof(GroundingVisualController).GetMethod("UpdateShadowVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var controller in controllers) update.Invoke(controller, null);
                var camera = new GameObject("Contact study camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform); camera.transform.position = new Vector3(2007.2f, 2f, -10f);
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 5.625f;
                camera.aspect = 480f / 270f; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.15f, 0.17f);
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Directory.CreateDirectory("Logs/ContactShadows");
                File.WriteAllBytes("Logs/ContactShadows/" + character + ".png", image.EncodeToPNG());
                var shaded = image.GetPixels32();
                var report = new System.Text.StringBuilder();
                foreach (var controller in controllers)
                {
                    var shadow = (SpriteRenderer)typeof(GroundingVisualController).GetField("_shadowRenderer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                    report.AppendLine(controller.name + ": enabled=" + shadow.enabled + ", bounds=" + shadow.bounds + ", alpha=" + shadow.color.a);
                    shadow.enabled = false;
                }
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                var baseline = image.GetPixels32();
                int darkened = 0;
                for (int i = 0; i < shaded.Length; i++) if (baseline[i].r - shaded[i].r >= 10) darkened++;
                report.AppendLine("Pixels darkened by at least 10/255: " + darkened);
                File.WriteAllBytes("Logs/ContactShadows/" + character + "-no-shadow.png", image.EncodeToPNG());
                File.WriteAllText("Logs/ContactShadows/" + character + "-visibility.txt", report.ToString());
            }
            finally
            {
                RenderTexture.active = previous;
                // The shadow is a sibling of its actor; remove it before actor OnDestroy.
                RemoveShadows(controllers);
                Object.DestroyImmediate(root); Object.DestroyImmediate(material); Object.DestroyImmediate(pixel);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        private static void RemoveShadows(List<GroundingVisualController> controllers)
        {
            foreach (var controller in controllers)
            {
                var field = typeof(GroundingVisualController).GetField("_shadowObject", BindingFlags.Instance | BindingFlags.NonPublic);
                var shadow = field.GetValue(controller) as GameObject;
                field.SetValue(controller, null);
                if (shadow != null) Object.DestroyImmediate(shadow);
            }
        }
    }
}
