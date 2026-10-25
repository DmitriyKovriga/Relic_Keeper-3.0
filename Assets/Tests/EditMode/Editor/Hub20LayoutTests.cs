using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
using Scripts.Hub;
using Scripts.Dungeon;
using Scripts.Enemies;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class Hub20LayoutTests
    {
        private Scene _loadedHub;
        private Scene _previousActive;
        private bool _openedForTests;

        [OneTimeSetUp]
        public void LoadSavedHubForIntegrationChecks()
        {
            _loadedHub = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            if (_loadedHub.IsValid() && _loadedHub.isLoaded) return;
            _previousActive = SceneManager.GetActiveScene();
            _loadedHub = EditorSceneManager.OpenScene("Assets/Scenes/HubScene.unity", OpenSceneMode.Additive);
            _openedForTests = true;
            if (_previousActive.IsValid()) SceneManager.SetActiveScene(_previousActive);
        }

        [OneTimeTearDown]
        public void CloseOnlyTestLoadedScene()
        {
            if (_openedForTests && _loadedHub.IsValid()) EditorSceneManager.CloseScene(_loadedHub, true);
        }

        [Test]
        public void AllAssetPathsAreEnglishAndSpritesUseNativePixelSettings()
        {
            var files = Directory.GetFiles(Hub20Builder.ArtRoot, "*", SearchOption.AllDirectories);
            Assert.That(files, Is.Not.Empty);
            foreach (string raw in files)
            {
                string path = raw.Replace('\\', '/');
                Assert.That(path.All(c => c < 128), Is.True, path);
                if (path.EndsWith(".meta")) continue;
                if (AssetImporter.GetAtPath(path) is AsepriteImporter ase)
                {
                    Assert.That(ase.spritePixelsPerUnit, Is.EqualTo(24f), path);
                    Assert.That(ase.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(ase.mipmapEnabled, Is.False, path);
                    foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                        Assert.That(sprite.pixelsPerUnit, Is.EqualTo(24f), sprite.name);
                }
                else if (AssetImporter.GetAtPath(path) is TextureImporter texture)
                {
                    Assert.That(texture.spritePixelsPerUnit, Is.EqualTo(24f), path);
                    Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point), path);
                    Assert.That(texture.mipmapEnabled, Is.False, path);
                    Assert.That(texture.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                }
            }
        }

        [Test]
        public void PixelCoordinateConversionMatchesCanvasAndGround()
        {
            Assert.That(Hub20Builder.PixelToWorld(779, 295), Is.EqualTo(Vector3.zero));
            Assert.That(Hub20Builder.PixelToWorld(803, 271), Is.EqualTo(new Vector3(1, 1, 0)));
            Assert.That(Hub20Builder.PixelToWorld(0, 0).x, Is.EqualTo(-1558f / 48f).Within(0.0001f));
            Assert.That(Hub20Builder.PixelToWorld(1558, 348).y, Is.EqualTo(-53f / 24f).Within(0.0001f));
        }

        [Test]
        public void EnvironmentHasAllNineArtworkLayersAndOriginalLoopTiming()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            Assert.That(prefab, Is.Not.Null);
            // Validate the nine source-art layers independently of authored
            // decoration added alongside LayoutArt (such as contact shadows).
            var renderers = prefab.transform.Find("LayoutArt").GetComponentsInChildren<SpriteRenderer>(true).Where(x => x.name != "FogBlend").ToArray();
            Assert.That(renderers.Length, Is.EqualTo(9));
            Assert.That(renderers.Select(x => x.name), Is.EquivalentTo(new[] { "Sky", "Mountains", "Castle", "DistantForest", "Fog", "Forest", "Ground", "Buildings", "TreesAndPlants" }));
            Assert.That(renderers.Where(x => x.name != "Fog" && x.name != "Buildings").All(x => x.sharedMaterial.shader.name == "Universal Render Pipeline/2D/Sprite-Unlit-Default"), Is.True);
            var buildingsMaterial = renderers.Single(x => x.name == "Buildings").sharedMaterial;
            Assert.That(buildingsMaterial.shader.name, Is.EqualTo("RelicKeeper/Hub Building Wind"));
            Assert.That(buildingsMaterial.GetFloat("_Period"), Is.EqualTo(6));
            Assert.That(buildingsMaterial.GetFloat("_Amplitude"), Is.EqualTo(1));
            Assert.That(renderers.Single(x => x.name == "Fog").sharedMaterial.shader.name, Is.EqualTo("RelicKeeper/Hub Fog"));
            Assert.That(prefab.GetComponentInChildren<HubFogVisual>(), Is.Not.Null);
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            AnimationClip clip = animator.runtimeAnimatorController.animationClips.First();
            Assert.That(clip.length, Is.EqualTo(2.16f).Within(0.025f));
            Assert.That(clip.isLooping, Is.True);
        }

        [Test]
        public void CollisionSurfaceMatchesArtworkGroundRow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            var ground = prefab.transform.Find("GroundCollision").GetComponent<BoxCollider2D>();
            Assert.That(ground.offset.y + ground.size.y / 2f, Is.Zero.Within(0.0001f));
            Assert.That(ground.size.x, Is.EqualTo(1558f / 24f).Within(0.0001f));
            Assert.That(ground.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")));
            Assert.That(ground.isTrigger, Is.False);
            var bounds = prefab.GetComponentInChildren<PolygonCollider2D>();
            Assert.That(bounds.isTrigger, Is.True);
            Assert.That(bounds.GetPath(0).Max(x => x.x), Is.EqualTo(1558f / 48f).Within(0.0001f));
        }

        [Test]
        public void ScenePreservesAllFiveOriginalActorsAndPlacesThemByTheirServices()
        {
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            Assert.That(scene.isLoaded, Is.True, "HubScene must be open for the integration check.");
            var hub = scene.GetRootGameObjects().First(x => x.name == "HubWorld");
            var npcs = hub.GetComponentsInChildren<HubServiceNpc>(true);
            var portals = hub.GetComponentsInChildren<DungeonPortal>(true);
            Assert.That(npcs.Length, Is.EqualTo(3));
            Assert.That(portals.Length, Is.EqualTo(2));
            long[] actorFileIds = { 3747383711529858298, 7813590655136366604, 3156715036723887759, 8854935430867960679, 8854935430867960679 };
            var ids = npcs.Select(x => GlobalObjectId.GetGlobalObjectIdSlow(x.gameObject)).Concat(portals.Select(x => GlobalObjectId.GetGlobalObjectIdSlow(x.gameObject))).ToArray();
            Assert.That(ids.Select(x => (long)x.targetObjectId), Is.EquivalentTo(actorFileIds));
            Assert.That(ids.Select(x => (long)x.targetPrefabId), Is.EquivalentTo(new long[] { 9045518769657461748, 3985822915088299744, 5261396444560616950, 50434182, 1877000100 }));
            foreach (HubServiceNpc npc in npcs)
            {
                // Preserve the authored tavern placement, adjusted after the original hub build.
                float x = npc.Service == HubService.Tavern ? 338.6f : npc.Service == HubService.Stash ? 515 : 1070;
                Assert.That(npc.transform.position.x, Is.EqualTo(Hub20Builder.PixelToWorld(x, 295).x).Within(0.001f));
                Assert.That(PrefabUtility.GetPropertyModifications(npc).Any(m => m.propertyPath == "m_LocalPosition.x"), Is.True, "Actor movement must be saved as a prefab override.");
            }
            Assert.That(hub.transform.Find("HubEnvironment"), Is.Not.Null);
            Assert.That(hub.transform.Find("NightSky"), Is.Null);
            Assert.That(hub.transform.Find("Korchma"), Is.Null);
            Assert.That(hub.transform.Find("Ground"), Is.Null);
        }

        [Test]
        public void SceneCameraSpawnAndSharedDungeonBackgroundAreWiredToNewEnvironment()
        {
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).ToArray();
            var controller = all.Select(x => x.GetComponent<DungeonController>()).First(x => x != null);
            var data = new SerializedObject(controller);
            var hub = (GameObject)data.FindProperty("_hubWorld").objectReferenceValue;
            var spawn = (Transform)data.FindProperty("_hubSpawnPoint").objectReferenceValue;
            var shared = (SpriteRenderer)data.FindProperty("_sharedBackgroundRenderer").objectReferenceValue;
            Assert.That(spawn.position, Is.EqualTo(Hub20Builder.PixelToWorld(325, 295) + Vector3.up * 1.5f));
            Assert.That(shared, Is.Not.Null);
            Assert.That(shared.transform.IsChildOf(hub.transform), Is.False);
            Assert.That(shared.enabled, Is.False);
            Assert.That(shared.sprite, Is.Null);
            var confiner = all.Select(x => x.GetComponent<CinemachineConfiner2D>()).First(x => x != null);
            var virtualCamera = all.Select(x => x.GetComponent<CinemachineCamera>()).First(x => x != null);
            Assert.That(virtualCamera.Lens.OrthographicSize, Is.EqualTo(5.625f));
            Assert.That(confiner.BoundingShape2D.transform.IsChildOf(hub.transform.Find("HubEnvironment")), Is.True);
            Assert.That(data.FindProperty("_cameraConfiner").objectReferenceValue, Is.SameAs(confiner));
            Assert.That(all.Any(x => x.GetComponent<LevelBoundaryGenerator>() != null), Is.False,
                "Old generated walls must not block the new portal and map edges.");
        }

        [Test]
        public void TrainingDummyUsesNewArtworkWhileKeepingItsOriginalStatsAndGameplay()
        {
            var original = Resources.Load<EnemyDataSO>("Enemy/SO_Dummy");
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(Hub20Builder.ArtRoot + "/HubTrainingDummyData.asset");
            Assert.That(data, Is.Not.Null);
            Assert.That(data.ID, Is.EqualTo(original.ID));
            Assert.That(data.Stats.Select(x => (x.Type, x.BaseValue, x.ScalingMode, x.ScalingValue)),
                Is.EqualTo(original.Stats.Select(x => (x.Type, x.BaseValue, x.ScalingMode, x.ScalingValue))));
            Assert.That(data.Animation.UsesSpriteSheets, Is.True);
            Assert.That(Resources.LoadAll<Sprite>(data.Animation.IdleSpritesResourcePath).Length, Is.EqualTo(1));
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/HubScene.unity");
            var dummy = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<DummyEvolution>(true)).Single();
            Assert.That(dummy.transform.parent.name, Is.EqualTo("HubWorld"));
            Assert.That(dummy.GetComponent<EnemyEntity>().Data, Is.SameAs(data));
            Assert.That(new SerializedObject(dummy).FindProperty("_dummyData").objectReferenceValue, Is.SameAs(data));
            Assert.That(dummy.transform.position.x, Is.EqualTo(Hub20Builder.PixelToWorld(831.5f, 295).x).Within(0.001f));
            Assert.That(dummy.transform.Find("Visual").GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("TrainingDummyVisual"));
            Assert.That(dummy.transform.Find("Visual").GetComponent<SpriteRenderer>().bounds.min.y, Is.Zero.Within(0.001f));
            Assert.That(data.Animation.VisualLocalOffset, Is.EqualTo(Vector2.zero), "The saved visual offset must not be applied twice at runtime.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            Assert.That(prefab.GetComponentInChildren<SpriteMask>(), Is.Not.Null, "Baked mannequin must be masked to avoid a second visible dummy.");
        }

        [Test]
        public void GpuRenderMatchesLayoutLandmarksAtNativeResolution()
        {
            int scenes = UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount;
            Hub20Builder.CaptureEnvironment();
            string path = Path.Combine(Path.GetTempPath(), "codex-hub-layout", "hub-unity-environment.png");
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(path));
                Assert.That(texture.width, Is.EqualTo(1558));
                Assert.That(texture.height, Is.EqualTo(348));
                // RGB samples from the supplied Aseprite frame, outside translucent fog.
                AssertPixel(texture, 10, 10, new Color32(150, 169, 176, 255));
                AssertPixel(texture, 185, 151, new Color32(100, 69, 47, 255));
                AssertPixel(texture, 520, 212, new Color32(119, 88, 56, 255));
                AssertPixel(texture, 10, 320, new Color32(27, 22, 19, 255));
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount, Is.EqualTo(scenes));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void AssertPixel(Texture2D texture, int x, int topY, Color32 expected)
        {
            Color32 pixel = texture.GetPixels32()[(texture.height - 1 - topY) * texture.width + x];
            Assert.That(pixel.r, Is.EqualTo(expected.r).Within(1));
            Assert.That(pixel.g, Is.EqualTo(expected.g).Within(1));
            Assert.That(pixel.b, Is.EqualTo(expected.b).Within(1));
        }
    }
}
