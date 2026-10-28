#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _landingReview;
        private static readonly string[] LandingCases = { "warrior-low", "warrior-high", "rogue-low", "rogue-high", "mage-low", "mage-high", "warrior-run", "rogue-run", "mage-run", "buffered", "tiny", "warrior-moving", "rogue-moving", "mage-moving" };
        private PlayerMovement _landingMovement;
        private PlayerCharacterAnimationController _landingAnimation;
        private readonly HashSet<int> _landingEffects = new HashSet<int>();
        private bool _queuedReviewJump, _reviewJumped;
        private int _reviewContacts;
        private string LandingHero => _case.name.StartsWith("rogue") ? "Rogue" : _case.name.StartsWith("mage") ? "Mage" : "Warrior";
        public sealed partial class FrameRecord { public float landingSpeed, landingScale; public int footContact, newSurfaceEffects, landingContacts; public bool reviewJumped; }
        public static void BeginLandingReview() { _landingReview = true; BeginCapture(true, true, hurtReview: true); }

        private void ConfigureLandingCase()
        {
            _frameLimit = _case.name.EndsWith("run") ? 220 : 125;
            _landingEffects.Clear(); _queuedReviewJump = _reviewJumped = false; _reviewContacts = 0;
            _enemy.gameObject.SetActive(false);
            _landingMovement = _owner.GetComponent<PlayerMovement>();
            _landingAnimation = _owner.GetComponent<PlayerCharacterAnimationController>();
            typeof(PlayerCharacterAnimationController).GetField("_combatPoseProfile", Private).SetValue(_landingAnimation, _comparisonProfile);
            _landingMovement.OnLanded += () => _reviewContacts++;
            _landingMovement.OnJumpStarted += () => _reviewJumped = true;
            var floor = _fixture.transform.Find("Fixture floor");
            floor.GetComponent<BoxCollider2D>().size = new Vector2(35, 1);
            floor.GetComponentInChildren<SpriteRenderer>().transform.localScale = new Vector3(35, 1, 1);
            floor.gameObject.AddComponent<GroundSurfaceVisuals>().Configure(UnityEditor.AssetDatabase.LoadAssetAtPath<GroundSurfaceProfileSO>(
                _case.name == "rogue-run" ? "Assets/Visuals/SurfaceEffects/Earth.asset" : "Assets/Visuals/SurfaceEffects/Stone.asset"));
            // Exercise the default density rather than the user's saved Hub sliders.
            var appearance = new GameObject("Fixture surface defaults"); appearance.transform.SetParent(_fixture.transform);
            var grading = appearance.AddComponent<Scripts.Visuals.Atmosphere.AtmosphereController>(); grading.enabled = false;
            typeof(GroundingVisualController).GetField("_atmosphere", Private).SetValue(_owner.GetComponent<GroundingVisualController>(), grading);
            var rb = _owner.GetComponent<Rigidbody2D>(); rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.gravityScale = 2.5f;
            float height = _case.name.EndsWith("high") ? 4 : _case.name == "tiny" ? .04f : _case.name.EndsWith("run") ? 0 : .4f;
            rb.position += Vector2.up * height; rb.linearVelocity = Vector2.zero;
            if (_case.name.EndsWith("moving")) _landingMovement.BeginMotionOverride(new Vector2(5, 0), false, true);
            Physics2D.SyncTransforms();
        }

        private void UpdateLandingCase()
        {
            if (_case.name.EndsWith("run") && _frame == 20)
                _landingMovement.BeginMotionOverride(new Vector2(5, 0), false, true);
            if (_case.name.EndsWith("run") && _frame == 180)
                _landingMovement.EndMotionOverride(Vector2.zero);
            if (_case.name == "buffered" && !_queuedReviewJump && _landingMovement.CurrentVelocity.y < -1 && _owner.transform.position.y < 2000.22f)
            {
                _queuedReviewJump = true;
                typeof(PlayerMovement).GetField("_availableJumpCount", Private).SetValue(_landingMovement, 0);
                typeof(PlayerMovement).GetField("_hasQueuedJump", Private).SetValue(_landingMovement, true);
                typeof(PlayerMovement).GetField("_jumpQueuedUntilTime", Private).SetValue(_landingMovement, Time.time + .12f);
            }
            _camera.transform.position = new Vector3(_owner.transform.position.x + .8f, 2000.7f, -10);
        }

        private void RecordLandingFrame(FrameRecord frame)
        {
            frame.landingSpeed = _landingMovement.LastLandingSpeed;
            frame.landingScale = _landingAnimation.PresentationScale.y;
            frame.footContact = _landingAnimation.RunContactVersion;
            frame.landingContacts = _reviewContacts; frame.reviewJumped = _reviewJumped;
            foreach (var effect in Object.FindObjectsByType<GroundingSpriteSheetVfx>(FindObjectsSortMode.None))
                if (Mathf.Abs(effect.transform.position.x - 3000) < 30 && _landingEffects.Add(effect.GetInstanceID())) frame.newSurfaceEffects++;
        }

        private void ValidateLandingCase()
        {
            if (_case.name.EndsWith("run"))
            {
                if (_case.frames.Sum(f => f.newSurfaceEffects) < 2) { _case.error = "Too few run contacts exercised."; return; }
                for (int i = 1; i < _case.frames.Count; i++)
                    if (_case.frames[i].newSurfaceEffects > 0 && _case.frames[i].footContact == _case.frames[i - 1].footContact)
                        _case.error = "Run dust occurred between planted frames.";
            }
            else if (!_case.frames.Any(f => f.landingContacts > 0)) _case.error = "No physical landing observed.";
            else if (_case.name.EndsWith("moving"))
            {
                var landed = _case.frames.Where(f => f.grounded && f.landingContacts > 0).ToArray();
                if (landed.Any(f => !f.bodySprite.Contains("Run"))) _case.error = "Moving landing held a stationary pose.";
                else if (landed.Select(f => f.bodySprite).Distinct().Count() < 3) _case.error = "Run animation did not advance after landing.";
            }
            else if (_case.name == "buffered" && !_reviewJumped) _case.error = "Buffered jump did not interrupt the landing.";
            else if (_case.name == "tiny" && _case.frames.Any(f => f.landingScale < .999f)) _case.error = "Tiny fall caused a full landing pose.";
            else if (_case.name != "tiny" && _case.name != "buffered" && !_case.frames.Any(f => f.landingScale < .995f)) _case.error = "Missing scaled landing pose.";
        }
    }
}
#endif
