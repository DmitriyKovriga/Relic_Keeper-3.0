using System;
using UnityEngine;

namespace Scripts.Stats
{
    // Класс для ресурсов типа ХП, Маны, Энергии
    public class StatResource
    {
        public event Action OnValueChanged;
        public event Action OnDepleted; // Событие "Закончилось" (смерть для ХП)

        private CharacterStat _maxStat; // Ссылка на стат (например, MaxHealth)
        private float _currentValue;
        private float _lastMax;

        public float Current => _currentValue;
        public float Max => _maxStat.Value;
        public float Percent => Max > 0 ? _currentValue / Max : 0;

        public StatResource(CharacterStat maxStat)
        {
            _maxStat = maxStat;
            _lastMax = Mathf.Max(0f, _maxStat.Value);
            _currentValue = _lastMax;
        }

        public void SetCurrent(float value)
        {
            RememberCurrentMax();
            _currentValue = Mathf.Clamp(value, 0, Max);
            OnValueChanged?.Invoke();
        }

        public void RestoreFull()
        {
            SetCurrent(Max);
        }

        public void Decrease(float amount)
        {
            _currentValue -= amount;
            if (_currentValue <= 0)
            {
                _currentValue = 0;
                OnDepleted?.Invoke();
            }
            OnValueChanged?.Invoke();
        }

        public void Increase(float amount)
        {
            _currentValue += amount;
            if (_currentValue > Max) _currentValue = Max;
            OnValueChanged?.Invoke();
        }

        public void ReevaluateMax()
        {
            float newMax = Mathf.Max(0f, Max);
            float next = AdjustCurrentForMaxChange(_currentValue, _lastMax, newMax);
            _lastMax = newMax;
            bool changed = Mathf.Abs(next - _currentValue) > 0.001f;
            _currentValue = next;
            if (changed)
                OnValueChanged?.Invoke();
        }

        /// <summary>
        /// Max up: current rises by the same amount. Max down: only clamp overcap,
        /// never pull current below the new cap.
        /// </summary>
        public static float AdjustCurrentForMaxChange(float current, float oldMax, float newMax)
        {
            newMax = Mathf.Max(0f, newMax);
            float delta = newMax - oldMax;
            if (delta > 0f)
                current += delta;
            return Mathf.Clamp(current, 0f, newMax);
        }

        private void RememberCurrentMax()
        {
            _lastMax = Mathf.Max(0f, Max);
        }
    }
}
