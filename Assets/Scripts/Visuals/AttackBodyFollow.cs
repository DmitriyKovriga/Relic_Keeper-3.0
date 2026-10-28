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
        private RangedBodyStyle _rangedStyle;
        private float _rangedWindupStart, _rangedRelease, _rangedRecoveryEnd;
        public object Owner => _owner;
        public AttackBodyPose Pose { get; private set; } = AttackBodyPose.Neutral;

        public bool TryBegin(object owner, SkillDataSO skill, CombatPoseProfile profile, float duration = 1f)
        {
            if (skill != null && skill.BodyFollowStyle != RangedBodyStyle.None)
                return TryBeginRanged(owner, skill, profile, duration);
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
            _rangedStyle = RangedBodyStyle.None;
            _weight = multiplier >= 2f ? Mathf.Clamp(profile.HeavyBodyMultiplier, 1f, 1.8f) : 1f;
            // Missing/zero-length recovery steps still use the recipe clock. No extra timer or weapon phase.
            _pipelineRecovery = !landing && !recovery;
            _recoveryStart = Mathf.Clamp01(Mathf.Max(contactEnd, recoveryStart));
            _hasStrike = _recovering = false;
            Pose = AttackBodyPose.Neutral;
            return true;
        }

        private bool TryBeginRanged(object owner, SkillDataSO skill, CombatPoseProfile profile, float duration)
        {
            if (skill.BodyFollowStyle < RangedBodyStyle.Bow || skill.BodyFollowStyle > RangedBodyStyle.SwordWave) return false;
            if (owner == null || profile == null || !profile.EnableBodyFollow || !profile.EnableRangedBodyFollow ||
                skill.WeaponRole == SkillWeaponRole.Equipment || skill.Recipe == null || skill.Recipe.IsChanneling || skill.Recipe.Steps == null ||
                float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0) return false;
            bool action = false;
            float release = 1f, preparation = 1f, recoveryStart = -1f, recoveryEnd = -1f;
            foreach (var step in skill.Recipe.Steps)
            {
                string id = step?.StepDefinition?.Id;
                if (step == null || step.IsParallelGroup) return false;
                switch (id)
                {
                    case "WeaponWindup": preparation = Mathf.Min(preparation, step.StartPercentPipeline); break;
                    case "WeaponStrike": release = Mathf.Min(release, step.StartPercentPipeline); break;
                    case "SpawnProjectile": case "InstantBeam": case "PrismaticCycle": case "ArrowRain":
                    case "SpawnOrbitProjectiles": case "ChainDamage": case "DealDamageCircle": case "DealDamageRectangle":
                    case "WinterStorm": case "FrostWall": case "LightningRod":
                        action = true; release = Mathf.Min(release, step.StartPercentPipeline); break;
                    case "WeaponRecovery":
                        if (step.EndPercentPipeline > step.StartPercentPipeline)
                        { recoveryStart = Mathf.Max(recoveryStart, step.StartPercentPipeline); recoveryEnd = Mathf.Max(recoveryEnd, step.EndPercentPipeline); }
                        break;
                    case "MovementLock": case "Wait": case "PlayerImpulse": case "SpawnVFX":
                    case "BuildChainTargets": case "SpawnChainVFX": break;
                    default: return false;
                }
            }
            if (!action || release >= 1f || release < 0f) return false;
            _rangedRelease = release;
            _rangedWindupStart = preparation < release ? preparation : 0f;
            // One cast envelope, even when a recipe has several shots/recoveries. Persistent effects
            // keep their own lifetime; they never own the caster's body after the recipe finishes.
            _recoveryStart = recoveryStart >= release ? recoveryStart : release;
            _rangedRecoveryEnd = recoveryEnd > _recoveryStart ? recoveryEnd : 1f;
            _owner = owner; _profile = profile; _rangedStyle = skill.BodyFollowStyle;
            _weight = Mathf.Clamp01(profile.RangedBodyStrength) * Mathf.Clamp(duration / .5f, .35f, 1f);
            _pipelineRecovery = _hasStrike = _recovering = false; Pose = AttackBodyPose.Neutral;
            return true;
        }

        private AttackBodyPose RangedPose(bool release)
        {
            float degrees = _rangedStyle == RangedBodyStyle.Bow ? (release ? -1.5f : 1.5f)
                : _rangedStyle == RangedBodyStyle.Wand ? (release ? 1.5f : -.25f)
                : _rangedStyle == RangedBodyStyle.Staff ? (release ? 2.5f : 1f)
                : _rangedStyle == RangedBodyStyle.SwordWave ? (release ? -3f : 3f)
                : (release ? -2f : 1f);
            float compression = _rangedStyle == RangedBodyStyle.Staff ? .01f : 0;
            return new AttackBodyPose(degrees * _weight, 1 - compression * _weight);
        }

        public void Windup(object owner, float phase)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase));
            if (_rangedStyle != RangedBodyStyle.None)
            {
                var pose = RangedPose(false);
                Pose = new AttackBodyPose(pose.Degrees * t, Mathf.Lerp(1, pose.Height, t)); return;
            }
            Pose = new AttackBodyPose(Mathf.Clamp(_profile.WindupDegrees * _weight, 0, 6) * t,
                Mathf.Lerp(1, Mathf.Clamp(1 - (1 - _profile.WindupHeight) * _weight, .94f, 1), t));
        }

        public void Strike(object owner)
        {
            if (!ReferenceEquals(owner, _owner) || _profile == null) return;
            if (_rangedStyle != RangedBodyStyle.None && _hasStrike) return;
            _hasStrike = true; _recovering = false;
            if (_rangedStyle != RangedBodyStyle.None) { Pose = RangedPose(true); return; }
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
            if (ReferenceEquals(owner, _owner) && _profile != null && _rangedStyle != RangedBodyStyle.None)
            {
                if (phase < _rangedWindupStart) { Pose = AttackBodyPose.Neutral; return; }
                if (phase < _rangedRelease)
                {
                    var windup = RangedPose(false);
                    float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(_rangedWindupStart, _rangedRelease, phase));
                    Pose = new AttackBodyPose(windup.Degrees * t, Mathf.Lerp(1, windup.Height, t));
                    return;
                }
                var strike = RangedPose(true);
                float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(_recoveryStart, _rangedRecoveryEnd, phase));
                Pose = new AttackBodyPose(Mathf.Lerp(strike.Degrees, 0, settle), Mathf.Lerp(strike.Height, 1, settle));
                return;
            }
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
        public void Clear() { _owner = null; _profile = null; _rangedStyle = RangedBodyStyle.None; Pose = AttackBodyPose.Neutral; }
    }
}
