using System;
using System.Collections.Generic;
using Scripts.Saving;
using UnityEngine;

namespace Scripts.Economy
{
    /// <summary>Account-wide crafting relics. Shared between characters and kept on death like gold and stash.</summary>
    public static class CraftingCurrencyWallet
    {
        public static event Action OnChanged;

        private static readonly List<OrbCountEntry> Counts = new List<OrbCountEntry>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Counts.Clear();
            OnChanged = null;
        }

        public static void Clear()
        {
            if (Counts.Count == 0)
                return;

            Counts.Clear();
            OnChanged?.Invoke();
        }

        public static int Get(string orbId)
        {
            OrbCountEntry entry = Find(orbId);
            return entry != null ? Mathf.Max(0, entry.Count) : 0;
        }

        public static void Add(string orbId, int count = 1)
        {
            if (count <= 0)
                return;

            string id = NormalizeId(orbId);
            if (string.IsNullOrEmpty(id))
                return;

            OrbCountEntry entry = Find(id);
            if (entry != null)
                entry.Count += count;
            else
                Counts.Add(new OrbCountEntry { OrbId = id, Count = count });

            OnChanged?.Invoke();
        }

        public static bool TryConsume(string orbId, int count = 1)
        {
            if (count <= 0)
                return true;

            OrbCountEntry entry = Find(orbId);
            if (entry == null || entry.Count < count)
                return false;

            entry.Count -= count;
            if (entry.Count <= 0)
                Counts.Remove(entry);

            OnChanged?.Invoke();
            return true;
        }

        public static void LoadFromSave(IReadOnlyList<OrbCountEntry> records)
        {
            Counts.Clear();
            MergeInto(Counts, records);
            OnChanged?.Invoke();
        }

        public static void WriteToSave(List<OrbCountEntry> target)
        {
            if (target == null)
                return;

            target.Clear();
            for (int i = 0; i < Counts.Count; i++)
            {
                OrbCountEntry entry = Counts[i];
                if (entry == null || string.IsNullOrEmpty(entry.OrbId) || entry.Count <= 0)
                    continue;

                target.Add(new OrbCountEntry
                {
                    OrbId = NormalizeId(entry.OrbId),
                    Count = entry.Count
                });
            }
        }

        public static List<OrbCountEntry> CollectFromLegacySave(GameSaveData data)
        {
            var merged = new List<OrbCountEntry>();
            if (data == null)
                return merged;

            MergeInto(merged, data.CraftingCurrency);
            MergeInto(merged, data.Inventory != null ? data.Inventory.OrbCounts : null);
            if (data.Characters == null)
                return merged;

            for (int i = 0; i < data.Characters.Count; i++)
            {
                CharacterSaveData character = data.Characters[i];
                MergeInto(merged, character != null && character.Inventory != null
                    ? character.Inventory.OrbCounts
                    : null);
            }

            return merged;
        }

        public static string NormalizeId(string currencyId)
        {
            if (string.IsNullOrEmpty(currencyId))
                return currencyId;

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

        private static OrbCountEntry Find(string orbId)
        {
            string id = NormalizeId(orbId);
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < Counts.Count; i++)
            {
                OrbCountEntry entry = Counts[i];
                if (entry != null && NormalizeId(entry.OrbId) == id)
                    return entry;
            }

            return null;
        }

        private static void MergeInto(List<OrbCountEntry> target, IReadOnlyList<OrbCountEntry> source)
        {
            if (target == null || source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                OrbCountEntry incoming = source[i];
                if (incoming == null || incoming.Count <= 0)
                    continue;

                string id = NormalizeId(incoming.OrbId);
                if (string.IsNullOrEmpty(id))
                    continue;

                OrbCountEntry existing = null;
                for (int j = 0; j < target.Count; j++)
                {
                    if (target[j] != null && NormalizeId(target[j].OrbId) == id)
                    {
                        existing = target[j];
                        break;
                    }
                }

                if (existing != null)
                {
                    existing.OrbId = id;
                    existing.Count += incoming.Count;
                }
                else
                {
                    target.Add(new OrbCountEntry { OrbId = id, Count = incoming.Count });
                }
            }
        }
    }
}
