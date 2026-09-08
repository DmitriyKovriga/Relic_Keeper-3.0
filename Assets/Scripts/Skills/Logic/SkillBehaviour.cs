using UnityEngine;
using Scripts.Stats;
using Scripts.Combat;

namespace Scripts.Skills
{
    public abstract class SkillBehaviour : MonoBehaviour
    {
        private const float DefaultActionSpeed = 1f;
        private const float MinActionSpeed = 0.05f;
        private const float MaxActionSpeed = 12f;

        protected PlayerStats _ownerStats;
        protected SkillDataSO _data;
        protected float _lastCastTime;
        protected bool _isCasting;
        protected PlayerSkillManager _skillManager;
        protected int _slotIndex = -1;

        public bool IsCasting => _isCasting;
        public SkillDataSO Data => _data;
        public int SlotIndex => _slotIndex;

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

        public void SetRuntimeSlot(PlayerSkillManager skillManager, int slotIndex)
        {
            _skillManager = skillManager;
            _slotIndex = slotIndex;
        }

        public void ReduceCooldownRemaining(float seconds)
        {
            if (seconds <= 0f || CooldownDuration <= 0f)
                return;

            float remaining = CooldownRemaining;
            if (remaining <= 0f)
                return;

            float newRemaining = Mathf.Max(0f, remaining - seconds);
            _lastCastTime = Time.time + newRemaining - CooldownDuration;
        }

        public void AddCooldownRemaining(float seconds)
        {
            if (seconds <= 0f || CooldownDuration <= 0f)
                return;

            float remaining = Mathf.Min(CooldownDuration, CooldownRemaining + seconds);
            _lastCastTime = Time.time + remaining - CooldownDuration;
        }

        protected DamageContext ResolveDamageContext()
        {
            StatContextTagFlags tags = _data != null ? _data.DamageContextTags : StatContextTagFlags.None;
            if (tags == StatContextTagFlags.None)
                tags = StatContextTagFlags.Attack | StatContextTagFlags.Melee;

            return new DamageContext(tags);
        }

        protected IStatsProvider ResolveSkillStats()
        {
            return WeaponHandStatScope.ForSkill(_ownerStats, _slotIndex);
        }

        protected float ResolveSkillSpeedMultiplier()
        {
            if (_data == null)
                return 1f;

            return Mathf.Max(0.05f, _data.SkillSpeedMultiplier <= 0f ? 1f : _data.SkillSpeedMultiplier);
        }

        protected float ResolveActionSpeed()
        {
            float attackSpeed = ResolveAttackActionSpeed();
            float spellSpeed = ResolveSpellActionSpeed();

            float speed = _data != null
                ? _data.ActionSpeedMode switch
                {
                    SkillActionSpeedMode.Spell => spellSpeed,
                    SkillActionSpeedMode.Universal => Mathf.Max(attackSpeed, spellSpeed),
                    _ => attackSpeed
                }
                : attackSpeed;

            speed *= ResolveSkillSpeedMultiplier();
            return Mathf.Clamp(speed <= 0f ? DefaultActionSpeed : speed, MinActionSpeed, MaxActionSpeed);
        }

        private float ResolveAttackActionSpeed()
        {
            IStatsProvider stats = WeaponHandStatScope.ForSkill(_ownerStats, _slotIndex);
            if (stats == null
                || !stats.TryGetStat(StatType.AttackSpeed, out CharacterStat attackStat)
                || attackStat == null)
            {
                return DefaultActionSpeed;
            }

            float flatSpeed = attackStat.GetRawFlatValue();
            if (flatSpeed <= 0f)
                flatSpeed = DefaultActionSpeed;

            float additiveFactor = Mathf.Max(0f, 1f + attackStat.GetTotalPercentAdd() / 100f);
            return flatSpeed * additiveFactor * attackStat.GetTotalMultiplier();
        }

        private float ResolveSpellActionSpeed()
        {
            float baseSpeed = ResolveAttackSpeedFlatBase();
            float castFlatBonus = 0f;
            float castPercentAdd = 0f;
            float castMultiplier = 1f;

            if (_ownerStats != null && _ownerStats.TryGetStat(StatType.CastSpeed, out CharacterStat castStat) && castStat != null)
            {
                foreach (StatModifier modifier in castStat.Modifiers)
                {
                    if (modifier.Type == StatModType.Flat)
                        castFlatBonus += modifier.Value;
                }

                castPercentAdd = castStat.GetTotalPercentAdd();
                castMultiplier = castStat.GetTotalMultiplier();
            }

            float flatSpeed = Mathf.Max(MinActionSpeed, baseSpeed + castFlatBonus);
            float additiveFactor = Mathf.Max(0f, 1f + castPercentAdd / 100f);
            return flatSpeed * additiveFactor * castMultiplier;
        }

        private float ResolveAttackSpeedFlatBase()
        {
            IStatsProvider stats = WeaponHandStatScope.ForSkill(_ownerStats, _slotIndex);
            if (stats != null && stats.TryGetStat(StatType.AttackSpeed, out CharacterStat attackStat) && attackStat != null)
            {
                float rawFlat = attackStat.GetRawFlatValue();
                if (rawFlat > 0f)
                    return rawFlat;
            }

            return DefaultActionSpeed;
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
