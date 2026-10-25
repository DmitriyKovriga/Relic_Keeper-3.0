using System.Collections;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Hub;
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
    public sealed class AmbientParticleTests
    {
        [TestCase(AmbientParticleKind.ForgeEmbers, 3)]
        [TestCase(AmbientParticleKind.DriftingLeaves, 1)]
        [TestCase(AmbientParticleKind.TorchDust, 4)]
        public void MotionIsDeterministicBoundedPixelSnappedAndCapped(AmbientParticleKind kind, int cap)
        {
            var extent = new Vector2(2, 3);
            int visible = 0, quiet = 0;
            for (int t = 0; t < 400; t++)
                for (int slot = 0; slot <= cap; slot++)
                {
                    float time = t * 0.13f;
                    Assert.That(AmbientParticleSource.Sample(kind, time, slot, 17, extent, 0).visible, Is.False);
                    var sample = AmbientParticleSource.Sample(kind, time, slot, 17, extent, 1);
                    var repeat = AmbientParticleSource.Sample(kind, time, slot, 17, extent, 1);
                    Assert.That(sample.offset, Is.EqualTo(repeat.offset)); Assert.That(sample.opacity, Is.EqualTo(repeat.opacity));
                    if (slot == cap) { Assert.That(sample.visible, Is.False); continue; }
                    if (!sample.visible) { quiet++; continue; }
                    visible++;
                    Assert.That(Mathf.Abs(sample.offset.x), Is.LessThanOrEqualTo(extent.x * 0.5f + 1f / 48));
                    float low = kind == AmbientParticleKind.ForgeEmbers ? 0 : kind == AmbientParticleKind.DriftingLeaves ? -extent.y : -extent.y * 0.5f;
                    float high = kind == AmbientParticleKind.DriftingLeaves ? 0 : kind == AmbientParticleKind.ForgeEmbers ? extent.y : extent.y * 0.5f;
                    Assert.That(sample.offset.y, Is.InRange(low - 1f / 48, high + 1f / 48));
                    Assert.That(sample.offset.x * 24, Is.EqualTo(Mathf.Round(sample.offset.x * 24)).Within(0.001f));
                    Assert.That(sample.offset.y * 24, Is.EqualTo(Mathf.Round(sample.offset.y * 24)).Within(0.001f));
                }
            Assert.That(visible, Is.GreaterThan(0));
            if (kind != AmbientParticleKind.TorchDust) Assert.That(quiet, Is.GreaterThan(0), "Sources need quiet gaps, especially leaves.");
            if (cap > 1) Assert.That(AmbientParticleSource.Sample(kind, 1, cap - 1, 17, extent, 0.1f).visible, Is.False);
        }

        [Test]
        public void SavedSourcesAreSparseClearOfTerrainAndBehindCharacters()
        {
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>(Hub20Builder.EnvironmentPath);
            // Tilemap world/cell conversion needs a loaded Grid hierarchy, not
            // Unity's inactive persistent prefab-asset representation.
            var room = PrefabUtility.LoadPrefabContents(LocalLightingSetup.RoomPath);
            try
            {
                var sources = hub.GetComponentsInChildren<AmbientParticleSource>(true).Concat(room.GetComponentsInChildren<AmbientParticleSource>(true)).ToArray();
                Assert.That(sources.Length, Is.EqualTo(3));
                foreach (var source in sources)
                {
                    Assert.That(source.Frames.Length, Is.EqualTo(8));
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source.Frames[0]));
                    Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                    Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                    Assert.That(source.Frames.All(x => x.pixelsPerUnit == 24), Is.True);
                    Assert.That(source.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                    var slots = source.GetComponentsInChildren<SpriteRenderer>(true);
                    Assert.That(slots.Length, Is.EqualTo(AmbientParticleSource.Capacity(source.Kind)));
                    foreach (var slot in slots)
                    {
                        Assert.That(slot.enabled, Is.False); Assert.That(slot.GetComponent<WorldColorGroup>().Role, Is.EqualTo(WorldColorRole.Environment));
                        Assert.That(SortingLayer.GetLayerValueFromID(slot.sortingLayerID), Is.LessThan(SortingLayer.GetLayerValueFromID(SortingLayer.NameToID("Hero"))));
                    }
                }
                var dust = sources.Single(x => x.Kind == AmbientParticleKind.TorchDust);
                var torch = dust.GetComponentInParent<LocalLight2DSource>(true); Assert.That(torch, Is.Not.Null);
                var maps = room.GetComponentsInChildren<Tilemap>(true).Where(x => x.GetComponent<TilemapCollider2D>() != null).ToArray();
                Assert.That(AmbientParticleSetup.ClearPocket(maps, torch.transform.position, dust.transform.position, dust.Extent), Is.True);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        [Test]
        public void PreviewCopiesDustWithItsOwnTorchAndDisposesAllSources()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null);
            var scene = stage.Scene;
            try
            {
                var dust = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<AmbientParticleSource>(true)).Single();
                var torch = dust.GetComponentInParent<LocalLight2DSource>();
                Assert.That(torch, Is.Not.Null); Assert.That(torch.gameObject.scene, Is.EqualTo(scene));
                Assert.That(dust.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(4));
                Assert.That(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Collider2D>(true)), Is.Empty);
            }
            finally { stage.Dispose(); }
            Assert.That(scene.IsValid(), Is.False);
        }

        [UnityTest]
        public IEnumerator DustRendersOnlyWithItsLightAndOwner_ThenRestoresAllSlots()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Particle camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 1; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var controller = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); controller.Profile = profile;
            profile.ambientParticleDensity = 1; profile.ambientParticleOpacity = 1;
            var torchObject = new GameObject("Torch"); SceneManager.MoveGameObjectToScene(torchObject, scene);
            var torch = torchObject.AddComponent<LocalLight2DSource>(); torch.Configure(0.3f, 4);
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AmbientParticleSetup.PrefabPath(AmbientParticleKind.TorchDust)), scene);
            source.transform.SetParent(torch.transform, false);
            var particles = source.GetComponent<AmbientParticleSource>(); particles.PreviewTime = 2;
            var target = new RenderTexture(128, 128, 24); target.Create();
            var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            bool Rendered()
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                return image.GetPixels32().Any(x => x.r > 3 || x.g > 3 || x.b > 3);
            }
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                Assert.That(Rendered(), Is.True, "Actual PixelLab sprites must reach the GPU.");
                foreach (var slot in source.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Assert.That(slot.enabled, Is.False); Assert.That(slot.transform.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(slot.color, Is.EqualTo(Color.white)); Assert.That(slot.sprite, Is.EqualTo(particles.Frames[0]));
                }
                profile.localLights = false; Assert.That(Rendered(), Is.False); profile.localLights = true;
                torch.enabled = false; Assert.That(Rendered(), Is.False); torch.enabled = true;
                torch.Configure(0, 4); Assert.That(Rendered(), Is.False); torch.Configure(0.3f, 4);
                profile.ambientParticleDensity = 0; Assert.That(Rendered(), Is.False); profile.ambientParticleDensity = 1;
                profile.ambientParticles = false; Assert.That(Rendered(), Is.False); profile.ambientParticles = true;
                profile.ambientParticleOpacity = 0; Assert.That(Rendered(), Is.False); profile.ambientParticleOpacity = 1;
                source.transform.localPosition = Vector3.right * 8;
                Assert.That(torch.ParticleIllumination(profile, source.transform.position), Is.Zero);
                source.transform.localPosition = Vector3.zero;
                WorldColorGrading.SetBindingsSuspended(true); Assert.That(Rendered(), Is.False); WorldColorGrading.SetBindingsSuspended(false);
                controller.enabled = false; Assert.That(Rendered(), Is.False);
            }
            finally
            {
                WorldColorGrading.SetBindingsSuspended(false); RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
