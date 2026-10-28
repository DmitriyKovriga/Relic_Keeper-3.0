using System.Collections.Generic;
using NUnit.Framework;
using Scripts.Combat;
using Scripts.Visuals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class ReducedCombatEffectsTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private bool _had, _previous;
        [SetUp] public void Setup() { _had = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey); _previous = GameplayPresentationSettings.ReducedCombatEffects; GameplayPresentationSettings.SetReducedCombatEffects(false); }
        [TearDown] public void Cleanup()
        {
            foreach (var value in _objects) if (value is GameObject root && root != null && root.TryGetComponent<EnemyHitFlash>(out var flash))
                typeof(EnemyHitFlash).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(flash, null);
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear(); GameplayPresentationSettings.SetReducedCombatEffects(_previous);
            if (!_had) PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey); PlayerPrefs.Save();
        }
        [Test] public void SharedBudgetBoundsTenThousandRequests_AndReleasesAreIdempotent()
        {
            var budget = new CombatFlashBudget(); var owners = new object[100];
            for (int i = 0; i < owners.Length; i++) owners[i] = new object();
            for (int i = 0; i < 10000; i++) { budget.TryAcquire(owners[i % 100], 3); Assert.That(budget.Count, Is.LessThanOrEqualTo(3)); }
            Assert.That(budget.Count, Is.EqualTo(3)); Assert.That(budget.TryAcquire(owners[0], 3), Is.True);
            budget.Release(owners[0]); budget.Release(owners[0]); Assert.That(budget.Count, Is.EqualTo(2));
            Assert.That(budget.TryAcquire(owners[99], 3), Is.True);
        }
        [Test] public void ChoicePersistsWithoutChangingCameraOrAttackOpacityPreferences()
        {
            float camera = GameplayPresentationSettings.CameraImpulseStrength, opacity = GameplayPresentationSettings.PlayerAttackVfxOpacity;
            int notifications = 0; System.Action changed = () => notifications++;
            GameplayPresentationSettings.Changed += changed;
            try
            {
                GameplayPresentationSettings.SetReducedCombatEffects(true); GameplayPresentationSettings.SetReducedCombatEffects(true);
                Assert.That(notifications, Is.EqualTo(1)); Assert.That(PlayerPrefs.GetInt(GameplayPresentationSettings.ReducedCombatEffectsKey), Is.EqualTo(1));
                Assert.That(CombatEffectsPolicy.DeathFragmentCount(6), Is.EqualTo(2)); Assert.That(CombatEffectsPolicy.DeathFragmentCount(0), Is.Zero);
                Assert.That(CombatEffectsPolicy.AccentStrength, Is.EqualTo(.5f));
                Assert.That(GameplayPresentationSettings.CameraImpulseStrength, Is.EqualTo(camera)); Assert.That(GameplayPresentationSettings.PlayerAttackVfxOpacity, Is.EqualTo(opacity));
            }
            finally { GameplayPresentationSettings.Changed -= changed; }
        }
        [TestCase(false, 8)] [TestCase(true, 3)]
        public void CrowdCapsActualFlashesWithoutRejectingConfirmedHits_AndSwitchingClearsThem(bool reduced, int limit)
        {
            GameplayPresentationSettings.SetReducedCombatEffects(reduced);
            var material = new Material(Shader.Find("RelicKeeper/Sprites/World Color Lit")); _objects.Add(material);
            var flashes = new List<EnemyHitFlash>();
            for (int i = 0; i < 24; i++)
            {
                var root = new GameObject("Crowd feedback test"); _objects.Add(root);
                var source = root.AddComponent<SpriteRenderer>(); source.sharedMaterial = material;
                var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f); _objects.Add(sprite); source.sprite = sprite;
                var flash = root.AddComponent<EnemyHitFlash>(); flash.Initialize(source); flashes.Add(flash);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(EnemyHitFlash).GetMethod("OnDisable", flags).Invoke(flash, null);
                typeof(EnemyHitFlash).GetMethod("OnEnable", flags).Invoke(flash, null);
                Assert.That(CombatHitFeedback.Confirm(source, new DamageSnapshot(null) { Physical = 10 }, 10, false), Is.True);
                Assert.That(source.sharedMaterial, Is.SameAs(material));
            }
            Assert.That(flashes.FindAll(x => x.Amount > 0).Count, Is.EqualTo(limit));
            float expected = CombatPoseProfile.LoadDefault().NormalHitFlash * (reduced ? .5f : 1);
            Assert.That(flashes[0].Amount, Is.EqualTo(expected).Within(.0001f));
            if (reduced) GameplayPresentationSettings.SetReducedCombatEffects(false);
            else foreach (var flash in flashes) flash.Sample(Time.time + .2f);
            Assert.That(flashes.FindAll(x => x.Amount > 0).Count, Is.Zero);
            GameplayPresentationSettings.SetReducedCombatEffects(!reduced);
            foreach (var flash in flashes) Assert.That(flash.Amount, Is.Zero);
        }
    }
}
