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
        public void SelectedOutfit_PreservesBasePixelsAndIdentity_WithGroundedBoots()
        {
            var warrior = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
            Assert.That(UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(warrior.PassiveTree)),
                Is.EqualTo("1ac34a4e0d7b149478f48e7eff040153"));
            var raw = new Texture2D(2, 2);
            var imported = new Texture2D(2, 2);
            try
            {
                raw.LoadImage(System.IO.File.ReadAllBytes("Docs/Art/WarriorVariantsRound2/Warrior-05.png"));
                imported.LoadImage(System.IO.File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(warrior.Portrait)));
                Assert.That(imported.GetPixels(), Is.EqualTo(raw.GetPixels(2, 2, 28, 28)),
                    "Selected face, hair and outfit must retain their exact native pixels.");
                Assert.That(imported.GetPixels(0, 0, 28, 1), Has.Some.Matches<Color>(pixel => pixel.a > 0),
                    "The transparent reference border must not lift boots off the ground.");
                Assert.That(warrior.Portrait.rect.size, Is.EqualTo(new Vector2(28, 28)));
                foreach (var animation in new[] { warrior.RunAnimation, warrior.JumpAnimation, warrior.FallAnimation, warrior.LandingAnimation })
                {
                    Assert.That(animation.WeaponHorizontalOffsets.Length, Is.EqualTo(animation.Frames.Length));
                    Assert.That(animation.WeaponVerticalOffsets.Length, Is.EqualTo(animation.Frames.Length));
                }
                Assert.That(warrior.LandingAnimation.Frames[0], Is.SameAs(warrior.Portrait));
            }
            finally { Object.DestroyImmediate(raw); Object.DestroyImmediate(imported); }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void AirJump_SkipsGroundTakeoff_AndRestartsCompletedAscent(bool falling, bool completed)
        {
            var owner = new GameObject("Air jump restart test");
            owner.SetActive(false);
            try
            {
                var type = typeof(PlayerCharacterAnimationController);
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                var warrior = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { warrior });
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(warrior.JumpAnimation.Frames[0]));
                if (falling)
                {
                    var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                    type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                }
                type.GetField("_frameTimer", Flags).SetValue(controller, completed ? 10f : 0.2f);
                type.GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                int start = warrior.JumpAnimation.AirborneRestartFrame;
                Assert.That(start, Is.GreaterThan(0), "Warrior needs an airborne restart pose distinct from ground anticipation.");
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(warrior.JumpAnimation.Frames[start]));
                Assert.That(type.GetField("_frameTimer", Flags).GetValue(controller), Is.EqualTo(0f));
                Assert.That(type.GetField("_animationComplete", Flags).GetValue(controller), Is.EqualTo(false));
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(new Vector3(warrior.JumpAnimation.WeaponHorizontalOffsets[start], warrior.JumpAnimation.WeaponVerticalOffsets[start], 0)));
                type.GetField("_frameTimer", Flags).SetValue(controller, 10f);
                type.GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(warrior.JumpAnimation.Frames[4]));
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Flags).SetValue(owner.GetComponent<PlayerMovement>(), 14f);
                type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(warrior.JumpAnimation.Frames[0]), "Landing must restore the next ground takeoff.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void FallArt_HoldsApexLegPose_InsteadOfSteppingBackToGroundStance()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath, "Resources/Heroes/WarriorResources/Refined/WarriorFall-Sheet.png")));
                for (int frame = 1; frame < 5; frame++)
                for (int y = 0; y < 28; y++)
                for (int x = 0; x < 28; x++)
                    Assert.That(texture.GetPixel(frame % 4 * 28 + x, (1 - frame / 4) * 28 + y), Is.EqualTo(texture.GetPixel(x, 28 + y)), "Descent must not advance a walking-like leg cycle.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void Landing_CompressesThenRecovers_AndNewJumpInterruptsIt()
        {
            var owner = new GameObject("Landing transition test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior") });
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Flags).SetValue(owner.GetComponent<PlayerMovement>(), 14f);
                type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one), "Spawn contact must not trigger landing.");
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Flags).SetValue(owner.GetComponent<PlayerMovement>(), 14f);
                type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale.x, Is.InRange(1.1f, 1.2f));
                Assert.That(controller.PresentationScale.y, Is.InRange(0.8f, 0.9f));
                Assert.That(type.GetProperty("IsLandingPlaying", Flags).GetValue(controller), Is.True);
                var visual = owner.AddComponent<PlayerMovementVisual>();
                typeof(PlayerMovementVisual).GetMethod("Awake", Flags).Invoke(visual, null);
                typeof(PlayerMovementVisual).GetField("_alignFeetToCollider", Flags).SetValue(visual, false);
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Flags).Invoke(visual, null);
                var display = visual.DisplayRenderer;
                Assert.That(display.transform.localPosition.y + display.sprite.bounds.min.y * display.transform.localScale.y,
                    Is.EqualTo(display.sprite.bounds.min.y).Within(0.0001f), "Squash must keep feet anchored.");
                Assert.That(owner.transform.localScale, Is.EqualTo(Vector3.one));
                type.GetField("_frameTimer", Flags).SetValue(controller, 10f);
                type.GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                Assert.That(type.GetProperty("IsLandingPlaying", Flags).GetValue(controller), Is.False, "Landing must finish rather than loop forever.");
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Flags).SetValue(owner.GetComponent<PlayerMovement>(), 14f);
                type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                Assert.That(type.GetProperty("IsLandingPlaying", Flags).GetValue(controller), Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void AirborneArt_TracksConnectedGeneratedHand_WhileUpperAndLowerPosesChange()
        {
            var run = new Texture2D(2, 2);
            var air = new Texture2D(2, 2);
            try
            {
                string root = System.IO.Path.Combine(Application.dataPath, "Resources/Heroes/WarriorResources/Refined");
                run.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "WarriorRun-Sheet.png")));
                bool legsChanged = false;
                bool torsoChanged = false;
                var upperPoses = new System.Collections.Generic.HashSet<string>();
                var lowerPoses = new System.Collections.Generic.HashSet<string>();
                foreach (string name in new[] { "WarriorJump", "WarriorFall" })
                {
                    air.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, name + "-Sheet.png")));
                    for (int frame = 0; frame < 5; frame++)
                    {
                        var upper = new System.Text.StringBuilder();
                        var lower = new System.Text.StringBuilder();
                        for (int y = 0; y < 28; y++)
                        for (int x = 0; x < 28; x++)
                        {
                            Color32 pixel = air.GetPixel(frame % 4 * 28 + x, (1 - frame / 4) * 28 + y);
                            (y >= 8 ? upper : lower).Append(pixel.r).Append(',').Append(pixel.g).Append(',').Append(pixel.b).Append(',').Append(pixel.a).Append(';');
                        }
                        upperPoses.Add(upper.ToString());
                        lowerPoses.Add(lower.ToString());
                    }
                    for (int i = 0; i < 5; i++)
                    for (int y = 0; y < 28; y++)
                    for (int x = 0; x < 28; x++)
                    {
                        var animation = name == "WarriorJump" ? Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").JumpAnimation
                            : Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").FallAnimation;
                        int dx = Mathf.RoundToInt(animation.WeaponHorizontalOffsets[i] * 24);
                        int dy = Mathf.RoundToInt(animation.WeaponVerticalOffsets[i] * 24);
                        var expected = run.GetPixel(x, 56 + y);
                        var actual = air.GetPixel(i % 4 * 28 + x, (1 - i / 4) * 28 + y);
                        bool gripAnchor = x == 19 + dx && y == 4 + dy;
                        if (gripAnchor)
                        {
                            Assert.That(IsGripSkin(actual), Is.True, name + " weapon anchor must land on the generated hand in frame " + i);
                            AssertGripConnected(air, i, x, y);
                        }
                        else if (actual != expected)
                        {
                            if (y < 6) legsChanged = true;
                            if (y >= 11 && y < 16) torsoChanged = true;
                        }
                    }
                }
                Assert.That(legsChanged, Is.True, "Airborne motion needs a drawn leg pose.");
                Assert.That(torsoChanged, Is.True, "Airborne torso shaping must be authored in pixels.");
                Assert.That(upperPoses.Count, Is.GreaterThanOrEqualTo(4), "Upper body must move through distinct poses, not hold a frozen silhouette.");
                Assert.That(lowerPoses.Count, Is.GreaterThanOrEqualTo(4), "Legs must progress through takeoff, tuck and descent poses.");
            }
            finally { Object.DestroyImmediate(run); Object.DestroyImmediate(air); }
        }

        [Test]
        public void JumpAndFall_ShareExactApex_WithConnectedGeneratedHandAndMatchingWeaponGrip()
        {
            var jump = new Texture2D(2, 2);
            var fall = new Texture2D(2, 2);
            var run = new Texture2D(2, 2);
            var owner = new GameObject("Shared apex grip test");
            owner.SetActive(false);
            try
            {
                string root = System.IO.Path.Combine(Application.dataPath, "Resources/Heroes/WarriorResources/Refined");
                jump.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "WarriorJump-Sheet.png")));
                fall.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "WarriorFall-Sheet.png")));
                run.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(root, "WarriorRun-Sheet.png")));
                for (int y = 0; y < 28; y++)
                for (int x = 0; x < 28; x++)
                    Assert.That(jump.GetPixel(x, y), Is.EqualTo(fall.GetPixel(x, 28 + y)), "Apex transition must not change pose.");
                // Derive the inner/lower fingertip from the generated skin cluster, not a copied run rectangle.
                int airborneGripX = -1, airborneGripY = 28;
                for (int y = 2; y < 11; y++)
                for (int x = 17; x < 26; x++)
                    if (IsGripSkin(jump.GetPixel(x, y)) && y < airborneGripY)
                    { airborneGripX = x; airborneGripY = y; }
                Assert.That(airborneGripX, Is.InRange(17, 25));
                Assert.That(airborneGripY, Is.InRange(2, 10));
                Vector3 expectedGripOffset = new Vector3((airborneGripX - 19) / 24f, (airborneGripY - 4) / 24f, 0);
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior") });
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                type.GetField("_frameIndex", Flags).SetValue(controller, 4);
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(expectedGripOffset));
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(expectedGripOffset));
                type.GetMethod("PlayIdle", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(new Vector3(Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").RunAnimation.WeaponHorizontalOffsets[0], Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").RunAnimation.WeaponVerticalOffsets[0], 0)));
            }
            finally { Object.DestroyImmediate(jump); Object.DestroyImmediate(fall); Object.DestroyImmediate(run); Object.DestroyImmediate(owner); }
        }

        private static bool IsGripSkin(Color color) => color.a > 0.99f && color.r > 0.4f && color.g > 0.3f && color.b > 0.18f;

        private static void AssertGripConnected(Texture2D sheet, int frame, int gripX, int gripY)
        {
            int originX = frame % 4 * 28, originY = (sheet.height / 28 - 1 - frame / 4) * 28;
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            var visited = new System.Collections.Generic.HashSet<Vector2Int>();
            queue.Enqueue(new Vector2Int(gripX, gripY));
            bool reachesBody = false;
            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                if (point.x < 0 || point.x >= 28 || point.y < 0 || point.y >= 28 || !visited.Add(point)) continue;
                if (sheet.GetPixel(originX + point.x, originY + point.y).a <= 0) continue;
                if (point.x >= 8 && point.x <= 17 && point.y >= 10 && point.y <= 16) reachesBody = true;
                queue.Enqueue(point + Vector2Int.left);
                queue.Enqueue(point + Vector2Int.right);
                queue.Enqueue(point + Vector2Int.up);
                queue.Enqueue(point + Vector2Int.down);
            }
            Assert.That(reachesBody, Is.True, "Generated grip must connect to the torso through the arm, not float as an isolated patch.");
        }

        [Test]
        public void RunArt_GripsStayConnectedToBody_WhileLegsAnimate()
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath,
                    "Resources/Heroes/WarriorResources/Refined/WarriorRun-Sheet.png")));
                var animation = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior").RunAnimation;
                bool legsChanged = false;
                for (int frame = 0; frame < 9; frame++)
                {
                    int originY = (2 - frame / 4) * 28;
                    int x = 19 + Mathf.RoundToInt(animation.WeaponHorizontalOffsets[frame] * 24);
                    int y = 4 + Mathf.RoundToInt(animation.WeaponVerticalOffsets[frame] * 24);
                    Assert.That(IsGripSkin(texture.GetPixel(frame % 4 * 28 + x, originY + y)), Is.True,
                        "Every frame must anchor equipment on the complete hand.");
                    AssertGripConnected(texture, frame, x, y);
                    for (int legY = 0; legY < 6; legY++)
                    for (int legX = 0; legX < 28; legX++)
                        if (texture.GetPixel(frame % 4 * 28 + legX, originY + legY) != texture.GetPixel(legX, 56 + legY)) legsChanged = true;
                }
                Assert.That(legsChanged, Is.True, "Outfit transfer must preserve the moving leg cycle.");
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
                Assert.That(controller.PresentationHorizontalScale, Is.EqualTo(1f));
                var visual = owner.AddComponent<PlayerMovementVisual>();
                typeof(PlayerMovementVisual).GetMethod("Awake", Flags).Invoke(visual, null);
                typeof(PlayerMovementVisual).GetMethod("LateUpdate", Flags).Invoke(visual, null);
                Assert.That(visual.DisplayRenderer.transform.localScale.x, Is.EqualTo(1f));
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one), "Airborne silhouette must be drawn into the sprite.");
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
                Assert.That(controller.RunWeaponVerticalOffset, Is.EqualTo(warrior.RunAnimation.WeaponVerticalOffsets[0]));
                type.GetField("_frameIndex", Flags).SetValue(controller, 2);
                type.GetField("_frameTimer", Flags).SetValue(controller, warrior.RunAnimation.FrameDuration * 0.5f);
                Assert.That(controller.RunWeaponVerticalOffset, Is.EqualTo(warrior.RunAnimation.WeaponVerticalOffsets[2]));
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
                var frames = animation.Frames != null && animation.Frames.Length > 0
                    ? animation.Frames : Resources.LoadAll<Sprite>(animation.SpriteSheetResourcesPath);
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
                var frames = (falling ? warrior.FallAnimation : warrior.JumpAnimation).Frames;
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                typeof(PlayerCharacterAnimationController).GetField("_frameTimer", Flags).SetValue(controller, 10f);
                typeof(PlayerCharacterAnimationController).GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[frames.Length - 1]));
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one), "Airborne art must not be stretched at runtime.");
                typeof(PlayerCharacterAnimationController).GetField("_frameTimer", Flags).SetValue(controller, 10f);
                typeof(PlayerCharacterAnimationController).GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(frames[frames.Length - 1]));
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one), "Airborne art must not be stretched at runtime.");
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
