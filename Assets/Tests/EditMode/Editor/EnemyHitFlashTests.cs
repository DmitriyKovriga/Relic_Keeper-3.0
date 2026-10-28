using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Enemies;
using Scripts.Visuals;
using Scripts.Visuals.SpriteFX;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class EnemyHitFlashTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private CombatPoseProfile _profile;
        private bool _hadReduced, _reduced;
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [SetUp] public void Setup()
        {
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = GameplayPresentationSettings.ReducedCombatEffects;
            GameplayPresentationSettings.SetReducedCombatEffects(false);
            _profile = Keep(Object.Instantiate(CombatPoseProfile.LoadDefault()));
        }
        [TearDown] public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            GameplayPresentationSettings.SetReducedCombatEffects(_reduced);
            if (!_hadReduced) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey); PlayerPrefs.Save();
        }

        private EnemyHitFlash Target(out SpriteRenderer source, out SpriteFxController fx)
        {
            var root = Keep(new GameObject("Confirmed-hit flash test"));
            source = root.AddComponent<SpriteRenderer>();
            source.sharedMaterial = Keep(new Material(Shader.Find("RelicKeeper/Sprites/World Color Lit")));
            source.sprite = Keep(Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, 24));
            var flash = root.AddComponent<EnemyHitFlash>(); Set(flash, "_profile", _profile); flash.Initialize(source);
            fx = source.GetComponent<SpriteFxController>(); return flash;
        }

        private static DamageSnapshot Hit(float multiplier = 1, bool critical = false) => new DamageSnapshot(null)
        {
            Physical = 10, IsCrit = critical,
            Impact = new AttackImpactContact(new AttackImpactContext(null, 0, enableHitStop: false), Vector2.left, Vector2.right, multiplier)
        };

        [TestCase(.5f, false, .18f)] [TestCase(1f, false, .28f)] [TestCase(2.5f, false, .4f)]
        [TestCase(.5f, true, .26f)] [TestCase(1f, true, .36f)] [TestCase(2.5f, true, .48f)]
        public void ConfirmedHitsUseAuthoredWeightAndCriticalBonus(float multiplier, bool critical, float expected)
        {
            var flash = Target(out var source, out _);
            Assert.That(CombatHitFeedback.Confirm(source, Hit(multiplier, critical), 10, false), Is.True);
            Assert.That(flash.Amount, Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void HugeDamageDoesNotIncreaseFlashAndMissingMetadataUsesNormalWeight()
        {
            var flash = Target(out var source, out _); var hit = Hit(); hit.Impact = null;
            CombatHitFeedback.Confirm(source, hit, 10000000, false);
            Assert.That(flash.Amount, Is.EqualTo(_profile.NormalHitFlash));
        }

        [TestCase(0, true)] [TestCase(10, false)]
        public void ZeroDamageAndDamageOverTimeCannotTriggerAFlash(float damage, bool direct)
        {
            var flash = Target(out var source, out _); var hit = Hit(); hit.IsDirectHit = direct;
            Assert.That(CombatHitFeedback.Confirm(source, hit, damage, false), Is.False);
            Assert.That(flash.Amount, Is.Zero);
        }

        [Test]
        public void FlashPreservesMaterialRendererTintAndAllOtherEffectChannels()
        {
            var flash = Target(out var source, out var fx); var material = source.sharedMaterial;
            source.color = new Color(.62f, .92f, 1, .8f); Color color = source.color;
            fx.Flash(Color.yellow, .55f); fx.TemporaryTint(Color.green, 2); fx.SetEmission(Color.cyan, .4f);
            fx.SetDissolve(.3f); fx.SetFade(.7f);
            var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block);
            block.SetFloat("_ForeignProperty", 42); block.SetFloat("_RKGradeGroup", 2); block.SetFloat("_RKPlayerOverlapShade", .2f);
            source.SetPropertyBlock(block);
            CombatHitFeedback.Confirm(source, Hit(), 10, false);
            flash.Sample(Time.time + .03f); source.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxHitFlash"), Is.GreaterThan(0));
            Assert.That(block.GetFloat("_FxRuntimeFlash"), Is.EqualTo(1));
            Assert.That(block.GetColor("_FxRuntimeFlashColor"), Is.EqualTo(Color.yellow));
            Assert.That(block.GetColor("_FxRuntimeTint"), Is.EqualTo(Color.green));
            Assert.That(block.GetFloat("_FxRuntimeEmission"), Is.EqualTo(.4f));
            Assert.That(block.GetFloat("_FxRuntimeDissolve"), Is.EqualTo(.3f));
            Assert.That(block.GetFloat("_FxRuntimeFade"), Is.EqualTo(.7f));
            Assert.That(block.GetFloat("_ForeignProperty"), Is.EqualTo(42));
            Assert.That(block.GetFloat("_RKGradeGroup"), Is.EqualTo(2));
            Assert.That(block.GetFloat("_RKPlayerOverlapShade"), Is.EqualTo(.2f));
            Assert.That(source.sharedMaterial, Is.SameAs(material)); Assert.That(source.color, Is.EqualTo(color));
            flash.Sample(Time.time + 1); source.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxHitFlash"), Is.Zero);
            Assert.That(block.GetFloat("_FxRuntimeFlash"), Is.EqualTo(1));
            Assert.That(block.GetFloat("_FxRuntimeEmission"), Is.EqualTo(.4f));
        }

        [Test]
        public void RecoilDisplayCopiesTheCurrentFlashAndItsMaterialWithoutGettingAnotherController()
        {
            var flash = Target(out var source, out _); var recoil = source.gameObject.AddComponent<EnemyHitRecoil>();
            Set(recoil, "_profile", _profile); recoil.Initialize(source); flash.Initialize(source);
            CombatHitFeedback.Confirm(source, Hit(), 10, false); recoil.Sample(Time.time + .01f);
            Assert.That(source.forceRenderingOff, Is.True); Assert.That(recoil.DisplayRenderer.enabled, Is.True);
            flash.Sample(Time.time + .02f); recoil.Sample(Time.time + .02f);
            var block = new MaterialPropertyBlock(); recoil.DisplayRenderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxHitFlash"), Is.EqualTo(flash.Amount).Within(.00001f));
            Assert.That(recoil.DisplayRenderer.sharedMaterial, Is.SameAs(source.sharedMaterial));
            Assert.That(recoil.DisplayRenderer.GetComponent<SpriteFxController>(), Is.Null);
            flash.Sample(Time.time + .1f); recoil.Sample(Time.time + .1f); recoil.DisplayRenderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxHitFlash"), Is.Zero);
        }

        [Test]
        public void FrozenTargetsCanFlashWithoutChangingTheirTintOrFreezeState()
        {
            var flash = Target(out var source, out _);
            var freeze = source.gameObject.AddComponent<EnemyFreezeController>();
            Assert.That(freeze.TryApplyFreeze(2), Is.True); Color frozen = source.color;
            CombatHitFeedback.Confirm(source, Hit(), 10, false);
            Assert.That(flash.Amount, Is.GreaterThan(0)); Assert.That(freeze.IsFrozen, Is.True);
            Assert.That(source.color, Is.EqualTo(frozen));
        }

        [TestCase("hidden")] [TestCase("forced-hidden")] [TestCase("disabled")] [TestCase("profile")]
        public void HiddenOrDisabledPresentationClearsOnlyItsOwnFlash(string mode)
        {
            var flash = Target(out var source, out var fx); fx.SetEmission(Color.green, .6f);
            CombatHitFeedback.Confirm(source, Hit(), 10, false); Assert.That(flash.Amount, Is.GreaterThan(0));
            if (mode == "hidden") source.enabled = false;
            if (mode == "forced-hidden") source.forceRenderingOff = true;
            if (mode == "disabled") flash.enabled = false;
            if (mode == "profile") _profile.EnableEnemyHitFlash = false;
            flash.Sample(Time.time + .01f);
            Assert.That(flash.Amount, Is.Zero);
            var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FxRuntimeEmission"), Is.EqualTo(.6f));
        }

        [Test]
        public void LethalHitClearsExistingFlashAndReuseStartsCleanWithoutAnOldCooldown()
        {
            var flash = Target(out var source, out _);
            CombatHitFeedback.Confirm(source, Hit(), 10, false);
            CombatHitFeedback.Confirm(source, Hit(), 10, true); Assert.That(flash.Amount, Is.Zero);
            flash.Initialize(source); CombatHitFeedback.Confirm(source, Hit(), 10, false);
            Assert.That(flash.Amount, Is.GreaterThan(0));
        }

        [Test]
        public void UnknownMaterialsAreKeptAndNeverReplacedByAnUnlitFlashMaterial()
        {
            var flash = Target(out var source, out _);
            var stock = Keep(new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")));
            source.sharedMaterial = stock; CombatHitFeedback.Confirm(source, Hit(), 10, false);
            Assert.That(flash.Amount, Is.Zero); Assert.That(source.sharedMaterial, Is.SameAs(stock));
        }

        [Test]
        public void PulseDecaysWithoutStackingAndCooldownSurvivesTheVisibleEnd()
        {
            var pulse = new HitFlashPulse(); Assert.That(pulse.Trigger(0, .4f, .08f, .18f), Is.True);
            pulse.Sample(.04f, out var half); Assert.That(half, Is.EqualTo(.1f).Within(.00001f));
            Assert.That(pulse.Trigger(.04f, .5f, .1f, .18f), Is.False);
            Assert.That(pulse.Sample(.09f, out _), Is.False);
            Assert.That(pulse.Trigger(.1f, .4f, .08f, .18f), Is.False);
            Assert.That(pulse.Trigger(.181f, .4f, .08f, .18f), Is.True);
            pulse.Sample(.181f, out var peak); Assert.That(peak, Is.EqualTo(.4f));
        }

        [Test]
        public void RapidHitsHaveQuietFramesEvenWithUnsafeInspectorValues()
        {
            var pulse = new HitFlashPulse(); int triggers = 0, quiet = 0;
            for (int i = 0; i < 1000; i++)
            {
                float now = i * .001f;
                if (pulse.Trigger(now, 100, 100, 0)) triggers++;
                pulse.Sample(now, out float strength); if (strength == 0) quiet++;
                Assert.That(strength, Is.InRange(0, .5f));
            }
            Assert.That(triggers, Is.InRange(5, 6)); Assert.That(quiet, Is.GreaterThan(200));
            pulse.Clear(); Assert.That(pulse.Trigger(0, .2f, .05f, .18f), Is.True);
        }

        [Test]
        public void ScaledClockHoldsThePulseDuringHitStopOrPause()
        {
            var pulse = new HitFlashPulse(); pulse.Trigger(0, .4f, .075f, .18f);
            pulse.Sample(.02f, out float before);
            for (int frame = 0; frame < 100; frame++) { pulse.Sample(.02f, out float held); Assert.That(held, Is.EqualTo(before)); }
            Assert.That(pulse.Sample(.08f, out _), Is.False);
        }
    }
}
