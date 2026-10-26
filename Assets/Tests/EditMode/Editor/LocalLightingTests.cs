using System.Collections;
using System.Linq;
using NUnit.Framework;
using Scripts.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LocalLightingTests
    {
        [Test]
        public void ReloadRecoveryClearsStalePauseOnlyAfterAStableEditorInterval()
        {
            const string key = "RelicKeeper.WorldColors.PlayTransition";
            var guard = typeof(RelicKeeper.Editor.Visuals.WorldColorSaveGuard);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            try
            {
                WorldColorGrading.SetBindingsSuspended(true); SessionState.SetBool(key, true);
                guard.GetField("_reloadStableSince", flags).SetValue(null, -1d);
                guard.GetMethod("ResumeAfterReload", flags).Invoke(null, null);
                Assert.That(WorldColorGrading.BindingsSuspended, Is.True, "Do not resume in the first apparently stable update.");
                guard.GetField("_reloadStableSince", flags).SetValue(null, EditorApplication.timeSinceStartup - 1d);
                guard.GetMethod("ResumeAfterReload", flags).Invoke(null, null);
                Assert.That(WorldColorGrading.BindingsSuspended, Is.False);
                Assert.That(SessionState.GetBool(key, true), Is.False);
            }
            finally { SessionState.SetBool(key, false); WorldColorGrading.SetBindingsSuspended(false); }
        }

        [Test]
        public void SourceLayerPolicySurvivesGlobalLightSync()
        {
            var source = new GameObject("Local light policy").AddComponent<LocalLight2DSource>();
            try
            {
                int hero = SortingLayer.NameToID("Hero"); source.Light.targetSortingLayers = new[] { hero };
                typeof(AtmosphereController).Assembly.GetType("Scripts.Visuals.WorldLight2DSetup")
                    .GetMethod("SyncAllLights", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                Assert.That(source.Light.targetSortingLayers, Is.EqualTo(new[] { hero }));
                Assert.That(source.Light.blendStyleIndex, Is.EqualTo(1));
                source.Light.intensity = 0.5f;
                source.enabled = false;
                Assert.That(source.Light.intensity, Is.Zero);
            }
            finally { Object.DestroyImmediate(source.gameObject); }
        }

        [Test]
        public void PilotSourcesAreAttachedToTheForgeAndOneRealRoom_WithoutCollision()
        {
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>(RelicKeeper.Editor.Hub.Hub20Builder.EnvironmentPath);
            var light = hub.transform.Find("WarmForgeLight");
            Assert.That(light, Is.Not.Null);
            Assert.That(light.GetComponent<LocalLight2DSource>(), Is.Not.Null);
            Assert.That(light.GetComponentsInChildren<Collider2D>(), Is.Empty);
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(RelicKeeper.Editor.Visuals.LocalLightingSetup.RoomPath);
            Assert.That(room.GetComponent<TorchLightBinding>(), Is.Not.Null);
            var torches = room.GetComponentsInChildren<LocalLight2DSource>(true).Where(x => !x.IsRoomFill).ToArray();
            Assert.That(torches.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(torches.All(x => x.HasFlameMotion && x.GetComponentsInChildren<Collider2D>().Length == 0), Is.True);
            Assert.That(room.GetComponentsInChildren<LocalLightOccluder>(true), Is.Not.Empty);
            Assert.That(hub.GetComponentsInChildren<LocalLightOccluder>(true), Is.Not.Empty);
            Assert.That(torches.Any(x => x.transform.position.y < 6), Is.True, "The entrance torch lights the player's platform.");
        }

        [UnityTest]
        public IEnumerator TerrainStopsLightInsideAndBehindWallsAndFloors() => CheckTerrainLighting(false);

        [UnityTest]
        public IEnumerator RoomFillHasIndependentColorAndControls_AndTerrainStillBlocksIt() => CheckTerrainLighting(true);

        private IEnumerator CheckTerrainLighting(bool roomFill)
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Terrain lighting camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 3.5f; camera.aspect = 480f / 270f;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var controller = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); controller.Profile = profile;
            var sourceObject = new GameObject("Source and terrain");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sourceObject, scene);
            var source = sourceObject.AddComponent<LocalLight2DSource>(); source.Configure(0.35f, 4); source.BindPreview(controller);
            if (roomFill)
            {
                source.ConfigureFill(0.35f, 4, RelicKeeper.Editor.Visuals.RoomLightingAuthoring.CoolTint);
                profile.localLightStrength = 0; profile.localLightRadius = 0.5f; profile.localLightTint = Color.red;
            }
            RelicKeeper.Editor.Visuals.LocalLightOccluderBuilder.AddPolygon(sourceObject.transform, new[]
            { new Vector3(0.75f, -3), new Vector3(1.25f, -3), new Vector3(1.25f, 3), new Vector3(0.75f, 3) });
            RelicKeeper.Editor.Visuals.LocalLightOccluderBuilder.AddPolygon(sourceObject.transform, new[]
            { new Vector3(-4, -1.25f), new Vector3(4, -1.25f), new Vector3(4, -0.75f), new Vector3(-4, -0.75f) });
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(new Color(0.2f, 0.2f, 0.2f, 1), 4).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var material = new Material(Shader.Find("RelicKeeper/Sprites/World Color Unlit")); material.SetFloat("_RKGradeGroup", 1);
            foreach (var point in new[] { new Vector3(-2, 0), new Vector3(2, 0), new Vector3(-2, -2), new Vector3(1, 0) })
            {
                var surface = new GameObject("Lighting probe"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(surface, scene);
                surface.transform.position = point;
                var renderer = surface.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingLayerName = "Hero";
            }
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false, true); var previous = RenderTexture.active;
            var foreignScene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                profile.localLights = false; profile.roomFillLights = false; var before = Render(camera, target, image);
                if (roomFill) profile.roomFillLights = true; else profile.localLights = true;
                var after = Render(camera, target, image);
                Assert.That(after[135 * 480 + 163].r, Is.GreaterThan(before[135 * 480 + 163].r + 5), "An exposed actor still receives light.");
                if (roomFill)
                {
                    Assert.That(source.Light.color, Is.EqualTo(RelicKeeper.Editor.Visuals.RoomLightingAuthoring.CoolTint));
                    Assert.That(source.Light.pointLightOuterRadius, Is.EqualTo(4));
                    Assert.That(source.HasFlameMotion, Is.False);
                }
                foreach (int pixel in new[] { 135 * 480 + 317, 58 * 480 + 163, 135 * 480 + 279 })
                    Assert.That(after[pixel].r, Is.EqualTo(before[pixel].r).Within(1), "Solid wall interiors, their far side, and underground areas must not receive source light.");
                foreignScene = EditorSceneManager.NewPreviewScene();
                var foreign = new GameObject("Another preview's overlapping solid terrain");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(foreign, foreignScene);
                var foreignCaster = RelicKeeper.Editor.Visuals.LocalLightOccluderBuilder.AddPolygon(foreign.transform, new[]
                { new Vector3(-5, -5), new Vector3(5, -5), new Vector3(5, 5), new Vector3(-5, 5) });
                var isolated = Render(camera, target, image);
                Assert.That(isolated[135 * 480 + 163].r, Is.EqualTo(after[135 * 480 + 163].r).Within(1),
                    "Another preview's terrain must not shadow this camera, or the actual game.");
                Assert.That(foreignCaster.enabled, Is.True, "Restore the other scene's caster after rendering.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                if (foreignScene.IsValid()) EditorSceneManager.ClosePreviewScene(foreignScene);
                Object.DestroyImmediate(texture); Object.DestroyImmediate(sprite); Object.DestroyImmediate(material); Object.DestroyImmediate(profile);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }

        [UnityTest]
        public IEnumerator ActorStoneAndSwayShadersShareTheLight_WhileBackgroundEffectsAndFlashStayReadable()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Lighting render camera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene; camera.enabled = false; camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 3.5f; camera.aspect = 480f / 270f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var controller = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            profile.localLights = true; controller.Profile = profile;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(new Color(0.15f, 0.15f, 0.15f, 1), 4).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var materials = new Material[8];
            var fillObject = new GameObject("Existing global fill");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillObject, scene);
            var fill = fillObject.AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            fill.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            fill.blendStyleIndex = 0; fill.intensity = 1; fill.color = Color.white;
            fill.targetSortingLayers = new[] { SortingLayer.NameToID("Default"), SortingLayer.NameToID("Hero"), SortingLayer.NameToID("Background") };
            var sourceObject = new GameObject("Local source");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sourceObject, scene);
            var source = sourceObject.AddComponent<LocalLight2DSource>(); source.Configure(0.25f, 4f); source.BindPreview(controller);
            var target = new RenderTexture(480, 270, 24); target.Create();
            var readback = new Texture2D(480, 270, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                string[] shaders = { "RelicKeeper/Sprites/Relic Sprite FX", "RelicKeeper/Hub Building Wind", "RelicKeeper/Sprites/World Color Unlit",
                    "RelicKeeper/Sprites/World Color Unlit", "RelicKeeper/Sprites/Relic Sprite FX",
                    "RelicKeeper/Sprites/World Color Lit", "RelicKeeper/Sprites/World Color Lit", "RelicKeeper/Sprites/World Color Lit" };
                for (int i = 0; i < shaders.Length; i++)
                {
                    materials[i] = new Material(Shader.Find(shaders[i]));
                    Assert.That(materials[i].shader.isSupported, Is.True, shaders[i]);
                    materials[i].SetFloat("_RKGradeGroup", i == 3 || i == 6 ? 3 : i == 0 || i == 4 ? 1 : 2);
                    materials[i].SetFloat("_RKGradeIsEffect", i == 4 || i == 7 ? 1 : 0);
                    if (i == 1) materials[i].SetFloat("_PreviewTime", 0);
                    var go = new GameObject("Lighting surface " + i);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                    go.transform.position = i < 3 ? new Vector3((i - 1) * 2, 0) : i < 5
                        ? new Vector3((i - 3) * 2, 2) : new Vector3((i - 6) * 2, -2);
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = materials[i];
                    renderer.sortingLayerName = i == 3 ? "Background" : i == 0 || i == 4 ? "Hero" : "Default";
                }
                // ExecuteAlways Light2D builds its point mesh/bounds on an engine update.
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                profile.localLights = false;
                var before = Render(camera, target, readback);
                profile.localLights = true;
                var after = Render(camera, target, readback);
                foreach (int x in new[] { 163, 240, 317 })
                    Assert.That(after[135 * 480 + x].r, Is.GreaterThan(before[135 * 480 + x].r + 5), "Actor, stone and wind material must receive the same real Light2D.");
                Assert.That(after[212 * 480 + 240], Is.EqualTo(before[212 * 480 + 240]), "Distant art must stay unlit.");
                Assert.That(after[212 * 480 + 317], Is.EqualTo(before[212 * 480 + 317]), "Combat effects must retain authored colors.");
                Assert.That(after[58 * 480 + 163].r, Is.GreaterThan(before[58 * 480 + 163].r + 1), "The existing lit path receives local light while preserving the dark albedo at the edge of its reach.");
                Assert.That(after[58 * 480 + 240], Is.EqualTo(before[58 * 480 + 240]), "Lit background retains its existing global fill without local illumination.");
                Assert.That(after[58 * 480 + 317], Is.EqualTo(before[58 * 480 + 317]), "Lit effects retain their existing global fill without local illumination.");
                materials[0].SetFloat("_FxFlash", 1f); materials[0].SetColor("_FxFlashColor", Color.blue);
                after = Render(camera, target, readback);
                Assert.That(after[135 * 480 + 163].b, Is.GreaterThan(240), "Local lighting must not wash out a hit flash.");
                Assert.That(after[135 * 480 + 163].r, Is.LessThan(5));
                Assert.That(source.Light.intensity, Is.Zero, "A completed render must not leak light into another camera.");
                var other = new GameObject("Unconfigured camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(other.gameObject, scene);
                other.CopyFrom(camera); other.enabled = false; other.scene = scene; other.transform.position = camera.transform.position;
                materials[0].SetFloat("_FxFlash", 0);
                var otherImage = Render(other, target, readback);
                Assert.That(otherImage[135 * 480 + 240], Is.EqualTo(before[135 * 480 + 240]), "A camera without an atmosphere owner sees no local illumination.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                foreach (var material in materials) if (material != null) Object.DestroyImmediate(material);
                Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); Object.DestroyImmediate(profile);
                Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
            }
        }
        private static Color32[] Render(Camera camera, RenderTexture target, Texture2D image)
        {
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); image.Apply(); return image.GetPixels32();
        }

        [Test]
        public void ExposedTerrainReceivesLightWithoutOpeningInternalStepSeams()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Stepped terrain", typeof(Grid)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var child = new GameObject("Ground", typeof(UnityEngine.Tilemaps.Tilemap), typeof(UnityEngine.Tilemaps.TilemapRenderer), typeof(UnityEngine.Tilemaps.TilemapCollider2D));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(child, scene); child.transform.SetParent(root.transform, false);
            var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>(); tile.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Grid;
            try
            {
                var map = child.GetComponent<UnityEngine.Tilemaps.Tilemap>();
                foreach (var cell in new[] { Vector3Int.zero, Vector3Int.right, new Vector3Int(1, 1) }) map.SetTile(cell, tile);
                RelicKeeper.Editor.Visuals.LocalLightOccluderBuilder.ConfigureRoom(root);
                var outlines = root.GetComponentsInChildren<LocalLightOccluder>().Select(x => x.Outline).ToArray();
                bool Blocked(Vector2 point) => outlines.Any(x => point.x > x.Min(v => v.x) && point.x < x.Max(v => v.x) &&
                    point.y > x.Min(v => v.y) && point.y < x.Max(v => v.y));
                Assert.That(Blocked(new Vector2(0.5f, 0.9f)), Is.False, "The exposed top can catch light.");
                Assert.That(Blocked(new Vector2(1.5f, 1.9f)), Is.False);
                Assert.That(Blocked(new Vector2(1.5f, 0.9f)), Is.True, "Do not carve an air slit under a taller neighboring tile.");
                Assert.That(Blocked(new Vector2(0.5f, 0.5f)), Is.True, "The terrain body stays opaque.");
                Assert.That(Blocked(new Vector2(1.5f, 1.5f)), Is.True);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(tile); }
        }

        [UnityTest]
        public IEnumerator BothSavedTorchesLightTheGroundSurfaceWithoutLightingBelowIt()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var stage = RelicKeeper.Editor.Visuals.WorldColorPreviewStage.Create(true, RelicKeeper.Editor.Visuals.LocalLightingSetup.RoomPath, null);
            var profile = Object.Instantiate(stage.Controller.ActiveDepthMistProfile);
            profile.ambientParticles = false; profile.ambientAnimations = false; profile.backgroundMist = 0; profile.shafts = 0;
            profile.localLightStrength = 3; profile.localLightRadius = 1;
            stage.Controller.PreviewDepthMistProfile = profile;
            var target = new RenderTexture(480, 270, 24); target.Create();
            var image = new Texture2D(480, 270, TextureFormat.RGBA32, false, true); var previous = RenderTexture.active;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                var torches = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).Where(x => !x.IsRoomFill).OrderBy(x => x.transform.position.x).ToArray();
                Assert.That(torches.Length, Is.EqualTo(2));
                foreach (var torch in torches)
                {
                    stage.Camera.transform.position = torch.transform.position + new Vector3(0, -1.2f, -10);
                    profile.localLights = false; var before = Render(stage.Camera, target, image);
                    profile.localLights = true; var after = Render(stage.Camera, target, image);
                    int Pixel(Vector3 world)
                    {
                        Vector3 viewport = stage.Camera.WorldToViewportPoint(world);
                        return Mathf.FloorToInt(viewport.y * 270) * 480 + Mathf.FloorToInt(viewport.x * 480);
                    }
                    int top = Pixel(new Vector3(torch.transform.position.x + 0.25f, 3.875f));
                    Assert.That(after[top].r, Is.GreaterThan(before[top].r + 3), torch.name + " must light its visible platform surface.");
                    foreach (float y in new[] { 3.4f, 2.5f })
                    {
                        int below = Pixel(new Vector3(torch.transform.position.x + 0.25f, y));
                        Assert.That(after[below].r, Is.EqualTo(before[below].r).Within(1), torch.name + " must still stop at the stone beneath the receiving surface.");
                    }
                }
            }
            finally
            {
                stage.Controller.PreviewDepthMistProfile = null; RenderTexture.active = previous;
                stage.Dispose(); Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
