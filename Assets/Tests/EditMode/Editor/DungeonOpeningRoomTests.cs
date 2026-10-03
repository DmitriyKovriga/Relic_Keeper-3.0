using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Dungeon;

namespace RelicKeeper.Tests.EditMode
{
    public class DungeonOpeningRoomTests
    {
        [Test]
        public void FreshRun_PlacesOpeningRoomsFirst()
        {
            var sequence = new List<string>();

            int placed = DungeonDataSO.AppendOpeningRooms(
                new[] { "Rooms/Easy", "", "Rooms/Tutorial" },
                roomsCompletedBeforeSegment: 0,
                slots: 5,
                sequence);

            Assert.That(placed, Is.EqualTo(2));
            Assert.That(sequence, Is.EqualTo(new[] { "Rooms/Easy", "Rooms/Tutorial" }));
        }

        [Test]
        public void LaterStart_SkipsOpeningRoomsAlreadyPassed()
        {
            var sequence = new List<string>();

            int placed = DungeonDataSO.AppendOpeningRooms(
                new[] { "Rooms/Easy", "Rooms/Tutorial", "Rooms/Third" },
                roomsCompletedBeforeSegment: 1,
                slots: 4,
                sequence);

            Assert.That(placed, Is.EqualTo(2));
            Assert.That(sequence, Is.EqualTo(new[] { "Rooms/Tutorial", "Rooms/Third" }));
        }

        [Test]
        public void OpeningList_StopsAtTheRemainingSlotCount()
        {
            var sequence = new List<string>();

            int placed = DungeonDataSO.AppendOpeningRooms(
                new[] { "Rooms/Easy", "Rooms/Tutorial" },
                roomsCompletedBeforeSegment: 0,
                slots: 1,
                sequence);

            Assert.That(placed, Is.EqualTo(1));
            Assert.That(sequence, Is.EqualTo(new[] { "Rooms/Easy" }));
        }
    }
}
