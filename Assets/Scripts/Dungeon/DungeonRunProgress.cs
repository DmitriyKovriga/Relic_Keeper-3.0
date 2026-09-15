using System;
using System.Collections.Generic;
using Scripts.Enemies;
using UnityEngine;

namespace Scripts.Dungeon
{
    public static class DungeonRunProgress
    {
        public const int FloorCheckpointSize = 10;
        public const float FloorSkipBonusPercent = 30f;

        public static int ResolveLocationLevel(int dungeonMinLevel, int roomsCompletedBeforeSegment, int roomIndexInSegment)
        {
            return Mathf.Max(1, dungeonMinLevel + Mathf.Max(0, roomsCompletedBeforeSegment) + Mathf.Max(0, roomIndexInSegment));
        }

        public static int ResolveDisplayedRoomNumber(int roomsCompletedBeforeSegment, int roomIndexInSegment)
        {
            return Mathf.Max(0, roomsCompletedBeforeSegment) + Mathf.Max(0, roomIndexInSegment) + 1;
        }

        public static int ResolveDisplayedRoomCount(int roomsCompletedBeforeSegment, int segmentRoomCount)
        {
            int count = Mathf.Max(0, roomsCompletedBeforeSegment) + Mathf.Max(0, segmentRoomCount);
            return AlignDisplayedRoomCountToCheckpoint(count);
        }

        public static int AlignDisplayedRoomCountToCheckpoint(int roomCount)
        {
            int count = Mathf.Max(0, roomCount);
            if (count == 0)
                return 0;

            int remainder = count % FloorCheckpointSize;
            return remainder == 0 ? count : count + FloorCheckpointSize - remainder;
        }

        public static DungeonModifierValues CreateLocationLevelLootModifier(int locationLevel)
        {
            float bonus = Mathf.Max(1, locationLevel);
            return new DungeonModifierValues
            {
                LootDropChancePercent = bonus,
                LootRarityPercent = bonus,
                EnemyCountPercent = bonus * EnemyLevelBalance.EnemyCountPercentPerLocationLevel
            };
        }

        public static void AddLocationLevelLootDescriptions(List<string> target, int locationLevel)
        {
            CreateLocationLevelLootModifier(locationLevel).AddDescriptions(target);
        }

        public static int ResolveStartingRoomsCompleted(int startingDisplayedRoom)
        {
            return Mathf.Max(0, startingDisplayedRoom - 1);
        }

        public static int ResolveSegmentRoomCount(int roomsCompletedBeforeSegment, int baseRoomCount)
        {
            int completed = Mathf.Max(0, roomsCompletedBeforeSegment);
            int firstRoom = completed + 1;
            int size = Mathf.Max(1, baseRoomCount);
            int remainder = firstRoom % size;
            int nextCheckpoint = remainder == 0 ? firstRoom : firstRoom + (size - remainder);
            return Mathf.Max(1, nextCheckpoint - completed);
        }

        public static int ResolveNextRoomAfterSegment(int roomsCompletedBeforeSegment, int segmentRoomCount)
        {
            return Mathf.Max(0, roomsCompletedBeforeSegment) + Mathf.Max(0, segmentRoomCount) + 1;
        }

        public static List<int> ResolveUnlockedFloorCheckpoints(int highestDisplayedRoom)
        {
            var floors = new List<int>();
            int highest = Mathf.Max(0, highestDisplayedRoom);
            for (int floor = FloorCheckpointSize; floor <= highest; floor += FloorCheckpointSize)
                floors.Add(floor);

            return floors;
        }

        public static DungeonModifierValues CreateFloorSkipBonusModifier()
        {
            return new DungeonModifierValues
            {
                LootDropChancePercent = FloorSkipBonusPercent,
                LootRarityPercent = FloorSkipBonusPercent,
                ExperiencePercent = FloorSkipBonusPercent,
                EnemyDamageDealtPercent = FloorSkipBonusPercent,
                EnemyCountPercent = FloorSkipBonusPercent
            };
        }

        public static bool IsFloorSelectPortalName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return false;

            string name = objectName.Trim();
            if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "(Clone)".Length).Trim();

            return string.Equals(name, FloorPortal.ObjectName, StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith(FloorPortal.ObjectName + " ", StringComparison.OrdinalIgnoreCase);
        }
    }
}
