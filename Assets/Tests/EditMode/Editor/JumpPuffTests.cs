using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class JumpPuffTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void DashJumpTrailStaysInWorld_AndStopsOnNormalJump()
        {
            var root = new GameObject("Dash jump trail test");
            var movement = root.AddComponent<PlayerMovement>();
            var trail = root.AddComponent<DashJumpTrail>();
            try
            {
                typeof(PlayerMovement).GetField("_rb", Private).SetValue(movement, root.GetComponent<Rigidbody2D>());
                typeof(PlayerMovement).GetField("_isDashJump", Private).SetValue(movement, true);
                root.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(8.5f, 5f);
                trail.Begin();
                float now = Time.time;
                trail.Tick(now);
                SpriteRenderer streak = null;
                foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (renderer.name == "Dash jump white streak") streak = renderer;
                Assert.That(streak, Is.Not.Null);
                Assert.That(streak.color.r, Is.GreaterThan(0.9f));
                Vector3 origin = streak.transform.position;
                root.transform.position += Vector3.right * 2;
                typeof(PlayerMovement).GetField("_isDashJump", Private).SetValue(movement, false);
                trail.Tick(now + 0.05f);
                Assert.That(streak.transform.position, Is.EqualTo(origin));
                Assert.That(typeof(DashJumpTrail).GetField("_emitting", Private).GetValue(trail), Is.False);
                trail.Tick(now + 0.3f);
                Assert.That(streak == null || !streak.enabled, Is.True);
            }
            finally
            {
                foreach (var renderer in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
                    if (renderer.name == "Dash jump white streak") Object.DestroyImmediate(renderer.gameObject);
                Object.DestroyImmediate((Sprite)typeof(DashJumpTrail).GetField("_pixel", Private).GetValue(trail));
                typeof(DashJumpTrail).GetField("_pixel", Private).SetValue(trail, null);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AirJumpCloudFormsLargerRing_WithOpenCentreAndDownwardJet()
        {
            var air = AirJumpPuffSprites.Frames[2].texture;
            int airCount = 0, groundCount = 0;
            foreach (var pixel in air.GetPixels32()) if (pixel.a > 0) airCount++;
            foreach (var pixel in JumpPuffSprites.Frames[2].texture.GetPixels32()) if (pixel.a > 0) groundCount++;
            Assert.That(airCount, Is.GreaterThan(groundCount));
            Assert.That(air.GetPixel(16, 17).a, Is.Zero, "The cushion should open into a cloud ring.");
            Assert.That(air.GetPixel(16, 8).a, Is.GreaterThan(0), "A downward jet should emphasize the push-off.");
        }

        [Test]
        public void PuffExpandsAndFadesOnNativePixelGrid()
        {
            var frames = JumpPuffSprites.Frames;
            Assert.That(frames.Length, Is.EqualTo(8));
            Assert.That(frames[0].pixelsPerUnit, Is.EqualTo(24));
            Assert.That(frames[0].texture.filterMode, Is.EqualTo(FilterMode.Point));
            float first = 0, last = 0;
            foreach (var pixel in frames[0].texture.GetPixels()) first += pixel.a;
            foreach (var pixel in frames[7].texture.GetPixels()) last += pixel.a;
            Assert.That(first, Is.GreaterThan(0));
            Assert.That(last, Is.LessThan(first * 0.2f));
            Assert.That(frames[2].texture.GetPixels32(), Is.Not.EqualTo(frames[0].texture.GetPixels32()));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void JumpEventSpawnsOnce_DisablingUnsubscribes_AndPuffStaysAtTakeoff(bool grounded, bool dashJump)
        {
            var root = new GameObject("Jump puff test");

            root.transform.position = new Vector3(1000, grounded ? 0.5f : 4f, 0);
            var floor = new GameObject("Jump puff test floor");
            floor.layer = LayerMask.NameToLayer("Ground");
            floor.transform.position = new Vector3(1000, -0.5f, 0);
            floor.AddComponent<BoxCollider2D>().size = new Vector2(4, 1);
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            try
            {
                root.AddComponent<SpriteRenderer>().sprite = sprite;
                root.AddComponent<BoxCollider2D>().size = Vector2.one;
                var driver = new GameObject("Inactive jump driver");
                driver.transform.SetParent(root.transform);
                driver.SetActive(false);
                var movement = driver.AddComponent<PlayerMovement>();
                var grounding = root.AddComponent<GroundingVisualController>();
                var type = typeof(GroundingVisualController);
                type.GetMethod("Awake", Private).Invoke(grounding, null);
                type.GetField("_playerMovement", Private).SetValue(grounding, movement);
                Physics2D.SyncTransforms();
                type.GetMethod("OnEnable", Private).Invoke(grounding, null);
                type.GetField("_wasGrounded", Private).SetValue(grounding, grounded);
                GroundingSpriteSheetVfx oldPuff = null, oldScrape = null;
                if (dashJump)
                {
                    // Begin with a real dash cloud and scrape, then convert mid-effect.
                    type.GetMethod("SpawnDashEffects", Private).Invoke(grounding, null);
                    oldPuff = (GroundingSpriteSheetVfx)type.GetField("_activeDashPuff", Private).GetValue(grounding);
                    oldScrape = (GroundingSpriteSheetVfx)type.GetField("_activeDashScrape", Private).GetValue(grounding);
                    Assert.That(oldPuff, Is.Not.Null);
                    Assert.That(oldScrape, Is.Not.Null);
                }
                typeof(PlayerMovement).GetField("_isDashJump", Private).SetValue(movement, dashJump);
                type.GetField("_spawnDashJumpTrail", Private).SetValue(grounding, false);
                var evt = typeof(PlayerMovement).GetField("OnJumpStarted", Private);
                var jump = (Action)evt.GetValue(movement);
                Assert.That(jump, Is.Not.Null);
                jump.Invoke();
                if (dashJump)
                {
                    Assert.That(oldPuff == null || !oldPuff.gameObject.activeSelf, Is.True);
                    Assert.That(oldScrape == null || !oldScrape.gameObject.activeSelf, Is.True);
                    // A late dash notification must not restore the regular cloud.
                    type.GetMethod("SpawnDashEffects", Private).Invoke(grounding, null);
                }
                var effects = Object.FindObjectsByType<GroundingSpriteSheetVfx>(FindObjectsSortMode.None);
                Assert.That(effects.Length, Is.EqualTo(1));
                var renderer = effects[0].GetComponent<SpriteRenderer>();
                if (grounded)
                    Assert.That(renderer.bounds.min.y, Is.InRange(0f, 1f / 24f), "Ground puff must start at the contact surface.");
                else
                    {
                    Assert.That(renderer.bounds.max.y, Is.GreaterThan(3.5f), "Cloud cushion must touch the feet from below.");
                    Assert.That(renderer.bounds.min.y, Is.InRange(2f, 3.5f), "Cloud must stay beneath the airborne player, not at the distant floor.");
                    Assert.That(renderer.sprite, Is.SameAs(AirJumpPuffSprites.Frames[0]));
                    Assert.That(typeof(GroundingSpriteSheetVfx).GetField("_duration", Private).GetValue(effects[0]), Is.EqualTo(0.48f));
                }
                Assert.That(renderer.color.a, Is.EqualTo(dashJump ? 0.95f : 0.9f).Within(0.001f));
                if (dashJump)
                {
                    Assert.That(renderer.sprite, Is.SameAs(DashJumpPuffSprites.Frames[0]));
                    Assert.That(typeof(GroundingSpriteSheetVfx).GetField("_duration", Private).GetValue(effects[0]), Is.EqualTo(0.65f));
                    Assert.That(renderer.bounds.size.x, Is.EqualTo(2.5f).Within(0.001f));
                }
                var heldPosition = effects[0].transform.position;
                root.transform.position += Vector3.up;
                Assert.That(effects[0].transform.position, Is.EqualTo(heldPosition));
                type.GetMethod("OnDisable", Private).Invoke(grounding, null);
                Assert.That(evt.GetValue(movement), Is.Null);
            }
            finally
            {
                foreach (var effect in Object.FindObjectsByType<GroundingSpriteSheetVfx>(FindObjectsSortMode.None)) Object.DestroyImmediate(effect.gameObject);
                var controller = root.GetComponent<GroundingVisualController>();
                if (controller != null)
                {
                    var shadowField = typeof(GroundingVisualController).GetField("_shadowObject", Private);
                    Object.DestroyImmediate((GameObject)shadowField.GetValue(controller));
                    shadowField.SetValue(controller, null);
                }
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(sprite);
            }
        }
    }
}
