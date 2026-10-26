using System.Collections;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LocalLightShaftTests
    {
        [Test]
        public void ResizedTiltedShaftClipsAtTheActualTerrain_AndKeepsDustInItsOwnCoordinates()
        {
            var root = new GameObject("Shaped shaft terrain", typeof(Grid));
            var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D)); floor.transform.SetParent(root.transform, false);
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.Grid;
            var light = new GameObject("Shaft"); light.transform.SetParent(root.transform, false); light.transform.position = new Vector3(0, 7);
            var source = light.AddComponent<LocalLightShaftSource>();
            try
            {
                var map = floor.GetComponent<Tilemap>();
                for (int x = -10; x < 20; x++) map.SetTile(new Vector3Int(x, 0), tile);
                source.Configure(AssetDatabase.LoadAssetAtPath<Sprite>(LocalLightShaftSetup.ArtPath), new Vector2Int(10, 14), Enumerable.Repeat(160f, 64).ToArray());
                var anchor = source.SourcePosition;
                source.SetShape(new Vector2(2.5f, 1.5f), 25);
                Assert.That(source.SourcePosition, Is.EqualTo(anchor), "Changing shape must not move the opening.");
                var limits = LocalLightShaftSetup.ClipColumns(new[] { map }, source);
                source.Configure(source.Artwork, source.SourcePixel, limits);
                Assert.That(limits.Any(x => x < 160), Is.True);
                for (int x = 0; x < limits.Length; x++)
                {
                    var hidden = source.PixelToLocal(new Vector2(x + 0.5f, limits[x] + 1));
                    Assert.That(source.Contains(hidden), Is.False, "Dust cannot cross the transformed terrain mask.");
                    if (limits[x] >= 160) continue;
                    for (int row = source.SourcePixel.y; row < limits[x]; row++)
                    {
                        var world = source.transform.TransformPoint(source.PixelToLocal(new Vector2(x + 0.5f, row + 0.5f)));
                        Assert.That(map.HasTile(map.WorldToCell(world)), Is.False, "The displayed part cannot run through a tile.");
                    }
                }
                var dust = source.DustPosition(new Vector2(0, -0.4f));
                Assert.That(source.Contains(dust), Is.True);
                Assert.That(source.ParticleIllumination(dust), Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void LightLibraryAndPreviewKeepAuthoredShapeAndFillSettings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomLightingAuthoring.SoftShaftPath);
            Assert.That(prefab, Is.Not.Null);
            var soft = prefab.GetComponent<LocalLightShaftSource>(); Assert.That(soft.Size.x, Is.GreaterThan(1));
            foreach (string path in new[] { RoomLightingAuthoring.CoolFillPath, RoomLightingAuthoring.WarmFillPath })
            {
                var fill = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<LocalLight2DSource>();
                Assert.That(fill.IsRoomFill, Is.True); Assert.That(fill.HasFlameMotion, Is.False);
                Assert.That(fill.GetComponentsInChildren<Collider2D>(), Is.Empty);
            }
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                var fills = stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).Where(x => x.IsRoomFill).ToArray();
                Assert.That(fills.Length, Is.EqualTo(1)); Assert.That(fills[0].Radius, Is.EqualTo(4.5f));
            }
        }
        [Test]
        public void ClippingStopsAtTheFirstFloorWallAndOneWayPlatform()
        {
            var root = new GameObject("Shaft terrain", typeof(Grid));
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.Grid;
            Tilemap Map(string name)
            {
                var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
                go.transform.SetParent(root.transform, false); return go.GetComponent<Tilemap>();
            }
            try
            {
                var ground = Map("Ground"); var platforms = Map("Platforms");
                for (int x = 0; x < 4; x++) ground.SetTile(new Vector3Int(x, 0), tile);
                ground.SetTile(new Vector3Int(1, 3), tile);
                var art = AssetDatabase.LoadAssetAtPath<Sprite>(LocalLightShaftSetup.ArtPath);
                var maps = new[] { ground, platforms }; var top = new Vector3(0, 6); var source = new Vector2Int(10, 14);
                var limits = LocalLightShaftSetup.ClipColumns(maps, top, art, source);
                Assert.That(limits[5], Is.EqualTo(116).Within(1));
                Assert.That(limits[30], Is.EqualTo(44).Within(1), "A wall must stop the shaft rather than letting it resume below.");
                platforms.SetTile(new Vector3Int(0, 2), tile);
                Assert.That(LocalLightShaftSetup.ClipColumns(maps, top, art, source)[5], Is.EqualTo(68).Within(1));
                Assert.That(LocalLightShaftSetup.ClipColumns(new Tilemap[0], top, art, source).All(x => x == 160), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(tile); }
        }

        [Test]
        public void PlacedBeamsRetainTheirOwnBakedClipsAndArtwork()
        {
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var shafts = room.GetComponentsInChildren<LocalLightShaftSource>(true);
                Assert.That(shafts, Is.Not.Empty);
                var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                foreach (var shaft in shafts)
                {
                Assert.That(shaft.Visibility, Is.EqualTo(CeilingBeamVisibility.Bake(maps, shaft)), "Authored shape and placement determine the pixel visibility.");
                Assert.That(shaft.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                var beam = shaft.transform.Find("Beam").GetComponent<SpriteRenderer>();
                int heroLayer = SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Hero"));
                Assert.That(SortingLayer.GetLayerValueFromID(beam.sortingLayerID), Is.LessThan(heroLayer));
                Assert.That(shaft.GetComponent<WorldColorGroup>().Role, Is.EqualTo(WorldColorRole.Background));
                Assert.That(shaft.Artwork.pixelsPerUnit, Is.EqualTo(24));
                shaft.PrepareForSave(); Assert.That(beam.sprite, Is.SameAs(shaft.Artwork));
                Assert.That(EditorUtility.IsPersistent(beam.sprite), Is.True, "Saving must keep the source asset, not an unsaved sprite clone.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [Test]
        public void PreviewCopiesTheClippedVolume_AndDustCannotCrossAnOverhang()
        {
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                Assert.That(stage.FocusShaft(), Is.True);
                var shaft = stage.Shafts.First();
                var dust = shaft.GetComponentsInChildren<AmbientParticleSource>(true).Single();
                Assert.That(dust.Kind, Is.EqualTo(AmbientParticleKind.ShaftDust));
                Assert.That(dust.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(3));
                var limits = Enumerable.Repeat(160f, 64).ToArray();
                for (int x = 32; x < limits.Length; x++) limits[x] = 60;
                shaft.Configure(shaft.Artwork, shaft.SourcePixel, limits);
                Assert.That(shaft.Contains(shaft.DustPosition(new Vector2(0, -0.4f))), Is.True);
                Assert.That(shaft.Contains(shaft.DustPosition(new Vector2(0.4f, 0.4f))), Is.False);
                Assert.That(shaft.ParticleIllumination(shaft.DustPosition(new Vector2(0.4f, 0.4f))), Is.Zero);
                Assert.That(AmbientParticleSource.Sample(AmbientParticleKind.ShaftDust, 1, 0, 1601, Vector2.one, 0).visible, Is.False);
                Assert.That(stage.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider2D>(true)), Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator BeamRendersOnlyForItsOwnerAndControls_AndNeverBelowTheBakedObstacle()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Shaft camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 3.6f; camera.aspect = 1;
            camera.transform.position = new Vector3(1.33f, -3.33f, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var owner = cameraObject.AddComponent<AtmosphereController>(); var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            profile.ambientParticles = false; owner.Profile = profile;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LocalLightShaftSetup.PrefabPath), scene);
            var shaft = root.GetComponent<LocalLightShaftSource>(); var beam = root.transform.Find("Beam").GetComponent<SpriteRenderer>();
            var target = new RenderTexture(128, 128, 24); target.Create(); var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Color32[] Render()
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetPixels32();
            }
            bool Dark(Color32[] pixels) => pixels.All(x => x.r == 0 && x.g == 0 && x.b == 0);
            try
            {
                yield return null;
                profile.localShafts = false; Assert.That(Dark(Render()), Is.True);
                profile.localShafts = true; Assert.That(Render().Any(x => x.r > 2), Is.True, "The actual pixel shaft must render.");
                var originalLook = shaft.ReadAppearance();
                var brighter = originalLook; brighter.strength = 2;
                double normalLight = Render().Sum(x => (double)x.r + x.g + x.b);
                shaft.ApplyAppearance(brighter);
                Assert.That(Render().Sum(x => (double)x.r + x.g + x.b), Is.GreaterThan(normalLight * 1.2), "Strength increases rendered brightness; final color grading can compress highlights.");
                Assert.That(shaft.ReadAppearance().opacity, Is.EqualTo(originalLook.opacity), "Strength must not rewrite opacity.");
                brighter.strength = 0; shaft.ApplyAppearance(brighter); Assert.That(Dark(Render()), Is.True);
                shaft.ApplyAppearance(originalLook);
                Assert.That(ShaderUtil.ShaderHasError(beam.sharedMaterial.shader), Is.False);
                var geometry = beam.sprite; Assert.That(beam.enabled, Is.False);
                Render(); Assert.That(beam.sprite, Is.SameAs(geometry), "Camera renders reuse the same geometry.");
                profile.localShaftStrength = 0; Assert.That(Dark(Render()), Is.True);
                var independent = shaft.ReadAppearance(); independent.followEnvironment = false;
                shaft.ApplyAppearance(independent);
                Assert.That(Render().Any(x => x.r > 2), Is.True, "An independent beam keeps its local brightness even with zero overall brightness.");
                independent.visible = false; shaft.ApplyAppearance(independent);
                Assert.That(Dark(Render()), Is.True, "This beam's own switch hides its volume.");
                independent.visible = true; independent.followEnvironment = true; shaft.ApplyAppearance(independent);
                profile.localShaftStrength = 1;
                var pixels = Enumerable.Repeat((byte)255, 64 * 160).ToArray();
                for (int row = 0; row < 80; row++) for (int x = 0; x < 64; x++) pixels[row * 64 + x] = 0;
                shaft.ConfigureVisibility(pixels);
                var masked = Render();
                Assert.That(masked.Take(128 * 50).All(x => x.r == 0 && x.g == 0 && x.b == 0), Is.True, "The GPU must apply the baked pixel mask.");
                Assert.That(masked.Any(x => x.r > 2), Is.True);
                var properties = new MaterialPropertyBlock(); beam.GetPropertyBlock(properties); var maskTexture = properties.GetTexture("_ShaftVisibility");
                Render(); beam.GetPropertyBlock(properties); Assert.That(properties.GetTexture("_ShaftVisibility"), Is.SameAs(maskTexture), "Camera renders reuse the same uploaded mask.");
                shaft.Configure(shaft.Artwork, shaft.SourcePixel, Enumerable.Repeat(80f, 64).ToArray());
                var uploaded = new MaterialPropertyBlock(); beam.GetPropertyBlock(uploaded);
                Assert.That(uploaded.GetFloatArray("_ShaftClearPixels").All(x => x == 80), Is.True);
                var clipped = Render();
                System.IO.Directory.CreateDirectory("Logs/LightShafts"); System.IO.File.WriteAllBytes("Logs/LightShafts/TestClip.png", image.EncodeToPNG());
                Assert.That(clipped.Take(128 * 50).All(x => x.r == 0 && x.g == 0 && x.b == 0), Is.True, "No volume can remain below the obstacle.");
                Assert.That(clipped.Any(x => x.r > 2), Is.True);
                owner.enabled = false; Assert.That(Dark(Render()), Is.True, "Cameras without an active atmosphere owner see no shaft.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
