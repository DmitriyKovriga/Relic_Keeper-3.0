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

        [Test]
        public void GroundProjectionUsesLightHeight_IsBounded_AndCollapsesContinuouslyAtCrossing()
        {
            Vector2 Sample(float x) => PlayerLightShadow.GroundResponse(x, 0.3f, 1, 0.65f);
            Assert.That(Sample(0.5f).x, Is.LessThan(Sample(1.25f).x));
            Assert.That(Sample(1.25f).x, Is.LessThan(Sample(3).x));
            Assert.That(Sample(-0.5f), Is.EqualTo(Sample(0.5f)), "Both sides follow the same curve.");
            Assert.That(Sample(0).y, Is.Zero); Assert.That(Sample(-0.1f).y, Is.Zero); Assert.That(Sample(0.1f).y, Is.Zero);
            for (float x = -0.8f; x < 0.8f; x += 0.01f)
                Assert.That(Mathf.Abs(Sample(x + 0.01f).y - Sample(x).y), Is.LessThan(0.04f), "Opacity cannot jump as direction changes.");
            float Length(float x, float height = 1) => PlayerLightShadow.ProjectedGroundLength(x, height, 1.2f, 2, 1, 0.8f);
            Assert.That(Length(0), Is.Zero);
            Assert.That(Length(-0.5f), Is.EqualTo(Length(0.5f)));
            Assert.That(Length(0.5f), Is.LessThan(Length(1.5f)), "An elevated light projects a longer shadow farther away.");
            Assert.That(Length(1, 3), Is.LessThan(Length(1, 1)), "A higher light projects a shorter shadow.");
            Assert.That(Length(100, 0.1f), Is.LessThanOrEqualTo(2), "A low flame must not create an infinite projection.");
            Assert.That(Length(1, 0), Is.Zero, "A source below the support cannot project onto its top.");
            for (float x = -2; x < 2; x += 0.01f)
                Assert.That(Mathf.Abs(Length(x + 0.01f) - Length(x)), Is.LessThan(0.04f), "Length cannot pop while crossing the source.");
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
                Assert.That(groundOn[Index(0.5f, -0.65f)].r, Is.LessThan(groundOff[Index(0.5f, -0.65f)].r - 2), "A light on the left must lose illumination on the floor to the right.");
                Assert.That(groundOn[Index(0.5f, -0.4f)].r, Is.EqualTo(groundOff[Index(0.5f, -0.4f)].r).Within(1), "The floor projection must not draw in the air.");
                Assert.That(groundOn[Index(-0.6f, -0.65f)].r, Is.EqualTo(groundOff[Index(-0.6f, -0.65f)].r).Within(1));
                profile.playerGroundShadowLength = 8;
                Assert.That(Render(true)[Index(1.7f, -0.65f)].r, Is.EqualTo(Render(false)[Index(1.7f, -0.65f)].r).Within(1), "Even a long shadow cannot pass the platform edge.");
                profile.playerGroundShadowLength = 1.8f;
                actor.PreviewLightVisibility = (from, to) => false;
                Assert.That(Render(true), Is.EqualTo(Render(false)), "An obstructed source cannot cast the floor extension.");
                actor.PreviewLightVisibility = null;
                light.enabled = false; other.enabled = true; Render(true);
                Assert.That(Render(true)[Index(-0.3f, -0.65f)].r, Is.LessThan(Render(false)[Index(-0.3f, -0.65f)].r - 2), "An opposing light projects to the left.");
                light.enabled = true; other.enabled = false;
                var singleLight = Render(true);
                other.enabled = true; var bothLights = Render(true);
                Assert.That(bothLights[Index(0.5f, -0.65f)].r, Is.GreaterThan(singleLight[Index(0.5f, -0.65f)].r + 2), "Another torch must still illuminate the shaded floor.");
                light.enabled = false; other.enabled = false;
                var ambientFloor = Render(false);
                Assert.That(Render(true), Is.EqualTo(ambientFloor), "A floor shadow must never darken ambient color when its lights are off.");
                Assert.That(singleLight[Index(0.5f, -0.65f)].r, Is.GreaterThanOrEqualTo(ambientFloor[Index(0.5f, -0.65f)].r - 1), "Even at full ground strength, ambient floor detail must survive beneath an active light's shadow.");
                light.enabled = true;
                actor.ConfigurePreviewGround(-0.6f, -10, 10);
                Assert.That(actor.GroundProjection, Is.Null, "Never create another dark oval overlay.");
                actor.transform.position = new Vector3(-1.5f, 0);
                Assert.That(Render(true), Is.EqualTo(Render(false)), "The extra projection must disappear while passing through the flame.");

                var bodyRenderer = actor.gameObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sprite = sprite; bodyRenderer.sortingLayerName = "Hero";
                bodyRenderer.sharedMaterial = receiver.sharedMaterial;
                actor.transform.localScale = new Vector3(0.3f, 0.6f, 1);
                actor.ConfigureBody(new Bounds(actor.transform.position, new Vector3(0.6f, 1.2f)));
                var properties = new MaterialPropertyBlock(); bodyRenderer.GetPropertyBlock(properties);
                properties.SetFloat("_FxRuntimeFlash", 0.37f); bodyRenderer.SetPropertyBlock(properties);
                actor.ConfigureBody(new Bounds(actor.transform.position, new Vector3(0.6f, 1.2f)));
                Assert.That(bodyRenderer.sharedMaterial.shader.FindPropertyIndex("_RKPlayerOverlapShade"), Is.GreaterThanOrEqualTo(0), "The compiled actor shader exposes the receiver property.");
                bodyRenderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_RKPlayerOverlapShade"), Is.EqualTo(1), "Bind the body receiver before camera rendering.");
                yield return null;
                profile.playerLightOverlapDarkness = 0; var uncovered = Render(true);
                profile.playerLightOverlapDarkness = 0.4f;
                float capturedCover = -1, capturedFlag = -1;
                System.Action<ScriptableRenderContext, Camera> probe = (context, renderingCamera) =>
                {
                    if (renderingCamera != camera) return;
                    capturedCover = Shader.GetGlobalFloat("_RKPlayerOverlapAmount");
                    bodyRenderer.GetPropertyBlock(properties); capturedFlag = properties.GetFloat("_RKPlayerOverlapShade");
                };
                Color32[] covered;
                RenderPipelineManager.beginCameraRendering += probe;
                try { covered = Render(true); } finally { RenderPipelineManager.beginCameraRendering -= probe; }
                Assert.That(capturedCover, Is.GreaterThan(0.3f), $"Shade must be uploaded for {bodyRenderer.sharedMaterial.shader.name}, flag {capturedFlag}, body {actor.Body}, illumination {light.ParticleIllumination(profile, actor.transform.position)}.");
                Assert.That(capturedFlag, Is.EqualTo(1));
                Assert.That(covered[Index(-1.5f)].r, Is.LessThan(uncovered[Index(-1.5f)].r - 3), "Only the actual player renderer should darken during overlap.");
                bodyRenderer.GetPropertyBlock(properties);
                Assert.That(Shader.GetGlobalFloat("_RKPlayerOverlapAmount"), Is.Zero, "Restore body shading between cameras.");
                Assert.That(properties.GetFloat("_FxRuntimeFlash"), Is.EqualTo(0.37f), "Preserve other effect owners' properties.");
                actor.transform.position = Vector3.zero; profile.playerGroundShadowOpacity = 0;
                Assert.That(Render(true), Is.EqualTo(Render(false)), "Overlap darkness must not follow the player away from the source.");
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
