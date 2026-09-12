using System.Collections.Generic;

namespace Scripts.Stats
{
    public sealed class ScopedStatsProvider : IStatsProvider
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
            return TryGetStat(type, out CharacterStat stat) && stat != null
                ? stat.Value
                : 0f;
        }

        public bool TryGetStat(StatType type, out CharacterStat stat)
        {
            if (_statCache.TryGetValue(type, out stat))
                return stat != null;

            CharacterStat baseStat = null;
            bool hasBaseStat = _baseProvider != null && _baseProvider.TryGetStat(type, out baseStat) && baseStat != null;
            bool hasScopedModifiers = HasScopedModifiers(type);
            if (!hasBaseStat && !hasScopedModifiers && _baseProvider == null)
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

        private bool HasScopedModifiers(StatType type)
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
