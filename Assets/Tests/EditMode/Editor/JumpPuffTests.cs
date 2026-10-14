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

        [TestCase(false)]
        [TestCase(true)]
        public void JumpEventSpawnsOnce_DisablingUnsubscribes_AndPuffStaysAtTakeoff(bool grounded)
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
                var evt = typeof(PlayerMovement).GetField("OnJumpStarted", Private);
                var jump = (Action)evt.GetValue(movement);
                Assert.That(jump, Is.Not.Null);
                jump.Invoke();
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
                Assert.That(renderer.color.a, Is.EqualTo(0.9f).Within(0.001f));
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
