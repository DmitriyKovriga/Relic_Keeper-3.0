using System.Collections;
using System.Linq;
using NUnit.Framework;
using RelicKeeper.Editor.Visuals;
using Scripts.Visuals.Atmosphere;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class StoneReliefTests
    {
        [Test]
        public void NewProfilesAreNeutral_AndStrengthIsSanitized()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                Assert.That(profile.stoneReliefStrength, Is.Zero);
                profile.stoneReliefStrength = -1; profile.Sanitize(); Assert.That(profile.stoneReliefStrength, Is.Zero);
                profile.stoneReliefStrength = 2; profile.Sanitize(); Assert.That(profile.stoneReliefStrength, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void RegionFollowsRoomPlacement_AndPreservesOtherPropertyOwners()
        {
            var go = new GameObject("Relief region"); var renderer = go.AddComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock(); block.SetFloat("_FxRuntimeFlash", 0.7f);
            block.SetVector("_RKStoneReliefRegion", Vector4.one); renderer.SetPropertyBlock(block);
            try
            {
                var receiver = go.AddComponent<StoneReliefSurface>(); receiver.Configure(new Rect(-1, -2, 3, 4));
                go.transform.position = new Vector3(5, 6); go.transform.localScale = new Vector3(2, 2, 1);
                receiver.SendMessage("LateUpdate"); renderer.GetPropertyBlock(block);
                Assert.That(block.GetVector("_RKStoneReliefRegion"), Is.EqualTo(new Vector4(3, 2, 9, 10)));
                Assert.That(block.GetFloat("_RKStoneReliefReceiver"), Is.EqualTo(1));
                block.SetFloat("_FxRuntimeFlash", 0.4f); renderer.SetPropertyBlock(block);
                receiver.enabled = false; renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_FxRuntimeFlash"), Is.EqualTo(0.4f));
                Assert.That(block.GetFloat("_RKStoneReliefReceiver"), Is.Zero);
                Assert.That(block.GetVector("_RKStoneReliefRegion"), Is.EqualTo(Vector4.one));
                Assert.That(go.GetComponents<Collider2D>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SavedPilotIsLimitedToRoom001_AndNormalsMatchTheOriginalSheets()
        {
            var room = AssetDatabase.LoadAssetAtPath<GameObject>(LocalLightingSetup.RoomPath);
            Assert.That(room.GetComponentsInChildren<StoneReliefSurface>(true).Select(x => x.name), Is.EquivalentTo(new[] { "Ground", "Background", "Platforms" }));
            var source = room.GetComponentsInChildren<LocalLight2DSource>(true).Where(x => !x.IsRoomFill).OrderBy(x => x.transform.position.x).First();
            Assert.That(source.Light.normalMapQuality, Is.EqualTo(Light2D.NormalMapQuality.Accurate));
            foreach (string path in WorldColorPreviewStage.GetMortfallRooms().Where(x => x != LocalLightingSetup.RoomPath))
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<StoneReliefSurface>(true), Is.Empty);
            foreach (var pair in new[] { (StoneReliefSetup.SheetPath, StoneReliefSetup.NormalPath), (StoneReliefSetup.WallSheetPath, StoneReliefSetup.WallNormalPath) })
            {
                var original = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Item1); var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Item2);
                Assert.That(new Vector2Int(normal.width, normal.height), Is.EqualTo(new Vector2Int(original.width, original.height)));
                var importer = (TextureImporter)AssetImporter.GetAtPath(pair.Item2);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                var sheet = (TextureImporter)AssetImporter.GetAtPath(pair.Item1);
                Assert.That(sheet.secondarySpriteTextures.Single(x => x.name == "_NormalMap").texture, Is.EqualTo(normal));
            }
        }

        private static void SetNormals(LocalLight2DSource light, bool enabled)
        {
            var fields = new SerializedObject(light.Light);
            fields.FindProperty("m_NormalMapQuality").intValue = (int)(enabled ? Light2D.NormalMapQuality.Accurate : Light2D.NormalMapQuality.Disabled);
            fields.FindProperty("m_UseNormalMap").boolValue = enabled;
            fields.FindProperty("m_NormalMapDistance").floatValue = 0.85f; fields.ApplyModifiedPropertiesWithoutUndo();
        }

        [UnityTest]
        public IEnumerator ActualNormalsRespondToDirection_WithoutChangingAmbientOrUnmarkedSurfaces()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            var camera = Make("Relief camera").AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 2.5f; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var owner = camera.gameObject.AddComponent<AtmosphereController>();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); owner.Profile = profile;
            var pixels = new Texture2D(2, 2); pixels.SetPixels(Enumerable.Repeat(new Color(0.2f, 0.2f, 0.2f), 4).ToArray()); pixels.Apply();
            var normals = new Texture2D(2, 2, TextureFormat.RGBA32, false, true); normals.SetPixels(Enumerable.Repeat(new Color(0.8f, 0.5f, 0.9f, 1), 4).ToArray()); normals.Apply();
            var sprite = Sprite.Create(pixels, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
            var material = new Material(Shader.Find("RelicKeeper/Sprites/Stone Relief")); material.SetTexture("_NormalMap", normals);
            var flat = new Material(Shader.Find("RelicKeeper/Sprites/World Color Unlit")); flat.SetFloat("_RKGradeGroup", 1);
            var actorFx = new Material(Shader.Find("RelicKeeper/Sprites/Relic Sprite FX")); actorFx.SetFloat("_RKGradeGroup", 2);
            var surface = Make("Marked relief").AddComponent<SpriteRenderer>(); surface.sprite = sprite; surface.sharedMaterial = material;
            surface.gameObject.AddComponent<StoneReliefSurface>().Configure(new Rect(-2, -2, 4, 4));
            var unmarked = Make("Unmarked surface").AddComponent<SpriteRenderer>(); unmarked.sprite = sprite; unmarked.sharedMaterial = material; unmarked.transform.position = new Vector3(0, -1.25f);
            var actor = Make("Ordinary actor").AddComponent<SpriteRenderer>(); actor.sprite = sprite; actor.sharedMaterial = flat; actor.transform.position = new Vector3(0, 1.25f);
            var light = Make("Relief torch").AddComponent<LocalLight2DSource>(); light.transform.position = new Vector3(-2, 0); light.Configure(0.4f, 5); light.BindPreview(owner); SetNormals(light, true);
            var target = new RenderTexture(160, 160, 24); target.Create(); var image = new Texture2D(160, 160, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Color32[] Render(float strength)
            {
                profile.stoneReliefStrength = strength;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 160, 160), 0, 0); image.Apply(); return image.GetPixels32();
            }
            int Pixel(float y) => Mathf.FloorToInt((y + 2.5f) * 32) * 160 + 80;
            try
            {
                yield return null;
                Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                var off = Render(0); var on = Render(1);
                Assert.That(on[Pixel(0)].r, Is.LessThan(off[Pixel(0)].r - 2), "A normal facing away from the torch must lose source light.");
                Assert.That(on[Pixel(-1.25f)].r, Is.EqualTo(off[Pixel(-1.25f)].r).Within(2), "Unmarked stone retains its flat response.");
                Assert.That(on[Pixel(1.25f)].r, Is.EqualTo(off[Pixel(1.25f)].r).Within(2), "Unlit actors must not disappear when a normal-enabled light is present.");
                actor.transform.position = Vector3.zero; actor.sortingLayerName = "Hero";
                Assert.That(Render(1)[Pixel(0)].r, Is.EqualTo(Render(0)[Pixel(0)].r).Within(2), "An actor in front of the wall must not inherit the wall's surface normals.");
                actor.sharedMaterial = actorFx;
                Assert.That(Render(1)[Pixel(0)].r, Is.EqualTo(Render(0)[Pixel(0)].r).Within(2), "The live character FX shader must also remain independent of stone normals.");
                actor.sharedMaterial = flat;
                actor.transform.position = new Vector3(0, 1.25f);
                SetNormals(light, false); var originalFlat = Render(0);
                Assert.That(originalFlat[Pixel(0)].r, Is.EqualTo(off[Pixel(0)].r).Within(2), "Zero must recover the original lighting, not a dimmed flat-normal response.");
                SetNormals(light, true); light.transform.position = new Vector3(2, 0);
                Assert.That(Render(1)[Pixel(0)].r, Is.GreaterThan(Render(0)[Pixel(0)].r + 2), "The same normal must catch light from the opposite side.");
                light.enabled = false; Assert.That(Render(1), Is.EqualTo(Render(0)), "Normal maps must not alter ambient color.");
                light.enabled = true; light.transform.position = new Vector3(-2, 0);
                var blocker = Make("Occluder");
                LocalLightOccluderBuilder.AddPolygon(blocker.transform, new[] { new Vector3(-1.25f, -2), new Vector3(-0.75f, -2), new Vector3(-0.75f, 2), new Vector3(-1.25f, 2) });
                Assert.That(Render(1)[Pixel(0)].r, Is.EqualTo(Render(0)[Pixel(0)].r).Within(1), "Relief cannot illuminate through a wall.");
                Assert.That(Shader.GetGlobalFloat("_RKStoneReliefStrength"), Is.Zero, "A following camera must not inherit the profile strength.");
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                foreach (var item in new Object[] { profile, pixels, normals, sprite, material, flat, actorFx, image, target }) Object.DestroyImmediate(item);
            }
        }
    }
}
