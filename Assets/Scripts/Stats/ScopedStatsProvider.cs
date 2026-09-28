using System.Collections.Generic;

namespace Scripts.Stats
{
    /// <summary>
    /// Attacker stats plus modifiers that exist only for one hit.
    /// Layers are combined in place. <see cref="TryGetStat"/> still materialises a
    /// <see cref="CharacterStat"/> for callers that walk the modifier list.
    /// </summary>
    public sealed class ScopedStatsProvider : IStatsProvider, IStatLayerSource
    {
        private readonly IStatsProvider _baseProvider;
        private readonly List<SerializableStatModifier> _modifiers = new List<SerializableStatModifier>();
        private readonly Dictionary<StatType, CharacterStat> _statCache = new Dictionary<StatType, CharacterStat>();

        public IStatsProvider BaseProvider => _baseProvider;

        public ScopedStatsProvider(IStatsProvider baseProvider, IEnumerable<SerializableStatModifier> modifiers)
        {
            _baseProvider = baseProvider;
            if (modifiers == null)
                return;

            foreach (SerializableStatModifier modifier in modifiers)
            {
                if (System.Math.Abs(modifier.Value) > 0.0001f)
                    _modifiers.Add(modifier);
            }
        }

        public float GetValue(StatType type)
        {
            return TryGetLayers(type, out float flat, out float additivePercent, out float multiplicativeFactor)
                ? StatLayerMath.Evaluate(flat, additivePercent, multiplicativeFactor)
                : 0f;
        }

        public bool TryGetLayers(StatType type, out float flat, out float additivePercent, out float multiplicativeFactor)
        {
            flat = 0f;
            additivePercent = 0f;
            multiplicativeFactor = 1f;
            bool hasBase = false;

            if (_baseProvider is IStatLayerSource layeredBase &&
                layeredBase.TryGetLayers(type, out flat, out additivePercent, out multiplicativeFactor))
            {
                hasBase = true;
            }
            else if (_baseProvider != null && _baseProvider.TryGetStat(type, out CharacterStat baseStat) && baseStat != null)
            {
                StatLayerMath.Read(baseStat, out flat, out additivePercent, out multiplicativeFactor);
                hasBase = true;
            }
            else if (_baseProvider != null)
            {
                flat = _baseProvider.GetValue(type);
                hasBase = true;
            }

            bool hasOverlay = false;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                SerializableStatModifier modifier = _modifiers[i];
                if (modifier.Stat != type)
                    continue;

                hasOverlay = true;
                StatLayerMath.Apply(modifier.Type, modifier.Value, ref flat, ref additivePercent, ref multiplicativeFactor);
            }

            return hasBase || hasOverlay;
        }

        public bool TryGetStat(StatType type, out CharacterStat stat)
        {
            if (_statCache.TryGetValue(type, out stat))
                return stat != null;

            CharacterStat baseStat = null;
            bool hasBaseStat = _baseProvider != null && _baseProvider.TryGetStat(type, out baseStat) && baseStat != null;
            bool hasOverlay = HasOverlay(type);
            if (!hasBaseStat && !hasOverlay && _baseProvider == null)
            {
                stat = null;
                _statCache[type] = null;
                return false;
            }

            stat = hasBaseStat
                ? CloneStat(baseStat)
                : new CharacterStat(_baseProvider != null ? _baseProvider.GetValue(type) : 0f);

            for (int i = 0; i < _modifiers.Count; i++)
            {
                SerializableStatModifier modifier = _modifiers[i];
                if (modifier.Stat == type)
                    stat.AddModifier(modifier.ToStatModifier(this));
            }

            _statCache[type] = stat;
            return true;
        }

        private bool HasOverlay(StatType type)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Stat == type)
                    return true;
            }

            return false;
        }

        private static CharacterStat CloneStat(CharacterStat source)
        {
            var clone = new CharacterStat(source.BaseValue);
            for (int i = 0; i < source.Modifiers.Count; i++)
                clone.AddModifier(source.Modifiers[i]);

            return clone;
        }
    }
}
