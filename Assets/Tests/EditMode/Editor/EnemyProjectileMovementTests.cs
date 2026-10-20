using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class EnemyProjectileTestReceiver : MonoBehaviour, IDamageable
    {
        public int Hits;
        public bool TakeDamage(DamageSnapshot damage) { Hits++; return true; }
    }

    public class EnemyProjectileMovementTests
    {
        private static readonly Vector2 Origin = new Vector2(7600, 3500);
        private readonly List<Object> _objects = new List<Object>();
        private EnemyProjectile _shot;
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private EnemyDataSO Data => Resources.Load<EnemyDataSO>("Enemy/SO_Mold");
        [TearDown]
        public void Cleanup()
        {
            if (_shot != null) Object.DestroyImmediate(_shot.gameObject);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }
        private void Spawn(EnemyDataSO data = null)
        {
            EnemyProjectile.Spawn(data ?? Data, new DamageSnapshot(null) { Lightning = 10 }, Origin, Vector2.right, null, null);
            _shot = Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Single();
            Physics2D.SyncTransforms();
        }
        private EnemyProjectileTestReceiver Receiver(bool childCollider)
        {
            var root = Keep(new GameObject("Projectile receiver")); root.transform.position = Origin + Vector2.right * .3f;
            root.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var receiver = root.AddComponent<EnemyProjectileTestReceiver>();
            GameObject body = root;
            if (childCollider) { body = new GameObject("Child collider"); body.transform.SetParent(root.transform, false); }
            body.AddComponent<BoxCollider2D>().size = Vector2.one * .25f;
            Physics2D.SyncTransforms(); return receiver;
        }
        [Test]
        public void ChildColliderWithoutAReceiverCannotPinTheProjectileInPlace()
        {
            var receiver = Receiver(true); Spawn();
            // Physics reports the Rigidbody's transform, distinct from the collider.
            var contact = Physics2D.CircleCastAll(Origin, .22f, Vector2.right, .55f).First(hit => hit.collider.transform.parent == receiver.transform);
            Assert.That(contact.transform, Is.SameAs(receiver.transform));
            Assert.That(contact.collider.transform, Is.Not.SameAs(receiver.transform));
            _shot.Tick(.1f);
            Assert.That(_shot != null, Is.True);
            Assert.That(_shot.transform.position.x, Is.EqualTo(Origin.x + .55f).Within(.003f));
            Assert.That(receiver.Hits, Is.Zero, "This collider is ignored by the existing trigger target rules.");
            _shot.Tick(.1f);
            Assert.That(_shot.transform.position.x, Is.EqualTo(Origin.x + 1.1f).Within(.003f));
        }
        [Test]
        public void ValidRootReceiverIsHitAndProjectileExpires()
        {
            var receiver = Receiver(false); Spawn(); _shot.Tick(.1f);
            Assert.That(receiver.Hits, Is.EqualTo(1)); Assert.That(_shot == null, Is.True);
        }
        [Test]
        public void IgnoredColliderDoesNotHideALaterValidHitInTheSameSweep()
        {
            var ignored = Receiver(true);
            var valid = Receiver(false); valid.transform.position = Origin + Vector2.right * .9f;
            Physics2D.SyncTransforms(); Spawn(); _shot.Tick(.2f);
            Assert.That(ignored.Hits, Is.Zero); Assert.That(valid.Hits, Is.EqualTo(1));
            Assert.That(_shot == null, Is.True);
        }
        [Test]
        public void ValidChildReceiverIsStillHitOnce()
        {
            var parent = Receiver(true); var body = parent.GetComponentInChildren<BoxCollider2D>();
            var child = body.gameObject.AddComponent<EnemyProjectileTestReceiver>(); Spawn(); _shot.Tick(.1f);
            Assert.That(child.Hits, Is.EqualTo(1)); Assert.That(parent.Hits, Is.Zero); Assert.That(_shot == null, Is.True);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void TerrainUsesTheProjectileStopSetting(bool stops)
        {
            var data = Keep(Object.Instantiate(Data)); data.Attack.ProjectileStopsOnGround = stops;
            var terrain = Keep(new GameObject("Terrain") { layer = 6 }); terrain.transform.position = Origin + Vector2.right * .3f;
            terrain.AddComponent<BoxCollider2D>().size = Vector2.one * .25f; Physics2D.SyncTransforms();
            Spawn(data); _shot.Tick(.1f);
            if (stops) Assert.That(_shot == null, Is.True);
            else Assert.That(_shot.transform.position.x, Is.EqualTo(Origin.x + .55f).Within(.003f));
        }
    }
}
