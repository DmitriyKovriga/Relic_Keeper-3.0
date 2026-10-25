using System.Linq;
using NUnit.Framework;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LayeredParallaxTests
    {
        [Test]
        public void AbsolutePixelMotionHasNoAccumulatedDriftAndKeepsArtworkDepth()
        {
            Vector3 origin = new Vector3(0.125f, 3f, 10f);
            Vector3 reference = new Vector3(12f, 8f, -10f);
            var moved = ParallaxEffect.EvaluatePosition(origin, reference, reference + new Vector3(4, -2, 0), 0.9f, 0.5f, 24);
            Assert.That(moved.x, Is.EqualTo(origin.x + 86f / 24f).Within(0.00001f));
            Assert.That(moved.y, Is.EqualTo(2f)); Assert.That(moved.z, Is.EqualTo(10f));
            for (int i = 0; i < 1000; i++)
                Assert.That(ParallaxEffect.EvaluatePosition(origin, reference, reference, 0.9f, 0.5f, 24), Is.EqualTo(origin));
        }

        [Test]
        public void HubMovesOnlyBackgroundAndRestoresBeforeSavingOrChangingCamera()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Parallax artwork");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                profile.layeredParallax = true;
                foreach (string name in new[] { "Sky", "Mountains", "Castle", "DistantForest", "Forest", "Fog", "FogBlend", "Ground", "Buildings", "TreesAndPlants" })
                { var go = new GameObject(name); go.transform.SetParent(root.transform); go.AddComponent<SpriteRenderer>(); }
                root.AddComponent<BoxCollider2D>();
                var group = LayeredParallaxGroup.ConfigureHub(root);
                group.ApplyView(new Vector3(10, 4, -10), profile);
                Assert.That(root.transform.Find("Mountains").position.x, Is.EqualTo(9f));
                Assert.That(root.transform.Find("Castle").position, Is.EqualTo(root.transform.Find("Mountains").position));
                Assert.That(root.transform.Find("DistantForest").position.x, Is.EqualTo(6.5f));
                Assert.That(root.transform.Find("Forest").position.x, Is.EqualTo(3f));
                foreach (string name in new[] { "Ground", "Buildings", "TreesAndPlants" })
                    Assert.That(root.transform.Find(name).position, Is.EqualTo(Vector3.zero));
                Assert.That(root.GetComponent<BoxCollider2D>().transform.position, Is.EqualTo(Vector3.zero));
                group.Restore(); Assert.That(group.Layers.All(x => x.renderer.transform.position == Vector3.zero), Is.True);
                group.ApplyView(new Vector3(-20, 0, -10), profile); group.enabled = false;
                Assert.That(group.Layers.All(x => x.renderer.transform.position == Vector3.zero), Is.True);
            }
            finally { Object.DestroyImmediate(profile); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void DungeonChildDoesNotDoubleItsParentsMotionAndRoomResetDoesNotJump()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Dungeon background");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var background = root.AddComponent<SpriteRenderer>();
            var legacy = root.AddComponent<ParallaxEffect>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            try
            {
                profile.layeredParallax = true;
                root.transform.position = new Vector3(12, 8, 10);
                profile.parallaxSilhouettes = 0.4f; // Existing profiles must become solid, without a migration.
                var group = LayeredParallaxGroup.ConfigureDungeon(background, sprite);
                Assert.That(legacy.enabled, Is.False);
                Vector3 pillar = group.Layers[1].renderer.transform.position;
                group.ResetAnchor(new Vector3(12, 8));
                group.ApplyView(new Vector3(22, 8, -10), profile);
                Assert.That(background.transform.position.x, Is.EqualTo(21f));
                Assert.That(group.Layers[1].renderer.transform.position.x, Is.EqualTo(pillar.x + 6.5f));
                Assert.That(group.Layers[1].renderer.color.a, Is.EqualTo(1f));
                group.Restore(); group.SetVisible(false); Assert.That(group.Layers[1].renderer.enabled, Is.False);
                group.SetVisible(true);
                root.transform.position = new Vector3(-40, -25, 10);
                group.ResetAnchor(new Vector3(-40, -25));
                Vector3 anchor = group.Layers[1].renderer.transform.position;
                group.ApplyView(new Vector3(-40, -25, -10), profile);
                Assert.That(group.Layers[1].renderer.transform.position, Is.EqualTo(anchor));
                group.enabled = false; Assert.That(legacy.enabled, Is.True);
                Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(sprite); EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RealHubRenderShowsDepthWhileGroundPixelsAndAuthoredTransformsStayFixed()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            using (var stage = RelicKeeper.Editor.Visuals.WorldColorPreviewStage.Create(false, null, null))
            {
                var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile);
                var target = new RenderTexture(480, 270, 24); target.Create();
                var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    stage.Controller.PreviewDepthMistProfile = profile;
                    stage.SetCameraOffset(10f);
                    var group = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LayeredParallaxGroup>()).Single();
                    var originals = group.Layers.Select(x => x.renderer.transform.position).ToArray();
                    profile.layeredParallax = false;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    var before = image.GetPixels32();
                    profile.layeredParallax = true;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    var after = image.GetPixels32();
                    int changed = 0;
                    for (int i = 0; i < after.Length; i++) if (!after[i].Equals(before[i])) changed++;
                    Assert.That(changed, Is.GreaterThan(1000), "The actual camera must render the moving background layers.");
                    for (int y = 2; y < 20; y++)
                    for (int x = 2; x < 478; x++)
                        Assert.That(after[y * 480 + x], Is.EqualTo(before[y * 480 + x]), "Ground must not move with the scenery.");
                    Assert.That(group.Layers.Select(x => x.renderer.transform.position), Is.EqualTo(originals), "Render callbacks must restore source transforms.");
                }
                finally
                {
                    RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                    Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
            }
        }

        [Test]
        public void MortfallActuallyRendersTheAdditionalColumnLayer()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            using (var stage = RelicKeeper.Editor.Visuals.WorldColorPreviewStage.Create(true,
                "Assets/Resources/Prefabs/Dungeon/MineDungeon/MortfallRoom_003.prefab", null))
            {
                var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile);
                var target = new RenderTexture(480, 270, 24); target.Create();
                var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
                var previous = RenderTexture.active;
                try
                {
                    stage.Controller.PreviewDepthMistProfile = profile;
                    profile.parallaxSilhouettes = 0;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    var before = image.GetPixels32();
                    profile.parallaxSilhouettes = 1;
                    UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(stage.Camera,
                        new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                    image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                    var after = image.GetPixels32();
                    Assert.That(after.Where((pixel, i) => !pixel.Equals(before[i])).Count(), Is.GreaterThan(100),
                        "The new middle-distance artwork must affect actual rendered pixels.");
                }
                finally
                {
                    RenderTexture.active = previous; stage.Controller.PreviewDepthMistProfile = null;
                    Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                }
            }
        }

        [Test]
        public void VisibleColumnOccludesTheCaveInsteadOfBlendingItThroughStone()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Opaque column test camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene; camera.enabled = false; camera.orthographic = true;
            camera.orthographicSize = 5.625f; camera.aspect = 480f / 270f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            profile.layeredParallax = true; profile.parallaxSilhouettes = 0.4f;
            cameraObject.AddComponent<AtmosphereController>().Profile = profile;
            var backdrop = new GameObject("Changing cave backdrop");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(backdrop, scene);
            var background = backdrop.AddComponent<SpriteRenderer>();
            var pixel = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            background.sprite = pixel; background.sharedMaterial = material;
            background.drawMode = SpriteDrawMode.Tiled; background.size = new Vector2(60, 40);
            background.sortingLayerName = "Background"; background.sortingOrder = -1000;
            var column = AssetDatabase.LoadAssetAtPath<Sprite>(RelicKeeper.Editor.Visuals.LayeredParallaxSetup.ColumnPath);
            LayeredParallaxGroup.ConfigureDungeon(background, column).ResetAnchor(Vector3.zero);
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                background.color = Color.red;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Color32 stoneBefore = image.GetPixel(72, 135), caveBefore = image.GetPixel(240, 135);
                background.color = Color.green;
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,
                    new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });
                image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply();
                Color32 stoneAfter = image.GetPixel(72, 135), caveAfter = image.GetPixel(240, 135);
                Assert.That(stoneAfter, Is.EqualTo(stoneBefore), "Changing the cave behind solid stone must not change the stone pixel.");
                Assert.That(stoneBefore.r, Is.LessThan(150), "The sample must hit the subdued column, not the red backdrop.");
                Assert.That(caveAfter, Is.Not.EqualTo(caveBefore), "The open cave must still show the changed backdrop.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(pixel); Object.DestroyImmediate(material);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        [Test]
        public void InstalledHubAndMortfallUseNativeLayersAndTheSavedAppearancePresets()
        {
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/HubObj/Hub2.0/HubEnvironment.prefab");
            var group = hub.GetComponent<LayeredParallaxGroup>();
            Assert.That(group, Is.Not.Null); Assert.That(group.Layers.Count, Is.EqualTo(7));
            Assert.That(group.Layers.Any(x => x.renderer.name == "Buildings" || x.renderer.name == "Ground"), Is.False);
            foreach (string name in new[] { "HubDepthMist", "MortfallDepthMist" })
                Assert.That(AssetDatabase.LoadAssetAtPath<AtmosphereProfileSO>("Assets/Visuals/RelicFX/" + name + ".asset").layeredParallax, Is.True);
            var data = AssetDatabase.LoadAssetAtPath<Scripts.Dungeon.DungeonDataSO>("Assets/Resources/Dungeons/MortFallDungeonSO.asset");
            Assert.That(data.ParallaxSilhouette, Is.Not.Null);
            Assert.That(data.ParallaxSilhouette.pixelsPerUnit, Is.EqualTo(24f));
        }
    }
}
