using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class GroundDashVisualTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ReferenceCloudStartsAtThirdSourceFrame_AnchoredAtTheFloor()
        {
            var frames = GroundDashPuffSprites.Frames;
            Assert.That(frames.Length, Is.EqualTo(6));
            Assert.That(frames[0].name, Is.EqualTo("GroundDashReference_02"), "Reference artwork must replace the procedural fallback.");
            foreach (var frame in frames)
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(32, 32)));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(frame.texture.filterMode, Is.EqualTo(FilterMode.Point));
            }
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(frames[0])));
                Assert.That(texture.GetPixels((int)frames[0].rect.x, 0, 32, 1), Has.Some.Matches<Color>(pixel => pixel.a > 0));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GroundAndAirAfterimagesUseLightSilhouette_AndSkipHiddenSourceBody(bool groundDash)
        {
            var root = new GameObject("Inactive afterimage test driver");
            root.SetActive(false);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            try
            {
                var attack = root.AddComponent<PlayerAttackInput>();
                var hidden = root.AddComponent<SpriteRenderer>();
                hidden.sprite = sprite;
                hidden.forceRenderingOff = true;
                var visibleObject = new GameObject("Visible presentation");
                visibleObject.transform.SetParent(root.transform);
                var visible = visibleObject.AddComponent<SpriteRenderer>();
                visible.sprite = sprite;
                typeof(PlayerAttackInput).GetField("_playerRenderers", Private).SetValue(attack, new[] { hidden, visible });
                typeof(PlayerAttackInput).GetField("_isGroundDash", Private).SetValue(attack, groundDash);
                typeof(PlayerAttackInput).GetMethod("SpawnAfterImageSnapshot", Private).Invoke(attack, null);
                var ghosts = Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None);
                Assert.That(ghosts.Length, Is.EqualTo(1));
                var renderers = ghosts[0].GetComponentsInChildren<SpriteRenderer>();
                Assert.That(renderers.Length, Is.EqualTo(1), "Hidden source sprite must not double the trail opacity.");
                Assert.That(renderers[0].color.r, Is.GreaterThan(0.8f));
                Assert.That(renderers[0].sortingLayerID, Is.EqualTo(visible.sortingLayerID));
                Assert.That(renderers[0].sortingOrder, Is.LessThan(visible.sortingOrder), "Light trail must remain behind the player.");
                Assert.That(renderers[0].sharedMaterial.shader.name, Is.EqualTo("RelicKeeper/GroundDashAfterimage"));
                var properties = new MaterialPropertyBlock();
                renderers[0].GetPropertyBlock(properties);
                Assert.That(properties.GetTexture("_MainTex"), Is.SameAs(visible.sprite.texture),
                    "Silhouette shader must use the sprite alpha texture, not its white default.");
                Assert.That(UnityEditor.ShaderUtil.ShaderHasError(renderers[0].sharedMaterial.shader), Is.False);
                Assert.That(typeof(DodgeAfterImageGhost).GetField("_duration", Private).GetValue(ghosts[0]), Is.EqualTo(groundDash ? 0.3f : 0.16f));
            }
            finally
            {
                foreach (var ghost in Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None)) Object.DestroyImmediate(ghost.gameObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void AfterimageCacheCapturesPresentationBodyOutsideWeaponRoot(bool groundDash)
        {
            var root = new GameObject("Dodge renderer ownership test");
            root.SetActive(false);
            var bodySprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.one*0.5f, 1);
            var weaponSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.one*0.5f, 2);
            PlayerMovementVisual visual = null;
            try
            {
                var attack = root.AddComponent<PlayerAttackInput>();
                var hidden = root.AddComponent<SpriteRenderer>();
                hidden.sprite = bodySprite;
                hidden.forceRenderingOff = true;
                var weapons = new GameObject("Visuals");
                weapons.transform.SetParent(root.transform);
                var weapon = weapons.AddComponent<SpriteRenderer>();
                weapon.sprite = weaponSprite;
                typeof(PlayerAttackInput).GetField("_visualRoot", Private).SetValue(attack, weapons.transform);

                // Cache once before the presentation body exists, reproducing Awake ordering.
                typeof(PlayerAttackInput).GetMethod("RefreshPlayerRendererCache", Private).Invoke(attack, null);
                visual = root.AddComponent<PlayerMovementVisual>();
                typeof(PlayerMovementVisual).GetMethod("Awake", Private).Invoke(visual, null);
                var display = visual.DisplayRenderer;
                display.sprite = bodySprite;
                display.transform.localPosition = new Vector3(0.2f,0.1f,0);
                display.transform.localScale = new Vector3(0.9f,1.1f,1);
                Assert.That(display.transform.parent, Is.EqualTo(root.transform));
                var dodgeOverlay = new GameObject("Temporary dodge VFX");
                dodgeOverlay.transform.SetParent(weapons.transform);
                dodgeOverlay.AddComponent<SpriteRenderer>().sprite = weaponSprite;
                dodgeOverlay.AddComponent<SpriteSheetOverlayVfx>();
                typeof(PlayerAttackInput).GetField("_isGroundDash", Private).SetValue(attack, groundDash);
                typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(attack, true);
                var emission = (System.Collections.IEnumerator)typeof(PlayerAttackInput)
                    .GetMethod("SpawnAfterImages", Private).Invoke(attack, new object[] {0.2f});
                Assert.That(emission.MoveNext(), Is.True);
                var ghosts = Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None);
                Assert.That(ghosts.Length, Is.EqualTo(1));
                var parts = ghosts[0].GetComponentsInChildren<SpriteRenderer>();
                Assert.That(parts.Length, Is.EqualTo(2), "Capture visible body and weapon, skipping the hidden root duplicate.");
                var body = System.Array.Find(parts, part => part.sprite == bodySprite);
                Assert.That(body, Is.Not.Null, "The real renderer cache must include MovementSprite.");
                Assert.That(System.Array.Exists(parts, part => part.sprite == weaponSprite), Is.True);
                Assert.That(body.transform.position, Is.EqualTo(display.transform.position));
                Assert.That(body.transform.lossyScale, Is.EqualTo(display.transform.lossyScale));
                Vector3 heldPosition = body.transform.position;
                root.transform.position += Vector3.right;
                Assert.That(body.transform.position, Is.EqualTo(heldPosition), "Body afterimage must remain at emission position.");
            }
            finally
            {
                foreach (var ghost in Object.FindObjectsByType<DodgeAfterImageGhost>(FindObjectsSortMode.None)) Object.DestroyImmediate(ghost.gameObject);
                if (visual != null)
                {
                    Object.DestroyImmediate(visual.DisplayRenderer.gameObject);
                    typeof(PlayerMovementVisual).GetField("_display", Private).SetValue(visual, null);
                }
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(bodySprite);
                Object.DestroyImmediate(weaponSprite);
            }
        }

        [TestCase(true, true)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void CompressionOnlyAppliesToGroundDash_KeepingFeetAndPhysicsStable(bool grounded, bool dashing)
        {
            var root = new GameObject("Dash presentation test");
            root.SetActive(false);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            try
            {
                root.AddComponent<SpriteRenderer>().sprite = sprite;
                var movement = root.AddComponent<PlayerMovement>();
                var attack = root.AddComponent<PlayerAttackInput>();
                typeof(PlayerMovement).GetField("_isGrounded", Private).SetValue(movement, grounded);
                typeof(PlayerAttackInput).GetField("_isDodging", Private).SetValue(attack, dashing);
                typeof(PlayerAttackInput).GetField("_isGroundDash", Private).SetValue(attack, dashing);
                var visual = root.AddComponent<PlayerMovementVisual>();
                var type = typeof(PlayerMovementVisual);
                type.GetMethod("Awake", Private).Invoke(visual, null);
                type.GetField("_dashShape", Private).SetValue(visual, grounded && dashing ? 1f : 0f);
                type.GetMethod("LateUpdate", Private).Invoke(visual, null);
                var display = visual.DisplayRenderer;
                Vector2 expected = grounded && dashing ? PlayerMovementVisual.GroundDashScale : Vector2.one;
                Assert.That(display.transform.localScale.x, Is.EqualTo(expected.x));
                Assert.That(display.transform.localScale.y, Is.EqualTo(expected.y));
                Assert.That(display.transform.localPosition.y + sprite.bounds.min.y * expected.y, Is.EqualTo(0).Within(0.0001f));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(root.GetComponent<BoxCollider2D>().size, Is.EqualTo(Vector2.one));
                Object.DestroyImmediate(display.gameObject);
                type.GetField("_display", Private).SetValue(visual, null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); }
        }

        [Test]
        public void GripCompressionScalesRelativeToFeet_AndNeutralRestoresIt()
        {
            var grip = new Vector3(2f / 24, 3f / 24, 0);
            var shifted = PlayerMovementVisual.AdjustDashGrip(grip, PlayerMovementVisual.GroundDashScale);
            Assert.That(5f / 24 + shifted.x, Is.EqualTo((5f / 24 + grip.x) * 0.96f).Within(0.0001f));
            Assert.That(4f / 24 + shifted.y, Is.EqualTo((4f / 24 + grip.y) * 0.87f).Within(0.0001f));
            Assert.That(PlayerMovementVisual.AdjustDashGrip(grip, Vector2.one), Is.EqualTo(grip));
            Assert.That(GroundDashPuffSprites.Frames.Length, Is.EqualTo(6));
        }
    }
}
