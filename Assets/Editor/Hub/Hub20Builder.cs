using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Scripts.Hub;
using Scripts.Enemies;
using Scripts.Dungeon;
using Unity.Cinemachine;
using UnityEditor.U2D.Aseprite;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Editor.Hub
{
    public static class Hub20Builder
    {
        public const string ArtRoot = "Assets/Resources/HubObj/Hub2.0";
        public const string LayoutPath = ArtRoot + "/HubLayout.aseprite";
        public const string EnvironmentPath = ArtRoot + "/HubEnvironment.prefab";
        public const int LayoutWidth = 1558, LayoutHeight = 348, GroundPixelY = 295;
        public const float Ppu = 24f;
        public static Vector3 PixelToWorld(float x, float y) => new Vector3((x - LayoutWidth / 2f) / Ppu, (GroundPixelY - y) / Ppu, 0f);

        private static readonly Dictionary<string, string> EnglishNames = new Dictionary<string, string>
        {
            { "береза большая", "LargeBirch" }, { "береза маленькая", "SmallBirch" },
            { "дуб большой", "LargeOak" }, { "дуб средний", "MediumOak" },
            { "осина вторая", "AspenVariant" }, { "осина", "Aspen" },
            { "пень 2", "StumpVariant" }, { "пень", "Stump" },
            { "ствол 2", "TrunkVariant" }, { "ствол", "Trunk" },
            { "забор", "Fence" }, { "кузня", "Forge" }, { "манекен", "TrainingDummy" },
            { "мешок", "Sack" }, { "палатка", "MarketTent" }, { "портал", "PortalGate" },
            { "склад", "Warehouse" }, { "стойка с оружием", "WeaponRack" },
            { "стол и табурет", "TableAndStool" }, { "таверна", "Tavern" },
            { "трава", "Grass" }, { "цветы", "Flowers" },
            { "горы", "Mountains" }, { "замок", "Castle" }, { "земля", "Ground" },
            { "лес_фон", "DistantForest" }, { "лес", "Forest" }, { "небо", "Sky" }, { "туман", "Fog" }
        };

        [MenuItem("Tools/Hub/Rebuild Hub 2.0 from Aseprite Layout")]
        public static void Rebuild()
        {
            FormatAssets();
            BuildEnvironment();
            CaptureEnvironment();
            ApplyToHubScene();
        }

        public static void FormatAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in Directory.GetFiles(ArtRoot, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (path.EndsWith(".meta")) continue;
                    if (!EnglishNames.TryGetValue(Path.GetFileNameWithoutExtension(path), out string english)) continue;
                    string target = Path.GetDirectoryName(path).Replace('\\', '/') + "/" + english + Path.GetExtension(path);
                    string error = AssetDatabase.MoveAsset(path, target);
                    if (error.Length > 0) throw new Exception(error);
                }
                foreach (var folder in new[] { ("деревья", "Trees"), ("здания", "Buildings"), ("растения", "Plants"), ("фон", "Background") })
                {
                    string old = ArtRoot + "/" + folder.Item1;
                    if (!AssetDatabase.IsValidFolder(old)) continue;
                    string error = AssetDatabase.MoveAsset(old, ArtRoot + "/" + folder.Item2);
                    if (error.Length > 0) throw new Exception(error);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string file in Directory.GetFiles(ArtRoot, "*", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (AssetImporter.GetAtPath(path) is AsepriteImporter ase)
                {
                    ase.spritePixelsPerUnit = Ppu;
                    ase.spriteMeshType = SpriteMeshType.FullRect;
                    ase.filterMode = FilterMode.Point;
                    ase.mipmapEnabled = false;
                    ase.importMode = FileImportModes.AnimatedSprite;
                    ase.layerImportMode = LayerImportModes.IndividualLayers;
                    ase.pivotSpace = PivotSpaces.Canvas;
                    ase.pivotAlignment = path == LayoutPath || path.Contains("/Background/") ? SpriteAlignment.Center : SpriteAlignment.BottomCenter;
                    ase.generateModelPrefab = true;
                    ase.generateAnimationClips = true;
                    ase.addSortingGroup = false;
                    ase.addShadowCasters = false;
                    ase.generateSpriteAtlas = false;
                    ase.SetImporterPlatformSettings(new TextureImporterPlatformSettings
                    {
                        name = "DefaultTexturePlatform", maxTextureSize = 8192,
                        textureCompression = TextureImporterCompression.Uncompressed,
                        format = TextureImporterFormat.RGBA32
                    });
                    ase.SaveAndReimport();
                }
                else if (AssetImporter.GetAtPath(path) is TextureImporter texture)
                {
                    texture.textureType = TextureImporterType.Sprite;
                    texture.spritePixelsPerUnit = Ppu;
                    texture.filterMode = FilterMode.Point;
                    texture.mipmapEnabled = false;
                    texture.alphaIsTransparency = true;
                    texture.textureCompression = TextureImporterCompression.Uncompressed;
                    texture.maxTextureSize = 8192;
                    if (texture.spriteImportMode == SpriteImportMode.Single)
                    {
                        texture.spritePivot = path.Contains("/Background/") ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f);
                        var settings = new TextureImporterSettings();
                        texture.ReadTextureSettings(settings);
                        settings.spriteAlignment = (int)SpriteAlignment.Custom;
                        settings.spritePivot = texture.spritePivot;
                        settings.spriteMeshType = SpriteMeshType.FullRect;
                        texture.SetTextureSettings(settings);
                    }
                    texture.SaveAndReimport();
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Hub 2.0 assets renamed with GUIDs preserved; 24 PPU, point filtering, no compression/mipmaps.");
        }

        public static Sprite PrepareTrainingDummyAssets()
        {
            string path = ArtRoot + "/TrainingDummyVisual.png";
            if (!File.Exists(path)) File.Copy(ArtRoot + "/Buildings/TrainingDummy.png", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = (TextureImporter)AssetImporter.GetAtPath(path);
            texture.textureType = TextureImporterType.Sprite;
            texture.spriteImportMode = SpriteImportMode.Single;
            texture.spritePixelsPerUnit = Ppu;
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            texture.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0f);
            texture.SetTextureSettings(settings);
            texture.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(ArtRoot + "/HubTrainingDummyData.asset");
            if (data == null)
            {
                data = Object.Instantiate(Resources.Load<EnemyDataSO>("Enemy/SO_Dummy"));
                data.name = "HubTrainingDummyData";
                AssetDatabase.CreateAsset(data, ArtRoot + "/HubTrainingDummyData.asset");
            }
            data.Animation.Controller = null;
            data.Animation.IdleSpritesResourcePath = "HubObj/Hub2.0/TrainingDummyVisual";
            data.Animation.MoveSpritesResourcePath = string.Empty;
            data.Animation.AttackSpritesResourcePath = string.Empty;
            data.Animation.ChargeSpritesResourcePath = string.Empty;
            data.Animation.HitSpritesResourcePath = string.Empty;
            data.Animation.DigInSpritesResourcePath = string.Empty;
            data.Animation.DigOutSpritesResourcePath = string.Empty;
            data.Animation.VisualLocalOffset = Vector2.zero;
            EditorUtility.SetDirty(data);
            return sprite;
        }

        public static void BuildEnvironment()
        {
            Sprite dummySprite = PrepareTrainingDummyAssets();
            GameObject model = AssetDatabase.LoadAllAssetsAtPath(LayoutPath).OfType<GameObject>().FirstOrDefault();
            if (model == null) throw new Exception("Format/import HubLayout.aseprite first.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/HubArt.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")) { name = "HubArt" };
                AssetDatabase.CreateAsset(material, ArtRoot + "/HubArt.mat");
            }
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var environment = new GameObject("HubEnvironment");
                SceneManager.MoveGameObjectToScene(environment, preview);
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                imported.name = "LayoutArt";
                imported.transform.SetParent(environment.transform, false);
                imported.transform.localPosition = new Vector3(0f, (GroundPixelY - LayoutHeight / 2f) / Ppu, 0f);
                foreach (var group in imported.GetComponentsInChildren<SortingGroup>(true)) Object.DestroyImmediate(group);
                foreach (SpriteRenderer renderer in imported.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.sortingLayerName = "Background";
                    if (renderer.name == "Forest") renderer.sortingOrder = 6;
                    var colors = renderer.gameObject.AddComponent<Scripts.Visuals.Atmosphere.WorldColorGroup>();
                    bool near = renderer.name == "Ground" || renderer.name == "Buildings" || renderer.name == "TreesAndPlants";
                    colors.Configure(near ? Scripts.Visuals.Atmosphere.WorldColorRole.Environment : Scripts.Visuals.Atmosphere.WorldColorRole.Background);
                }
                ConfigureFog(imported);
                Scripts.Visuals.Atmosphere.LayeredParallaxGroup.ConfigureHub(environment);
                HubBuildingWindBuilder.Configure(imported);
                // Replace the baked mannequin silhouette with the live training
                // actor. A SpriteMask preserves the supplied artwork/source pixels.
                var buildings = imported.GetComponentsInChildren<SpriteRenderer>().Single(x => x.name == "Buildings");
                buildings.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
                var mask = NewChild(environment.transform, "TrainingDummyArtworkMask").AddComponent<SpriteMask>();
                mask.sprite = dummySprite;
                mask.transform.localPosition = PixelToWorld(831.5f, GroundPixelY);
                mask.isCustomRangeActive = true;
                mask.frontSortingLayerID = buildings.sortingLayerID;
                mask.backSortingLayerID = buildings.sortingLayerID;
                mask.frontSortingOrder = buildings.sortingOrder + 1;
                mask.backSortingOrder = buildings.sortingOrder - 1;
                var ground = NewChild(environment.transform, "GroundCollision");
                ground.layer = LayerMask.NameToLayer("Ground");
                var groundBox = ground.AddComponent<BoxCollider2D>();
                float depth = (LayoutHeight - GroundPixelY) / Ppu;
                groundBox.size = new Vector2(LayoutWidth / Ppu, depth);
                groundBox.offset = new Vector2(0f, -depth / 2f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var wall = NewChild(environment.transform, side < 0 ? "LeftBoundary" : "RightBoundary");
                    wall.layer = LayerMask.NameToLayer("Ground");
                    wall.transform.localPosition = new Vector3(side * (LayoutWidth / Ppu / 2f + 0.25f), 5f, 0f);
                    wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 16f);
                }
                var bounds = NewChild(environment.transform, "HubCameraBounds").AddComponent<PolygonCollider2D>();
                bounds.isTrigger = true;
                bounds.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                bounds.SetPath(0, new[] { (Vector2)PixelToWorld(0, 0), (Vector2)PixelToWorld(LayoutWidth, 0),
                    (Vector2)PixelToWorld(LayoutWidth, LayoutHeight), (Vector2)PixelToWorld(0, LayoutHeight) });
                var spawn = NewChild(environment.transform, "LayoutSpawnReference");
                spawn.transform.localPosition = PixelToWorld(325f, GroundPixelY) + Vector3.up * 1.5f;
                RelicKeeper.Editor.VFX.EnvironmentContactShadingBuilder.ConfigureHub(environment);
                RelicKeeper.Editor.Visuals.LocalLightingSetup.ConfigureHub(environment);
                PrefabUtility.SaveAsPrefabAsset(environment, EnvironmentPath);
                var text = new StringBuilder();
                WriteTree(environment.transform, text, 0);
                string folder = Path.Combine(Path.GetTempPath(), "codex-hub-layout");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "imported-environment.txt"), text.ToString());
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            SceneManager.MoveGameObjectToScene(child, parent.gameObject.scene);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void ConfigureFog(GameObject imported)
        {
            var animator = imported.GetComponentInChildren<Animator>();
            var fog = imported.GetComponentsInChildren<SpriteRenderer>().Single(x => x.name == "Fog");
            string path = AnimationUtility.CalculateTransformPath(fog.transform, animator.transform);
            AnimationClip original = animator.runtimeAnimatorController.animationClips.First();
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(original)
                .Single(x => x.path == path && x.propertyName == "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(original, binding).OrderBy(x => x.time).ToList();
            float duration = original.length;
            // The importer appends a repeated sprite at the loop endpoint; it is
            // a closure key, not an extra frame that should pause the fog.
            if (keys.Count > 1 && keys.Last().time >= original.length - 0.04f &&
                (keys.Last().value == keys[0].value || keys.Last().value == keys[keys.Count - 2].value))
            {
                keys.RemoveAt(keys.Count - 1);
            }
            Sprite[] frames = keys.Select(x => (Sprite)x.value).ToArray();
            if (frames.Length == 0) throw new Exception("Hub fog has no animation frames.");

            string clipPath = ArtRoot + "/HubEnvironmentAnimation.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            EditorUtility.CopySerialized(original, clip);
            clip.name = "HubEnvironmentAnimation";
            foreach (var curve in AnimationUtility.GetObjectReferenceCurveBindings(clip).Where(x => x.path == path))
                AnimationUtility.SetObjectReferenceCurve(clip, curve, null);
            foreach (var curve in AnimationUtility.GetCurveBindings(clip).Where(x => x.path == path))
                AnimationUtility.SetEditorCurve(clip, curve, null);
            EditorUtility.SetDirty(clip);

            string controllerPath = ArtRoot + "/HubEnvironmentAnimation.overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
            if (controller == null) { controller = new AnimatorOverrideController(); AssetDatabase.CreateAsset(controller, controllerPath); }
            controller.runtimeAnimatorController = animator.runtimeAnimatorController;
            controller[original.name] = clip;
            EditorUtility.SetDirty(controller);
            animator.runtimeAnimatorController = controller;

            string materialPath = ArtRoot + "/HubFog.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material = new Material(Shader.Find("RelicKeeper/Hub Fog")); AssetDatabase.CreateAsset(material, materialPath); }
            fog.sharedMaterial = material;
            var next = NewChild(fog.transform, "FogBlend").AddComponent<SpriteRenderer>();
            next.sharedMaterial = material;
            next.sortingLayerID = fog.sortingLayerID;
            next.sortingOrder = fog.sortingOrder;
            fog.gameObject.AddComponent<HubFogVisual>().Configure(fog, next, frames, duration / frames.Length * 8f);
        }

        public static void CaptureEnvironment()
        {
            var preview = new PreviewRenderUtility();
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath));
            preview.AddSingleGO(root);
            try
            {
                foreach (Animator animator in root.GetComponentsInChildren<Animator>())
                {
                    var clip = animator.runtimeAnimatorController?.animationClips.FirstOrDefault();
                    if (clip != null) clip.SampleAnimation(animator.gameObject, 0f);
                }
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = LayoutHeight / Ppu / 2f;
                preview.camera.transform.position = PixelToWorld(LayoutWidth / 2f, LayoutHeight / 2f) + new Vector3(0, 0, -10);
                preview.camera.transform.rotation = Quaternion.identity;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = Color.clear;
                preview.BeginStaticPreview(new Rect(0, 0, LayoutWidth, LayoutHeight));
                preview.Render(true);
                var texture = preview.EndStaticPreview();
                string folder = Path.Combine(Path.GetTempPath(), "codex-hub-layout");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "hub-unity-environment.png"), texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            finally { preview.Cleanup(); }
        }

        public static void ApplyToHubScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Open HubScene first.");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
            if (prefab == null) throw new Exception("Build HubEnvironment before applying.");
            GameObject hub = scene.GetRootGameObjects().First(x => x.name == "HubWorld");
            var npcs = hub.GetComponentsInChildren<HubServiceNpc>(true);
            var portals = hub.GetComponentsInChildren<DungeonPortal>(true);
            if (npcs.Length != 3 || portals.Length != 2) throw new Exception("Unexpected NPC/portal set; inspect before rebuilding.");
            var ids = npcs.Select(x => GlobalObjectId.GetGlobalObjectIdSlow(x.gameObject)).Concat(portals.Select(x => GlobalObjectId.GetGlobalObjectIdSlow(x.gameObject))).ToArray();
            EditorSceneManager.SaveScene(scene);
            string backup = Path.Combine(Path.GetTempPath(), "codex-hub-layout", "HubScene-before-rebuild.unity");
            if (!File.Exists(backup)) File.Copy(scene.path, backup);
            var keep = new HashSet<GameObject>(npcs.Select(x => x.gameObject).Concat(portals.Select(x => x.gameObject)));
            var dummy = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DummyEvolution>(true)).FirstOrDefault();
            if (dummy != null) keep.Add(dummy.gameObject);
            foreach (GameObject actor in keep) actor.transform.SetParent(hub.transform, true);
            hub.transform.position = Vector3.zero;
            hub.transform.rotation = Quaternion.identity;
            hub.transform.localScale = Vector3.one;
            var old = new List<GameObject>();
            foreach (Transform child in hub.transform) if (!keep.Contains(child.gameObject)) old.Add(child.gameObject);
            foreach (GameObject environment in old) Undo.DestroyObjectImmediate(environment);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(hub.transform, true);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            foreach (HubServiceNpc npc in npcs)
            {
                float x = npc.Service == HubService.Tavern ? 315f : npc.Service == HubService.Stash ? 515f : 1070f;
                PlaceActorOnGround(npc.gameObject, x);
            }
            foreach (DungeonPortal portal in portals)
                PlaceActorOnGround(portal.gameObject, portal.OpensReachedFloorSelect ? 1455f : 1376f);
            if (dummy != null)
            {
                var dummyData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(ArtRoot + "/HubTrainingDummyData.asset");
                var entity = dummy.GetComponent<EnemyEntity>();
                var entitySerialized = new SerializedObject(entity);
                entitySerialized.FindProperty("_defaultData").objectReferenceValue = dummyData;
                entitySerialized.ApplyModifiedPropertiesWithoutUndo();
                var dummySerialized = new SerializedObject(dummy);
                dummySerialized.FindProperty("_dummyData").objectReferenceValue = dummyData;
                dummySerialized.ApplyModifiedPropertiesWithoutUndo();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/TrainingDummyVisual.png");
                dummy.transform.localScale = Vector3.one;
                EnemyPhysicsFit.CalculatePhysicsBox(sprite.bounds.size, out var size, out var offset);
                float physicsBottom = offset.y - size.y / 2f;
                dummy.transform.position = PixelToWorld(831.5f, GroundPixelY) - Vector3.up * physicsBottom;
                SpriteRenderer rootRenderer = dummy.GetComponent<SpriteRenderer>();
                rootRenderer.sprite = sprite;
                rootRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/HubArt.mat");
                rootRenderer.enabled = false;
                if (dummy.TryGetComponent<Animator>(out var oldAnimator)) oldAnimator.enabled = false;
                Transform visual = entity.VisualRoot;
                visual.localPosition = Vector3.up * (physicsBottom - sprite.bounds.min.y);
                var visualRenderer = entity.VisualRenderer;
                visualRenderer.sprite = sprite;
                visualRenderer.sharedMaterial = rootRenderer.sharedMaterial;
                visualRenderer.sortingLayerName = "World";
                var body = dummy.GetComponent<BoxCollider2D>();
                if (body != null) { body.size = size; body.offset = offset; }
                foreach (Component component in dummy.GetComponentsInChildren<Component>(true))
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                EditorUtility.SetDirty(dummy.gameObject);
            }
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform spawn = all.First(x => x.name == "HubSpawnPoint");
            spawn.position = PixelToWorld(325f, GroundPixelY) + Vector3.up * 1.5f;
            var player = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerStats>(true)).First();
            Vector3 delta = spawn.position - player.transform.position;
            player.transform.position = spawn.position;
            foreach (var camera in scene.GetRootGameObjects().Where(x => x.GetComponent<Camera>() != null || x.GetComponent<CinemachineCamera>() != null))
                camera.transform.position += new Vector3(delta.x, delta.y, 0f);
            var virtualCamera = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CinemachineCamera>(true)).First();
            virtualCamera.Lens.OrthographicSize = 270f / (2f * Ppu);
            EditorUtility.SetDirty(virtualCamera);
            var confiner = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CinemachineConfiner2D>(true)).First();
            confiner.BoundingShape2D = instance.GetComponentsInChildren<PolygonCollider2D>().Single();
            confiner.InvalidateBoundingShapeCache();
            // The old standalone bounds generated solid walls at the previous
            // hub edges. They would block the new gateway area after this resize.
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "LevelBounds" && root.GetComponent<LevelBoundaryGenerator>() != null)
                    Undo.DestroyObjectImmediate(root);
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DungeonController>(true)).First();
            var serialized = new SerializedObject(controller);
            SpriteRenderer shared = serialized.FindProperty("_sharedBackgroundRenderer").objectReferenceValue as SpriteRenderer;
            if (shared == null || shared.gameObject == null)
            {
                var backgroundObject = new GameObject("DungeonSharedBackground");
                SceneManager.MoveGameObjectToScene(backgroundObject, scene);
                shared = backgroundObject.AddComponent<SpriteRenderer>();
                backgroundObject.AddComponent<ParallaxEffect>();
            }
            shared.transform.position = new Vector3(0, 5, 10);
            shared.sprite = null;
            shared.enabled = false;
            shared.drawMode = SpriteDrawMode.Tiled;
            shared.size = new Vector2(160, 100);
            shared.sortingLayerName = "Background";
            shared.sortingOrder = -1000;
            shared.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/HubArt.mat");
            serialized.FindProperty("_sharedBackgroundRenderer").objectReferenceValue = shared;
            serialized.FindProperty("_cameraConfiner").objectReferenceValue = confiner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var id in ids)
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) == null) throw new Exception("A preserved actor reference was lost.");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = instance;
            Debug.Log("Hub 2.0 applied. Preserved all three service NPCs and both portals; scene backup: " + backup);
        }

        private static void PlaceActorOnGround(GameObject actor, float pixelX)
        {
            Vector3 position = PixelToWorld(pixelX, GroundPixelY);
            actor.transform.position = position;
            SpriteRenderer renderer = actor.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
                actor.transform.position = new Vector3(position.x, -renderer.sprite.bounds.min.y * actor.transform.lossyScale.y, 0f);
            foreach (SpriteRenderer child in actor.GetComponentsInChildren<SpriteRenderer>(true))
            {
                child.sortingLayerName = "World";
                if (PrefabUtility.IsPartOfPrefabInstance(child)) PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            }
            if (PrefabUtility.IsPartOfPrefabInstance(actor.transform)) PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
            EditorUtility.SetDirty(actor.transform);
            EditorUtility.SetDirty(actor);
        }

        public static void ValidateRuntimeAndCapture()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            GameObject hub = scene.GetRootGameObjects().First(x => x.name == "HubWorld");
            GameObject environment = hub.transform.Find("HubEnvironment").gameObject;
            var player = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<PlayerStats>(true)).First();
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DungeonController>(true)).First();
            var field = typeof(DungeonController).GetField("_sharedBackgroundRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var shared = (SpriteRenderer)field.GetValue(controller);
            if (shared == null || shared.enabled) throw new Exception("Dungeon background must be assigned and hidden in the hub.");
            var dungeon = Resources.Load<DungeonDataSO>("Dungeons/MortFallDungeonSO");
            var apply = typeof(DungeonController).GetMethod("ApplyDungeonBackground", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var restore = typeof(DungeonController).GetMethod("RestoreHubBackground", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            try
            {
                Vector2 tiledSize = shared.size;
                apply.Invoke(controller, new object[] { dungeon });
                if (!shared.enabled || shared.sprite != dungeon.LoadBackgroundSprite()) throw new Exception("Dungeon background failed to switch.");
                if (shared.size != tiledSize) throw new Exception("Dungeon background lost its configured tiled area.");
            }
            finally { restore.Invoke(controller, null); }
            if (shared.enabled || shared.sprite != null || !environment.activeInHierarchy) throw new Exception("Hub background restoration failed.");
            if (hub.GetComponentsInChildren<HubServiceNpc>().Length != 3 || hub.GetComponentsInChildren<DungeonPortal>().Length != 2)
                throw new Exception("Hub actor count changed in Play Mode.");
            if (player.transform.position.x < -LayoutWidth / Ppu / 2f || player.transform.position.x > LayoutWidth / Ppu / 2f || player.transform.position.y < -1f)
                throw new Exception("Player spawned outside the new hub or fell through its ground.");
            var dummy = hub.GetComponentInChildren<DummyEvolution>();
            if (dummy == null || Mathf.Abs(dummy.GetComponent<EnemyEntity>().VisualRenderer.bounds.min.y) > 0.06f)
                throw new Exception("Training dummy artwork does not align with the new ground.");
            var animator = environment.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || !animator.enabled)
                throw new Exception("Hub environment animation is not running.");
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime <= 0f) throw new Exception("Wait for gameplay startup before checking animation.");
            var camera = Camera.main;
            if (Mathf.Abs(camera.transform.position.x - player.transform.position.x) > 10f)
                throw new Exception("Camera is not following the player within the native viewport.");
            var target = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            var previous = RenderTexture.active;
            var texture = new Texture2D(480, 270, TextureFormat.RGBA32, false);
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 480, 270), 0, 0);
                texture.Apply();
                string folder = Path.Combine(Path.GetTempPath(), "codex-hub-layout");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "hub-play-mode.png"), texture.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "runtime-validation.txt"),
                    "Player=" + player.transform.position.ToString("F3") + "\nCamera=" + camera.transform.position.ToString("F3") +
                    "\nNPCs=3; portals=2; background switch/restore=passed; animator state time=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(texture);
            }
            Debug.Log("Hub 2.0 runtime validation passed; captured 480x270 gameplay view.");
        }

        public static void CaptureRuntimeArtViews()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
            var go = new GameObject("Hub art verification camera");
            go.hideFlags = HideFlags.HideAndDontSave;
            var camera = go.AddComponent<Camera>();
            camera.scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            camera.enabled = false;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = false;
            additional.volumeLayerMask = 0;
            RenderTexture previous = RenderTexture.active;
            string folder = Path.Combine(Path.GetTempPath(), "codex-hub-layout");
            try
            {
                foreach (var view in new[] { ("full", LayoutWidth, LayoutHeight, 0f, 5.0416665f),
                    ("training", 480, 270, PixelToWorld(831.5f, 295).x, 3.4166665f),
                    ("portals", 480, 270, LayoutWidth / Ppu / 2f - 10f, 3.4166665f) })
                {
                    var target = new RenderTexture(view.Item2, view.Item3, 24, RenderTextureFormat.ARGB32);
                    var texture = new Texture2D(view.Item2, view.Item3, TextureFormat.RGBA32, false);
                    try
                    {
                        camera.orthographicSize = view.Item3 / Ppu / 2f;
                        camera.aspect = view.Item2 / (float)view.Item3;
                        camera.transform.position = new Vector3(view.Item4, view.Item5, -10f);
                        target.Create();
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target;
                        texture.ReadPixels(new Rect(0, 0, view.Item2, view.Item3), 0, 0);
                        texture.Apply();
                        File.WriteAllBytes(Path.Combine(folder, "hub-play-" + view.Item1 + ".png"), texture.EncodeToPNG());
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        target.Release();
                        Object.DestroyImmediate(target);
                        Object.DestroyImmediate(texture);
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static void InspectCurrentHub()
        {
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            if (!scene.IsValid() || !scene.isLoaded) throw new Exception("Open HubScene first.");
            var text = new StringBuilder();
            text.AppendLine("Scene roots: " + string.Join(", ", scene.GetRootGameObjects().Select(x => x.name)));
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "HubWorld" || root.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 || root.name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                    WriteTree(root.transform, text, 0);
            }
            string folder = Path.Combine(Path.GetTempPath(), "codex-hub-layout");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "current-hub.txt"), text.ToString());
            Debug.Log("Hub inspection written to " + folder);
        }
        private static void WriteTree(Transform t, StringBuilder text, int depth)
        {
            text.Append(' ', depth * 2).Append(t.name).Append(" world=").Append(t.position.ToString("F3"));
            text.Append(" scale=").Append(t.lossyScale.ToString("F3"));
            text.Append(" id=").Append(GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject));
            text.Append(" types=").Append(string.Join(",", t.GetComponents<Component>().Where(x => x != null).Select(x => x.GetType().Name))).AppendLine();
            foreach (Transform child in t) WriteTree(child, text, depth + 1);
        }
    }
}
