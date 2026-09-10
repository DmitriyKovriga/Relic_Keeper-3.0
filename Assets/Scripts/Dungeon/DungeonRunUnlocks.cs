using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Dungeon
{
    [Serializable]
    public sealed class DungeonUnlockRecord
    {
        public string DungeonId;
        public int HighestDisplayedRoom;
    }

    public static class DungeonRunUnlocks
    {
        private static readonly List<DungeonUnlockRecord> Records = new List<DungeonUnlockRecord>();

        public static IReadOnlyList<DungeonUnlockRecord> All => Records;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Records.Clear();
        }

        public static void Clear()
        {
            Records.Clear();
        }

        public static void LoadFromSave(IReadOnlyList<DungeonUnlockRecord> records)
        {
            Records.Clear();
            if (records == null)
                return;

            for (int i = 0; i < records.Count; i++)
            {
                DungeonUnlockRecord record = records[i];
                if (record == null || string.IsNullOrEmpty(record.DungeonId))
                    continue;

                RecordReachedRoom(record.DungeonId, record.HighestDisplayedRoom);
            }
        }

        public static void WriteToSave(List<DungeonUnlockRecord> target)
        {
            if (target == null)
                return;

            target.Clear();
            for (int i = 0; i < Records.Count; i++)
            {
                DungeonUnlockRecord record = Records[i];
                target.Add(new DungeonUnlockRecord
                {
                    DungeonId = record.DungeonId,
                    HighestDisplayedRoom = record.HighestDisplayedRoom
                });
            }
        }

        public static void RecordReachedRoom(string dungeonId, int displayedRoom)
        {
            if (string.IsNullOrEmpty(dungeonId))
                return;

            int room = Mathf.Max(0, displayedRoom);
            DungeonUnlockRecord record = Find(dungeonId);
            if (record == null)
            {
                Records.Add(new DungeonUnlockRecord
                {
                    DungeonId = dungeonId,
                    HighestDisplayedRoom = room
                });
                return;
            }

            if (room > record.HighestDisplayedRoom)
                record.HighestDisplayedRoom = room;
        }

        public static int GetHighestDisplayedRoom(string dungeonId)
        {
            DungeonUnlockRecord record = Find(dungeonId);
            return record != null ? record.HighestDisplayedRoom : 0;
        }

        private static DungeonUnlockRecord Find(string dungeonId)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                if (string.Equals(Records[i].DungeonId, dungeonId, StringComparison.Ordinal))
                    return Records[i];
            }

            return null;
        }
    }
}
