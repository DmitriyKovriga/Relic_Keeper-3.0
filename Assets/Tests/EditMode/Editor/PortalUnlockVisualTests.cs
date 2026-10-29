using NUnit.Framework;
using Scripts.Visuals;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class PortalUnlockVisualTests
    {
        private GameObject _host;
        private Sprite _sprite;
        private PortalUnlockVisual _visual;
        private bool _hadReduced; private int _reduced;
        [SetUp] public void Setup()
        {
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = PlayerPrefs.GetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, 0);
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, 0);
            _host = new GameObject("Portal visual test");
            var renderer = _host.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 24);
            renderer.sprite = _sprite; _host.transform.localScale = new Vector3(2, 3, 1);
            _host.AddComponent<BoxCollider2D>().isTrigger = true;
            _visual = new PortalUnlockVisual(renderer);
        }
        [TearDown] public void Cleanup()
        {
            _visual.Dispose(); Object.DestroyImmediate(_host); Object.DestroyImmediate(_sprite);
            if (_hadReduced) PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, _reduced);
            else PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
        }
        [Test] public void EchoReusesExistingArtWithoutChangingPortalTransformColliderOrMaterial()
        {
            var source = _host.GetComponent<SpriteRenderer>(); var material = source.sharedMaterial;
            var collider = _host.GetComponent<BoxCollider2D>(); Vector2 size = collider.size;
            _visual.Tick(.2f);
            var echo = _host.transform.Find("PortalUnlockEcho").GetComponent<SpriteRenderer>();
            Assert.That(echo.sprite, Is.SameAs(source.sprite)); Assert.That(echo.transform.localScale.x, Is.GreaterThan(1));
            Assert.That(_host.transform.localScale, Is.EqualTo(new Vector3(2, 3, 1)));
            Assert.That(collider.size, Is.EqualTo(size)); Assert.That(collider.enabled, Is.True);
            Assert.That(echo.GetComponent<Collider2D>(), Is.Null); Assert.That(source.sharedMaterial, Is.SameAs(material));
        }
        [Test] public void TheEchoFollowsAnimationSpriteChangesThenCleansItselfUp()
        {
            _host.GetComponent<SpriteRenderer>().sprite = null; _visual.Tick(.1f);
            Assert.That(_host.transform.Find("PortalUnlockEcho").GetComponent<SpriteRenderer>().enabled, Is.False);
            _visual.Tick(1); Assert.That(_visual.Finished, Is.True);
            Assert.That(_host.transform.Find("PortalUnlockEcho"), Is.Null);
        }
        [Test] public void DisableCancelsTheEchoInsteadOfReplayingItOnEnable()
        {
            _host.SetActive(false); _visual.Tick(.1f); _host.SetActive(true);
            Assert.That(_visual.Finished, Is.True); Assert.That(_host.transform.Find("PortalUnlockEcho"), Is.Null);
        }
        [Test] public void ReducedEffectsUseASmallerQuieterEcho()
        {
            _visual.Dispose(); PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, 1);
            _visual = new PortalUnlockVisual(_host.GetComponent<SpriteRenderer>()); _visual.Tick(.2f);
            var echo = _host.transform.Find("PortalUnlockEcho").GetComponent<SpriteRenderer>();
            Assert.That(echo.transform.localScale.x, Is.LessThanOrEqualTo(1.22f)); Assert.That(echo.color.a, Is.LessThan(.3f));
            _visual.Tick(.21f); Assert.That(_visual.Finished, Is.True);
        }
    }
}
