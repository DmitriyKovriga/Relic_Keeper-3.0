using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Skills;
using Scripts.Skills.Modules;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public class DualWieldHandAnimationTestSkill : SkillBehaviour
    {
        protected override void Execute() { }
    }

    public class DualWieldHandAnimationTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private PlayerStats _stats;
        private WeaponVisualController _visuals;
        private Transform _mainPivot;
        private Transform _offPivot;
        private SpriteRenderer _mainRenderer;
        private SpriteRenderer _offRenderer;
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            var player = Create("Test player");
            player.SetActive(false);
            _stats = player.AddComponent<PlayerStats>();
            var visualRoot = Create("Visuals", player.transform).transform;
            _mainPivot = Create("HandPivot", visualRoot).transform;
            _mainPivot.localPosition = new Vector3(-1f / 24f, 0.1f, 0);
            _mainPivot.localRotation = Quaternion.Euler(0, 0, 7);
            _mainRenderer = Create("WeaponHolder", _mainPivot).AddComponent<SpriteRenderer>();
            _mainRenderer.transform.localRotation = Quaternion.Euler(0, 0, -13.5f);
            _mainRenderer.transform.localPosition = new Vector3(0.25f, -0.25f, 0);
            _texture = new Texture2D(2, 2);
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 24);
            _mainRenderer.sprite = _sprite;
            _visuals = player.AddComponent<WeaponVisualController>();
            typeof(WeaponVisualController).GetField("_weaponRenderer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_visuals, _mainRenderer);
            typeof(WeaponVisualController).GetMethod("EnsureOffhandRenderer", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_visuals, null);
            _offPivot = visualRoot.Find("OffhandHandPivot");
            _offRenderer = _offPivot.GetComponentInChildren<SpriteRenderer>();
            _offRenderer.sprite = _sprite;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
        }

        private GameObject Create(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            _objects.Add(go);
            return go;
        }

        private SkillHandAnimation Skill(int slot)
        {
            var go = Create("Skill", _stats.transform);
            go.SetActive(false);
            var skill = go.AddComponent<DualWieldHandAnimationTestSkill>();
            // Runtime creates the animation before SetRuntimeSlot; binding must also work at cast time.
            var animation = go.AddComponent<SkillHandAnimation>();
            animation.Initialize(_stats);
            skill.SetRuntimeSlot(null, slot);
            return animation;
        }

        [TestCase(0)]
        [TestCase(1)]
        public void SwingAndVisibilityAffectOnlyTheCastingHand(int slot)
        {
            var moving = slot == 0 ? _mainPivot : _offPivot;
            var still = slot == 0 ? _offPivot : _mainPivot;
            var renderer = slot == 0 ? _mainRenderer : _offRenderer;
            var otherRenderer = slot == 0 ? _offRenderer : _mainRenderer;
            Vector3 rest = moving.localPosition;
            Quaternion restRotation = moving.localRotation;
            Vector3 otherRest = still.localPosition;
            Quaternion otherRotation = still.localRotation;
            Quaternion heldRotation = renderer.transform.localRotation;
            var animation = Skill(slot);
            animation.SetActiveStyleId("overhead_stab");
            animation.LerpWindup(1);
            animation.SetWeaponVisible(false);
            animation.SnapImpact();
            Assert.That(Vector3.Distance(moving.localPosition, rest), Is.GreaterThan(0.01f));
            Assert.That(still.localPosition, Is.EqualTo(otherRest));
            Assert.That(Quaternion.Angle(still.localRotation, otherRotation), Is.LessThan(0.001f));
            Assert.That(renderer.enabled, Is.False);
            Assert.That(otherRenderer.enabled, Is.True);
            animation.LerpRecovery(1);
            animation.ForceReset();
            Assert.That(moving.localPosition, Is.EqualTo(rest));
            Assert.That(Quaternion.Angle(moving.localRotation, restRotation), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(renderer.transform.localRotation, heldRotation), Is.LessThan(0.001f));
            Assert.That(renderer.enabled, Is.True);
        }

        [Test]
        public void NewSkillDuringImpactCannotSaveTheImpactAsIdle()
        {
            Vector3 rest = _mainPivot.localPosition;
            Quaternion restRotation = _mainPivot.localRotation;
            var first = Skill(0);
            first.SetActiveStyleId("overhead_stab");
            first.SnapImpact();
            var next = Skill(0);
            next.ForceReset(); // An idle/new skill must not reset another skill's active swing.
            Assert.That(Vector3.Distance(_mainPivot.localPosition, rest), Is.GreaterThan(0.01f));
            next.SetActiveStyleId("overhead_stab");
            next.SnapImpact();
            next.ForceReset();
            Assert.That(_mainPivot.localPosition, Is.EqualTo(rest));
            Assert.That(Quaternion.Angle(_mainPivot.localRotation, restRotation), Is.LessThan(0.001f));
        }

        [Test]
        public void SupersededSkillCleanupCannotResetTheCurrentSwing()
        {
            var first = Skill(0);
            first.SetActiveStyleId("overhead_stab");
            first.SnapImpact();
            var next = Skill(0);
            next.SetActiveStyleId("slash");
            next.LerpWindup(1);
            next.SetWeaponVisible(false);
            var position = _mainPivot.localPosition;
            var rotation = _mainPivot.localRotation;
            first.ForceReset();
            first.LerpRecovery(1);
            Assert.That(_mainPivot.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(_mainPivot.localRotation, rotation), Is.LessThan(0.001f));
            Assert.That(_mainRenderer.enabled, Is.False);
            next.ForceReset();
        }

        [Test]
        public void ResetOneHandDoesNotInterruptTheOtherHand()
        {
            var main = Skill(0);
            var off = Skill(1);
            main.SetActiveStyleId("overhead_stab");
            main.SnapImpact();
            off.SetActiveStyleId("overhead_stab");
            off.LerpWindup(1);
            var position = _offPivot.localPosition;
            var rotation = _offPivot.localRotation;
            main.ForceReset();
            Assert.That(_offPivot.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(_offPivot.localRotation, rotation), Is.LessThan(0.001f));
            off.ForceReset();
        }

        [Test]
        public void SecondarySkillWithoutOffhandStillUsesMainWeapon()
        {
            _offRenderer.sprite = null;
            var rest = _mainPivot.localPosition;
            var animation = Skill(1);
            animation.SetActiveStyleId("bow_draw");
            animation.LerpWindup(1);
            Assert.That(Vector3.Distance(_mainPivot.localPosition, rest), Is.GreaterThan(0.001f));
            animation.ForceReset();
            Assert.That(_mainPivot.localPosition, Is.EqualTo(rest));
        }
    }
}
