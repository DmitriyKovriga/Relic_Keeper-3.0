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

        /// <summary> Вызвать при додже/прерывании — рантайм скилла должен выйти и сделать Cleanup. </summary>
        public virtual void Cancel() { }

        // Инициализация (вызывается при экипировке)
        public virtual void Initialize(PlayerStats stats, SkillDataSO data)
        {
            _ownerStats = stats;
            _data = data;
        }

        // Внешний вызов (Input System дергает это)
        public void TryCast()
        {
            // 1. Проверки
            if (_isCasting) 
            {
                return; 
            }
            
            if (Time.time < _lastCastTime + _data.Cooldown)
            {
                return;
            }

            // ВАЖНО: Проверка маны
            if (_ownerStats.Mana.Current < _data.ManaCost) 
            {
                Debug.Log($"[Skill] Отказ: Нет маны. Нужно {_data.ManaCost}, есть {_ownerStats.Mana.Current}");
                return; 
            }

            Debug.Log("[Skill] Успех! Выполняем Execute()."); // <--- LOG
            
            // 2. Оплата
            _ownerStats.Mana.Decrease(_data.ManaCost);
            _lastCastTime = Time.time;

            // 3. Выполнение
            Execute();
        }

        // Этот метод каждый скилл реализует по-своему
        protected abstract void Execute();
    }
}
