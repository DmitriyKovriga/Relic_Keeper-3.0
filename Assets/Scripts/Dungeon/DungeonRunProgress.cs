using System.Collections.Generic;
using UnityEngine;

namespace Scripts.Dungeon
{
    public static class DungeonRunProgress
    {
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
            return Mathf.Max(0, roomsCompletedBeforeSegment) + Mathf.Max(0, segmentRoomCount);
        }

        public static DungeonModifierValues CreateLocationLevelLootModifier(int locationLevel)
        {
            float bonus = Mathf.Max(1, locationLevel);
            return new DungeonModifierValues
            {
                LootDropChancePercent = bonus,
                LootRarityPercent = bonus
            };
        }

        public static void AddLocationLevelLootDescriptions(List<string> target, int locationLevel)
        {
            CreateLocationLevelLootModifier(locationLevel).AddDescriptions(target);
        }
    }
}
