using Scripts.GameplayEvents;
using UnityEngine;

namespace Scripts.Visuals
{
    /// <summary>Presentation only: positive direct health damage, bounded retriggers and no hitstun.</summary>
    public sealed class PlayerHurtPulse
    {
        private readonly HitFlashPulse _pulse = new HitFlashPulse();
        private float _direction;
        public bool Trigger(GameplayEventContext context, GameObject player, CombatPoseProfile profile, float now)
        {
            if (context == null || context.Target != player || context.Type != GameplayEventType.DamageTaken ||
                context.Damage == null || !context.Damage.IsDirectHit || context.Amount <= 0 ||
                float.IsNaN(context.Amount) || float.IsInfinity(context.Amount) || profile == null || !profile.EnablePlayerHurt) return false;
            if (!_pulse.Trigger(now, profile.PlayerHurtFlash, profile.PlayerHurtDuration, profile.PlayerHurtInterval)) return false;
            float origin = context.Damage.Impact != null ? context.Damage.Impact.Origin.x : context.Source != null ? context.Source.transform.position.x : player.transform.position.x;
            _direction = Mathf.Sign(player.transform.position.x - origin);
            return true;
        }
        public float Sample(float now, out float direction)
        {
            direction = _direction;
            return _pulse.Sample(now, out float strength) ? strength : 0;
        }
        public void Clear() { _pulse.Clear(); _direction = 0; }
    }
}
