using System.Reflection;
using NUnit.Framework;
using Scripts.Stats;
using Scripts.Visuals;
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
            _player = new GameObject("MovementTest", typeof(BoxCollider2D));
            _movement = _player.AddComponent<PlayerMovement>();
            _body = _player.GetComponent<Rigidbody2D>();
            Set("_rb", _body);
            Set("_mainCollider", _player.GetComponent<BoxCollider2D>());
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
        public void FastFallWaitsForApexWithoutCuttingVerticalMomentum()
        {
            Set("_jumpStartedTime", Time.time);
            Set("_moveInput", Vector2.down);
            _body.linearVelocity = Vector2.up * 10f;
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.EqualTo(10f));
            Set("_jumpStartedTime", Time.time - 0.2f);
            Call("UpdateFastFallState");
            Assert.That(_body.linearVelocity.y, Is.EqualTo(10f));
            Assert.That(Get<bool>("_isFastFalling"), Is.False);

            _body.linearVelocity = Vector2.down;
            Call("UpdateFastFallState");

            Assert.That(Get<bool>("_isFastFalling"), Is.True);
            Assert.That(_body.linearVelocity.y, Is.EqualTo(-1f),
                "Fast fall should strengthen gravity without snapping the trajectory.");
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
        public void HeldFastFallDoesNotBecomeDropThroughIntentAtLanding()
        {
            Set("_wasDropThroughInputHeld", true);
            Set("_lastDropThroughDownPressedTime", Time.time - 1f);
            Set("_lastDropThroughJumpPressedTime", Time.time);

            Call("UpdateDropThroughInputIntent", -1f);

            Assert.That((bool)Call("HasFreshDropThroughIntent"), Is.False,
                "Continuing to hold fast fall must not turn a buffered landing jump into drop-through.");
        }

        [Test]
        public void RecentDownThenJumpCreatesForgivingDropThroughIntent()
        {
            Set("_wasDropThroughInputHeld", true);
            Set("_lastDropThroughDownPressedTime", Time.time - 1f);
            Set("_lastDropThroughJumpPressedTime", Time.time);

            Call("UpdateDropThroughInputIntent", 0f);
            Call("UpdateDropThroughInputIntent", -1f);

            Assert.That((bool)Call("HasFreshDropThroughIntent"), Is.True,
                "A fresh Down press should combine with a recent Jump press near landing.");
        }

        [Test]
        public void RecentJumpThenDownCreatesForgivingDropThroughIntent()
        {
            Set("_lastDropThroughJumpPressedTime", Time.time);
            Set("_wasDropThroughInputHeld", false);

            Call("UpdateDropThroughInputIntent", -1f);

            Assert.That((bool)Call("HasFreshDropThroughIntent"), Is.True,
                "The same short input window should work regardless of press order.");
        }

        [Test]
        public void OldJumpDoesNotCombineWithFreshDownPress()
        {
            Set("_dropThroughIntentBufferDuration", 0.3f);
            Set("_lastDropThroughJumpPressedTime", Time.time - 0.31f);
            Set("_wasDropThroughInputHeld", false);

            Call("UpdateDropThroughInputIntent", -1f);

            Assert.That((bool)Call("HasFreshDropThroughIntent"), Is.False);
        }

        [Test]
        public void PerformedJumpCannotBeReusedForLaterDropThrough()
        {
            Set("_lastDropThroughDownPressedTime", Time.time);
            Set("_lastDropThroughJumpPressedTime", Time.time);

            Call("ApplyJumpForce", 1f, null);

            Assert.That((bool)Call("HasFreshDropThroughIntent"), Is.False,
                "A completed normal or double jump must consume its drop-through intent.");
        }

        [Test]
        public void BufferedJumpAfterFastFallStartsLightBunnyHopBoost()
        {
            Set("_isGrounded", true);
            Set("_wasGroundedLastFixedUpdate", false);
            Set("_isFastFalling", true);
            Set("_moveInput", new Vector2(1f, -1f));
            Set("_horizontalInput", 1f);
            Set("_availableJumpCount", 0);
            Set("_hasQueuedJump", true);
            Set("_jumpQueuedUntilTime", Time.time + 0.12f);

            Call("RefreshJumpCountIfLanded");
            Call("ProcessQueuedJump");

            Assert.That(Get<bool>("_hasJumpMomentumBoost"), Is.True);
            Assert.That(_movement.IsGrounded, Is.False);
        }

        [Test]
        public void FastFallBunnyHopTemporarilyRaisesHorizontalTargetSpeed()
        {
            _player.GetComponent<PlayerStats>().GetStat(StatType.MoveSpeed).BaseValue = 5f;
            Set("_horizontalInput", 1f);
            _body.linearVelocity = Vector2.right * 5f;
            Call("StartFastFallBunnyHopBoost");

            Call("ApplyMovement");

            Assert.That(_body.linearVelocity.x, Is.EqualTo(5.6f).Within(0.01f));
        }

        [Test]
        public void FastFallPreservesHorizontalMomentumWithoutDirectionalInput()
        {
            Set("_isFastFalling", true);
            Set("_horizontalInput", 0f);
            _body.linearVelocity = new Vector2(5f, -2f);

            Call("ApplyMovement");

            Assert.That(_body.linearVelocity.x, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void RegularGroundJumpGetsSmallerForwardBoostThanBunnyHop()
        {
            Set("_isGrounded", true);
            Set("_groundJumpAvailable", true);
            Set("_horizontalInput", 1f);
            Set("_hasQueuedJump", true);
            Set("_jumpQueuedUntilTime", Time.time + 0.12f);

            Call("ProcessQueuedJump");

            Assert.That(Get<bool>("_hasJumpMomentumBoost"), Is.True);
            Assert.That(Get<float>("_jumpMomentumSpeedMultiplier"),
                Is.EqualTo(Get<float>("_normalJumpSpeedMultiplier")));
            Assert.That(Get<float>("_jumpMomentumSpeedMultiplier"),
                Is.LessThan(Get<float>("_fastFallBunnyHopSpeedMultiplier")));
        }

        [Test]
        public void DropThroughRequiresConfirmedGroundedState()
        {
            Set("_groundCheckPoint", _player.transform);
            Set("_mainCollider", _player.GetComponent<BoxCollider2D>());
            Set("_oneWayPlatformLayer", (LayerMask)(1 << 7));
            Set("_lastDropThroughDownPressedTime", Time.time);
            Set("_lastDropThroughJumpPressedTime", Time.time);
            Set("_isGrounded", false);

            Assert.That((bool)Call("TryStartDropThrough"), Is.False);
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
        public void PresentationSpriteFeetAlignWithBoxBottom()
        {
            var box = _player.AddComponent<BoxCollider2D>();
            box.offset = new Vector2(0f, -0.04f);
            box.size = new Vector2(0.51f, 0.92f);

            var texture = new Texture2D(28, 28);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 28f, 28f), new Vector2(0.5f, 0.5f), 24f);
            try
            {
                var renderer = _player.GetComponent<SpriteRenderer>();
                if (renderer == null)
                    renderer = _player.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                var visual = _player.GetComponent<PlayerMovementVisual>();
                if (visual == null)
                    visual = _player.AddComponent<PlayerMovementVisual>();
                if (visual.DisplayRenderer == null)
                {
                    typeof(PlayerMovementVisual)
                        .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(visual, null);
                }
                typeof(PlayerMovementVisual)
                    .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(visual, null);

                Assert.That(visual.DisplayRenderer.bounds.min.y,
                    Is.EqualTo(box.bounds.min.y).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
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

        [Test]
        public void TouchingPlatformWithHeadDoesNotRefreshJumps()
        {
            var ceiling = new GameObject("Ceiling", typeof(BoxCollider2D));
            try
            {
                ceiling.layer = 6;
                ceiling.transform.position = Vector3.up;
                Set("_groundLayer", (LayerMask)(1 << 6));
                Set("_availableJumpCount", 0);
                Set("_groundJumpAvailable", false);
                Set("_wasGroundedLastFixedUpdate", false);
                _body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();

                Call("CheckGround");
                Call("RefreshJumpCountIfLanded");

                Assert.That(_movement.IsGrounded, Is.False);
                Assert.That(Get<int>("_availableJumpCount"), Is.Zero,
                    "A ceiling contact must not restore air jumps.");
            }
            finally { Object.DestroyImmediate(ceiling); }
        }

        [Test]
        public void PlatformDirectlyUnderFeetRefreshesJumps()
        {
            var floor = new GameObject("Floor", typeof(BoxCollider2D));
            try
            {
                floor.layer = 6;
                floor.transform.position = Vector3.down;
                Set("_groundLayer", (LayerMask)(1 << 6));
                Set("_availableJumpCount", 0);
                Set("_groundJumpAvailable", false);
                Set("_wasGroundedLastFixedUpdate", false);
                _body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();

                Call("CheckGround");
                Call("RefreshJumpCountIfLanded");

                Assert.That(_movement.IsGrounded, Is.True);
                Assert.That(Get<int>("_availableJumpCount"), Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(floor); }
        }

        [Test]
        public void AirReleaseStopsWithinThreeTenthsOfAUnit()
        {
            _body.linearVelocity = Vector2.right * 5f;
            float drift = 0f;
            for (int i = 0; i < 8; i++)
            {
                Call("ApplyMovement");
                drift += _body.linearVelocity.x * Time.fixedDeltaTime;
            }
            Assert.That(_body.linearVelocity.x, Is.Zero);
            Assert.That(drift, Is.LessThan(0.3f));
        }

        [Test]
        public void DashJumpCapsPeakDashVelocity()
        {
            _body.linearVelocity = Vector2.right * 16f;
            Assert.That(_movement.TryPerformDashJump(1f), Is.True);
            Assert.That(_body.linearVelocity.x, Is.EqualTo(8.5f).Within(0.01f));
        }

        [Test]
        public void CarryDoesNotWeakenAirControlAfterCollision()
        {
            _body.linearVelocity = Vector2.zero;
            _movement.ApplyHorizontalMomentumCarry(8.5f, 0.1f);
            Set("_horizontalInput", -1f);
            Call("ApplyMovement");
            Assert.That(_body.linearVelocity.x, Is.LessThanOrEqualTo(-1.5f));
        }

        [Test]
        public void NormalJumpHasCompactControllableArc()
        {
            SimulationMode2D originalMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                _body.position = Vector2.zero;
                _body.linearVelocity = Vector2.up * 13f;
                float peak = 0f;
                float flight = 0f;
                do
                {
                    Call("UpdateFastFallState");
                    Physics2D.Simulate(Time.fixedDeltaTime);
                    peak = Mathf.Max(peak, _body.position.y);
                    flight += Time.fixedDeltaTime;
                } while (_body.position.y > 0f && flight < 2f);
                Assert.That(peak, Is.InRange(1.8f, 2.2f));
                Assert.That(flight, Is.InRange(0.55f, 0.68f));
            }
            finally { Physics2D.simulationMode = originalMode; }
        }

        [Test]
        public void DropThroughKeepsCollisionIgnoredUntilPlayerClearsOneTile()
        {
            var bodyCollider = _player.AddComponent<BoxCollider2D>();
            bodyCollider.size = new Vector2(0.6f, 1.2f);
            bodyCollider.offset = new Vector2(0f, 0.6f);
            Set("_mainCollider", bodyCollider);
            _player.transform.position = Vector3.zero;
            Physics2D.SyncTransforms();

            var surfaces = Get<System.Collections.Generic.Dictionary<Collider2D, float>>("_ignoredPlatformSurfaceY");
            var dummyPlatform = _player.AddComponent<CircleCollider2D>();
            dummyPlatform.radius = 0.01f;
            dummyPlatform.isTrigger = true;
            surfaces[dummyPlatform] = 0f;

            var method = typeof(PlayerMovement).GetMethod(
                "IsBelowDroppedPlatformSurface",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            Assert.That((bool)method.Invoke(_movement, new object[] { dummyPlatform }), Is.False,
                "Head still overlapping the dropped tile must not restore collision.");

            _player.transform.position = new Vector3(0f, -1.25f, 0f);
            Physics2D.SyncTransforms();
            Assert.That((bool)method.Invoke(_movement, new object[] { dummyPlatform }), Is.False,
                "Barely below the top surface is still inside a 1-tile platform.");

            _player.transform.position = new Vector3(0f, -2.4f, 0f);
            Physics2D.SyncTransforms();
            Assert.That((bool)method.Invoke(_movement, new object[] { dummyPlatform }), Is.True,
                "After leaving one full tile the dropped platform can collide again.");
        }

        private void Set(string name, object value) => typeof(PlayerMovement)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_movement, value);
        private T Get<T>(string name) => (T)typeof(PlayerMovement)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_movement);
        private object Call(string name, params object[] arguments) => typeof(PlayerMovement)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_movement, arguments);
    }
}
