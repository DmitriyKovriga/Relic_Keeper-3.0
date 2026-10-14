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

        [TestCase(1)]
        [TestCase(-1)]
        public void AirborneLean_RotatesBodyAndGripTogether_AndRestoresPhysicsAndSockets(int facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            _body.localRotation = Quaternion.Euler(0, 0, 11);
            _weapons.localRotation = Quaternion.Euler(0, 0, -19);
            Vector3 body = _body.localPosition, weapons = _weapons.localPosition;
            Quaternion bodyRotation = _body.localRotation, weaponsRotation = _weapons.localRotation;
            Vector3 root = _owner.transform.position;
            Physics2D.SyncTransforms();
            var collider = _owner.GetComponent<BoxCollider2D>();
            var bounds = collider.bounds;
            var grip = new GameObject("Authored attack socket").transform;
            grip.SetParent(_weapons, false);
            grip.localPosition = new Vector3(0.2f, 0.1f, 0);
            Vector3 socketBefore = grip.position;
            Vector3 bodyUpBefore = _body.up;
            _anchor.PresentationLeanDegrees = -6f;
            _anchor.WeaponPresentationOffset = new Vector3(2f / 24, 3f / 24, 0);
            Vector3 relativeBefore = _weapons.position + _owner.transform.TransformVector(_anchor.WeaponPresentationOffset) - _body.position;
            Quaternion lean = Quaternion.Euler(0, 0, -6f * facing);
            for (int i = 0; i < 3; i++)
            {
                _anchor.BeginRender(_camera);
                Assert.That(Vector3.Distance(_weapons.position - _body.position, lean * relativeBefore), Is.LessThan(0.00001f), "Weapon must share body lean around the same pivot.");
                Assert.That(Vector3.Distance(_body.up, lean * bodyUpBefore), Is.LessThan(0.00001f));
                Assert.That(_owner.transform.position, Is.EqualTo(root));
                Assert.That(_owner.transform.rotation, Is.EqualTo(Quaternion.identity));
                Physics2D.SyncTransforms();
                Assert.That(collider.bounds, Is.EqualTo(bounds));
                // Also covers a render interrupted before the camera-end callback.
                _anchor.Restore();
                Assert.That(_body.localPosition, Is.EqualTo(body));
                Assert.That(_weapons.localPosition, Is.EqualTo(weapons));
                Assert.That(_body.localRotation, Is.EqualTo(bodyRotation));
                Assert.That(_weapons.localRotation, Is.EqualTo(weaponsRotation));
                Assert.That(Vector3.Distance(grip.position, socketBefore), Is.LessThan(0.00001f));
            }
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

        [TestCase(1)]
        [TestCase(-1)]
        public void AirborneGrip_MovesWeaponInwardInBothFacings_AndRestoresSocket(int facing)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            Vector3 weapon = _weapons.position, body = _body.position, logicalSocket = _weapons.localPosition;
            _anchor.WeaponPresentationOffset = Vector3.left * (2f / 24);
            _anchor.BeginRender(_camera);
            Vector3 shared = _body.position - body;
            Vector3 correction = _weapons.position - weapon - shared;
            Assert.That(correction.x, Is.EqualTo(-facing * 2f / 24).Within(0.00001f));
            _anchor.EndRender(_camera);
            Assert.That(_weapons.localPosition, Is.EqualTo(logicalSocket));
        }

        [Test]
        public void WeaponBob_IsPresentationOnly_AndRestoresTheAttackSocket()
        {
            Vector3 body = _body.position, weapon = _weapons.position, root = _owner.transform.position;
            _anchor.WeaponPresentationOffset = Vector3.up / 24;
            _anchor.BeginRender(_camera);
            Vector3 shared = _body.position - body;
            Assert.That(Vector3.Distance(_weapons.position - weapon - shared, Vector3.up / 24), Is.LessThan(0.000001f));
            Assert.That(_owner.transform.position, Is.EqualTo(root));
            _anchor.EndRender(_camera);
            Assert.That(Vector3.Distance(_weapons.position, weapon), Is.LessThan(0.000001f));
        }

        [TestCase(1, true)]
        [TestCase(-1, true)]
        [TestCase(1, false)]
        [TestCase(-1, false)]
        public void NeutralDodge_HidesWeaponsOnlyDuringRendering_AndPreservesHiddenFlags(int facing, bool pixelCamera)
        {
            _owner.transform.localScale = new Vector3(facing, 1, 1);
            _camera.GetComponent<PixelPerfectCamera>().enabled = pixelCamera;
            var collider = _owner.GetComponent<BoxCollider2D>();
            collider.offset = new Vector2(0.02f, -0.04f);
            Physics2D.SyncTransforms();
            Bounds bounds = collider.bounds;
            Vector3 root = _owner.transform.position;
            Vector3 body = _body.localPosition, weapon = _weapons.localPosition;
            Vector3 bodyScale = _body.localScale, weaponScale = _weapons.localScale;
            Quaternion bodyRotation = _body.localRotation, weaponRotation = _weapons.localRotation;
            var visible = _weapons.gameObject.AddComponent<SpriteRenderer>();
            var hidden = new GameObject("Already hidden weapon part").AddComponent<SpriteRenderer>();
            hidden.transform.SetParent(_weapons, false);
            hidden.forceRenderingOff = true;
            _anchor.HideWeaponsForDodge = true;
            for (int i = 0; i < 3; i++)
            {
                _anchor.BeginRender(_camera);
                Assert.That(visible.forceRenderingOff, Is.True);
                Assert.That(hidden.forceRenderingOff, Is.True);
                Assert.That(_weapons.gameObject.activeSelf, Is.True, "Hiding must not disable equipment components or sockets.");
                Physics2D.SyncTransforms();
                Assert.That(collider.bounds, Is.EqualTo(bounds));
                Assert.That(_owner.transform.position, Is.EqualTo(root));
                Assert.That(_owner.transform.rotation, Is.EqualTo(Quaternion.identity));
                _anchor.EndRender(_camera);
                Assert.That(visible.forceRenderingOff, Is.False);
                Assert.That(hidden.forceRenderingOff, Is.True);
                Assert.That(_body.localPosition, Is.EqualTo(body));
                Assert.That(_weapons.localPosition, Is.EqualTo(weapon));
                Assert.That(_body.localRotation, Is.EqualTo(bodyRotation));
                Assert.That(_weapons.localRotation, Is.EqualTo(weaponRotation));
                Assert.That(_body.localScale, Is.EqualTo(bodyScale));
                Assert.That(_weapons.localScale, Is.EqualTo(weaponScale));
            }
        }

        [Test]
        public void NeutralDodge_CancellationRestoresWeapons_AndNextDodgeIncludesNewEquipment()
        {
            var original = _weapons.gameObject.AddComponent<SpriteRenderer>();
            _anchor.HideWeaponsForDodge = true;
            _anchor.BeginRender(_camera);
            _anchor.HideWeaponsForDodge = false;
            Assert.That(original.forceRenderingOff, Is.False);
            var replacement = new GameObject("New equipped weapon").AddComponent<SpriteRenderer>();
            replacement.transform.SetParent(_weapons, false);
            _anchor.HideWeaponsForDodge = true;
            _anchor.BeginRender(_camera);
            Assert.That(replacement.forceRenderingOff, Is.True);
            _anchor.enabled = false;
            typeof(PlayerRenderAnchor).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_anchor, null);
            Assert.That(original.forceRenderingOff, Is.False);
            Assert.That(replacement.forceRenderingOff, Is.False);
        }
    }
}
