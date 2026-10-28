#if UNITY_EDITOR
using System.Linq;
using Scripts.Skills;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _inputReview;
        private static readonly string[] InputCases = { "ledge-early", "ledge-late", "buffered", "expired", "drop", "solid-down", "hitstop", "menu-input", "menu-pending" };
        private PlayerMovement _inputMovement;
        private Rigidbody2D _inputBody;
        private BoxCollider2D _inputFloor;
        private GamePauseService.PauseHandle _inputPause;
        private bool _inputSent, _inputSawFreeze;
        private float _inputLeftAt;
        private int _inputJumpCount, _inputRemaining;
        public sealed partial class FrameRecord { public int inputJumps, inputRemaining; public bool inputBuffered, inputIgnoredFloor, inputHitStop; public float inputVelocityY; }
        public static void BeginInputReview() { _inputReview = true; BeginCapture(true, true, hurtReview: true); }
        private void ConfigureInputCase()
        {
            _frameLimit = 100; _inputSent = _inputSawFreeze = false; _inputJumpCount = 0; _inputRemaining = -1; _inputLeftAt = -1;
            _enemy.gameObject.SetActive(false);
            _inputMovement = _owner.GetComponent<PlayerMovement>(); _inputBody = _owner.GetComponent<Rigidbody2D>();
            _inputMovement.OnJumpStarted += () => _inputJumpCount++;
            _inputBody.constraints = RigidbodyConstraints2D.FreezeRotation; _inputBody.gravityScale = 2.5f;
            _inputFloor = _fixture.transform.Find("Fixture floor").GetComponent<BoxCollider2D>();
            if (_case.name.StartsWith("ledge"))
            {
                _inputFloor.size = new Vector2(2, 1); _inputFloor.GetComponentInChildren<SpriteRenderer>().transform.localScale = new Vector3(2, 1, 1);
            }
            if (_case.name == "drop") _inputFloor.gameObject.layer = LayerMask.NameToLayer("OneWayPlatform");
            if (_case.name == "buffered" || _case.name == "expired")
            {
                _inputBody.position += Vector2.up * (_case.name == "expired" ? 2.5f : .6f);
                typeof(PlayerMovement).GetField("_availableJumpCount", Private).SetValue(_inputMovement, 0);
            }
            Physics2D.SyncTransforms();
        }
        private void PressReviewJump(bool down = false)
        {
            typeof(PlayerMovement).GetMethod("OnJumpPerformed", Private).Invoke(_inputMovement, new object[] { default(InputAction.CallbackContext) });
            if (down) typeof(PlayerMovement).GetMethod("UpdateDropThroughInputIntent", Private).Invoke(_inputMovement, new object[] { -1f });
            _inputSent = true;
        }
        private void UpdateInputCase()
        {
            if (_case.name.StartsWith("ledge"))
            {
                if (_frame >= 20 && _inputJumpCount == 0) _inputBody.linearVelocity = new Vector2(5, _inputBody.linearVelocity.y);
                if (_frame > 20 && !_inputMovement.IsGrounded && _inputLeftAt < 0) _inputLeftAt = Time.time;
                if (!_inputSent && _inputLeftAt >= 0 && Time.time - _inputLeftAt >= (_case.name.EndsWith("early") ? .04f : .14f)) PressReviewJump();
            }
            else if (_case.name == "buffered")
            {
                if (!_inputSent && _owner.transform.position.y < 2000.28f && _inputBody.linearVelocity.y < -1) PressReviewJump();
            }
            else if (_case.name == "expired") { if (_frame == 1) PressReviewJump(); }
            else if (_case.name == "hitstop")
            {
                if (_frame == 20) HitStopService.Request(5);
                if (_frame == 21) { _inputSawFreeze = HitStopService.IsActive; PressReviewJump(); }
            }
            else if (_case.name.StartsWith("menu"))
            {
                if (_frame == 20)
                {
                    if (_case.name == "menu-pending") PressReviewJump();
                    _inputPause = GamePauseService.Acquire(GamePauseReason.PauseMenu);
                }
                if (_frame == 21 && _case.name == "menu-input") PressReviewJump();
                if (_frame == 35) { _inputPause?.Dispose(); _inputPause = null; }
            }
            else if (_frame == 20) PressReviewJump(true);
        }
        private void RecordInputFrame(FrameRecord frame)
        {
            if (_inputJumpCount > 0 && _inputRemaining < 0) _inputRemaining = (int)typeof(PlayerMovement).GetField("_availableJumpCount", Private).GetValue(_inputMovement);
            frame.inputJumps = _inputJumpCount; frame.inputRemaining = _inputRemaining;
            frame.inputBuffered = _inputMovement.HasBufferedJump;
            frame.inputIgnoredFloor = Physics2D.GetIgnoreCollision(_owner.GetComponent<BoxCollider2D>(), _inputFloor);
            frame.inputHitStop = HitStopService.IsActive; frame.inputVelocityY = _inputBody.linearVelocity.y;
        }
        private void ValidateInputCase()
        {
            bool shouldJump = _case.name != "drop" && _case.name != "expired" && !_case.name.StartsWith("menu");
            if (!_inputSent) _case.error = "Input scenario did not run.";
            else if (_inputJumpCount != (shouldJump ? 1 : 0)) _case.error = "Jump input was lost, repeated or fired after expiry/menu.";
            else if (_case.name == "ledge-early" && _inputRemaining != 1 || _case.name == "ledge-late" && _inputRemaining != 0) _case.error = "Ledge grace spent the wrong jump.";
            else if (_case.name == "hitstop" && !_inputSawFreeze) _case.error = "Input did not occur during a real hit-stop.";
            else if (_case.name == "drop" && (!_case.frames.Any(f => f.inputIgnoredFloor) || _case.frames.Last().inputIgnoredFloor)) _case.error = "Drop-through did not restore collision after clearing the platform.";
            else if (_case.name != "drop" && _case.frames.Any(f => f.inputIgnoredFloor)) _case.error = "Ignored solid-ground collision.";
        }
    }
}
#endif
