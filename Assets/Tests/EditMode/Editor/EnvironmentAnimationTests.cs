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
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class EnvironmentAnimationTests
    {
        [UnityTest]
        public IEnumerator DropFromAnOffscreenCeilingCanLandInsideTheCameraView()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Offscreen drip camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var controller = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); controller.Profile = profile;
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Visuals/CaveDrip.prefab"), scene);
            source.transform.position = Vector3.up * 10;
            var animation = source.GetComponent<EnvironmentAnimation>();
            animation.Configure(EnvironmentAnimationKind.WaterDrip, animation.Frames, 0, 10);
            var target = new RenderTexture(128, 128, 24); target.Create();
            var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                yield return null;
                float fall = Mathf.Sqrt(20f / 9);
                animation.PreviewTime = 6.25f + fall * 0.5f;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().All(x => x.r == 0 && x.g == 0 && x.b == 0), Is.True);
                animation.PreviewTime = 6.25f + fall + 0.1f;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Any(x => x.r > 5), Is.True, "An offscreen emitter must still show the splash on the visible floor.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
        [Test]
        public void DripHasQuietFormationFallAndImpact_AndZeroRateDisablesIt()
        {
            const float distance = 3;
            float fall = Mathf.Sqrt(2f * distance / 9f);
            Assert.That(EnvironmentAnimation.SampleDrip(0, 0, distance, 1).visible, Is.False);
            Assert.That(EnvironmentAnimation.SampleDrip(100, 0, distance, 0).visible, Is.False);
            var forming = EnvironmentAnimation.SampleDrip(6.05f, 0, distance, 1);
            Assert.That(forming.visible, Is.True); Assert.That(forming.frame, Is.InRange(0, 1)); Assert.That(forming.offsetY, Is.Zero);
            var falling = EnvironmentAnimation.SampleDrip(6.25f + fall * 0.5f, 0, distance, 1);
            Assert.That(falling.frame, Is.InRange(2, 3)); Assert.That(falling.offsetY, Is.EqualTo(-distance * 0.25f).Within(0.001f));
            var impact = EnvironmentAnimation.SampleDrip(6.25f + fall + 0.1f, 0, distance, 1);
            Assert.That(impact.frame, Is.InRange(4, 7)); Assert.That(impact.offsetY, Is.EqualTo(-distance));
            Assert.That(EnvironmentAnimation.SampleDrip(6.25f + fall + 0.31f, 0, distance, 1).visible, Is.False);
            Assert.That(EnvironmentAnimation.SampleDrip(3.1f, 0, distance, 2).visible, Is.True);
            Assert.That(EnvironmentAnimation.SampleDrip(3.1f, 0, distance, 1).visible, Is.False);
        }

        [Test]
        public void PixelLabClipsAndSavedSourcesHaveCorrectFramesGridAndNoCollision()
        {
            foreach (var clip in new[] { ("ChimneySmoke", 16), ("CaveDrip", 8) })
            {
                string path = EnvironmentAnimationSetup.ArtRoot + "/" + clip.Item1 + ".png";
                var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.That(frames.Length, Is.EqualTo(clip.Item2)); Assert.That(frames.All(x => x.pixelsPerUnit == 24), Is.True);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            }
            var hub = AssetDatabase.LoadAssetAtPath<GameObject>(RelicKeeper.Editor.Hub.Hub20Builder.EnvironmentPath);
            Assert.That(hub.transform.Find("TavernChimneySmoke"), Is.Not.Null);
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(LocalLightingSetup.RoomPath);
            var drip = room.transform.Find("CeilingDrip").GetComponent<EnvironmentAnimation>();
            Assert.That(drip.FallDistance, Is.InRange(1.5f, 6));
            foreach (var flame in room.GetComponentsInChildren<LocalLight2DSource>(true))
                if (flame.transform.position.y < drip.transform.position.y && flame.transform.position.y > drip.transform.position.y - drip.FallDistance)
                    Assert.That(Mathf.Abs(flame.transform.position.x - drip.transform.position.x), Is.GreaterThanOrEqualTo(0.8f), "Do not drop water directly onto a torch.");
            foreach (var source in hub.GetComponentsInChildren<EnvironmentAnimation>(true).Concat(room.GetComponentsInChildren<EnvironmentAnimation>(true)))
            {
                Assert.That(source.GetComponentsInChildren<Collider2D>(true), Is.Empty);
                Assert.That(source.GetComponentInChildren<WorldColorGroup>(true).Role, Is.EqualTo(WorldColorRole.Environment));
            }
        }

        [UnityTest]
        public IEnumerator SmokeRendersOnlyForItsOwner_AndRestoresTransientFrameColorAndPosition()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Ambient render camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 1, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var controller = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); controller.Profile = profile;
            var source = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Visuals/ChimneySmoke.prefab"), scene);
            var animation = source.GetComponent<EnvironmentAnimation>(); animation.PreviewTime = 0.5f;
            var visual = source.GetComponentInChildren<SpriteRenderer>(true);
            var target = new RenderTexture(128, 128, 24); target.Create();
            var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate(); yield return null;
                profile.ambientAnimations = false;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().All(x => x.r == 0 && x.g == 0 && x.b == 0), Is.True);
                profile.ambientAnimations = true;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().Any(x => x.r > 5), Is.True, "The actual smoke sprite must render.");
                Assert.That(visual.enabled, Is.False); Assert.That(visual.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(visual.color, Is.EqualTo(Color.white)); Assert.That(visual.sprite, Is.EqualTo(animation.Frames[0]));
                controller.enabled = false;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply();
                Assert.That(image.GetPixels32().All(x => x.r == 0 && x.g == 0 && x.b == 0), Is.True, "A camera without an active owner sees no ambient animation.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
