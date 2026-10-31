using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using RelicKeeper.Editor.Visuals;
using Scripts.Dungeon;
using Scripts.Skills;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Art
{
    // G00 evidence only. Never instantiates room gameplay or writes source assets/saves.
    public static class GraphicsRefreshBaselineCapture
    {
        public const string RoomPath = "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_001.prefab";
        public const string Output = "Docs/Art/GraphicsRefreshPlan/Baseline";

        [Serializable] public sealed class Reference
        {
            public string path, guid; public long localId;
            public Reference(Object asset)
            {
                path = AssetDatabase.GetAssetPath(asset);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId);
            }
        }
        [Serializable] public sealed class TransformRecord
        {
            public string path; public Vector3 position, scale; public Quaternion rotation;
            public TransformRecord(Transform t, Transform root)
            { path = HierarchyPath(t, root); position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
        }
        [Serializable] public sealed class ObjectState
        {
            public int layer; public string tag; public bool activeSelf;
            public ObjectState(GameObject go) { layer = go.layer; tag = go.tag; activeSelf = go.activeSelf; }
        }
        [Serializable] public sealed class ComponentRecord
        {
            public TransformRecord transform; public ObjectState state; public string type, serialized;
            public ComponentRecord(Component c, Transform root)
            { transform = new TransformRecord(c.transform, root); state = new ObjectState(c.gameObject); type = c.GetType().FullName; serialized = EditorJsonUtility.ToJson(c); }
        }
        [Serializable] public sealed class CellRecord
        { public Vector3Int cell; public Reference tile; public string colliderType; public Matrix4x4 matrix; }
        [Serializable] public sealed class MapRecord
        { public TransformRecord transform; public ObjectState state; public Vector3 anchor; public List<CellRecord> occupied = new List<CellRecord>(); }
        [Serializable] public sealed class RoomSnapshot
        {
            public Reference room; public TransformRecord transform; public ObjectState state;
            public List<MapRecord> maps = new List<MapRecord>();
            public List<ComponentRecord> grids = new List<ComponentRecord>(), collision = new List<ComponentRecord>(), spawns = new List<ComponentRecord>(), portals = new List<ComponentRecord>();
            public List<string> ignoredCollisionLayerPairs = new List<string>();
        }
        [Serializable] public sealed class AssetHash
        { public string path, guid, sha256; }
        [Serializable] public sealed class AppearanceRecord
        { public Reference profile; public string serialized; }
        [Serializable] public sealed class ViewRecord
        {
            public string name, png; public Vector3 cameraPosition; public Quaternion cameraRotation;
            public float orthographicSize, aspect; public int width = 480, height = 270;
            public List<TransformRecord> actors = new List<TransformRecord>();
        }
        [Serializable] public sealed class Manifest
        {
            public string capturedUtc, unity, sourceRoomSha256, sourceRoomSha256After, scene, limits, hud;
            public Reference room; public TransformRecord roomTransform;
            public List<ViewRecord> views = new List<ViewRecord>();
            public List<AppearanceRecord> appearance = new List<AppearanceRecord>();
            public List<ComponentRecord> lights = new List<ComponentRecord>();
            public List<AssetHash> sources = new List<AssetHash>();
        }

        public static RoomSnapshot Snapshot(GameObject room)
        {
            var snapshot = new RoomSnapshot { room = new Reference(room), transform = new TransformRecord(room.transform, room.transform), state = new ObjectState(room) };
            for (int a = 0; a < 32; a++) snapshot.ignoredCollisionLayerPairs.Add(string.Concat(Enumerable.Range(0, 32).Select(b => Physics2D.GetIgnoreLayerCollision(a, b) ? "1" : "0")));
            foreach (var map in room.GetComponentsInChildren<Tilemap>(true).OrderBy(x => HierarchyPath(x.transform, room.transform)))
            {
                var record = new MapRecord { transform = new TransformRecord(map.transform, room.transform), state = new ObjectState(map.gameObject), anchor = map.tileAnchor };
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(cell); if (tile == null) continue;
                    record.occupied.Add(new CellRecord { cell = cell, tile = new Reference(tile), colliderType = map.GetColliderType(cell).ToString(), matrix = map.GetTransformMatrix(cell) });
                }
                snapshot.maps.Add(record);
            }
            foreach (var component in room.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                if (component is Grid) snapshot.grids.Add(new ComponentRecord(component, room.transform));
                if (component is Collider2D || component is Rigidbody2D || component is PlatformEffector2D)
                    snapshot.collision.Add(new ComponentRecord(component, room.transform));
                if (component is EnemySpawner || component.GetType().Name == "PlayerSpawnPoint")
                    snapshot.spawns.Add(new ComponentRecord(component, room.transform));
                if (component is DungeonPortal) snapshot.portals.Add(new ComponentRecord(component, room.transform));
            }
            return snapshot;
        }

        public static void Capture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Capture in Edit Mode; source assets remain untouched.");
            if (File.Exists(Path.Combine(Output, "Framing.json"))) throw new InvalidOperationException("The comparison baseline is frozen. Archive it explicitly before starting a different baseline.");
            Directory.CreateDirectory(Output);
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath);
            if (room == null) throw new InvalidOperationException("Room 001 not found.");
            var manifest = new Manifest
            {
                capturedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                sourceRoomSha256 = Hash(RoomPath), room = new Reference(room), roomTransform = new TransformRecord(room.transform, room.transform),
                scene = SceneManager.GetActiveScene().path,
                limits = "Native isolated World Appearance visual copies, static source sprites and staged HUD. No AI, physics, gameplay progression or save writes. Not a live gameplay or performance validation."
            };
            WriteSnapshot(room);
            foreach (var light in room.GetComponentsInChildren<LocalLight2DSource>(true)) manifest.lights.Add(new ComponentRecord(light, room.transform));
            foreach (var shaft in room.GetComponentsInChildren<LocalLightShaftSource>(true)) manifest.lights.Add(new ComponentRecord(shaft, room.transform));
            foreach (var path in AssetDatabase.GetDependencies(RoomPath, true)
                .Concat(new[] { WorldColorPreviewStage.MortfallDataPath, "Assets/Scripts/Visuals/Editor/WorldColorPreviewStage.cs", "Assets/Scripts/Visuals/Tiles/MortfallWallRuleTile.cs", "Assets/Scripts/Visuals/Tiles/MortfallWallSupport.cs" }).Distinct().OrderBy(x => x))
                if (File.Exists(path)) manifest.sources.Add(new AssetHash { path = path, guid = AssetDatabase.AssetPathToGUID(path), sha256 = Hash(path) });
            using (var stage = WorldColorPreviewStage.Create(true, RoomPath, null))
            {
                foreach (Object profile in new Object[] { stage.AssignedProfile, stage.Controller.ActiveDepthMistProfile })
                    if (profile != null) manifest.appearance.Add(new AppearanceRecord { profile = new Reference(profile), serialized = EditorJsonUtility.ToJson(profile) });
                GameObject hud = CopyHud(stage);
                manifest.hud = hud != null ? "Visual clone of the open scene's HUD Canvas; six actual slots initialized with staged Ninja Stub, Venom Strike and Ice Skates icons; health/mana 50/50 and level 30 are serialized fixture values. Controller scripts removed; same artwork/text in both views. Not a player loadout or balance claim." : "No HUD source found in open scene; comparison is world-only.";
                stage.Camera.transform.position += new Vector3(3, -1, 0);
                CaptureView(stage, "Entrance", manifest);
                CaptureTerrain(stage);
                var portal = room.GetComponentsInChildren<DungeonPortal>(true).First(x => x.Type == PortalType.NextRoom);
                stage.Camera.transform.position = new Vector3(portal.transform.position.x - 3f, portal.transform.position.y + .5f, -10f);
                CaptureView(stage, "Exit", manifest);
            }
            manifest.sourceRoomSha256After = Hash(RoomPath);
            if (manifest.sourceRoomSha256After != manifest.sourceRoomSha256) throw new InvalidOperationException("Source room changed during capture.");
            Write("Framing.json", manifest);
            File.WriteAllText(Path.Combine(Output, "Report.txt"), "PASS: two native visual-only views; room hash preserved; semantic tile/collision/spawn/portal snapshot recorded. Gameplay and performance not exercised.");
        }

        private static GameObject CopyHud(WorldColorPreviewStage stage)
        {
            var source = Object.FindObjectsByType<HUDController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(x => !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(x.gameObject.scene));
            var canvas = source != null ? source.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return null;
            var clone = Object.Instantiate(canvas.gameObject);
            clone.name = "Graphics comparison HUD (visual only)";
            SceneManager.MoveGameObjectToScene(clone, stage.Scene);
            var controller = clone.GetComponentInChildren<HUDController>(true);
            typeof(HUDController).GetMethod("EnsureSkillSlots", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
            var slots = (UISkillSlot[])typeof(HUDController).GetField("_skillSlots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            string[] skills = { "Skills/1HWeapon/Dagger/NinjaStubSkill", "Skills/1HWeapon/Dagger/VenomStrike/VenomStrikeSkill", null, null, null, "Skills/Boots/Mystic/IceSkates" };
            for (int i = 0; i < slots.Length; i++)
            {
                typeof(UISkillSlot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(slots[i], null);
                slots[i].Setup(skills[i] != null ? Resources.Load<SkillDataSO>(skills[i]) : null, i == 0 ? "MOUSE1" : i == 1 ? "MOUSE2" : "F", i);
            }
            foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && !(script is UnityEngine.EventSystems.UIBehaviour)) Object.DestroyImmediate(script);
            var target = clone.GetComponent<Canvas>();
            target.renderMode = RenderMode.ScreenSpaceCamera; target.worldCamera = stage.Camera; target.planeDistance = 1; target.sortingOrder = 100;
            Canvas.ForceUpdateCanvases();
            return clone;
        }

        private static void CaptureTerrain(WorldColorPreviewStage stage)
        {
            var maps = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Tilemap>(true))
                .Where(x => x.name == "Ground" || x.name == "Platforms").ToArray();
            int[] layers = maps.Select(x => x.gameObject.layer).ToArray();
            int oldMask = stage.Camera.cullingMask; Color oldClear = stage.Camera.backgroundColor;
            try
            {
                foreach (var map in maps) map.gameObject.layer = 31;
                stage.Camera.cullingMask = 1 << 31; stage.Camera.backgroundColor = Color.clear;
                CaptureView(stage, "TerrainAlpha", new Manifest());
            }
            finally
            {
                for (int i = 0; i < maps.Length; i++) maps[i].gameObject.layer = layers[i];
                stage.Camera.cullingMask = oldMask; stage.Camera.backgroundColor = oldClear;
            }
        }

        // Alpha-only evidence masks. Rebuild disposable copies, never recapture or overwrite the frozen comparison image.
        public static void CaptureProtectionMasks()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Output, "Framing.json")));
            if (Hash(RoomPath) != manifest.sourceRoomSha256) throw new InvalidOperationException("The source room no longer matches the frozen baseline.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPath);
            WriteSnapshot(source);
            manifest.lights.Clear();
            foreach (var light in source.GetComponentsInChildren<LocalLight2DSource>(true)) manifest.lights.Add(new ComponentRecord(light, source.transform));
            foreach (var shaft in source.GetComponentsInChildren<LocalLightShaftSource>(true)) manifest.lights.Add(new ComponentRecord(shaft, source.transform));
            using (var stage = WorldColorPreviewStage.Create(true, RoomPath, null))
            {
                var hud = CopyHud(stage);
                stage.Camera.transform.SetPositionAndRotation(manifest.views[0].cameraPosition, manifest.views[0].cameraRotation);
                stage.Camera.orthographicSize = manifest.views[0].orthographicSize;
                stage.Camera.backgroundColor = Color.clear;
                foreach (var root in stage.Scene.GetRootGameObjects())
                {
                    if (root.name == "Static character samples") foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 29;
                    foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
                        if (map.name == "Ground" || map.name == "Platforms") map.gameObject.layer = 31;
                        else if (map.name == "Environment") map.gameObject.layer = 30;
                }
                if (hud == null) throw new InvalidOperationException("Missing HUD source.");
                foreach (var t in hud.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 28;
                foreach (var pair in new[] { ("HudAlpha", 28), ("ActorAlpha", 29), ("DecorationAlpha", 30) })
                {
                    stage.Camera.cullingMask = 1 << pair.Item2;
                    CaptureView(stage, pair.Item1, new Manifest());
                }
            }
            var paths = manifest.appearance.Select(x => x.profile.path).Concat(new[]
            {
                manifest.scene, "Assets/UI/Inventory/PixelArtPanelSettings.asset",
                "Assets/Visuals/MortfallSprites.spriteatlas", "Assets/Editor/Art/GraphicsRefreshBaselineCapture.cs",
                "ProjectSettings/Physics2DSettings.asset", "ProjectSettings/TagManager.asset"
            }).Concat(manifest.sources.Select(x => x.path)).Distinct().ToArray();
            manifest.sources = paths.Concat(paths.Select(x => x + ".meta")).Distinct().OrderBy(x => x)
                .Where(File.Exists).Select(x => new AssetHash { path = x, guid = AssetDatabase.AssetPathToGUID(x), sha256 = Hash(x) }).ToList();
            Write("Framing.json", manifest);
        }

        [Serializable] public sealed class ConceptRecord
        { public string id, rawSha256, nativeSha256; public int rawWidth, rawHeight, protectedPixelMismatches; }
        [Serializable] public sealed class ConceptAudit
        {
            public string method = "Built-in image-generation paintovers, nearest-neighbor native resampling, exact frozen-source pixels restored inside captured terrain/HUD/actor/decoration alpha masks plus one-pixel clearance. These are concepts, not Unity integration or a physical geometry test.";
            public string baselineSha256, protectedMaskSha256;
            public int protectedPixels; public List<ConceptRecord> concepts = new List<ConceptRecord>();
        }

        public static void PrepareConcepts()
        {
            const string concepts = "Docs/Art/GraphicsRefreshPlan/Concepts";
            var images = new List<Texture2D>();
            try
            {
                Texture2D Load(string path) { var t = new Texture2D(2, 2, TextureFormat.RGBA32, false); images.Add(t); if (!t.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException(path); return t; }
                var source = Load(Path.Combine(Output, "Entrance.png")); var original = source.GetPixels32();
                var mask = new bool[480 * 270];
                foreach (string name in new[] { "TerrainAlpha", "HudAlpha", "ActorAlpha", "DecorationAlpha" })
                {
                    var alpha = Load(Path.Combine(Output, name + ".png"));
                    if (alpha.width != 480 || alpha.height != 270) throw new InvalidOperationException("Mask size changed.");
                    var colors = alpha.GetPixels32();
                    for (int i = 0; i < mask.Length; i++) if (colors[i].a > 0) mask[i] = true;
                }
                var expanded = (bool[])mask.Clone();
                for (int y = 0; y < 270; y++) for (int x = 0; x < 480; x++)
                    if (mask[y * 480 + x])
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                            if (x + dx >= 0 && x + dx < 480 && y + dy >= 0 && y + dy < 270) expanded[(y + dy) * 480 + x + dx] = true;
                var audit = new ConceptAudit { protectedPixels = expanded.Count(x => x), baselineSha256 = Hash(Path.Combine(Output, "Entrance.png")) };
                var maskPixels = expanded.Select(x => x ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 255)).ToArray();
                SavePixels(Path.Combine(concepts, "ProtectedPixels.png"), maskPixels, images);
                audit.protectedMaskSha256 = Hash(Path.Combine(concepts, "ProtectedPixels.png"));
                foreach (string id in new[] { "A", "B", "C" })
                {
                    string path = Path.Combine(concepts, "Raw", id + ".png"); var raw = Load(path); var rawPixels = raw.GetPixels32();
                    var output = new Color32[mask.Length];
                    for (int y = 0; y < 270; y++) for (int x = 0; x < 480; x++)
                    {
                        int i = y * 480 + x;
                        output[i] = expanded[i] ? original[i] : rawPixels[Mathf.Min(raw.height - 1, (int)((y + .5f) * raw.height / 270)) * raw.width + Mathf.Min(raw.width - 1, (int)((x + .5f) * raw.width / 480))];
                    }
                    int mismatches = Enumerable.Range(0, mask.Length).Count(i => expanded[i] && !output[i].Equals(original[i]));
                    if (mismatches != 0) throw new InvalidOperationException("Protected pixels drifted.");
                    string target = Path.Combine(concepts, id + ".png"); SavePixels(target, output, images);
                    audit.concepts.Add(new ConceptRecord { id = id, rawSha256 = Hash(path), rawWidth = raw.width, rawHeight = raw.height, nativeSha256 = Hash(target), protectedPixelMismatches = mismatches });
                }
                File.WriteAllText(Path.Combine(concepts, "Preservation.json"), JsonUtility.ToJson(audit, true));
            }
            finally { foreach (var image in images) Object.DestroyImmediate(image); }
        }

        private static void SavePixels(string path, Color32[] pixels, List<Texture2D> owned)
        {
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false); owned.Add(image); image.SetPixels32(pixels); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }

        private static void CaptureView(WorldColorPreviewStage stage, string name, Manifest manifest)
        {
            var record = new ViewRecord { name = name, png = name + ".png", cameraPosition = stage.Camera.transform.position, cameraRotation = stage.Camera.transform.rotation, orthographicSize = stage.Camera.orthographicSize, aspect = stage.Camera.aspect };
            foreach (var root in stage.Scene.GetRootGameObjects())
                if (root.name == "Static character samples")
                    foreach (Transform child in root.transform) record.actors.Add(new TransformRecord(child, root.transform));
            var texture = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var pixels = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                texture.Create(); Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(stage.Camera, new RenderPipeline.StandardRequest { destination = texture });
                RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, record.png), pixels.EncodeToPNG());
                manifest.views.Add(record);
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); texture.Release(); Object.DestroyImmediate(texture); }
        }

        private static string HierarchyPath(Transform t, Transform root) => t == root ? root.name : HierarchyPath(t.parent, root) + "/" + t.name;
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        private static void WriteSnapshot(GameObject room)
        {
            using (var stream = new GZipStream(File.Create(Path.Combine(Output, "RoomSemanticSnapshot.json.gz")), CompressionMode.Compress))
            using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(Snapshot(room)));
        }
        private static void Write(string name, object data) => File.WriteAllText(Path.Combine(Output, name), JsonUtility.ToJson(data, true));
    }
}
