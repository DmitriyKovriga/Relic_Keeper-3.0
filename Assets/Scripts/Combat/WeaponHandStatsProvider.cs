using System.Collections.Generic;
using Scripts.Inventory;
using Scripts.Stats;

namespace Scripts.Combat
{
    /// <summary>
    /// Player stats with the inactive dual-wield weapon's local combat stats stripped.
    /// Global affixes from that weapon remain.
    /// </summary>
    public sealed class WeaponHandStatsProvider : IStatsProvider
    {
        private readonly IStatsProvider _baseProvider;
        private readonly InventoryItem _inactiveWeapon;
        private readonly Dictionary<StatType, CharacterStat> _statCache = new Dictionary<StatType, CharacterStat>();

        public IStatsProvider BaseProvider => _baseProvider;
        public InventoryItem InactiveWeapon => _inactiveWeapon;

        public WeaponHandStatsProvider(IStatsProvider baseProvider, InventoryItem inactiveWeapon)
        {
            _baseProvider = baseProvider;
            _inactiveWeapon = inactiveWeapon;
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

            if (_baseProvider == null || !_baseProvider.TryGetStat(type, out CharacterStat baseStat) || baseStat == null)
            {
                stat = null;
                _statCache[type] = null;
                return false;
            }

            stat = CloneWithoutInactiveWeaponLocals(baseStat, _inactiveWeapon);
            _statCache[type] = stat;
            return true;
        }

        private static CharacterStat CloneWithoutInactiveWeaponLocals(CharacterStat source, InventoryItem inactiveWeapon)
        {
            var clone = new CharacterStat(source.BaseValue);
            for (int i = 0; i < source.Modifiers.Count; i++)
            {
                StatModifier modifier = source.Modifiers[i];
                if (IsLocalStatFromWeapon(modifier.Source, inactiveWeapon))
                    continue;

                clone.AddModifier(modifier);
            }

            return clone;
        }

        public static bool IsLocalStatFromWeapon(object source, InventoryItem weapon)
        {
            return weapon != null
                && source is WeaponLocalStatSource localSource
                && localSource.Item == weapon;
        }
    }
}
