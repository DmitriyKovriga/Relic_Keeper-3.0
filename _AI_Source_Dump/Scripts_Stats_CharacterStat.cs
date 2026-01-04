using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CharacterStat
{
    public float BaseValue;

    protected bool _isDirty = true;
    protected float _value;

    private readonly List<StatModifier> _modifiers = new List<StatModifier>();

    public CharacterStat(float baseValue)
    {
        BaseValue = baseValue;
    }

    public float Value 
    {
        get {
            if (_isDirty) {
                _value = CalculateFinalValue();
                _isDirty = false;
            }
            return _value;
        }
    }

    public void AddModifier(StatModifier mod)
    {
        _isDirty = true;
        _modifiers.Add(mod);
    }

    public bool RemoveModifier(StatModifier mod)
    {
        if (_modifiers.Remove(mod)) {
            _isDirty = true;
            return true;
        }
        return false;
    }

    public bool RemoveAllModifiersFromSource(object source)
    {
        bool didRemove = _modifiers.RemoveAll(mod => mod.Source == source) > 0;
        if (didRemove) _isDirty = true;
        return didRemove;
    }

    private float CalculateFinalValue()
    {
        float finalValue = BaseValue;
        float sumPercentAdd = 0; // Increased
        float finalMultiplier = 1; // More

        for (int i = 0; i < _modifiers.Count; i++)
        {
            StatModifier mod = _modifiers[i];

            if (mod.Type == StatModType.Flat) 
            {
                finalValue += mod.Value;
            }
            else if (mod.Type == StatModType.PercentAdd) 
            {
                sumPercentAdd += mod.Value; // Складываем проценты (Increased)
            }
            else if (mod.Type == StatModType.PercentMult) 
            {
                finalMultiplier *= mod.Value; // Перемножаем (More)
            }
        }

        // Формула: (Base + Flat) * (1 + SumIncreased) * Product(More)
        return (float)Math.Round(finalValue * (1 + sumPercentAdd) * finalMultiplier, 4);
    }
}