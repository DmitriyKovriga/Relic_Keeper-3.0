using System.Reflection;
using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RelicKeeper.Tests.EditMode
{
    public class PlayerRenderAnchorTests
    {
        private GameObject _owner;
        private Camera _camera;
        private Transform _body;
        private Transform _weapons;
        private PlayerRenderAnchor _anchor;
        private RenderTexture _target;

        [SetUp]
        public void SetUp()
        {
            _owner = new GameObject("Render anchor test");
            _owner.transform.position = new Vector3(0.023f, 0.017f, 2);
            _owner.AddComponent<BoxCollider2D>();
            _body = new GameObject("MovementSprite").transform;
            _body.SetParent(_owner.transform, false);
            _body.localPosition = new Vector3(0.011f, 0.027f, 0);
            _weapons = new GameObject("Visuals").transform;
            _weapons.SetParent(_owner.transform, false);
            _weapons.localPosition = new Vector3(0.319444f, -0.132278f, 0);
            _anchor = _owner.AddComponent<PlayerRenderAnchor>();
            _anchor.Initialize(_body, _weapons);
            _camera = new GameObject("Pixel camera test").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _target = new RenderTexture(960, 540, 0);
            _camera.targetTexture = _target;
            var pixel = _camera.gameObject.AddComponent<PixelPerfectCamera>();
            pixel.assetsPPU = 24;
            pixel.refResolutionX = 480;
            pixel.refResolutionY = 270;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.None;
            typeof(PixelPerfectCamera).GetMethod("UpdateCameraProperties", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pixel, null);
        }

        [TearDown]
        public void TearDown()
        {
            _anchor.Restore();
            Object.DestroyImmediate(_owner);
            Object.DestroyImmediate(_camera.gameObject);
            Object.DestroyImmediate(_target);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void Rendering_AlignsBodyAndWeaponTogether_WithoutMovingPhysics(int facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            Vector3 original = _owner.transform.position;
            var collider = _owner.GetComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            Vector3 colliderCenter = collider.bounds.center;
            Vector3 bodyBefore = _body.position;
            Vector3 weaponBefore = _weapons.position;
            _anchor.BeginRender(_camera);
            Vector3 offset = _body.position - bodyBefore;
            Assert.That(offset.sqrMagnitude, Is.GreaterThan(0.0000001f));
            Assert.That(Vector3.Distance(_weapons.position - weaponBefore, offset), Is.LessThan(0.000001f));
            Assert.That(_owner.transform.position, Is.EqualTo(original));
            Assert.That(collider.bounds.center, Is.EqualTo(colliderCenter));
            _anchor.EndRender(_camera);
            Assert.That(Vector3.Distance(_body.position, bodyBefore), Is.LessThan(0.000001f));
            Assert.That(Vector3.Distance(_weapons.position, weaponBefore), Is.LessThan(0.000001f));
        }

        [Test]
        public void RepeatedRenders_DoNotAccumulateOffset_OrChangeAuthoredSockets()
        {
            Vector3 body = _body.localPosition, weapons = _weapons.localPosition;
            for (int i = 0; i < 100; i++)
            {
                _anchor.BeginRender(_camera);
                _anchor.BeginRender(_camera);
                _anchor.EndRender(_camera);
                Assert.That(_body.localPosition, Is.EqualTo(body));
                Assert.That(_weapons.localPosition, Is.EqualTo(weapons));
            }
        }

        [Test]
        public void NonPixelCamera_DoesNotMoveVisuals()
        {
            _camera.GetComponent<PixelPerfectCamera>().enabled = false;
            Vector3 body = _body.position, weapons = _weapons.position;
            _anchor.BeginRender(_camera);
            Assert.That(_body.position, Is.EqualTo(body));
            Assert.That(_weapons.position, Is.EqualTo(weapons));
        }

        [Test]
        public void Restore_HandlesCancelledRenderBeforeEndCallback()
        {
            Vector3 body = _body.localPosition, weapons = _weapons.localPosition;
            _anchor.BeginRender(_camera);
            _anchor.Restore();
            Assert.That(_body.localPosition, Is.EqualTo(body));
            Assert.That(_weapons.localPosition, Is.EqualTo(weapons));
        }
    }
}
