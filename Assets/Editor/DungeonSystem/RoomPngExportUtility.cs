using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Dungeons
{
    public readonly struct RoomExportEntry
    {
        public readonly string Path;
        public readonly string Role;
        public RoomExportEntry(string path, string role) { Path = path; Role = role; }
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    public readonly struct RoomExportFrame
    {
        public readonly Rect WorldRect;
        public readonly int Width, Height;
        public RoomExportFrame(Rect rect, int width, int height) { WorldRect = rect; Width = width; Height = height; }
    }

    public static class RoomPngExportUtility
    {
        public const int PixelsPerUnit = 24;
        public const long MaximumOutputPixels = 16000000;

        public static string NormalizeRoomPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            path = path.Trim().Replace('\\', '/');
            if (path.StartsWith("Assets/", StringComparison.Ordinal)) return path;
            int index = path.IndexOf("Resources/", StringComparison.Ordinal);
            if (index >= 0) path = path.Substring(index + "Resources/".Length);
            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) path += ".prefab";
            return "Assets/Resources/" + path;
        }

        public static List<RoomExportEntry> GetRooms(DungeonDataSO dungeon)
        {
            var paths = new List<string>();
            var roles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            if (dungeon == null) return new List<RoomExportEntry>();
            void Add(string rawPath, string role)
            {
                string path = NormalizeRoomPath(rawPath);
                if (path.Length == 0) return;
                if (!roles.TryGetValue(path, out var tags))
                {
                    paths.Add(path);
                    roles[path] = tags = new List<string>();
                }
                if (!tags.Contains(role)) tags.Add(role);
            }
            foreach (string path in dungeon.OpeningRoomPrefabPaths) Add(path, "Opening");
            foreach (string path in dungeon.NormalRoomPrefabPaths) Add(path, "Normal");
            Add(dungeon.BossRoomPrefabPath, "Boss");
            return paths.Select(path => new RoomExportEntry(path, string.Join(" / ", roles[path]))).ToList();
        }

        public static RoomExportFrame CalculateFrame(Bounds bounds, int paddingPixels, int scale)
        {
            if (scale != 1 && scale != 2 && scale != 4) throw new ArgumentOutOfRangeException(nameof(scale));
            if (paddingPixels < 0) throw new ArgumentOutOfRangeException(nameof(paddingPixels));
            if (bounds.size.x <= 0f || bounds.size.y <= 0f ||
                float.IsNaN(bounds.min.x) || float.IsNaN(bounds.max.y) ||
                float.IsInfinity(bounds.min.x) || float.IsInfinity(bounds.max.y))
                throw new ArgumentException("Room has no finite visible bounds.");
            double left = Math.Floor(bounds.min.x * PixelsPerUnit) - paddingPixels;
            double bottom = Math.Floor(bounds.min.y * PixelsPerUnit) - paddingPixels;
            double right = Math.Ceiling(bounds.max.x * PixelsPerUnit) + paddingPixels;
            double top = Math.Ceiling(bounds.max.y * PixelsPerUnit) + paddingPixels;
            double width = right - left, height = top - bottom;
            if (width * scale > SystemInfo.maxTextureSize || height * scale > SystemInfo.maxTextureSize ||
                width * height * scale * scale > MaximumOutputPixels)
                throw new InvalidOperationException("Export exceeds the texture/memory limit. Reduce padding or use 1× scale.");
            return new RoomExportFrame(new Rect((float)left / PixelsPerUnit, (float)bottom / PixelsPerUnit,
                (float)width / PixelsPerUnit, (float)height / PixelsPerUnit), (int)width, (int)height);
        }

        // Modify only the isolated clone; never run room entry, enemy spawning or gameplay.
        public static void PrepareRoom(GameObject root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) behaviour.enabled = false;
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
            {
                var clip = animator.runtimeAnimatorController?.animationClips.FirstOrDefault();
                if (clip != null && animator.gameObject.activeInHierarchy) clip.SampleAnimation(animator.gameObject, 0f);
                animator.enabled = false;
            }
            // Animation curves may change child activation/script properties. Hide
            // actors and enforce disabled gameplay after the static pose is sampled.
            foreach (EnemyEntity enemy in root.GetComponentsInChildren<EnemyEntity>(true)) enemy.gameObject.SetActive(false);
            foreach (EnemyHealth enemy in root.GetComponentsInChildren<EnemyHealth>(true)) enemy.gameObject.SetActive(false);
            foreach (PlayerStats player in root.GetComponentsInChildren<PlayerStats>(true)) player.gameObject.SetActive(false);
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null) behaviour.enabled = false;
        }

        public static Bounds GetVisibleBounds(GameObject root)
        {
            Bounds? combined = null;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                Bounds bounds = renderer.bounds;
                if (renderer is TilemapRenderer)
                {
                    Tilemap map = renderer.GetComponent<Tilemap>();
                    if (map == null || map.GetUsedTilesCount() == 0) continue;
                }
                if (bounds.size.x <= 0f || bounds.size.y <= 0f) continue;
                if (combined.HasValue) { Bounds value = combined.Value; value.Encapsulate(bounds); combined = value; }
                else combined = bounds;
            }
            return combined ?? throw new InvalidOperationException("Room contains no visible environment renderers.");
        }

        public static Bounds GetCaptureBounds(GameObject root, bool includeOuterTerrain = false)
        {
            if (!includeOuterTerrain)
            {
                var room = root.GetComponentInChildren<RoomController>(true);
                Collider2D source = room != null
                    ? new SerializedObject(room).FindProperty("_cameraBoundsSource")?.objectReferenceValue as Collider2D
                    : null;
                if (source == null)
                    source = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "CameraBounds")?.GetComponent<Collider2D>();
                // Bounds on preview-scene colliders need not have been synchronized by
                // Physics2D. Resolve serialized geometry without touching live physics.
                var points = new List<Vector2>();
                if (source is BoxCollider2D box)
                {
                    Vector2 half = box.size / 2f;
                    points.Add(box.offset + new Vector2(-half.x, -half.y));
                    points.Add(box.offset + new Vector2(half.x, -half.y));
                    points.Add(box.offset + new Vector2(half.x, half.y));
                    points.Add(box.offset + new Vector2(-half.x, half.y));
                }
                else if (source is PolygonCollider2D polygon)
                {
                    for (int i = 0; i < polygon.pathCount; i++)
                        points.AddRange(polygon.GetPath(i).Select(point => point + polygon.offset));
                }
                if (points.Count > 0)
                {
                    var bounds = new Bounds(source.transform.TransformPoint(points[0]), Vector3.zero);
                    foreach (Vector2 point in points) bounds.Encapsulate(source.transform.TransformPoint(point));
                    if (bounds.size.x > 0f && bounds.size.y > 0f) return bounds;
                }
                else if (source != null && source.bounds.size.x > 0f && source.bounds.size.y > 0f) return source.bounds;
            }
            return GetVisibleBounds(root);
        }

        public static Color32[] Upscale(Color32[] pixels, int width, int height, int scale)
        {
            if (pixels == null || pixels.Length != width * height) throw new ArgumentException("Invalid pixel array.");
            if (scale != 1 && scale != 2 && scale != 4) throw new ArgumentOutOfRangeException(nameof(scale));
            if ((long)width * height * scale * scale > MaximumOutputPixels) throw new ArgumentException("Image is too large.");
            if (scale == 1) return pixels;
            var result = new Color32[width * height * scale * scale];
            int outputWidth = width * scale;
            for (int y = 0; y < height * scale; y++)
                for (int x = 0; x < outputWidth; x++) result[y * outputWidth + x] = pixels[(y / scale) * width + x / scale];
            return result;
        }

        public static string Export(DungeonDataSO dungeon, RoomExportEntry entry, string folder, int scale = 1, int paddingPixels = 24, bool includeOuterTerrain = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before exporting rooms.");
            if (dungeon == null) throw new ArgumentNullException(nameof(dungeon));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);
            if (prefab == null) throw new FileNotFoundException("Room prefab not found: " + entry.Path);
            Sprite background = dungeon.LoadBackgroundSprite();
            if (background == null) throw new InvalidOperationException("Assign the dungeon Background Sprite Resource Path before exporting.");
            folder = System.IO.Path.GetFullPath(folder);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D readback = null, output = null;
            Material neutral = null;
            Sprite tiledBackground = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                PrepareRoom(root);
                RoomExportFrame frame = CalculateFrame(GetCaptureBounds(root, includeOuterTerrain), paddingPixels, scale);

                // Standard room art uses a white global light in game. Use its equivalent
                // unlit material so this preview cannot depend on lighting in the open scene.
                neutral = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        if (materials[i] != null && materials[i].shader.name == "Universal Render Pipeline/2D/Sprite-Lit-Default")
                            materials[i] = neutral;
                    renderer.sharedMaterials = materials;
                }
                var backgroundObject = new GameObject("Export background");
                SceneManager.MoveGameObjectToScene(backgroundObject, scene);
                var backdrop = backgroundObject.AddComponent<SpriteRenderer>();
                // Tiled rendering requires a FullRect mesh. Make a temporary sprite
                // instead of altering the source texture's import settings.
                tiledBackground = Sprite.Create(background.texture, background.rect,
                    new Vector2(background.pivot.x / background.rect.width, background.pivot.y / background.rect.height),
                    background.pixelsPerUnit, 0, SpriteMeshType.FullRect, background.border);
                backdrop.sprite = tiledBackground;
                backdrop.sharedMaterial = neutral;
                backdrop.sortingLayerName = "Background";
                backdrop.sortingOrder = short.MinValue;
                backdrop.drawMode = SpriteDrawMode.Tiled;
                backdrop.tileMode = SpriteTileMode.Continuous;
                backdrop.size = frame.WorldRect.size;
                backdrop.transform.position = new Vector3(frame.WorldRect.center.x, frame.WorldRect.center.y, 10f);

                var cameraObject = new GameObject("Room export camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = frame.WorldRect.height / 2f;
                camera.aspect = frame.Width / (float)frame.Height;
                camera.transform.position = new Vector3(frame.WorldRect.center.x, frame.WorldRect.center.y, -100f);
                camera.farClipPlane = 300f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.volumeLayerMask = 0;
                target = new RenderTexture(frame.Width, frame.Height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
                target.Create();
                if (!RenderPipeline.SupportsRenderRequest(camera, new RenderPipeline.StandardRequest()))
                    throw new InvalidOperationException("The active render pipeline cannot render this export camera.");
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                readback = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, frame.Width, frame.Height), 0, 0);
                readback.Apply();
                output = new Texture2D(frame.Width * scale, frame.Height * scale, TextureFormat.RGBA32, false);
                output.SetPixels32(Upscale(readback.GetPixels32(), frame.Width, frame.Height, scale));
                output.Apply();
                Directory.CreateDirectory(folder);
                string name = string.Concat(entry.Name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string file = System.IO.Path.Combine(folder, name + ".png");
                File.WriteAllBytes(file, output.EncodeToPNG());
                return file;
            }
            finally
            {
                RenderTexture.active = previous;
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
                if (output != null) Object.DestroyImmediate(output);
                if (neutral != null) Object.DestroyImmediate(neutral);
                if (tiledBackground != null) Object.DestroyImmediate(tiledBackground);
            }
        }
    }
}
