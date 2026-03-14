using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Scripts.Stats
{
    [Serializable]
    public class CharacterStat
    {
        public float BaseValue;

        protected bool _isDirty = true;
        protected float _value;
        protected float _lastBaseValue = float.MinValue;

        protected readonly List<StatModifier> _modifiers;
        public readonly ReadOnlyCollection<StatModifier> Modifiers;

        public CharacterStat()
        {
            _modifiers = new List<StatModifier>();
            Modifiers = _modifiers.AsReadOnly();
        }

        public CharacterStat(float baseValue) : this()
        {
            BaseValue = baseValue;
        }

        public float Value
        {
            get
            {
                if (_isDirty || Math.Abs(BaseValue - _lastBaseValue) > 0.001f)
                {
                    _lastBaseValue = BaseValue;
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
            _modifiers.Sort(CompareModifierOrder);
        }

        public bool RemoveModifier(StatModifier mod)
        {
            if (_modifiers.Remove(mod))
            {
                _isDirty = true;
                return true;
            }

            return false;
        }

        public bool RemoveAllModifiersFromSource(object source)
        {
            bool didRemove = false;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].Source == source)
                {
                    _isDirty = true;
                    didRemove = true;
                    _modifiers.RemoveAt(i);
                }
            }

            return didRemove;
        }

        public float GetRawFlatValue()
        {
            float finalValue = BaseValue;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Type == StatModType.Flat)
                    finalValue += _modifiers[i].Value;
            }

            return finalValue;
        }

        public float GetTotalPercentAdd()
        {
            float sum = 0f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Type.IsAdditivePercent())
                    sum += _modifiers[i].Type.ToSignedPercent(_modifiers[i].Value);
            }

            return sum;
        }

        public float GetTotalMultiplier()
        {
            float mult = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Type.IsMultiplicativePercent())
                    mult *= _modifiers[i].Type.ToMultiplierFactor(_modifiers[i].Value);
            }

            return mult;
        }

        protected virtual int CompareModifierOrder(StatModifier a, StatModifier b)
        {
            if (a.Order < b.Order) return -1;
            if (a.Order > b.Order) return 1;
            return 0;
        }

        private float CalculateFinalValue()
        {
            float finalValue = BaseValue;
            float sumPercentAdd = 0f;
            float finalMultiplier = 1f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier mod = _modifiers[i];

                if (mod.Type == StatModType.Flat)
                    finalValue += mod.Value;
                else if (mod.Type.IsAdditivePercent())
                    sumPercentAdd += mod.Type.ToSignedPercent(mod.Value);
                else if (mod.Type.IsMultiplicativePercent())
                    finalMultiplier *= mod.Type.ToMultiplierFactor(mod.Value);
            }

            float additiveFactor = Math.Max(0f, 1f + (sumPercentAdd / 100f));
            return (float)Math.Round(finalValue * additiveFactor * finalMultiplier, 4);
        }
    }
}
