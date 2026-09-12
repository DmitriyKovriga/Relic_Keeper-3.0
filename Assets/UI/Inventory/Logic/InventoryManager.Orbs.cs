using System;
using Scripts.Saving;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public int GetOrbCount(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return 0;
            string normalizedId = NormalizeCraftingCurrencyId(orbId);
            var e = _orbCounts.Find(x => x != null && NormalizeCraftingCurrencyId(x.OrbId) == normalizedId);
            return e?.Count ?? 0;
        }

        public void AddOrb(string orbId, int count = 1)
        {
            if (string.IsNullOrEmpty(orbId) || count <= 0) return;
            string normalizedId = NormalizeCraftingCurrencyId(orbId);
            var e = _orbCounts.Find(x => x != null && NormalizeCraftingCurrencyId(x.OrbId) == normalizedId);
            if (e != null) e.Count += count;
            else _orbCounts.Add(new OrbCountEntry { OrbId = normalizedId, Count = count });
        }

        public bool ConsumeOrb(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return false;
            string normalizedId = NormalizeCraftingCurrencyId(orbId);
            var e = _orbCounts.Find(x => x != null && NormalizeCraftingCurrencyId(x.OrbId) == normalizedId);
            if (e == null || e.Count <= 0) return false;
            e.Count--;
            if (e.Count <= 0) _orbCounts.Remove(e);
            return true;
        }

        private static string NormalizeCraftingCurrencyId(string currencyId)
        {
            switch (currencyId)
            {
                case "CreationOrb": return "RelicOfMutation";
                case "PerfectioOrb": return "RelicOfPerfectio";
                case "FortuneOrb": return "RelicOfFortune";
                case "ElevationOrb": return "RelicOfElevation";
                case "ExcelsiorOrb": return "RelicOfExcelsior";
                case "PurgatioOrb": return "RelicOfPurgatio";
                case "ExpurgatioOrb": return "RelicOfExpurgatio";
                default: return currencyId;
            }
        }

        private void NormalizeCraftingCurrencyCounts()
        {
            for (int i = _orbCounts.Count - 1; i >= 0; i--)
            {
                OrbCountEntry entry = _orbCounts[i];
                if (entry == null || string.IsNullOrEmpty(entry.OrbId) || entry.Count <= 0)
                {
                    _orbCounts.RemoveAt(i);
                    continue;
                }

                entry.OrbId = NormalizeCraftingCurrencyId(entry.OrbId);
                int earlierIndex = _orbCounts.FindIndex(0, i, other =>
                    other != null && NormalizeCraftingCurrencyId(other.OrbId) == entry.OrbId);
                if (earlierIndex < 0) continue;

                _orbCounts[earlierIndex].OrbId = entry.OrbId;
                _orbCounts[earlierIndex].Count += entry.Count;
                _orbCounts.RemoveAt(i);
            }
        }
    }
}
