using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEditor;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class RogueMovementAnimationTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static CharacterDataSO Rogue => Resources.Load<CharacterDataSO>("Heroes/RogueResources/Rogue");

        [TestCase("RogueRun", 8)]
        [TestCase("RogueJump", 5)]
        [TestCase("RogueFall", 1)]
        public void RefinedFrames_PreservePixelGridAndImportSettings(string name, int count)
        {
            string path = "Assets/Resources/Heroes/RogueResources/Refined/" + name + "-Sheet.png";
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Assert.That(frames.Length, Is.EqualTo(count));
            foreach (var frame in frames)
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(28, 28)));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(24));
                Assert.That(frame.pivot, Is.EqualTo(new Vector2(14, 14)));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        }

        [Test]
        public void RestPoseAndPortraitShareTheNewModel_WithCompleteGripProfiles()
        {
            var rogue = Rogue;
            Assert.That(rogue.ID, Is.EqualTo("Rogue"));
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(rogue.PassiveTree)), Is.EqualTo("20763e4315a1c554b8aa3afbdc108ae2"));
            Assert.That(FramePixels(rogue.Portrait), Is.EqualTo(FramePixels(rogue.RunAnimation.Frames[0])));
            Assert.That(rogue.RunAnimation.Loop, Is.True);
            foreach (var animation in new[] { rogue.RunAnimation, rogue.JumpAnimation, rogue.FallAnimation, rogue.LandingAnimation })
            {
                Assert.That(animation.Frames, Is.Not.Empty);
                Assert.That(animation.WeaponHorizontalOffsets.Length, Is.EqualTo(animation.Frames.Length));
                Assert.That(animation.WeaponVerticalOffsets.Length, Is.EqualTo(animation.Frames.Length));
            }
            Assert.That(rogue.JumpAnimation.Loop, Is.False);
            Assert.That(rogue.FallAnimation.Loop, Is.False);
            Assert.That(FramePixels(rogue.FallAnimation.Frames[0]), Is.EqualTo(FramePixels(rogue.JumpAnimation.Frames[4])), "Fall must retain the apex without an air-walking cycle.");
        }

        [Test]
        public void IdleGrip_UsesRestPose_AndCharacterSwapResetsIt()
        {
            var owner = new GameObject("Rogue idle grip test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Rogue });
                var expected = new Vector3(Rogue.RunAnimation.WeaponHorizontalOffsets[0], Rogue.RunAnimation.WeaponVerticalOffsets[0], 0);
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(expected));
                var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Run"), type.GetField("_run", Flags).GetValue(controller) });
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(expected));
                type.GetMethod("PlayIdle", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(expected));
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior") });
                var swapped = Resources.Load<CharacterDataSO>("Heroes/WarriorResources/Warrior");
                Assert.That(controller.PresentationWeaponOffset, Is.EqualTo(new Vector3(
                    swapped.RunAnimation.WeaponHorizontalOffsets[0], swapped.RunAnimation.WeaponVerticalOffsets[0], 0)));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AirJumpSkipsGroundAnticipation_AndLandingReturnsToNewRestPose(bool falling)
        {
            var owner = new GameObject("Rogue airborne test");
            owner.SetActive(false);
            try
            {
                var controller = owner.AddComponent<PlayerCharacterAnimationController>();
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("Awake", Flags).Invoke(controller, null);
                type.GetMethod("ApplyCharacterData", Flags).Invoke(controller, new object[] { Rogue });
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                type.GetField("_frameTimer", Flags).SetValue(controller, 10f);
                type.GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                if (falling)
                {
                    var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                    type.GetMethod("Play", Flags).Invoke(controller, new[] { System.Enum.Parse(state, "Fall"), type.GetField("_fall", Flags).GetValue(controller) });
                }
                type.GetMethod("HandleJumpStarted", Flags).Invoke(controller, null);
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Rogue.JumpAnimation.Frames[3]));
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                typeof(PlayerMovement).GetField("<LastLandingSpeed>k__BackingField", Flags).SetValue(owner.GetComponent<PlayerMovement>(), 14f);
                type.GetMethod("HandleLanded", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale.y, Is.LessThan(1));
                type.GetField("_frameTimer", Flags).SetValue(controller, 10f);
                type.GetMethod("TickCurrentAnimation", Flags).Invoke(controller, null);
                Assert.That(controller.PresentationScale, Is.EqualTo(Vector2.one));
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(Rogue.Portrait));
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [Test]
        public void GeneratedGrips_AreConnectedToBody_InRunAndJump()
        {
            foreach (var animation in new[] { Rogue.RunAnimation, Rogue.JumpAnimation })
            for (int frame = 0; frame < animation.Frames.Length; frame++)
            {
                var pixels = FramePixels(animation.Frames[frame]);
                int x = 19 + Mathf.RoundToInt(animation.WeaponHorizontalOffsets[frame] * 24);
                int y = 4 + Mathf.RoundToInt(animation.WeaponVerticalOffsets[frame] * 24);
                Assert.That(x, Is.InRange(0, 27));
                Assert.That(y, Is.InRange(0, 27));
                Assert.That(pixels[y * 28 + x].a, Is.GreaterThan(0), "Equipment anchor must lie on the complete generated glove.");
                var pending = new System.Collections.Generic.Queue<Vector2Int>();
                var visited = new System.Collections.Generic.HashSet<Vector2Int>();
                pending.Enqueue(new Vector2Int(x, y));
                bool connected = false;
                while (pending.Count > 0)
                {
                    var point = pending.Dequeue();
                    if (point.x < 0 || point.x >= 28 || point.y < 0 || point.y >= 28 || !visited.Add(point) || pixels[point.y * 28 + point.x].a <= 0) continue;
                    if (point.x >= 9 && point.x <= 17 && point.y >= 10 && point.y <= 16) connected = true;
                    pending.Enqueue(point + Vector2Int.left);
                    pending.Enqueue(point + Vector2Int.right);
                    pending.Enqueue(point + Vector2Int.up);
                    pending.Enqueue(point + Vector2Int.down);
                }
                Assert.That(connected, Is.True, "The new glove must connect through the arm to the torso.");
            }
        }

        private static Color[] FramePixels(Sprite sprite)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                var rect = sprite.rect;
                return texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
