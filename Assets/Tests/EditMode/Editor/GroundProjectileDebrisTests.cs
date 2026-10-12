using NUnit.Framework;
using System.Reflection;
using Scripts.Skills.Projectiles;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class GroundProjectileDebrisTests
    {
        private GameObject _owner;
        private GameObject _floor;
        private Sprite _sprite;
        private GroundProjectileDebris _debris;

        private void InvokeLifecycle(string method)
        {
            typeof(GroundProjectileDebris).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_debris, null);
        }

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Debris test projectile");
            var renderer = _owner.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
            renderer.sprite = _sprite;
            _owner.transform.position = new Vector3(0, 0.5f, 0);
            _debris = _owner.AddComponent<GroundProjectileDebris>();
            // Ordinary MonoBehaviours do not receive lifecycle callbacks in EditMode.
            InvokeLifecycle("OnEnable");
        }

        private void CreateFloor()
        {
            _floor = new GameObject("Debris test floor");
            _floor.layer = 6;
            _floor.transform.position = new Vector3(0, -0.1f, 0);
            _floor.AddComponent<BoxCollider2D>().size = new Vector2(12, 0.2f);
            Physics2D.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            InvokeLifecycle("OnDisable");
            Object.DestroyImmediate(_owner);
            if (_floor != null) Object.DestroyImmediate(_floor);
            Object.DestroyImmediate(_sprite);
        }

        [Test]
        public void AirborneProjectile_DoesNotEmitGroundDebris()
        {
            _debris.EmitAfterMovement();
            Assert.That(_debris.RuntimeParticles, Is.Null);
            CreateFloor();
            _owner.transform.position = Vector3.up * 3;
            _debris.EmitAfterMovement();
            Assert.That(_debris.RuntimeParticles, Is.Null);
        }

        [Test]
        public void MovingProjectile_LeavesExistingParticlesInWorldSpace()
        {
            CreateFloor();
            _debris.EmitAfterMovement();
            var system = _debris.RuntimeParticles;
            Assert.That(system, Is.Not.Null);
            Assert.That(system.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            Assert.That(system.transform.parent, Is.Null);
            var particles = new ParticleSystem.Particle[256];
            int originalCount = system.GetParticles(particles);
            Assert.That(originalCount, Is.GreaterThan(0));
            Vector3 originalPosition = particles[0].position;
            _owner.transform.position += Vector3.right * 2;
            _debris.EmitAfterMovement();
            int newCount = system.GetParticles(particles);
            Assert.That(newCount, Is.GreaterThan(originalCount));
            Assert.That(Vector3.Distance(particles[0].position, originalPosition), Is.LessThan(0.0001f));
            Assert.That(particles[0].velocity.x, Is.LessThan(0), "Earth must be thrown behind a right-facing wave.");
        }

        [Test]
        public void ReenabledProjectile_StartsFreshCloudAtNewGroundContact()
        {
            CreateFloor();
            _debris.EmitAfterMovement();
            var first = _debris.RuntimeParticles;
            InvokeLifecycle("OnDisable");
            _owner.SetActive(false);
            _owner.transform.position += Vector3.left * 2;
            _owner.GetComponent<SpriteRenderer>().flipX = true;
            _owner.SetActive(true);
            InvokeLifecycle("OnEnable");
            _debris.EmitAfterMovement();
            Assert.That(_debris.RuntimeParticles, Is.Not.SameAs(first));
            var particles = new ParticleSystem.Particle[256];
            Assert.That(_debris.RuntimeParticles.GetParticles(particles), Is.EqualTo(6));
            Assert.That(particles[0].velocity.x, Is.GreaterThan(0));
            Assert.That(particles[0].position.x, Is.LessThan(-2));
        }
    }
}

