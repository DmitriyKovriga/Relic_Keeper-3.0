using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Scripts.Dungeon;
using Scripts.Enemies;
using Scripts.Visuals;
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
        private Vector3 _viewCenter;
        private readonly List<LocalLight2DSource> _localLights = new List<LocalLight2DSource>();
        private readonly List<EnvironmentAnimation> _environmentAnimations = new List<EnvironmentAnimation>();
        private readonly List<AmbientParticleSource> _particles = new List<AmbientParticleSource>();
        private readonly List<LocalLightShaftSource> _shafts = new List<LocalLightShaftSource>();
        private readonly List<Tilemap> _shaftTerrain = new List<Tilemap>();
        public IReadOnlyList<LocalLightShaftSource> Shafts => _shafts;
        public void ReclipShaft(LocalLightShaftSource shaft)
        {
            // Visual copies deliberately have no colliders, but retain each tile's collider type.
            CeilingBeamVisibility.Apply(_shaftTerrain.ToArray(), shaft);
        }
        private GroundSurfaceProfileSO _groundPreviewProfile;
        private SpriteRenderer _groundPreviewPlayer;
        private GroundingSpriteSheetVfx _groundPreviewEffect;
        private Sprite _groundBaseSprite;
        private Sprite[] _groundRunFrames = Array.Empty<Sprite>();
        private Vector3 _groundBasePosition;
        private float _groundFloor, _groundBodyWidth = 0.65f;
        private bool _groundPreviewActive;
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
            GameObject prefab = mortfall ? AssetDatabase.LoadAssetAtPath<GameObject>(roomPath) ?? dungeon?.LoadRoomPrefab(roomPath) :
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab");
            if (prefab == null) throw new InvalidOperationException("The selected preview artwork could not be loaded.");
            _groundPreviewProfile = prefab.GetComponentInChildren<GroundSurfaceVisuals>(true)?.Profile;
            GameObject artwork = CopyVisuals(prefab.transform, null);
            foreach (var shaft in _shafts) ReclipShaft(shaft);
            if (!mortfall) LayeredParallaxGroup.ConfigureHub(artwork);
            _spawn = mortfall ? prefab.GetComponent<RoomController>().PlayerSpawnPosition : new Vector3(7f, 0.8f, 0f);

            Camera = NewObject("Color preview camera").AddComponent<Camera>();
            Camera.scene = _scene; Camera.enabled = false; Camera.orthographic = true;
            Camera.aspect = 480f / 270f; Camera.allowMSAA = false;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.09f, 0.12f, 0.14f);
            if (mortfall)
            {
                _background = NewObject("DungeonSharedBackground").AddComponent<SpriteRenderer>();
                _background.sprite = dungeon.LoadBackgroundSprite();
                _background.drawMode = SpriteDrawMode.Tiled;
                _background.sharedMaterial = StockMaterial(false);
                _background.sortingLayerName = "Background"; _background.sortingOrder = -1000;
                _background.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Background);
                if (dungeon.ParallaxSilhouette != null) LayeredParallaxGroup.ConfigureDungeon(_background, dungeon.ParallaxSilhouette);
            }
            _actors = NewObject("Static character samples");
            var player = Object.FindObjectsByType<global::PlayerStats>(FindObjectsSortMode.None).FirstOrDefault();
            var sampleData = player != null ? player.CurrentCharacterData ?? new SerializedObject(player).FindProperty("_defaultCharacterData").objectReferenceValue as CharacterDataSO : null;
            if (sampleData == null) sampleData = Resources.Load<CharacterDataSO>("Heroes/RogueResources/Rogue");
            SpriteRenderer body = player != null ? player.GetComponentsInChildren<SpriteRenderer>(true)
                .FirstOrDefault(x => x.sprite != null && (x.name == "MovementSprite" || x.name == "Player")) : null;
            if (body != null) AddActor("Player sample", body.sprite, body.transform.lossyScale, _spawn.x, body.flipX);
            else
            {
                // Before Play, the movement animator may not have assigned a
                // sprite yet. Keep the lighting preview's character visible.
                var data = sampleData;
                var animation = data != null ? data.RunAnimation : null;
                Sprite pose = animation?.Frames?.FirstOrDefault();
                if (pose == null && !string.IsNullOrWhiteSpace(animation?.SpriteSheetResourcesPath))
                    pose = Resources.LoadAll<Sprite>(animation.SpriteSheetResourcesPath).OrderBy(x => x.name, StringComparer.Ordinal).FirstOrDefault();
                if (pose == null && data != null) pose = data.Portrait;
                if (pose != null) AddActor("Player sample", pose, player != null ? player.transform.lossyScale : Vector3.one, _spawn.x, false);
            }
            if (mortfall)
            {
                AddMonster("Enemy/SO_Zombie", _spawn.x + 1.25f);
                AddMonster("Enemy/SO_Mold", _spawn.x + 8f);
            }
            _groundPreviewPlayer = _actors.GetComponentsInChildren<SpriteRenderer>().FirstOrDefault(x => x.name == "Player sample");
            if (_groundPreviewPlayer != null)
            {
                _groundBaseSprite = _groundPreviewPlayer.sprite; _groundBasePosition = _groundPreviewPlayer.transform.localPosition;
                _groundFloor = mortfall ? _groundPreviewPlayer.bounds.min.y : 0;
                var data = sampleData;
                if (data?.RunAnimation != null)
                {
                    _groundRunFrames = data.RunAnimation.Frames;
                    if ((_groundRunFrames == null || _groundRunFrames.Length == 0) && !string.IsNullOrWhiteSpace(data.RunAnimation.SpriteSheetResourcesPath))
                        _groundRunFrames = Resources.LoadAll<Sprite>(data.RunAnimation.SpriteSheetResourcesPath).OrderBy(x => x.name, StringComparer.Ordinal).ToArray();
                }
                if (player != null && player.TryGetComponent<Collider2D>(out var collider)) _groundBodyWidth = Mathf.Max(0.3f, collider.bounds.size.x);
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
            foreach (var light in _localLights) light.BindPreview(Controller);
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

        private GameObject CopyVisuals(Transform source, Transform parent)
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
                if (source.GetComponent<TilemapCollider2D>() != null) _shaftTerrain.Add(target);
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
            if (source.TryGetComponent<LocalLight2DSource>(out var sourceLight))
            {
                var target = copy.AddComponent<LocalLight2DSource>();
                EditorUtility.CopySerialized(sourceLight, target); _localLights.Add(target);
            }
            if (source.TryGetComponent<LocalLightOccluder>(out var blocker))
            {
                LocalLightOccluderBuilder.Configure(copy, blocker.Outline);
            }
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
            if (source.TryGetComponent<EnvironmentAnimation>(out var animation))
            {
                var target = copy.AddComponent<EnvironmentAnimation>();
                EditorUtility.CopySerialized(animation, target); _environmentAnimations.Add(target);
            }
            copy.SetActive(source.gameObject.activeSelf);
            if (source.TryGetComponent<AmbientParticleSource>(out var particles))
            {
                var target = copy.AddComponent<AmbientParticleSource>();
                EditorUtility.CopySerialized(particles, target); _particles.Add(target);
            }
            if (source.TryGetComponent<LocalLightShaftSource>(out var shaft))
            {
                var target = copy.AddComponent<LocalLightShaftSource>();
                EditorUtility.CopySerialized(shaft, target); _shafts.Add(target);
            }
            return copy;
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
            _actors.transform.localPosition = Vector3.zero;
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
            _viewCenter = new Vector3(center.x, center.y, -10f);
            Camera.orthographicSize = size; Camera.transform.position = _viewCenter;
            if (_background != null && _background.sprite != null)
            {
                _background.transform.position = new Vector3(center.x, center.y, 0f);
                _background.transform.localScale = Vector3.one;
                Vector2 tile = _background.sprite.rect.size / _background.sprite.pixelsPerUnit;
                int columns = Mathf.CeilToInt(Mathf.Max(160f, size * 2f * Camera.aspect + 20f) / tile.x) | 1;
                int rows = Mathf.CeilToInt(Mathf.Max(100f, size * 2f + 20f) / tile.y) | 1;
                _background.size = new Vector2(columns * tile.x, rows * tile.y);
                _background.GetComponent<LayeredParallaxGroup>()?.ResetAnchor(center);
            }
        }

        public void SetCameraOffset(float x)
        {
            Vector3 position = _viewCenter + Vector3.right * x;
            if (!_mortfall)
            {
                float available = Mathf.Max(0f, 1558f / 48f - Camera.orthographicSize * Camera.aspect);
                position.x = Mathf.Clamp(position.x, -available, available);
            }
            Camera.transform.position = position;
        }

        public void FocusLighting()
        {
            if (_localLights.Count == 0) return;
            _viewCenter = new Vector3(_localLights[0].transform.position.x,
                _localLights[0].transform.position.y + (_mortfall ? 0.5f : 2.5f), -10f);
            if (!_mortfall) _actors.transform.localPosition = Vector3.right * (_localLights[0].transform.position.x - _spawn.x - 0.75f);
            SetCameraOffset(0);
        }

        public int AmbientSourceCount => _environmentAnimations.Count;
        public void FocusAmbient(int sourceIndex = 0)
        {
            if (_environmentAnimations.Count == 0) return;
            var source = _environmentAnimations[Mathf.Clamp(sourceIndex, 0, _environmentAnimations.Count - 1)];
            _viewCenter = new Vector3(source.transform.position.x, _mortfall
                ? source.transform.position.y - source.FallDistance + 3.5f : 4.5f, -10f);
            if (!_mortfall) _actors.transform.localPosition = Vector3.right * (source.transform.position.x - _spawn.x + 3f);
            SetCameraOffset(0);
        }

        public void FocusParticles(AmbientParticleKind kind)
        {
            var source = _particles.FirstOrDefault(x => x.Kind == kind);
            if (source == null) { FocusLighting(); return; }
            _viewCenter = new Vector3(source.transform.position.x, _mortfall ? source.transform.position.y - 0.5f : 3.5f, -10);
            if (!_mortfall) _actors.transform.localPosition = Vector3.right * (source.transform.position.x - _spawn.x + 1.5f);
            SetCameraOffset(0);
        }

        public bool FocusShaft(int index = 0)
        {
            if (_shafts.Count == 0) return false;
            var source = _shafts[Mathf.Clamp(index, 0, _shafts.Count - 1)];
            Camera.orthographicSize = 5.625f;
            _viewCenter = source.SourcePosition + new Vector3(0.6f, -2.5f, -10);
            SetCameraOffset(0); return true;
        }

        public void FocusGroundMovement()
        {
            _actors.transform.localPosition = Vector3.zero;
            _viewCenter = new Vector3(_spawn.x, _groundFloor + 3.5f, -10);
            Camera.orthographicSize = 5.625f; SetCameraOffset(0);
        }

        public void SampleGroundMovement(bool active, float seconds)
        {
            if (_groundPreviewEffect != null) _groundPreviewEffect.GetComponent<SpriteRenderer>().enabled = false;
            if (!active || _groundPreviewPlayer == null || _groundPreviewProfile == null)
            {
                if (_groundPreviewActive && _groundPreviewPlayer != null)
                { _groundPreviewPlayer.sprite = _groundBaseSprite; _groundPreviewPlayer.transform.localPosition = _groundBasePosition; }
                _groundPreviewActive = false; return;
            }
            _groundPreviewActive = true;
            var profile = _groundPreviewProfile; var appearance = Controller.ActiveDepthMistProfile;
            float t = Mathf.Repeat(seconds, 3.6f);
            bool run = t < 1.6f, hop = t >= 1.6f && t < 2.4f;
            float offset = run ? Mathf.Lerp(-0.5f, 0.5f, t / 1.6f) : hop ? Mathf.Lerp(0.5f, -0.5f, (t - 1.6f) / 0.8f) : -0.5f;
            float height = hop ? Mathf.Sin((t - 1.6f) / 0.8f * Mathf.PI) * 0.7f : 0;
            _groundPreviewPlayer.sprite = run && _groundRunFrames != null && _groundRunFrames.Length > 0
                ? _groundRunFrames[Mathf.FloorToInt(t * 10) % _groundRunFrames.Length] : _groundBaseSprite;
            _groundPreviewPlayer.transform.localPosition = _groundBasePosition + Vector3.right * offset;
            _groundPreviewPlayer.transform.position += Vector3.up * (_groundFloor + height - _groundPreviewPlayer.bounds.min.y);
            if (appearance != null && (!appearance.surfaceMovementEffects || appearance.surfaceEffectOpacity <= 0)) return;
            float density = appearance != null ? appearance.surfaceRunDust : 1;
            float age, width, opacity, duration;
            Sprite[] frames;
            if (run && density > 0)
            {
                float interval = Mathf.Max(profile.runInterval, profile.runStride / (4f * density));
                age = Mathf.Repeat(t, interval); duration = profile.stepDuration; width = profile.stepWidth;
                opacity = profile.stepOpacity; frames = profile.stepFrames;
                offset = Mathf.Lerp(-0.5f, 0.5f, (t - age) / 1.6f);
                offset -= _groundBodyWidth * 0.675f;
            }
            else if (t >= 2.4f)
            {
                var response = profile.EvaluateLanding(9, false, appearance != null ? appearance.surfaceLandingStrength : 1);
                if (!response.visible) return;
                age = t - 2.4f; duration = response.duration; width = response.width;
                opacity = response.opacity; frames = profile.landingFrames;
            }
            else return;
            if (age > duration || frames == null || frames.Length == 0) return;
            if (_groundPreviewEffect == null)
            {
                _groundPreviewEffect = NewObject("Surface movement sample").AddComponent<GroundingSpriteSheetVfx>();
                _groundPreviewEffect.enabled = false; // Only the preview clock drives this copy.
                _groundPreviewEffect.GetComponent<SpriteRenderer>().sharedMaterial = StockMaterial(false);
                _groundPreviewEffect.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment);
            }
            width = Mathf.Min(profile.maxWorldWidth, width * _groundBodyWidth);
            _groundPreviewEffect.Initialize(frames, duration, opacity * (appearance != null ? appearance.surfaceEffectOpacity : 1),
                SortingLayer.NameToID(WorldRenderSorting.Settings.LayerDefault), WorldRenderSorting.ResolveOrder(RenderDepthCategory.Environment, _groundFloor, 6), width, false);
            _groundPreviewEffect.transform.position = new Vector3(_spawn.x + offset, _groundFloor + frames[0].bounds.size.y * width / frames[0].bounds.size.x * 0.5f + 0.02f);
            _groundPreviewEffect.Sample(age / duration); _groundPreviewEffect.GetComponent<SpriteRenderer>().enabled = true;
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
