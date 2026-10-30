using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scripts.Enemies;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public class EnemyAttackIntentTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _owner;
        private Transform _target;
        private EnemyEntity _entity;
        private EnemyAttackController _attack;
        private EnemyAnimationBridge _animation;
        private EnemyAttackTelegraphVfx _warning;
        private bool _hadReduced;
        private int _reduced;
        private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
        private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);

        [SetUp]
        public void SetUp()
        {
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = PlayerPrefs.GetInt(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _owner = new GameObject("Owned intent fixture");
            _owner.transform.position = new Vector3(1000, 1000, 0);
            _target = new GameObject("Non-damageable target").transform;
            _target.SetParent(_owner.transform, false);
            _target.localPosition = Vector3.right;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_owner);
            if (_hadReduced) PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, _reduced);
            else PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
        }

        private void Create(string resource)
        {
            var data = Resources.Load<EnemyDataSO>("Enemy/" + resource);
            var root = Object.Instantiate(data.Prefab.gameObject, _owner.transform);
            _entity = root.GetComponent<EnemyEntity>();
            _entity.Setup(data, 1);
            root.transform.localPosition = Vector3.zero;
            _attack = root.GetComponent<EnemyAttackController>();
            _animation = root.GetComponent<EnemyAnimationBridge>();
            _warning = root.GetComponent<EnemyAttackTelegraphVfx>();
        }

        [TestCase("SO_Knight", false)]
        [TestCase("SO_Mold", false)]
        [TestCase("SO_Knight", true)]
        public void ActualReleaseEndsContinuousPreparationWithoutChangingAttackWindows(string resource, bool charge)
        {
            Create(resource);
            Assert.That(charge ? _attack.TryStartChargeAttack(_target) : _attack.TryStartAttack(_target), Is.True);
            Assert.That(_warning.IsPlaying && _attack.IsPreparingAttack, Is.True);
            Assert.That((float)Get(_attack, "_phaseTimer"), Is.EqualTo(.5f));
            Call(_attack, "EnterWindupPhase");
            float windup = charge ? _entity.Data.ChargeAttack.Windup : _entity.Data.Attack.Windup;
            Assert.That((float)Get(_attack, "_phaseTimer"), Is.EqualTo(windup).Within(.001f));
            // A local visual clock running ahead must not hide authoritative preparation.
            Set(_warning, "_elapsed", 20f);
            Call(_warning, "LateUpdate");
            Assert.That(_warning.IsPlaying, Is.True);
            var config = _entity.Data.Animation;
            float impactTime = charge ? config.ChargeImpactFrame / config.ChargeFps : config.AttackImpactFrame / config.AttackFps;
            Set(_animation, "_stateTime", impactTime - .001f);
            Call(_animation, "ApplySpriteFrame", true);
            Assert.That(_animation.ConsumeAttackImpactSignal(), Is.False);
            Assert.That(_warning.IsPlaying, Is.True);
            Set(_animation, "_stateTime", impactTime + .001f);
            Call(_animation, "ApplySpriteFrame", true);
            Call(_attack, "Update");
            Assert.That(Get(_attack, "_phase").ToString(), Is.EqualTo("Active"));
            Assert.That(_warning.IsPlaying || _attack.IsPreparingAttack, Is.False);
            Assert.That(_animation.ConsumeAttackImpactSignal(), Is.False, "Controller consumes the original impact once.");
        }

        [TestCase("SO_Knight")]
        [TestCase("SO_Mold")]
        public void TimerFallbackAlsoEndsWarningAndRecoveryKeepsAuthoredFollowThrough(string resource)
        {
            Create(resource);
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Call(_attack, "EnterWindupPhase");
            Set(_attack, "_phaseTimer", 0f);
            Call(_attack, "Update");
            Assert.That(Get(_attack, "_phase").ToString(), Is.EqualTo("Active"));
            Assert.That(_warning.IsPlaying, Is.False);
            Assert.That((float)Get(_attack, "_phaseTimer"), Is.EqualTo(_entity.Data.Attack.ActiveTime).Within(.001f));
            Set(_attack, "_phaseTimer", 0f);
            Call(_attack, "Update");
            Assert.That(Get(_attack, "_phase").ToString(), Is.EqualTo("Recovery"));
            Assert.That((float)Get(_attack, "_phaseTimer"), Is.GreaterThanOrEqualTo(_entity.Data.Attack.Recovery));
            Assert.That(_attack.TryStartAttack(_target), Is.False);
            Assert.That(_animation.TryPlayHitReaction(), Is.False, "Existing attack/recoil ownership is retained.");
            Set(_animation, "_stateTime", 20f);
            Call(_animation, "ApplySpriteFrame", true);
            var followThrough = _entity.VisualRenderer.sprite;
            Call(_animation, "Update");
            Assert.That(_entity.VisualRenderer.sprite, Is.SameAs(followThrough));
            Set(_attack, "_phaseTimer", 0f);
            Call(_attack, "Update");
            Call(_animation, "Update");
            Assert.That(_attack.IsBusy, Is.False);
            Assert.That(_entity.VisualRenderer.sprite, Is.Not.SameAs(followThrough));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ControlLockCancelsCueAndResetReusesOnlyOneVisual(bool freeze)
        {
            Create("SO_Knight");
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Call(_attack, "EnterWindupPhase");
            if (freeze) _attack.SetFrozen(true); else _attack.SetStunned(true);
            Assert.That(_attack.IsBusy || _warning.IsPlaying, Is.False);
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "AttackTelegraphAura").gameObject.activeSelf, Is.False);
            _attack.Initialize(_entity, _entity.Data);
            Assert.That(_attack.TryStartAttack(_target), Is.False, "Reset preserves the existing primary cooldown.");
            Set(_attack, "_nextAttackAllowedAt", Time.time - .1f);
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Count(x => x.name == "AttackTelegraphAura"), Is.EqualTo(1));
            _entity.gameObject.SetActive(false);
            // EditMode does not drive ordinary MonoBehaviour lifecycle callbacks.
            Call(_attack, "OnDisable");
            Call(_warning, "OnDisable");
            Assert.That(_warning.IsPlaying, Is.False);
        }

        [TestCase("SO_Knight", false)]
        [TestCase("SO_Mold", false)]
        [TestCase("SO_Knight", true)]
        [TestCase("SO_Mold", true)]
        public void OriginalMarkIsRetainedAndBodyAppearanceIsNotOverwritten(string resource, bool reduced)
        {
            Create(resource);
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, reduced ? 1 : 0);
            _entity.VisualRenderer.color = new Color(.2f, .5f, .8f, .7f);
            var material = _entity.VisualRenderer.sharedMaterial;
            var color = _entity.VisualRenderer.color;
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Set(_warning, "_elapsed", 20f);
            Call(_warning, "LateUpdate");
            var marker = _entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "AttackTelegraphAura");
            Assert.That(marker.sprite.name, Is.EqualTo("EnemyAttackTelegraphAura"));
            Assert.That(marker.sprite.rect.size, Is.EqualTo(new Vector2(18, 8)));
            Assert.That(marker.sprite.pixelsPerUnit, Is.EqualTo(24f));
            Assert.That(marker.color.a, Is.GreaterThanOrEqualTo(.17f));
            Assert.That(_entity.VisualRenderer.color, Is.EqualTo(color));
            Assert.That(_entity.VisualRenderer.sharedMaterial, Is.SameAs(material));
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "AttackTelegraphFlash").sprite, Is.SameAs(_entity.VisualRenderer.sprite));
            Call(_attack, "EnterActivePhase");
            Assert.That(marker.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ProjectileLineFollowsCurrentAimAndTheActualReleasedShot()
        {
            Create("SO_Mold");
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            var mark = _entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "AttackTelegraphAura");
            var line = _entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "ProjectileAimLine");
            Assert.That(mark.sprite.name, Is.EqualTo("EnemyAttackTelegraphAura"));
            Assert.That(line.gameObject.activeSelf, Is.True);
            _target.position += new Vector3(-4, 3, 0);
            Call(_attack, "EnterWindupPhase");
            Call(_warning, "LateUpdate");
            Assert.That(_attack.TryGetPreparingProjectileAim(out Vector2 origin, out Vector2 direction), Is.True);
            Assert.That(Vector2.Distance(line.transform.position, origin), Is.LessThan(.001f));
            Vector2 end = line.transform.TransformPoint(Vector3.right * line.size.x);
            Assert.That(Vector2.Distance(end, origin + direction * 3f), Is.LessThan(.002f));
            var originalOrigin = origin;
            Call(_attack, "EnterActivePhase");
            Assert.That(line.gameObject.activeSelf, Is.False);
            Assert.That(_attack.TryGetPreparingProjectileAim(out _, out _), Is.False);
            var shot = _owner.GetComponentsInChildren<EnemyProjectile>().Single();
            Assert.That(Vector2.Distance(shot.transform.position, originalOrigin), Is.LessThan(.001f));
            Assert.That(Vector2.Distance((Vector2)Get(shot, "_direction"), direction), Is.LessThan(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ProjectileLineRemainsVisibleInBothModesAndClearsWithCancellation(bool reduced)
        {
            Create("SO_Mold");
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, reduced ? 1 : 0);
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            var line = _entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "ProjectileAimLine");
            Assert.That(line.color.a, Is.GreaterThanOrEqualTo(.45f));
            Assert.That(line.size.y, Is.EqualTo(1f / 24).Within(.001f));
            _attack.SetFrozen(true);
            Assert.That(line.gameObject.activeSelf, Is.False);
            _attack.Initialize(_entity, _entity.Data);
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Count(x => x.name == "ProjectileAimLine"), Is.EqualTo(1));
            _attack.SetStunned(true);
            Assert.That(line.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void MeleeAndChargeDoNotCreateProjectileDirectionCues()
        {
            Create("SO_Knight");
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Assert.That(_attack.TryGetPreparingProjectileAim(out _, out _), Is.False);
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Any(x => x.name == "ProjectileAimLine"), Is.False);
            _attack.SetStunned(true);
            _attack.Initialize(_entity, _entity.Data);
            Assert.That(_attack.TryStartChargeAttack(_target), Is.True);
            Assert.That(_attack.TryGetPreparingProjectileAim(out _, out _), Is.False);
            Assert.That(_entity.GetComponentsInChildren<SpriteRenderer>(true).Any(x => x.name == "ProjectileAimLine"), Is.False);
        }

        [Test]
        public void ProjectileLineHandlesReflectedScaledParentsAndMissingTarget()
        {
            Create("SO_Mold");
            _entity.transform.localScale = new Vector3(-2f, 1.5f, 1);
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            var line = _entity.GetComponentsInChildren<SpriteRenderer>(true).Single(x => x.name == "ProjectileAimLine");
            Assert.That(_attack.TryGetPreparingProjectileAim(out Vector2 origin, out Vector2 direction), Is.True);
            Vector2 end = line.transform.TransformPoint(Vector3.right * line.size.x);
            Assert.That(Vector2.Distance(end, origin + direction * 3f), Is.LessThan(.002f));
            Object.DestroyImmediate(_target.gameObject);
            Call(_warning, "LateUpdate");
            Assert.That(line.gameObject.activeSelf, Is.False);
            Assert.That(_attack.TryGetPreparingProjectileAim(out _, out _), Is.False);
        }

        [Test]
        public void CoincidentTargetUsesTheProjectilesExistingFacingFallback()
        {
            Create("SO_Mold");
            Assert.That(_attack.TryStartAttack(_target), Is.True);
            Assert.That(_attack.TryGetPreparingProjectileAim(out Vector2 origin, out _), Is.True);
            _target.position = origin;
            Assert.That(_attack.TryGetPreparingProjectileAim(out _, out Vector2 direction), Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void RejectedInputDoesNotCreateWarning()
        {
            Create("SO_Knight");
            Assert.That(_attack.TryStartAttack(null), Is.False);
            Assert.That(_warning.IsPlaying, Is.False);
        }
    }
}
