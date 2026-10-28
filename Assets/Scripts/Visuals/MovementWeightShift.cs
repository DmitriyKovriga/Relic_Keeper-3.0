using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Velocity-following presentation only. World-space lean survives a facing flip without reversing itself.</summary>
    public sealed class MovementWeightShift
    {
        private bool _initialized;
        private float _filteredVelocity, _lean, _compression, _turn;
        private int _intent;
        public AttackBodyPose Pose(float facing) => new AttackBodyPose(_lean * (facing < 0 ? -1 : 1), 1 - _compression);

        public AttackBodyPose Sample(float velocity, float input, float speed, float facing, float deltaTime, bool allowed, CombatPoseProfile profile)
        {
            if (!allowed || profile == null || !profile.EnableMovementWeight || !Finite(velocity) || !Finite(input) || !Finite(speed) || !Finite(deltaTime) || deltaTime < 0)
            { Clear(); return AttackBodyPose.Neutral; }
            int intent = input > .1f ? 1 : input < -.1f ? -1 : 0;
            if (!_initialized) { _initialized = true; _filteredVelocity = velocity; _intent = intent; return AttackBodyPose.Neutral; }
            if (deltaTime == 0) return Pose(facing);
            float dt = Mathf.Min(deltaTime, .1f);
            if (intent != 0 && _intent != 0 && intent != _intent && Mathf.Abs(velocity) > .2f) _turn = 1;
            _intent = intent;
            float lag = Mathf.Clamp((velocity - _filteredVelocity) / Mathf.Max(1, Mathf.Abs(speed) * .3f), -1, 1);
            _filteredVelocity = Mathf.Lerp(_filteredVelocity, velocity, 1 - Mathf.Exp(-18 * dt));
            float response = 1 - Mathf.Exp(-24 * dt);
            _lean = Mathf.Lerp(_lean, -lag * Mathf.Clamp(profile.MovementLeanDegrees, 0, 4), response);
            float squash = Mathf.Max(Mathf.Abs(lag) * Mathf.Clamp(profile.MovementCompression, 0, .025f),
                _turn * Mathf.Clamp(profile.TurnCompression, 0, .03f));
            _compression = Mathf.Lerp(_compression, squash, response);
            _turn = Mathf.Max(0, _turn - dt / .12f);
            if (Mathf.Abs(_lean) < .001f) _lean = 0;
            if (_compression < .00001f) _compression = 0;
            return Pose(facing);
        }
        public void Clear() { _initialized = false; _filteredVelocity = _lean = _compression = _turn = 0; _intent = 0; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
