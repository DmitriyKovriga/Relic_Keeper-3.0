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
    public sealed class PlayerLightShadowTests
    {
        [Test]
        public void BodyBlocksOnlyBehindIt_FadesWithDistance_AndDoesNotShadowItselfOrAnOverlappingSource()
        {
            var body = new Vector4(0, 0, 0.3f, 0.6f);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), new Vector2(1, 0), body, 3), Is.GreaterThan(0.9f));
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), new Vector2(-1, 0), body, 3), Is.Zero);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), Vector2.zero, body, 3), Is.Zero);
            Assert.That(PlayerLightShadow.Occlusion(Vector2.zero, Vector2.right, body, 3), Is.Zero);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), new Vector2(4, 0), body, 3), Is.Zero);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), new Vector2(1, 2), body, 3), Is.Zero);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(2, 0), Vector2.left, body, 3), Is.GreaterThan(0.9f));
            var jumped = body; jumped.y = 2;
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-2, 0), Vector2.right, jumped, 3), Is.Zero);
        }

        [Test]
        public void StableBodyFollowsMovementAndScale_WithoutAffectingCollidersOrContactShadow()
        {
            var go = new GameObject("Player shadow body");
            try
            {
                var shadow = go.AddComponent<PlayerLightShadow>(); shadow.ConfigureBody(new Bounds(new Vector3(0, 0.5f), new Vector3(0.6f, 1.2f)));
                var original = shadow.Body;
                go.transform.position = new Vector3(2, 3); go.transform.localScale = new Vector3(2, 2, 1);
                Assert.That(shadow.Body, Is.EqualTo(new Vector4(2, 4, original.z * 2, original.w * 2)));
                Assert.That(go.GetComponentsInChildren<Collider2D>(), Is.Empty);
                Assert.That(go.GetComponentsInChildren<SpriteRenderer>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PlatformImmediatelyBelowTheFeetIsNotMistakenForTheLitPlayerBody()
        {
            var body = new Vector4(0, 0, 0.3f, 0.6f);
            Assert.That(PlayerLightShadow.Occlusion(new Vector2(-1.5f, 0), new Vector2(0.1f, -0.65f), body, 4),
                Is.GreaterThan(0.2f), "A ray crosses the lower body before reaching the platform receiving surface.");
        }

        [Test]
        public void WiderControlsKeepTheFeetAnchored_AndTheFullValuesAreNotSilentlyClamped()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                profile.playerLightShadowStrength = 1; profile.playerLightShadowReach = 30;
                profile.playerLightShadowWidth = 4; profile.playerLightShadowHeight = 3; profile.playerLightShadowSoftness = 2;
                profile.Sanitize();
                Assert.That(profile.playerLightShadowStrength, Is.EqualTo(1)); Assert.That(profile.playerLightShadowReach, Is.EqualTo(30));
                Assert.That(profile.playerLightShadowWidth, Is.EqualTo(4)); Assert.That(profile.playerLightShadowHeight, Is.EqualTo(3));
                Assert.That(profile.playerLightShadowSoftness, Is.EqualTo(2));
                var original = new Vector4(0, 0, 0.3f, 0.6f);
                var shaped = PlayerLightShadow.ShapeBody(original, 4, 3);
                Assert.That(shaped.y - shaped.w, Is.EqualTo(original.y - original.w).Within(0.0001f));
                Assert.That(shaped.z, Is.EqualTo(original.z * 4));
                Assert.That(PlayerLightShadow.Occlusion(new Vector2(-1.5f, 0), new Vector2(0, -0.65f), shaped, 30, 0.25f, original),
                    Is.GreaterThan(0.2f), "Increasing the caster must not expand the exemption over the ground.");
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [UnityTest]
        public IEnumerator ActualPointLightIsPartiallyBlocked_AndAmbientBodyAndOtherLightRemainReadable()
        {
            WorldColorGrading.SetBindingsSuspended(false);
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            var camera = Make("Player shadow camera").AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.backgroundColor = Color.black; camera.clearFlags = CameraClearFlags.SolidColor;
            var owner = camera.gameObject.AddComponent<AtmosphereController>();
            var fields = new SerializedObject(owner); fields.FindProperty("_colorUnlitShader").objectReferenceValue = Shader.Find("RelicKeeper/Sprites/World Color Unlit"); fields.ApplyModifiedPropertiesWithoutUndo();
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>(); profile.playerLightShadowStrength = 0.5f; profile.playerLightShadowReach = 4; owner.Profile = profile;
            var texture = new Texture2D(2, 2); texture.SetPixels(Enumerable.Repeat(new Color(0.18f, 0.18f, 0.18f), 4).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 1);
            var receiver = Make("Receiving stone").AddComponent<SpriteRenderer>(); receiver.sprite = sprite; receiver.transform.localScale = new Vector3(3, 3, 1);
            receiver.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            receiver.gameObject.AddComponent<WorldColorGroup>().Configure(WorldColorRole.Environment); owner.RefreshColorGroups();
            var light = Make("Left torch").AddComponent<LocalLight2DSource>(); light.transform.position = new Vector3(-1.5f, 0); light.Configure(0.7f, 5); light.BindPreview(owner);
            var other = Make("Right torch").AddComponent<LocalLight2DSource>(); other.transform.position = new Vector3(1.5f, 0); other.Configure(0.7f, 5); other.BindPreview(owner); other.enabled = false;
            var actor = Make("Stable player body").AddComponent<PlayerLightShadow>(); actor.ConfigureBody(new Bounds(Vector3.zero, new Vector3(0.6f, 1.2f)));
            var target = new RenderTexture(128, 128, 24); target.Create(); var image = new Texture2D(128, 128, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            Color32[] Render(bool enabled)
            {
                profile.playerLightShadow = enabled;
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); image.Apply(); return image.GetPixels32();
            }
            int Index(float x, float y = 0) => Mathf.Clamp(Mathf.FloorToInt((y + 2) * 32), 0, 127) * 128 + Mathf.Clamp(Mathf.FloorToInt((x + 2) * 32), 0, 127);
            try
            {
                yield return null;
                Assert.That(ShaderUtil.ShaderHasError(Shader.Find("Hidden/RelicKeeper/Light2D")), Is.False);
                var off = Render(false); var on = Render(true);
                Assert.That(on[Index(0.1f, -0.65f)].r, Is.LessThan(off[Index(0.1f, -0.65f)].r - 1),
                    "The platform directly under the feet must receive a shadow, not the body-lighting exemption.");
                Assert.That(on[Index(0.9f)].r, Is.LessThan(off[Index(0.9f)].r - 2), "The real URP point-light shader must attenuate light behind the body.");
                Assert.That(on[Index(-0.7f)].r, Is.EqualTo(off[Index(-0.7f)].r).Within(1));
                Assert.That(on[Index(0)].r, Is.EqualTo(off[Index(0)].r).Within(1), "The player's own body stays lit.");
                profile.playerLightShadowStrength = 1;
                Assert.That(Render(true)[Index(0.1f, -0.65f)].r, Is.LessThan(on[Index(0.1f, -0.65f)].r), "The expanded strength must actually reach the GPU.");
                profile.playerLightShadowWidth = 2.5f; profile.playerLightShadowHeight = 1.8f;
                var shapedOn = Render(true); var shapedOff = Render(false);
                Assert.That(shapedOn[Index(0.1f, -0.65f)].r, Is.LessThan(shapedOff[Index(0.1f, -0.65f)].r - 2));
                Assert.That(shapedOn[Index(0)].r, Is.EqualTo(shapedOff[Index(0)].r).Within(1));
                profile.playerLightShadowWidth = 1; profile.playerLightShadowHeight = 1; profile.playerLightShadowStrength = 0.5f;
                Assert.That(Shader.GetGlobalInt("_RKPlayerShadowSourceCount"), Is.Zero, "No globals may leak into another camera.");
                light.enabled = false; var ambient = Render(false);
                Assert.That(Render(true), Is.EqualTo(ambient), "Ambient light must not be reduced.");
                other.enabled = true; Assert.That(Render(true)[Index(0.9f)].r, Is.EqualTo(Render(false)[Index(0.9f)].r).Within(1), "The opposing light still reaches this point.");
                other.enabled = false; light.enabled = true; actor.transform.position = new Vector3(0, 2);
                Assert.That(Render(true)[Index(0.9f)].r, Is.EqualTo(Render(false)[Index(0.9f)].r).Within(1), "Jumping moves the shadow away from its previous ray.");
                actor.transform.position = Vector3.zero;
                actor.ConfigurePreviewGround(-0.6f, -0.5f, 1.6f);
                profile.playerLightShadowStrength = 0; profile.playerGroundShadowOpacity = 1;
                profile.playerGroundShadowLength = 1.8f; profile.playerGroundShadowThickness = 0.3f;
                var groundOn = Render(true); var groundOff = Render(false);
                Assert.That(groundOn[Index(0.5f, -0.65f)].r, Is.LessThan(groundOff[Index(0.5f, -0.65f)].r - 2), "The separate floor projection must visibly darken actual floor pixels.");
                Assert.That(actor.GroundProjection.bounds.center.x, Is.GreaterThan(0), "A light on the left projects to the right.");
                Assert.That(actor.GroundProjection.bounds.min.x, Is.GreaterThanOrEqualTo(-0.501f));
                Assert.That(actor.GroundProjection.bounds.max.x, Is.LessThanOrEqualTo(1.601f), "It cannot extend past the platform edge.");
                light.enabled = false; other.enabled = true; Render(true);
                Assert.That(actor.GroundProjection.bounds.center.x, Is.LessThan(0), "Crossing the light reverses the floor projection.");
                actor.transform.position = new Vector3(0, 3);
                Assert.That(Render(true), Is.EqualTo(Render(false)), "The projection fades away on a high jump.");
                actor.enabled = false; Assert.That(actor.GroundProjection, Is.Null); Assert.That(Render(true), Is.EqualTo(Render(false)));
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(profile); Object.DestroyImmediate(texture); Object.DestroyImmediate(sprite); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            }
        }
    }
}
