using System;
using UnityEngine;

// Это обычный C# класс, не MonoBehaviour
public class StatResource
{
    public event Action OnValueChanged;
    public event Action OnDepleted; // Событие "Закончилось" (смерть для ХП)

    private CharacterStat _maxStat; // Ссылка на стат (например, MaxHealth)
    private float _currentValue;

    public float Current => _currentValue;
    public float Max => _maxStat.Value;
    public float Percent => Max > 0 ? _currentValue / Max : 0;

    public StatResource(CharacterStat maxStat)
    {
        _maxStat = maxStat;
        _currentValue = _maxStat.Value;
    }

    public void SetCurrent(float value)
    {
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
}