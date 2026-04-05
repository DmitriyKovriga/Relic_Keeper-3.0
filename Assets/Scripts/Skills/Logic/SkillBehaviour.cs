using UnityEngine;
using Scripts.Stats;

namespace Scripts.Skills
{
    public abstract class SkillBehaviour : MonoBehaviour
    {
        protected PlayerStats _ownerStats;
        protected SkillDataSO _data;
        protected float _lastCastTime;
        protected bool _isCasting;

        public bool IsCasting => _isCasting;

        public float CooldownDuration => _data != null ? Mathf.Max(0f, _data.Cooldown) : 0f;

        public float CooldownRemaining
        {
            get
            {
                float duration = CooldownDuration;
                if (duration <= 0f)
                    return 0f;

                return Mathf.Max(0f, (_lastCastTime + duration) - Time.time);
            }
        }

        public float CooldownNormalized
        {
            get
            {
                float duration = CooldownDuration;
                if (duration <= 0f)
                    return 0f;

                return Mathf.Clamp01(CooldownRemaining / duration);
            }
        }

        public virtual void Cancel() { }

        public virtual void Initialize(PlayerStats stats, SkillDataSO data)
        {
            _ownerStats = stats;
            _data = data;
        }

        protected DamageContext ResolveDamageContext()
        {
            StatContextTagFlags tags = _data != null ? _data.DamageContextTags : StatContextTagFlags.None;
            if (tags == StatContextTagFlags.None)
                tags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;

            return new DamageContext(tags);
        }

        public void TryCast()
        {
            if (_isCasting)
                return;

            if (_data == null)
                return;

            if (Time.time < _lastCastTime + _data.Cooldown)
                return;

            if (_ownerStats == null || _ownerStats.Mana == null)
                return;

            if (_ownerStats.Mana.Current < _data.ManaCost)
                return;

            _ownerStats.Mana.Decrease(_data.ManaCost);
            _lastCastTime = Time.time;
            Execute();
        }

        protected abstract void Execute();
    }
}
