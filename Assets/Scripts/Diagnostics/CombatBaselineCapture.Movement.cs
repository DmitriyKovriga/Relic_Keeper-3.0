#if UNITY_EDITOR
using System.Linq;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _movementReview;
        private static readonly string[] MovementCases = { "warrior-off", "warrior-on", "rogue-off", "rogue-on", "mage-off", "mage-on", "attack", "air", "locked" };
        private PlayerMovement _reviewMovement;
        private PlayerCharacterAnimationController _reviewAnimation;
        private float _reviewVelocity;
        public sealed partial class FrameRecord { public float movementLean, movementHeight, movementVelocity; }
        public static void BeginMovementReview() { _movementReview = true; BeginCapture(true, true, hurtReview: true); }
        private string MovementHero => _case.name.StartsWith("rogue") ? "Rogue" : _case.name.StartsWith("mage") ? "Mage" : "Warrior";
        private void ConfigureMovementCase()
        {
            _frameLimit = 180; _reviewVelocity = 0;
            _comparisonProfile.EnableMovementWeight = !_case.name.EndsWith("off");
            _reviewMovement = _owner.GetComponent<PlayerMovement>();
            _reviewAnimation = _owner.GetComponent<PlayerCharacterAnimationController>();
            _reviewAnimation.enabled = false; // Explicitly sample once, after supplying the controlled velocity trace.
            _enemy.gameObject.SetActive(false);
        }
        private void UpdateMovementCase()
        {
            float intent = _frame < 20 ? 0 : _frame < 65 ? 1 : _frame < 100 ? -1 : _frame < 120 ? 0 : _frame < 145 ? 1 : 0;
            _reviewVelocity = Mathf.MoveTowards(_reviewVelocity, intent * 5, (intent == 0 ? 60 : 90) / 60f);
            var rb = _owner.GetComponent<Rigidbody2D>(); rb.linearVelocity = new Vector2(_reviewVelocity, 0);
            typeof(PlayerMovement).GetField("_moveInput", Private).SetValue(_reviewMovement, new Vector2(intent, 0));
            typeof(PlayerMovement).GetField("_isGrounded", Private).SetValue(_reviewMovement, _case.name != "air");
            typeof(PlayerMovement).GetField("_isMovementLocked", Private).SetValue(_reviewMovement, _case.name == "locked");
            if (intent != 0) _owner.transform.localScale = new Vector3(intent, 1, 1);
            typeof(PlayerCharacterAnimationController).GetMethod("Update", Private).Invoke(_reviewAnimation, null);
            if (_case.name == "attack" && _frame == 25) { StartCaseCast(); _frameLimit = 180; }
        }
        private void RecordMovementFrame(FrameRecord frame)
        {
            var pose = _owner.GetComponent<PlayerMovementVisual>().VisibleMovementPose;
            frame.movementLean = pose.Degrees; frame.movementHeight = pose.Height; frame.movementVelocity = _reviewVelocity;
        }
        private void ValidateMovementCase()
        {
            bool expected = !_case.name.EndsWith("off") && _case.name != "air" && _case.name != "locked";
            if (_case.frames.Any(f => Mathf.Abs(f.movementLean) > .01f) != expected) _case.error = "Movement presentation enable/suppression mismatch.";
            else if (Mathf.Abs(_case.frames.Last().movementLean) > .01f) _case.error = "Movement weight did not settle.";
            else if (_case.name == "attack" && _case.frames.Any(f => f.casting && Mathf.Abs(f.movementLean) > .001f)) _case.error = "Movement weight competed with a cast.";
        }
    }
}
#endif
