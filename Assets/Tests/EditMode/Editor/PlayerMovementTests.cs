using System.Reflection;
using NUnit.Framework;
using Scripts.Stats;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class PlayerMovementTests
    {
        private GameObject _player;
        private PlayerMovement _movement;
        private Rigidbody2D _body;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("MovementTest");
            _movement = _player.AddComponent<PlayerMovement>();
            _body = _player.GetComponent<Rigidbody2D>();
            Set("_rb", _body);
            Set("_stats", _player.GetComponent<PlayerStats>());
            Set("_baseGravityScale", 2.5f);
            Set("_availableJumpCount", 2);
            _body.gravityScale = 2.5f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_player);

        [Test]
        public void OppositeInputOverridesDashCarryImmediately()
        {
            _body.linearVelocity = new Vector2(12f, 0f);
            _movement.ApplyHorizontalMomentumCarry(12f, 0.35f);
            Set("_horizontalInput", -1f);
            Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.LessThan(11f));
            Assert.That(Get<bool>("_hasHorizontalLaunch"), Is.False);
        }

        [Test]
        public void ReleasingInputBrakesCarriedMomentum()
        {
            Set("_isGrounded", true);
            _body.linearVelocity = new Vector2(12f, 0f);
            _movement.ApplyHorizontalMomentumCarry(12f, 0.35f);
            Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.LessThan(12f));
        }

        [Test]
        public void CarryDoesNotReinjectDashSpeedAfterWallCollision()
        {
            _movement.ApplyHorizontalMomentumCarry(12f, 0.35f);
            _body.linearVelocity = Vector2.zero;
            Set("_horizontalInput", 1f);
            for (int i = 0; i < 100; i++) Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.LessThanOrEqualTo(5f));
        }

        [Test]
        public void AirReversalCrossesZeroWithinSixPhysicsSteps()
        {
            _body.linearVelocity = new Vector2(10f, 0f);
            Set("_horizontalInput", -1f);
            for (int i = 0; i < 6; i++) Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.LessThan(0f));
        }

        [Test]
        public void MovementStillUsesRpgMoveSpeed()
        {
            _player.GetComponent<PlayerStats>().GetStat(StatType.MoveSpeed).BaseValue = 8f;
            Set("_horizontalInput", 1f);
            for (int i = 0; i < 100; i++) Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.EqualTo(8f).Within(0.01f));
        }

        [Test]
        public void MovementLockPreservesPlayerIntent()
        {
            Set("_moveInput", Vector2.left);
            _movement.SetMovementLock(true);
            Assert.That(_movement.CurrentMoveInput, Is.EqualTo(Vector2.left));
        }

        [Test]
        public void LeavingLedgeRetainsCoyoteJumpThenOnlyAirJump()
        {
            Set("_isGrounded", true);
            Call("RefreshJumpCountIfLanded");
            Set("_isGrounded", false);
            Call("RefreshJumpCountIfLanded");
            Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(2));
            Set("_lastGroundedTime", Time.time - 1f);
            Call("RefreshJumpCountIfLanded");
            Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(1));
        }

        [Test]
        public void TakingOffCannotRefreshJumpCountWhileRising()
        {
            Set("_groundCheckPoint", _player.transform);
            _body.linearVelocity = Vector2.up * 12f;
            Set("_isGrounded", true);
            Call("CheckGround");
            Assert.That(_movement.IsGrounded, Is.False);
        }

        [Test]
        public void AirJumpsCannotBeRepeatedWithoutLanding()
        {
            Set("_groundJumpAvailable", true);
            Call("ConsumeJump");
            Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(1));
            Call("ConsumeJump");
            Assert.That((bool)Call("CanPerformJump"), Is.False);
        }

        [Test]
        public void FastFallCanBePrimedEarlyWithoutCuttingInitialRise()
        {
            Set("_jumpStartedTime", Time.time);
            Set("_moveInput", Vector2.down);
            _body.linearVelocity = Vector2.up * 10f;
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.EqualTo(10f));
            Set("_jumpStartedTime", Time.time - 0.2f);
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.LessThan(-4f));
        }

        [Test]
        public void ReleasingDownCancelsPendingFastFall()
        {
            Set("_jumpStartedTime", Time.time);
            Set("_moveInput", Vector2.down);
            Call("UpdateFastFallState");
            Set("_moveInput", Vector2.zero);
            Set("_jumpStartedTime", Time.time - 1f);
            _body.linearVelocity = Vector2.up * 5f;
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.EqualTo(5f));
            Assert.That(Get<bool>("_isFastFalling"), Is.False);
        }

        [Test]
        public void GroundDashOverrideKeepsGravityAndVerticalVelocity()
        {
            _body.linearVelocity = new Vector2(0f, -3f);
            _movement.BeginMotionOverride(new Vector2(15f, 0f), false, true);
            Call("ApplyMotionOverride");
            Assert.That(_body.linearVelocity, Is.EqualTo(new Vector2(15f, -3f)));
            Assert.That(_body.gravityScale, Is.EqualTo(2.5f));
        }

        [Test]
        public void AirDodgeRestoresBaseGravityAfterFastFall()
        {
            _body.gravityScale = 5f;
            _movement.BeginMotionOverride(Vector2.right * 10f, true);
            Assert.That(_body.gravityScale, Is.Zero);
            _movement.EndMotionOverride(Vector2.zero);
            Assert.That(_body.gravityScale, Is.EqualTo(2.5f));
        }

        [Test]
        public void FallSpeedIsBounded()
        {
            _body.linearVelocity = Vector2.down * 100f;
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.EqualTo(-24f));
        }

        [Test]
        public void BufferedJumpWaitsForUnlockAndIsConsumedOnce()
        {
            Set("_hasQueuedJump", true);
            Set("_jumpQueuedUntilTime", Time.time + 0.12f);
            _movement.SetMovementLock(true);
            Call("ProcessQueuedJump");
            Assert.That(_movement.HasBufferedJump, Is.True);
            _movement.SetMovementLock(false);
            Call("ProcessQueuedJump");
            Assert.That(_movement.HasBufferedJump, Is.False);
            Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(1));
            Call("ProcessQueuedJump");
            Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(1));
        }

        [Test]
        public void GroundDashDoesNotGrantDodgeInvulnerability()
        {
            var attack = _player.AddComponent<PlayerAttackInput>();
            typeof(PlayerAttackInput).GetField("_isDodging", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(attack, true);
            var groundDash = typeof(PlayerAttackInput).GetField("_isGroundDash", BindingFlags.Instance | BindingFlags.NonPublic);
            groundDash.SetValue(attack, true);
            Assert.That(attack.IsDashing, Is.True);
            Assert.That(attack.IsDamageImmune, Is.False);
            groundDash.SetValue(attack, false);
            Assert.That(attack.IsDamageImmune, Is.True);
            typeof(PlayerAttackInput).GetField("_isDodging", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(attack, false);
        }

        [Test]
        public void SolidWallAtFeetIsNotGround()
        {
            var wall = new GameObject("Wall", typeof(BoxCollider2D));
            try
            {
                wall.layer = 6;
                wall.transform.position = new Vector3(0.15f, 0.6f, 0f);
                wall.transform.localScale = new Vector3(0.1f, 2f, 1f);
                Set("_groundCheckPoint", _player.transform);
                Set("_groundLayer", (LayerMask)(1 << 6));
                Physics2D.SyncTransforms();
                Call("CheckGround");
                Assert.That(_movement.IsGrounded, Is.False);
            }
            finally { Object.DestroyImmediate(wall); }
        }

        private void Set(string name, object value) => typeof(PlayerMovement)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_movement, value);
        private T Get<T>(string name) => (T)typeof(PlayerMovement)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_movement);
        private object Call(string name) => typeof(PlayerMovement)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_movement, null);
    }
}
