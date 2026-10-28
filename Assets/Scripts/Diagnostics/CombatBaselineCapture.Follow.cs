#if UNITY_EDITOR
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

namespace RelicKeeper.Editor.Combat
{
    public sealed partial class CombatBaselineCapture
    {
        private static bool _followReview;
        private static readonly string[] FollowCases = { "straight-off", "straight-on", "turn-off", "turn-on", "jump-off", "jump-on", "edge-off", "edge-on" };
        public static void BeginFollowReview() { _followReview = true; BeginCapture(true, true, cameraReview: true); }
        public static void ApplyFollowCandidate(CinemachinePositionComposer camera, float lead = .12f, float damping = .05f)
        {
            camera.Lookahead = new LookaheadSettings { Enabled = true, IgnoreY = true, Time = Mathf.Clamp(lead, 0, .2f), Smoothing = 3 };
            camera.Damping = new Vector3(Mathf.Clamp(damping, 0, .1f), 0, 0);
        }
        private void ConfigureFollowCase(CinemachinePositionComposer composer)
        {
            _frameLimit = 180; _enemy.gameObject.SetActive(false);
            if (_case.name.EndsWith("on")) ApplyFollowCandidate(composer);
            _reviewBounds.size = new Vector2(_case.name.StartsWith("edge") ? 24 : 42, 24);
            _reviewVcam.GetComponent<CinemachineConfiner2D>().InvalidateBoundingShapeCache();
        }
        private void UpdateFollowCase()
        {
            float t = Mathf.Max(0, _frame - 30) / 60f;
            float x = Mathf.Min(t, 1.5f) * 5;
            if (_case.name.StartsWith("turn")) x = t < .8f ? t * 5 : t < 1.6f ? 8 - t * 5 : 0;
            if (_case.name.StartsWith("edge")) x = t < 1.2f ? t * 7 : Mathf.Max(0, 16.8f - t * 7);
            float y = _case.name.StartsWith("jump") ? Mathf.Max(0, 3 * Mathf.Sin(Mathf.Clamp01(t / 1.2f) * Mathf.PI)) : 0;
            _owner.transform.position = new Vector3(3000 + x, 2000 + y, 0);
            _owner.GetComponent<Rigidbody2D>().position = _owner.transform.position;
            Physics2D.SyncTransforms();
        }
        private void ValidateFollowCase()
        {
            if (_case.frames.Any(f => !f.cameraInsideBounds)) _case.error = "Follow camera viewport escaped the confiner.";
            else if (_case.frames.Any(f => float.IsNaN(f.cameraPosition.x) || float.IsNaN(f.cameraPosition.y))) _case.error = "Non-finite follow position.";
            else if (_case.frames.Select(f => f.cameraPosition.x).Max() - _case.frames.Select(f => f.cameraPosition.x).Min() < .5f)
                _case.error = "Camera did not follow the supplied travel.";
        }
    }
}
#endif
