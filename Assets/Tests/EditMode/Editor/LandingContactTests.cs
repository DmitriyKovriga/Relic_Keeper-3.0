using System;
using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class LandingContactTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

        [Test]
        public void ContactPublishesApproachSpeedBeforeEvent_DespiteCollisionZeroingVelocity()
        {
            var owner = new GameObject("Landing speed regression", typeof(BoxCollider2D));
            try
            {
                var move = owner.AddComponent<PlayerMovement>(); var rb = owner.GetComponent<Rigidbody2D>(); Set(move, "_rb", rb);
                rb.linearVelocity = Vector2.down * 12;
                Set(move, "_isGrounded", false); Call(move, "RefreshJumpCountIfLanded");
                rb.linearVelocity = Vector2.zero; Set(move, "_isGrounded", true);
                float observed = -1; move.OnLanded += () => observed = move.LastLandingSpeed;
                Call(move, "RefreshJumpCountIfLanded");
                Assert.That(observed, Is.EqualTo(12)); Assert.That(rb.linearVelocity, Is.EqualTo(Vector2.zero));
                Set(move, "_wasGroundedLastFixedUpdate", true); Call(move, "RefreshJumpCountIfLanded");
                Set(move, "_isGrounded", false); rb.linearVelocity = Vector2.down * 3; Call(move, "RefreshJumpCountIfLanded");
                Set(move, "_wasGroundedLastFixedUpdate", false); Set(move, "_isGrounded", true); rb.linearVelocity = Vector2.zero;
                Call(move, "RefreshJumpCountIfLanded"); Assert.That(move.LastLandingSpeed, Is.EqualTo(3), "A later small fall cannot retain a previous heavy impact.");
                move.TeleportForSkill(new Vector2(1000, 10), 1);
                Assert.That(move.LastLandingSpeed, Is.Zero, "A teleport must not carry an old impact to its destination.");
                Call(move, "OnDisable"); Assert.That(move.LastLandingSpeed, Is.Zero);
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("Warrior")]
        [TestCase("Rogue")]
        [TestCase("Mage")]
        public void LandingScalesWithImpact_RecoversAtTheFeet_AndJumpInterrupts(string hero)
        {
            var owner = new GameObject("Landing pose regression"); owner.SetActive(false);
            var profile = ScriptableObject.CreateInstance<CombatPoseProfile>();
            try
            {
                var data = Resources.Load<CharacterDataSO>($"Heroes/{hero}Resources/{hero}");
                var dodge = owner.AddComponent<PlayerAttackInput>();
                var animation = owner.AddComponent<PlayerCharacterAnimationController>(); Call(animation, "Awake");
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Private).Invoke(animation, new object[] { data });
                Set(animation, "_combatPoseProfile", profile);
                var move = owner.GetComponent<PlayerMovement>();
                Func<float, Vector2> land = speed => {
                    Call(animation, "HandleJumpStarted"); Set(move, "<LastLandingSpeed>k__BackingField", speed);
                    Call(animation, "HandleLanded"); return animation.PresentationScale;
                };
                Assert.That(land(1), Is.EqualTo(Vector2.one), "Tiny contacts need no recovery pose.");
                Vector2 low = land(4), high = land(14);
                Assert.That(low.y, Is.GreaterThan(high.y)); Assert.That(low.y, Is.GreaterThan(.97f));
                Assert.That(high, Is.EqualTo(data.LandingAnimation.PresentationScales[0]));
                Assert.That(land(10000), Is.EqualTo(high), "High drops must not stretch the class pose beyond its authored limit.");
                Set(animation, "_frameTimer", 1f); Call(animation, "TickCurrentAnimation");
                Assert.That(animation.PresentationScale, Is.EqualTo(Vector2.one));
                land(14); Call(animation, "HandleJumpStarted");
                Assert.That(animation.PresentationScale, Is.EqualTo(Vector2.one));
                Assert.That(owner.GetComponent<SpriteRenderer>().sprite, Is.SameAs(data.JumpAnimation.Frames[0]));
                land(14); Set(dodge, "_isDodging", true); Set(dodge, "_isGroundDash", true);
                Call(animation, "Update");
                Assert.That(animation.PresentationScale, Is.EqualTo(Vector2.one), "Ground dash must immediately release landing compression.");
                Set(dodge, "_isDodging", false);
                profile.LandingPoseStrength = 0; Assert.That(land(14), Is.EqualTo(Vector2.one));
                profile.EnableLandingWeight = false; Assert.That(land(4), Is.EqualTo(high), "Off restores the original class landing.");
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(profile); }
        }

        [TestCase("Warrior")]
        [TestCase("Rogue")]
        [TestCase("Mage")]
        [TestCase("Warrior", -1)]
        [TestCase("Rogue", -1)]
        [TestCase("Mage", -1)]
        public void MovingLandingImmediatelyResumesRun_StationaryLandingCanBeInterrupted(string hero, int direction = 1)
        {
            var owner = new GameObject("Moving landing regression", typeof(BoxCollider2D));
            try
            {
                var animation = owner.AddComponent<PlayerCharacterAnimationController>(); Call(animation, "Awake");
                var data = Resources.Load<CharacterDataSO>($"Heroes/{hero}Resources/{hero}");
                typeof(PlayerCharacterAnimationController).GetMethod("ApplyCharacterData", Private).Invoke(animation, new object[] { data });
                var move = owner.GetComponent<PlayerMovement>(); var rb = owner.GetComponent<Rigidbody2D>(); Set(move, "_rb", rb);
                Action<float> contact = speed => {
                    Call(animation, "HandleJumpStarted"); Set(move, "<LastLandingSpeed>k__BackingField", speed);
                    Set(move, "_isGrounded", true); Call(animation, "HandleLanded");
                };
                Vector2 travel = Vector2.right * (5 * direction); Vector3 position = owner.transform.position;
                rb.linearVelocity = travel; contact(14); Call(animation, "Update");
                Assert.That(typeof(PlayerCharacterAnimationController).GetField("_state", Private).GetValue(animation).ToString(), Is.EqualTo("Run"),
                    "Forward movement cannot retain the stationary landing sprite or wait for the ground-confirm timer.");
                Assert.That(rb.linearVelocity, Is.EqualTo(travel)); Assert.That(owner.transform.position, Is.EqualTo(position));
                rb.linearVelocity = Vector2.zero; contact(14); Call(animation, "Update");
                Assert.That(typeof(PlayerCharacterAnimationController).GetField("_state", Private).GetValue(animation).ToString(), Is.EqualTo("Land"));
                rb.linearVelocity = travel; Call(animation, "Update");
                Assert.That(typeof(PlayerCharacterAnimationController).GetField("_state", Private).GetValue(animation).ToString(), Is.EqualTo("Run"), "Starting movement interrupts recovery immediately.");
                contact(1); Call(animation, "Update");
                Assert.That(typeof(PlayerCharacterAnimationController).GetField("_state", Private).GetValue(animation).ToString(), Is.EqualTo("Run"), "Quiet contacts also resume running without an idle hold.");
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("Warrior")]
        [TestCase("Rogue")]
        [TestCase("Mage")]
        public void FootContactsOccurOnlyWhenEnteringAnAuthoredPlantedFrame(string hero)
        {
            var owner = new GameObject("Foot phase regression"); owner.SetActive(false);
            try
            {
                var data = Resources.Load<CharacterDataSO>($"Heroes/{hero}Resources/{hero}");
                var animation = owner.AddComponent<PlayerCharacterAnimationController>(); Call(animation, "Awake");
                var type = typeof(PlayerCharacterAnimationController);
                type.GetMethod("ApplyCharacterData", Private).Invoke(animation, new object[] { data });
                var state = type.GetNestedType("AnimationState", BindingFlags.NonPublic);
                type.GetMethod("Play", Private).Invoke(animation, new[] { Enum.Parse(state, "Run"), type.GetField("_run", Private).GetValue(animation) });
                Assert.That(animation.HasRunContactFrames, Is.True);
                for (int frame = 1; frame < data.RunAnimation.Frames.Length; frame++)
                {
                    int version = animation.RunContactVersion;
                    Set(animation, "_frameTimer", data.RunAnimation.FrameDuration + .0001f - Time.deltaTime);
                    Call(animation, "TickCurrentAnimation");
                    bool contact = Array.IndexOf(data.RunAnimation.FootContactFrames, frame) >= 0;
                    Assert.That(animation.RunContactVersion - version, Is.EqualTo(contact ? 1 : 0), $"{hero} frame {frame}");
                }
                int beforeJump = animation.RunContactVersion;
                Call(animation, "HandleJumpStarted"); Set(animation, "_frameTimer", .3f); Call(animation, "TickCurrentAnimation");
                Assert.That(animation.RunContactVersion, Is.EqualTo(beforeJump), "Airborne poses cannot create steps.");
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
