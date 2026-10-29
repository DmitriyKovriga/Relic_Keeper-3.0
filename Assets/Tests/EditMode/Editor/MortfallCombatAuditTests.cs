using System.Collections.Generic;
using System.Linq;
using Scripts.Dungeon;
using NUnit.Framework;
using RelicKeeper.Diagnostics;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class MortfallCombatAuditTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private static readonly Vector2 Origin = new Vector2(18000, 18000);
        [TestCase(0L, 1, true)] [TestCase(-1L, 1, true)]
        [TestCase(2500000L, 0, true)] [TestCase(2500000L, 2, true)] [TestCase(2500000L, 1, false)]
        public void MissingOrAmbiguousGpuSamplesStayUnavailable(long nanoseconds, int blocks, bool supported)
        {
            Assert.That(CombatDecorationStressProbe.TryCameraGpuMilliseconds(nanoseconds, blocks, supported, out var ms), Is.False);
            Assert.That(ms, Is.EqualTo(-1));
        }
        [Test] public void NativeGpuNanosecondsConvertToMillisecondsWithoutCpuFallback()
        {
            Assert.That(CombatDecorationStressProbe.TryCameraGpuMilliseconds(2500000, 1, true, out var ms), Is.True);
            Assert.That(ms, Is.EqualTo(2.5f));
        }
        private void Solid(Vector2 center, Vector2 size)
        {
            var go = new GameObject("Audit pad test") { layer = 6 }; _objects.Add(go);
            go.transform.position = Origin + center; go.AddComponent<BoxCollider2D>().size = size;
            Physics2D.SyncTransforms();
        }
        [TearDown] public void Cleanup() { foreach (var go in _objects) Object.DestroyImmediate(go); _objects.Clear(); }
        [Test] public void PlatformBesideWallDoesNotPlaceShooterInsideWallOrOnLowerFloor()
        {
            Solid(new Vector2(2, -.25f), new Vector2(4, .5f));
            Solid(new Vector2(-1, -1), new Vector2(2, 4));
            Solid(new Vector2(-3, -2), new Vector2(2, .5f));
            Assert.That(CombatDecorationStressProbe.TryFindRoomAuditPosition(Origin + new Vector2(.5f, .5f), out var point), Is.True);
            Assert.That(point.x, Is.GreaterThan(Origin.x + .22f));
            Assert.That(point.y - Origin.y, Is.EqualTo(.55f).Within(.002f));
        }
        [Test] public void MissingFloorRejectsPlacementInsteadOfFreezingActorInAir()
        {
            Assert.That(CombatDecorationStressProbe.TryFindRoomAuditPosition(Origin, out _), Is.False);
        }
        [TestCase("MortfallRoom_001")] [TestCase("MortfallRoom_006")]
        public void ActualRoomHasAClearSupportedCombatPosition(string name)
        {
            var room = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Dungeon/MineDungeon/" + name));
            _objects.Add(room); room.transform.position = Origin;
            Physics2D.SyncTransforms();
            Assert.That(room.GetComponentsInChildren<EnemySpawner>(true).Any(spawner =>
                CombatDecorationStressProbe.TryFindRoomAuditPosition(spawner.transform.position, out _)), Is.True);
        }
    }
}
