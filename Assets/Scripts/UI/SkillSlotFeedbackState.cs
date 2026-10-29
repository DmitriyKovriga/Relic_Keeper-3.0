using Scripts.Skills;

namespace Scripts.UI
{
    public enum SkillSlotCue { None, Ready, Cooldown, NoMana, Busy, Blocked }

    /// <summary>Per-runtime UI state. Only a committed long-cooldown cast arms readiness.</summary>
    public sealed class SkillSlotFeedbackState
    {
        public const float MinimumReadyCooldown = 3f;
        public const float RejectionInterval = .65f;
        private bool _armed, _observedRecovery, _lastWasRejection;
        private SkillCastResult _lastRejection;
        private float _lastRejectionTime = float.NegativeInfinity;

        public void Reset()
        {
            _armed = _observedRecovery = _lastWasRejection = false;
            _lastRejectionTime = float.NegativeInfinity;
        }

        public SkillSlotCue RecordAttempt(SkillCastResult result, float cooldownDuration, float unscaledTime)
        {
            if (result == SkillCastResult.Accepted)
            {
                _armed = cooldownDuration >= MinimumReadyCooldown;
                _observedRecovery = _lastWasRejection = false;
                return SkillSlotCue.None;
            }
            if (result == SkillCastResult.Paused || result == SkillCastResult.Unavailable) return SkillSlotCue.None;
            if (_lastWasRejection && result == _lastRejection && unscaledTime - _lastRejectionTime < RejectionInterval) return SkillSlotCue.None;
            _lastWasRejection = true; _lastRejection = result; _lastRejectionTime = unscaledTime;
            switch (result)
            {
                case SkillCastResult.Cooldown: return SkillSlotCue.Cooldown;
                case SkillCastResult.InsufficientMana: return SkillSlotCue.NoMana;
                case SkillCastResult.Busy: return SkillSlotCue.Busy;
                default: return SkillSlotCue.Blocked;
            }
        }

        public bool ObserveCooldown(float remaining, bool canAfford, bool casting, bool paused)
        {
            if (!_armed) return false;
            if (remaining > .01f) _observedRecovery = true;
            if (remaining > .01f || !_observedRecovery || !canAfford || casting || paused) return false;
            _armed = false;
            return true;
        }

        public static string Caption(SkillSlotCue cue, string binding)
        {
            string text;
            switch (cue)
            {
                case SkillSlotCue.Ready: text = RuntimeLocalization.Resolve("skill.feedback.ready", "Ready", "Готово"); break;
                case SkillSlotCue.Cooldown: text = RuntimeLocalization.Resolve("skill.feedback.cooldown", "Cooldown", "Перезарядка"); break;
                case SkillSlotCue.NoMana: text = RuntimeLocalization.Resolve("skill.feedback.mana", "Not enough mana", "Недостаточно маны"); break;
                case SkillSlotCue.Busy: text = RuntimeLocalization.Resolve("skill.feedback.busy", "Already casting", "Навык выполняется"); break;
                case SkillSlotCue.Blocked: text = RuntimeLocalization.Resolve("skill.feedback.blocked", "Cannot cast now", "Сейчас недоступно"); break;
                default: return "";
            }
            return string.IsNullOrWhiteSpace(binding) ? text : binding + ": " + text;
        }
    }
}
