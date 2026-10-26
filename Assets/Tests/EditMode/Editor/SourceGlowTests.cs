using System.Collections;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Dungeon;
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
    public sealed class SourceGlowTests
    {
        [Test]
        public void IndividualSourceSettingsSurvivePrefabSave_WithoutSavingTheTransientRenderer()
        {
            string path = "Assets/__SourceGlow_" + System.Guid.NewGuid().ToString("N") + ".prefab";
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Saved glow source"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var light = root.AddComponent<LocalLight2DSource>(); light.ConfigureFlame(17);
                var look = SourceGlowSettings.Flame;
                look.radius = 1.7f; look.strength = 0.6f; look.softness = 2.3f; look.offset = new Vector2(0.1f, 0.2f);
                light.ConfigureGlow(look);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved); Assert.That(saved, Is.True);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(asset.GetComponent<LocalLight2DSource>().Glow, Is.EqualTo(look));
                Assert.That(asset.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
                Assert.That(copy.GetComponent<LocalLight2DSource>().Glow, Is.EqualTo(look));
                Assert.That(copy.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(1));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); AssetDatabase.DeleteAsset(path); }
        }

        [Test]
        public void FlamesOwnOneTransientHaloAndReenableWithoutDuplicates_FillsHaveNone()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Glow lifecycle"); SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var light = root.AddComponent<LocalLight2DSource>();
                Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
                light.ConfigureFlame(17); light.ConfigureFlame(17);
                var halo = root.GetComponentsInChildren<SpriteRenderer>(true).Single();
                Assert.That(halo.gameObject.hideFlags.HasFlag(HideFlags.DontSaveInBuild), Is.True);
                Assert.That(halo.enabled, Is.False); Assert.That(halo.sprite.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(halo.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(ShaderUtil.ShaderHasError(halo.sharedMaterial.shader), Is.False);
                light.enabled = false; Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Is.Empty);
                light.enabled = true; Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<Collider2D>(), Is.Empty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void RealMortfallPreviewIncludesFlameAndPortalHalosWithoutGameplayScripts()
        {
            using (var stage = WorldColorPreviewStage.Create(true, LocalLightingSetup.RoomPath, null))
            {
                var roots = stage.Scene.GetRootGameObjects();
                var halos = roots.SelectMany(x => x.GetComponentsInChildren<SpriteRenderer>(true)).Where(x => x.name == "Source glow (preview)").ToArray();
                var lights = roots.SelectMany(x => x.GetComponentsInChildren<LocalLight2DSource>(true)).Where(x => x.HasFlameMotion).ToArray();
                Assert.That(lights.Length, Is.GreaterThanOrEqualTo(2));
                Assert.That(halos.Length, Is.GreaterThan(lights.Length), "The portal also owns a halo in the visual-only preview.");
                Assert.That(roots.SelectMany(x => x.GetComponentsInChildren<DungeonPortal>(true)), Is.Empty);
                Assert.That(roots.SelectMany(x => x.GetComponentsInChildren<Collider2D>(true)), Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator GlowHonorsCameraProfileControlsAndSourceVisibility_WithoutLeakingBetweenCameras()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Glow camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var owner = cameraObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); owner.Profile = profile;
            var root = new GameObject("Glow source"); SceneManager.MoveGameObjectToScene(root, scene);
            var look = SourceGlowSettings.Portal; look.color = Color.white; look.radius = 0.7f; look.strength = 0.4f;
            var glow = new SourceGlowVisual(root.transform, () => look);
            var target = new RenderTexture(128, 128, 24); target.Create();
            var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Color32[] Render()
            {
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetPixels32();
            }
            double Light(Color32[] pixels) => pixels.Sum(x => (double)x.r + x.g + x.b);
            try
            {
                yield return null;
                profile.sourceGlow = false; Assert.That(Light(Render()), Is.Zero);
                profile.sourceGlow = true; var normal = Render(); Assert.That(Light(normal), Is.GreaterThan(1));
                Assert.That(normal[0].r, Is.Zero, "No full-screen haze or blur.");
                // Readback is display-encoded; doubling linear light is not a
                // twofold change in 8-bit sRGB values.
                profile.sourceGlowStrength *= 2; Assert.That(Light(Render()), Is.GreaterThan(Light(normal) * 1.25));
                profile.sourceGlowStrength *= 0.5f;
                profile.sourceGlowRadius = 2; Assert.That(Render().Count(x => x.r > 1), Is.GreaterThan(normal.Count(x => x.r > 1) * 2));
                profile.sourceGlowRadius = 1; profile.sourceGlowSoftness = 2;
                Assert.That(Light(Render()), Is.GreaterThan(Light(normal) * 1.2)); profile.sourceGlowSoftness = 1;
                var material = glow.Renderer.sharedMaterial; Render(); Assert.That(glow.Renderer.sharedMaterial, Is.SameAs(material));
                Assert.That(glow.Renderer.enabled, Is.False, "The halo is hidden between camera renders.");
                look.enabled = false; Assert.That(Light(Render()), Is.Zero); look.enabled = true;
                owner.enabled = false; Assert.That(Light(Render()), Is.Zero, "An unowned camera must see no glow."); owner.enabled = true;
                owner.ApplyDungeonPresentation(null, profile);
                var quiet = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); quiet.sourceGlow = false;
                owner.ApplyDungeonPresentation(null, quiet); Assert.That(Light(Render()), Is.Zero);
                owner.RestoreHubColors(); Assert.That(Light(Render()), Is.GreaterThan(1), "Hub profile returns after the dungeon profile.");
                Object.DestroyImmediate(quiet);
                glow.Dispose();
                var portal = root.AddComponent<SpriteRenderer>(); portal.sprite = Resources.Load<Sprite>("Visuals/SourceGlow/Halo");
                // Exclude only the original art from this render, so the test
                // measures the halo's tint and opacity in isolation.
                root.layer = 31; camera.cullingMask &= ~(1 << 31);
                portal.color = new Color(1, 0.05f, 0.8f, 1);
                glow = new SourceGlowVisual(root.transform, () => look, portal: portal); glow.Renderer.gameObject.layer = 0;
                var colored = Render();
                Assert.That(colored.Sum(x => (double)x.r), Is.GreaterThan(colored.Sum(x => (double)x.g) * 2));
                portal.color = Color.clear; Assert.That(Light(Render()), Is.Zero, "Fading the source also fades its halo.");
                portal.color = Color.white; portal.enabled = false; Assert.That(Light(Render()), Is.Zero);
                portal.enabled = true;
                WorldColorGrading.SetBindingsSuspended(true); Assert.That(Light(Render()), Is.Zero);
            }
            finally
            {
                WorldColorGrading.SetBindingsSuspended(false); RenderTexture.active = previous;
                glow.Dispose(); EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(profile);
            }
        }
    }
}
