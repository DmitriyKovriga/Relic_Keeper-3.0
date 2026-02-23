using System;
using Scripts.Saving;

namespace Scripts.Inventory
{
    public partial class InventoryManager
    {
        public int GetOrbCount(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return 0;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            return e?.Count ?? 0;
        }

        public void AddOrb(string orbId, int count = 1)
        {
            if (string.IsNullOrEmpty(orbId) || count <= 0) return;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            if (e != null) e.Count += count;
            else _orbCounts.Add(new OrbCountEntry { OrbId = orbId, Count = count });
        }

        public bool ConsumeOrb(string orbId)
        {
            if (string.IsNullOrEmpty(orbId)) return false;
            var e = _orbCounts.Find(x => x.OrbId == orbId);
            if (e == null || e.Count <= 0) return false;
            e.Count--;
            if (e.Count <= 0) _orbCounts.Remove(e);
            return true;
        }
    }
}
