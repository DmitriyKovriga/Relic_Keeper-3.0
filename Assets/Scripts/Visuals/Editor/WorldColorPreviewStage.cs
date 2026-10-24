using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Visuals
{
    // Visual-only copies: no room controllers, spawners, AI, physics or save systems run here.
    internal sealed class WorldColorPreviewStage : IDisposable
    {
        internal const string MortfallDataPath = "Assets/Resources/Dungeons/MortFallDungeonSO.asset";
        private Scene _scene;
        private readonly List<Tilemap> _tiles = new List<Tilemap>();
        private readonly List<Tilemap> _ground = new List<Tilemap>();
        private readonly List<SpriteRenderer> _art = new List<SpriteRenderer>();
        private readonly Dictionary<Material, Material> _flatMaterials = new Dictionary<Material, Material>();
        private SpriteRenderer _background;
        private GameObject _actors;
        private Vector3 _spawn;
        private bool _mortfall;
        public Camera Camera { get; private set; }
        public AtmosphereController Controller { get; private set; }
        public WorldColorProfileSO AssignedProfile { get; private set; }
        public Scene Scene => _scene;

        public static string[] GetMortfallRooms()
        {
            var data = AssetDatabase.LoadAssetAtPath<DungeonDataSO>(MortfallDataPath);
            if (data == null) return Array.Empty<string>();
            return data.OpeningRoomPrefabPaths.Concat(data.NormalRoomPrefabPaths).Append(data.BossRoomPrefabPath)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
                .OrderBy(x => Path.GetFileNameWithoutExtension(x), StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static WorldColorPreviewStage Create(bool mortfall, string roomPath, AtmosphereController live)
        {
            var stage = new WorldColorPreviewStage();
            try { stage.Build(mortfall, roomPath, live); return stage; }
            catch { stage.Dispose(); throw; }
        }

        private GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }

        private void Build(bool mortfall, string roomPath, AtmosphereController live)
        {
            _mortfall = mortfall;
            _scene = EditorSceneManager.NewPreviewScene();
            var dungeon = AssetDatabase.LoadAssetAtPath<DungeonDataSO>(MortfallDataPath);
            GameObject prefab = mortfall ? dungeon?.LoadRoomPrefab(roomPath) :
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab");
            if (prefab == null) throw new InvalidOperationException("The selected preview artwork could not be loaded.");
            CopyVisuals(prefab.transform, null);
            _spawn = mortfall ? prefab.GetComponent<RoomController>().PlayerSpawnPosition : new Vector3(7f, 0.8f, 0f);

            Camera = NewObject("Color preview camera").AddComponent<Camera>();
            Camera.scene = _scene; Camera.enabled = false; Camera.orthographic = true;
            Camera.aspect = 480f / 270f; Camera.allowMSAA = false;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.09f, 0.12f, 0.14f);
            if (mortfall)
            {
                _background = NewObject("DungeonSharedBackground").AddComponent<SpriteRenderer>();
                _background.sprite = dungeon.LoadBackgroundSprite();
                _background.sharedMaterial = StockMaterial(false);
                _background.sortingLayerName = "Background"; _background.sortingOrder = -100;
                _background.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
            }
            _actors = NewObject("Static character samples");
            var player = Object.FindObjectsByType<global::PlayerStats>(FindObjectsSortMode.None).FirstOrDefault();
            SpriteRenderer body = player != null ? player.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(x => x.sprite != null && (x.name == "MovementSprite" || x.name == "Player")) : null;
            if (body != null) AddActor("Player sample", body.sprite, body.transform.lossyScale, _spawn.x, body.flipX);
            if (mortfall)
            {
                AddMonster("Enemy/SO_Zombie", _spawn.x + 1.25f);
                AddMonster("Enemy/SO_Mold", _spawn.x + 8f);
            }
            Controller = Camera.gameObject.AddComponent<AtmosphereController>();
            AssignedProfile = mortfall ? dungeon.ColorProfile : ReadProfile(live, "_hubColorProfile");
            if (AssignedProfile == null)
                AssignedProfile = AssetDatabase.LoadAssetAtPath<WorldColorProfileSO>("Assets/Visuals/ColorProfiles/" + (mortfall ? "Mortfall" : "HubDay") + ".asset");
            var fields = new SerializedObject(Controller);
            fields.FindProperty("_hubColorProfile").objectReferenceValue = AssignedProfile;
            fields.FindProperty("_colorLitShader").objectReferenceValue = Shader.Find("RelicKeeper/Sprites/World Color Lit");
            fields.FindProperty("_colorUnlitShader").objectReferenceValue = Shader.Find("RelicKeeper/Sprites/World Color Unlit");
            fields.FindProperty("_depthMistShader").objectReferenceValue = Shader.Find("RelicKeeper/Atmosphere/Depth Mist");
            fields.FindProperty("_hubDepthMistProfile").objectReferenceValue = mortfall ? dungeon.DepthMistProfile :
                (ReadMistProfile(live) ?? AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/HubDepthMist.asset"));
            fields.ApplyModifiedPropertiesWithoutUndo();
            Controller.Profile = live != null ? live.Profile : null;
            var focus = NewObject("Preview reveal focus").transform; focus.position = _spawn;
            Controller.BindPlayer(focus);
            Controller.ResetDepthMistAnchor(mortfall ? (Vector2)_spawn : Vector2.zero);
            SetView(false, true);
            Controller.RefreshColorGroups();
        }

        private static WorldColorProfileSO ReadProfile(AtmosphereController controller, string field) => controller != null
            ? new SerializedObject(controller).FindProperty(field).objectReferenceValue as WorldColorProfileSO : null;

        private static AtmosphereProfileSO ReadMistProfile(AtmosphereController controller) => controller != null
            ? new SerializedObject(controller).FindProperty("_hubDepthMistProfile").objectReferenceValue as AtmosphereProfileSO : null;

        private static Material StockMaterial(bool lit) => AssetDatabase.LoadAssetAtPath<Material>(
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-" + (lit ? "Lit" : "Unlit") + "-Default.mat");

        private void CopyVisuals(Transform source, Transform parent)
        {
            var copy = NewObject(source.name);
            copy.layer = source.gameObject.layer;
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            if (source.TryGetComponent(out Grid grid)) EditorUtility.CopySerialized(grid, copy.AddComponent<Grid>());
            if (source.TryGetComponent(out Tilemap tilemap))
            {
                var target = copy.AddComponent<Tilemap>(); EditorUtility.CopySerialized(tilemap, target); target.RefreshAllTiles();
                _tiles.Add(target);
                if (source.GetComponent<TilemapCollider2D>() != null || source.name == "Ground" || source.name == "Platforms") _ground.Add(target);
            }
            if (source.TryGetComponent(out SpriteRenderer sprite))
            {
                var target = copy.AddComponent<SpriteRenderer>(); EditorUtility.CopySerialized(sprite, target); _art.Add(target);
            }
            if (source.TryGetComponent(out TilemapRenderer tiles))
            {
                var target = copy.GetComponent<TilemapRenderer>();
                if (target == null) target = copy.AddComponent<TilemapRenderer>();
                EditorUtility.CopySerialized(tiles, target);
            }
            if (source.TryGetComponent(out SpriteMask mask)) EditorUtility.CopySerialized(mask, copy.AddComponent<SpriteMask>());
            if (source.TryGetComponent(out SortingGroup sorting)) EditorUtility.CopySerialized(sorting, copy.AddComponent<SortingGroup>());
            if (source.TryGetComponent(out WorldColorGroup colors))
                copy.AddComponent<WorldColorGroup>().Configure(colors.Role, colors.Influence, colors.CombatEffect);
            var renderer = copy.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material original = materials[i];
                    if (original == null || original.shader.name != "Universal Render Pipeline/2D/Sprite-Lit-Default") continue;
                    if (!_flatMaterials.TryGetValue(original, out Material flat))
                    {
                        // Color samples use flat lighting, independent of global Light2D state
                        // in the open game scene. Preserve authored material tint/texture values.
                        flat = new Material(original) { shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"),
                            name = original.name + " (flat preview)", hideFlags = HideFlags.HideAndDontSave };
                        _flatMaterials[original] = flat;
                    }
                    materials[i] = flat; changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
            for (int i = 0; i < source.childCount; i++) CopyVisuals(source.GetChild(i), copy.transform);
            copy.SetActive(source.gameObject.activeSelf);
        }

        private void AddMonster(string path, float x)
        {
            var data = Resources.Load<EnemyDataSO>(path);
            if (data == null) return;
            Sprite sprite = !string.IsNullOrWhiteSpace(data.Animation.IdleSpritesResourcePath)
                ? Resources.LoadAll<Sprite>(data.Animation.IdleSpritesResourcePath).FirstOrDefault() : null;
            if (sprite == null && data.Animation.Controller != null)
            {
                AnimationClip clip = data.Animation.Controller.animationClips.FirstOrDefault(x => x.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                    ?? data.Animation.Controller.animationClips.FirstOrDefault();
                if (clip != null)
                    foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    {
                        if (binding.propertyName != "m_Sprite") continue;
                        sprite = AnimationUtility.GetObjectReferenceCurve(clip, binding).Select(x => x.value as Sprite).FirstOrDefault(x => x != null);
                        if (sprite != null) break;
                    }
            }
            if (sprite != null) AddActor(data.name + " sample", sprite, Vector3.one, x, true);
        }

        private void AddActor(string name, Sprite sprite, Vector3 scale, float x, bool flip)
        {
            var renderer = NewObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(_actors.transform, false); renderer.transform.localScale = scale;
            renderer.sprite = sprite; renderer.flipX = flip; renderer.sharedMaterial = StockMaterial(false);
            renderer.sortingLayerName = "Hero"; renderer.sortingOrder = 1;
            renderer.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Characters);
            float floor = float.NegativeInfinity;
            foreach (Tilemap map in _ground)
            {
                Vector3Int start = map.WorldToCell(new Vector3(x, _spawn.y, 0));
                for (int i = 0; i < 24; i++)
                {
                    Vector3Int cell = start + Vector3Int.down * i;
                    if (!map.HasTile(cell)) continue;
                    floor = Mathf.Max(floor, map.CellToWorld(cell + Vector3Int.up).y); break;
                }
            }
            if (float.IsNegativeInfinity(floor)) floor = _spawn.y - 0.7f;
            renderer.transform.position += new Vector3(x - renderer.bounds.center.x, floor - renderer.bounds.min.y, 0f);
        }

        public void SetView(bool overview, bool actors)
        {
            _actors.SetActive(actors);
            Vector3 center = _spawn + new Vector3(3f, _mortfall ? 2f : 3.7f, 0f);
            float size = 5.625f;
            if (overview && _tiles.Count > 0)
            {
                Bounds bounds = new Bounds(_tiles[0].transform.TransformPoint(_tiles[0].localBounds.center), Vector3.zero);
                foreach (Tilemap map in _tiles)
                    bounds.Encapsulate(new Bounds(map.transform.TransformPoint(map.localBounds.center), Vector3.Scale(map.localBounds.size, map.transform.lossyScale)));
                center = bounds.center; size = Mathf.Max(5.625f, bounds.extents.y + 0.75f, bounds.extents.x / Camera.aspect + 0.75f);
            }
            else if (overview && _art.Count > 0)
            {
                Bounds bounds = _art[0].bounds;
                foreach (SpriteRenderer renderer in _art) bounds.Encapsulate(renderer.bounds);
                center = bounds.center; size = Mathf.Max(5.625f, bounds.extents.y + 0.75f, bounds.extents.x / Camera.aspect + 0.75f);
            }
            Camera.orthographicSize = size; Camera.transform.position = new Vector3(center.x, center.y, -10f);
            if (_background != null && _background.sprite != null)
            {
                _background.transform.position = new Vector3(center.x, center.y, 0f);
                _background.transform.localScale = Vector3.one * Mathf.Max(size * 2f * Camera.aspect / _background.sprite.bounds.size.x, size * 2f / _background.sprite.bounds.size.y);
            }
        }

        public void Dispose()
        {
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _scene = default;
            foreach (Material material in _flatMaterials.Values) if (material != null) Object.DestroyImmediate(material);
            _flatMaterials.Clear();
        }
    }
}
