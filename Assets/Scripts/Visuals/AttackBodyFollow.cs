using Scripts.Skills;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Samples the hand recipe's actual phases. It never moves a hand, collider or owner root.</summary>
    public sealed class AttackBodyFollow
    {
        private object _owner;
        private CombatPoseProfile _profile;
        private float _weight;
        private bool _pipelineRecovery, _hasStrike, _recovering;
        private float _recoveryStart;
        private AttackBodyPose _recoveryFrom;
        public object Owner => _owner;
        public AttackBodyPose Pose { get; private set; } = AttackBodyPose.Neutral;

        public bool TryBegin(object owner, SkillDataSO skill, CombatPoseProfile profile)
        {
            if (owner == null || profile == null || !profile.EnableBodyFollow ||
                skill == null || skill.Recipe == null || skill.Recipe.Steps == null || skill.Recipe.IsChanneling ||
                skill.WeaponRole == SkillWeaponRole.Equipment || skill.ActionSpeedMode == SkillActionSpeedMode.Spell)
                return false;
            bool windup = false, strike = false, recovery = false, damage = false;
            bool landing = skill.Recipe.Steps.Count == 1 && skill.Recipe.Steps[0]?.StepDefinition?.Id == "FallingAnvil";
            float multiplier = 1f, contactEnd = 0f, recoveryStart = -1f;
            foreach (var step in skill.Recipe.Steps)
            {
                string id = step?.StepDefinition != null ? step.StepDefinition.Id : null;
                if (id == "SpinAround" || id == "FallingAnvil" && !landing || id == "SpawnProjectile" ||
                    id == "ArrowRain" || step != null && step.IsParallelGroup)
                    return false;
                windup |= id == "WeaponWindup";
                strike |= id == "WeaponStrike" || id == "BlinkStab";
                if (id == "WeaponStrike") contactEnd = Mathf.Max(contactEnd, step.StartPercentPipeline);
                if (id == "WeaponRecovery")
                {
                    recovery |= step.EndPercentPipeline > step.StartPercentPipeline;
                    recoveryStart = Mathf.Max(recoveryStart, step.StartPercentPipeline);
                }
                if (id == "DealDamageCircle" || id == "DealDamageRectangle" || id == "TectonicPress" || id == "NeedlePrick" ||
                    id == "CrimsonHook" || id == "RetreatingThrust" || id == "BlinkStab" || id == "ParryWindow" ||
                    id == "PersistentDamageCircle" || id == "PersistentDamageRectangle" || id == "SpawnGroundProjectile" || landing)
                {
                    damage = true;
                    multiplier = Mathf.Max(multiplier, step.GetFloat("DamageMultiplier", 1f));
                    contactEnd = Mathf.Max(contactEnd, step.EndPercentPipeline);
                }
            }
            if (!landing && (!windup || !strike || !damage)) return false;
            _owner = owner; _profile = profile;
            _weight = multiplier >= 2f ? Mathf.Clamp(profile.HeavyBodyMultiplier, 1f, 1.8f) : 1f;
            // Missing/zero-length recovery steps still use the recipe clock. No extra timer or weapon phase.
            _pipelineRecovery = !landing && !recovery;
            _recoveryStart = Mathf.Clamp01(Mathf.Max(contactEnd, recoveryStart));
            _hasStrike = _recovering = false;
            Pose = AttackBodyPose.Neutral;
            return true;
        }

        public void Windup(object owner, float phase)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase));
            Pose = new AttackBodyPose(Mathf.Clamp(_profile.WindupDegrees * _weight, 0, 6) * t,
                Mathf.Lerp(1, Mathf.Clamp(1 - (1 - _profile.WindupHeight) * _weight, .94f, 1), t));
        }

        public void Strike(object owner)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null) return;
            _hasStrike = true; _recovering = false;
            Pose = new AttackBodyPose(-Mathf.Clamp(_profile.StrikeDegrees * _weight, 0, 6),
                Mathf.Clamp(1 - (1 - _profile.StrikeHeight) * _weight, .94f, 1));
        }

        public void Recovery(object owner, float phase)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null || _pipelineRecovery) return;
            ApplyRecovery(phase);
        }

        public void SamplePipeline(object owner, float phase)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null || !_pipelineRecovery || !_hasStrike || phase < _recoveryStart) return;
            ApplyRecovery(_recoveryStart < 1f ? Mathf.InverseLerp(_recoveryStart, 1f, phase) : 1f);
        }

        private void ApplyRecovery(float phase)
        {
            // A failed blink never struck: ease its preparation away instead of inventing a forward snap.
            if (!_recovering) { _recoveryFrom = Pose; _recovering = true; }
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase));
            Pose = new AttackBodyPose(Mathf.Lerp(_recoveryFrom.Degrees, 0, t), Mathf.Lerp(_recoveryFrom.Height, 1, t));
        }

        public void Release(object owner) { if (ReferenceEquals(owner, _owner)) Clear(); }
        public void Clear() { _owner = null; _profile = null; Pose = AttackBodyPose.Neutral; }
    }
}
