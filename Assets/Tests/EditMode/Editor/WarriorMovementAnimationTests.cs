using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class WarriorMovementAnimationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void RunArt_GripTracksWeaponWithinOnePixel_WhileLegsAnimate()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath,
                    "Resources/Heroes/WarriorResources/Refined/WarriorRun-Sheet.png"))), Is.True);
                var pixels = texture.GetPixels32();
                var offsets = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").RunAnimation.WeaponVerticalOffsets;
                bool legsChanged = false;
                bool upperBodyChanged = false;
                for (int frame = 1; frame < 9; frame++)
                {
                    int originX = frame % 4 * 28, originY = (2 - frame / 4) * 28;
                    int handDrop = -Mathf.RoundToInt(offsets[frame] * 24);
                    Assert.That(handDrop, Is.InRange(0, 1));
                    for (int y = 0; y < 28; y++)
                    for (int x = 0; x < 28; x++)
                    {
                        var reference = pixels[(56 + y) * texture.width + x];
                        var actual = pixels[(originY + y) * texture.width + originX + x];
                        // The lowered hand extends below the waist into the leg rows.
                        // Protect its fingertips and outline too, so a passing leg cannot erase it.
                        bool loweredHand = x >= 19 && x <= 21 && y >= 3 - handDrop && y <= 7 - handDrop;
                        bool offhandContour = x >= 3 && x <= 7 && y >= 3 - handDrop && y <= 10 - handDrop;
                        if (loweredHand || offhandContour || (y >= 6 - handDrop && y <= 10 - handDrop))
                            Assert.That(actual, Is.EqualTo(pixels[(56 + y + handDrop) * texture.width + x]), "Grip separated from weapon in frame " + frame);
                        else if (!actual.Equals(reference))
                        {
                            if (y < 6) legsChanged = true;
                            else upperBodyChanged = true;
                        }
                    }
                }
                Assert.That(legsChanged, Is.True);
                Assert.That(upperBodyChanged, Is.True);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void JumpWidth_IsPresentationOnly_AndReturnsToNormalOnRunAndIdle()
        {
            var owner = new GameObject("Warrior jump width test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior") });
                var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationHorizontalScale, Is.EqualTo(0.94f));
                var visual = owner.AddComponent<PlayerMovementVisual>();
                typeof(PlayerMovementVisual).GetMethod("Awake", Flags).Invoke(visual, null);
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Flags).Invoke(visual, null);
                Assert.That(visual.DisplayRenderer.transform.localScale.x, Is.EqualTo(0.94f));
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                Assert.That(controller.PresentationHorizontalScale, Is.EqualTo(0.97f));
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Run"), type.GetField("_run", Flags).GetValue(controller) });
                Assert.That(controller.PresentationHorizontalScale, Is.EqualTo(1f));
                type.GetMethod("PlayIdle", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationHorizontalScale, Is.EqualTo(1f));
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Flags).Invoke(visual, null);
                Assert.That(visual.DisplayRenderer.transform.localScale.x, Is.EqualTo(1f));
                Assert.That(owner.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void SmallGripMotion_FollowsSpriteFrame_AndStopsInAirborneStates()
        {
            var owner = new GameObject("Warrior weapon bob test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                var warrior = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { warrior });
                var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Run"), type.GetField("_run", Flags).GetValue(controller) });
                type.GetField("_frameTimer", Flags).SetValue(controller, warrior.RunAnimation.FrameDuration * 0.5f);
                Assert.That(controller.RunWeaponVerticalOffset, Is.Zero);
                type.GetField("_frameIndex", Flags).SetValue(controller, 2);
                type.GetField("_frameTimer", Flags).SetValue(controller, warrior.RunAnimation.FrameDuration * 0.5f);
                Assert.That(controller.RunWeaponVerticalOffset, Is.EqualTo(-1f / 24));
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(controller.RunWeaponVerticalOffset, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void RefinedMovement_LoadsAllStates_WithStableFrameBoundsAndAirborneHolds()
        {
            var warrior = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
            Assert.That(warrior, Is.Not.Null);
            foreach (var animation in new[] { warrior.RunAnimation, warrior.JumpAnimation, warrior.FallAnimation })
            {
                var frames = Resources.LoadAll<Sprite>(animation.SpriteSheetResourcesPath);
                Assert.That(frames.Length, Is.GreaterThanOrEqualTo(5));
                foreach (var frame in frames)
                {
                    Assert.That(frame.pixelsPerUnit, Is.EqualTo(24));
                    Assert.That(frame.rect.size, Is.EqualTo(new Vector2(28, 28)));
                    Assert.That(frame.pivot, Is.EqualTo(new Vector2(14, 14)));
                }
            }
            Assert.That(warrior.RunAnimation.Loop, Is.True);
            Assert.That(warrior.JumpAnimation.Loop, Is.False);
            Assert.That(warrior.FallAnimation.Loop, Is.False);
            Assert.That(Resources.LoadAll<Sprite>(warrior.RunAnimation.SpriteSheetResourcesPath).Length * warrior.RunAnimation.FrameDuration,
                Is.EqualTo(0.72f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AirborneAnimation_ReachesFinalPoseAndHoldsUntilStateChanges(bool falling)
        {
            var owner = new GameObject("Warrior animation state test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                typeof(PlayerCharacterAnimationController).GetMethod("Awake", Flags).Invoke(controller, null);
                var warrior = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { warrior });
                typeof(PlayerCharacterAnimationController).GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                if (falling)
                {
                    var state = typeof(PlayerCharacterAnimationController).GetNestedType("AnimationState", BindingFlags.NonPublic);
                    typeof(PlayerCharacterAnimationController).GetMethod("Play", Flags).Invoke(controller, new[]
                    {
                        System.Enum.Parse(state, "Fall"),
                        typeof(PlayerCharacterAnimationController).GetField("_fall", Flags).GetValue(controller)
                    });
                }
                var frames = Resources.LoadAll<Sprite>((falling ? warrior.FallAnimation : warrior.JumpAnimation).SpriteSheetResourcesPath);
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                typeof(PlayerCharacterAnimationController).GetField("_frameTimer", Flags).SetValue(controller, 10f);
                typeof(PlayerCharacterAnimationController).GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[frames.Length - 1]));
                typeof(PlayerCharacterAnimationController).GetField("_frameTimer", Flags).SetValue(controller, 10f);
                typeof(PlayerCharacterAnimationController).GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[frames.Length - 1]));
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
