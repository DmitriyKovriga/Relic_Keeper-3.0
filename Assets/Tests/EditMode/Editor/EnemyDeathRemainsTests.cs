using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Dungeon;
using Scripts.Enemies;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyDeathRemainsTests
    {
        [Test]
        public void PackedSlots_DoNotOverlapAndLaterDeathsGoBelow()
        {
            var layer = new EnemyDeathRemainsLayer();
            int first = layer.AllocateSpriteOrder();
            int second = layer.AllocateSpriteOrder();
            int third = layer.AllocateSpriteOrder();

            Assert.That(first, Is.EqualTo(EnemyDeathRemainsLayer.FirstSpriteOrder));
            Assert.That(second, Is.EqualTo(first - EnemyDeathRemainsLayer.SlotStride));
            Assert.That(third, Is.LessThan(second));
            Assert.That(layer.NextSpriteOrder, Is.LessThan(third));
        }

        [Test]
        public void PackedSlots_KeepUniqueMaskRanges()
        {
            var layer = new EnemyDeathRemainsLayer();
            var used = new HashSet<int>();

            for (int i = 0; i < 8; i++)
            {
                int sprite = layer.AllocateSpriteOrder();
                int[] band =
                {
                    EnemyDeathRemainsLayer.MaskBackOrder(sprite),
                    sprite,
                    EnemyDeathRemainsLayer.OverlayOrder(sprite),
                    EnemyDeathRemainsLayer.MaskFrontOrder(sprite)
                };

                Assert.That(band[3] - band[0], Is.EqualTo(EnemyDeathRemainsLayer.SlotStride - 1));
                for (int j = 0; j < band.Length; j++)
                    Assert.That(used.Add(band[j]), Is.True, $"order {band[j]} reused");
            }
        }

        [Test]
        public void RemainsSheet_ReusesOneHostPerRoom()
        {
            var room = new GameObject("RoomRoot");
            var nested = new GameObject("SpawnerGroup");
            try
            {
                room.AddComponent<RoomController>();
                nested.transform.SetParent(room.transform, false);

                EnemyDeathRemainsSheet first = EnemyDeathRemainsSheet.GetOrCreate(nested.transform);
                EnemyDeathRemainsSheet second = EnemyDeathRemainsSheet.GetOrCreate(nested.transform);

                Assert.That(second, Is.SameAs(first));
                Assert.That(first.transform.parent, Is.EqualTo(room.transform));
                Assert.That(first.name, Is.EqualTo(EnemyDeathRemainsSheet.ObjectName));
            }
            finally
            {
                Object.DestroyImmediate(nested);
                Object.DestroyImmediate(room);
            }
        }
    }
}
