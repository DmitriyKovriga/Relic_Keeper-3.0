using System.Reflection;
using NUnit.Framework;
using Scripts.StatusEffects;
using Scripts.Visuals.Atmosphere;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class IgniteBurnVisualTests
    {
        private GameObject _host;
        private SpriteRenderer _body;
        private Sprite _sprite;
        private IgniteVisualController _visual;
        private bool _hadReduced;
        private int _reduced;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private void Tick() => typeof(IgniteVisualController).GetMethod("LateUpdate", Private).Invoke(_visual, null);
        private Transform Root => _host.transform.Find("IgniteFlames");
        private SpriteRenderer Heat => Root.Find("Ignite body heat").GetComponent<SpriteRenderer>();
        private LocalLight2DSource Light => Root.GetComponentInChildren<LocalLight2DSource>(true);

        [SetUp] public void Setup()
        {
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = PlayerPrefs.GetInt(GameplayPresentationSettings.ReducedCombatEffectsKey);
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, 0);
            _host = new GameObject("Burning test actor"); _body = _host.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(.5f,0), 1);
            _body.sprite = _sprite; _visual = _host.AddComponent<IgniteVisualController>();
        }
        [TearDown] public void Cleanup()
        {
            _visual.SetBurning(false); Object.DestroyImmediate(_host); Object.DestroyImmediate(_sprite);
            if (_hadReduced) PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, _reduced);
            else PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
        }
        [Test] public void BurningPreservesBodyMaterialTintAndExistingHitFlashProperties()
        {
            var material = _body.sharedMaterial; var tint = new Color(.2f,.5f,.8f,.7f); _body.color = tint;
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_FxHitFlash", .7f); _body.SetPropertyBlock(properties);
            _visual.SetBurning(true); Tick();
            Assert.That(_body.sharedMaterial, Is.SameAs(material)); Assert.That(_body.color, Is.EqualTo(tint));
            _body.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_FxHitFlash"), Is.EqualTo(.7f));
            _visual.SetBurning(false);
            Assert.That(_body.color, Is.EqualTo(tint)); Assert.That(_body.sharedMaterial, Is.SameAs(material));
        }
        [Test] public void OverlayTracksAnimationFlipSortingAndVisibility()
        {
            _visual.SetBurning(true); _body.flipX = true; _body.sortingOrder = 42; Tick();
            Assert.That(Heat.sprite, Is.SameAs(_body.sprite)); Assert.That(Heat.flipX, Is.True);
            Assert.That(Heat.sortingOrder, Is.EqualTo(43));
            Assert.That(Root.Find("Ignite rising fire").GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(41),
                "The body must cover the large flame envelope instead of being hidden by ground fire.");
            _body.sprite = null; Tick(); Assert.That(Heat.enabled, Is.False); Assert.That(Light.isActiveAndEnabled, Is.False);
            _body.sprite = _sprite; _body.enabled = false; Tick(); Assert.That(Heat.enabled, Is.False);
            _body.enabled = true; Tick(); Assert.That(Heat.enabled, Is.True); Assert.That(Light.isActiveAndEnabled, Is.True);
        }
        [Test] public void RefreshAndStopReuseOneVisualAndOneLightWithoutExtraColliders()
        {
            _visual.SetBurning(true); var root = Root; int count = root.GetComponentsInChildren<Renderer>(true).Length;
            for (int i=0;i<20;i++) _visual.SetBurning(true);
            Assert.That(Root, Is.SameAs(root)); Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(count));
            Assert.That(root.GetComponentsInChildren<LocalLight2DSource>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            _visual.SetBurning(false); Assert.That(root.gameObject.activeSelf, Is.False); Assert.That(Light.Light.intensity, Is.Zero);
            _visual.SetBurning(true); Assert.That(Root, Is.SameAs(root));
        }
        [Test] public void ReducedEffectsLowersFlameHeightEmissionAndLightStrength()
        {
            _visual.SetBurning(true); var flame = Root.Find("Ignite rising fire"); float height = flame.localScale.y;
            var properties = new MaterialPropertyBlock(); Heat.GetPropertyBlock(properties); float strength = properties.GetFloat("_BurnStrength");
            float lightStrength = (float)typeof(LocalLight2DSource).GetField("_strength", Private).GetValue(Light);
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey, 1); Tick();
            Heat.GetPropertyBlock(properties);
            Assert.That(flame.localScale.y, Is.LessThan(height)); Assert.That(properties.GetFloat("_BurnStrength"), Is.LessThan(strength));
            Assert.That((float)typeof(LocalLight2DSource).GetField("_strength", Private).GetValue(Light), Is.LessThan(lightStrength));
        }
        [Test] public void BurnLightAndGlowRemainWarmEvenWithAColdEnvironmentTint()
        {
            var profile = ScriptableObject.CreateInstance<AtmosphereProfileSO>();
            try
            {
                profile.localLightTint = Color.blue; _visual.SetBurning(true);
                Assert.That(Light.SourceTint(profile).r, Is.GreaterThan(.9f)); Assert.That(Light.SourceTint(profile).b, Is.LessThan(.2f));
                Assert.That(Light.HasFlameMotion, Is.True); Assert.That(Light.Glow.enabled, Is.True);
            }
            finally { Object.DestroyImmediate(profile); }
        }
        [Test] public void ShockDoesNotCacheOrRestoreTheOwnedFireOrGlowRenderers()
        {
            _visual.SetBurning(true); var shock = _host.AddComponent<ShockVisualController>(); shock.Play(3);
            var heatTint = new Color(1,.4f,.1f,.6f); Heat.color = heatTint; shock.Stop();
            Assert.That(Heat.color, Is.EqualTo(heatTint));
            _visual.SetBurning(false); Assert.That(_body.color, Is.EqualTo(Color.white));
        }
        [Test] public void FireMaterialIsBuildIncludedAndUsesTheSupportedIgniteShader()
        {
            var material = Resources.Load<Material>("Visuals/IgniteBurn");
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Is.EqualTo("RelicKeeper/Status/Ignite Burn"));
            Assert.That(material.shader.isSupported, Is.True);
        }
        [Test] public void DeadOwnerStopsEmissionEvenWhenItsCorpseRemains()
        {
            var health = _host.AddComponent<Scripts.Enemies.EnemyHealth>(); _visual.SetBurning(true);
            typeof(Scripts.Enemies.EnemyHealth).GetField("_isDead", Private).SetValue(health, true); Tick();
            Assert.That(_visual.IsBurning, Is.False); Assert.That(Root.gameObject.activeSelf, Is.False);
            Assert.That(Light.Light.intensity, Is.Zero);
            _visual.SetBurning(true);
            Assert.That(_visual.IsBurning, Is.False, "Remaining ailment stacks must not relight a corpse.");
        }
        [Test] public void FireSamplesTheCurrentAnimatedSilhouetteWithItsActualAtlasCoordinates()
        {
            var next = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), new Vector2(.15f,.8f), 24);
            try
            {
                _visual.SetBurning(true); _body.sprite = next; _body.flipX = true; Tick();
                var fire = Root.Find("Ignite rising fire").GetComponent<SpriteRenderer>();
                var properties = new MaterialPropertyBlock(); fire.GetPropertyBlock(properties);
                Assert.That(properties.GetTexture("_BurnMaskTex"), Is.SameAs(next.texture));
                Assert.That(properties.GetVector("_BurnArea").z, Is.LessThan(0), "Mirror the mask inside the actor-centered fire envelope.");
                var x = properties.GetVector("_MaskUVX"); var y = properties.GetVector("_MaskUVY");
                var area = properties.GetVector("_BurnArea");
                var vertices = next.vertices; var uv = next.uv;
                for (int i=0;i<vertices.Length;i++)
                {
                    Assert.That(vertices[i].x*x.x + vertices[i].y*x.y + x.z, Is.EqualTo(uv[i].x).Within(.00001f));
                    Assert.That(vertices[i].x*y.x + vertices[i].y*y.y + y.z, Is.EqualTo(uv[i].y).Within(.00001f));
                    float quadX = (vertices[i].x - area.x) / area.z - .5f;
                    Assert.That(fire.transform.TransformPoint(new Vector3(quadX,0,0)).x,
                        Is.EqualTo(_body.transform.TransformPoint(new Vector3(-vertices[i].x,0,0)).x).Within(.00001f),
                        "The mask must stay on the flipped actor even with an off-center pivot.");
                }
                Assert.That(fire.transform.localPosition.y, Is.GreaterThan(next.bounds.min.y), "No flat emission base on the floor.");
            }
            finally { Object.DestroyImmediate(next); }
        }
        [Test] public void SpriteFrameChangesBlendEmissionInsteadOfSwitchingSitesInstantly()
        {
            var next = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.1f,.8f),24);
            try
            {
                _visual.SetBurning(true); _body.sprite = next; Tick();
                var fire = Root.Find("Ignite rising fire").GetComponent<SpriteRenderer>(); var block = new MaterialPropertyBlock();
                fire.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_MaskBlend"), Is.Zero);
                Assert.That(block.GetVector("_PreviousMaskRect").x, Is.EqualTo(_sprite.bounds.min.x));
                typeof(IgniteVisualController).GetField("_maskChangedAt",Private).SetValue(_visual,Time.time-.2f); Tick();
                fire.GetPropertyBlock(block); Assert.That(block.GetFloat("_MaskBlend"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(next); }
        }
        [Test] public void DefaultBreathingIsIdentitySoExistingTorchesKeepTheirMotion()
        {
            for(int i=0;i<100;i++) Assert.That(LocalLight2DSource.SampleBreathing(i*.1f,17,0), Is.EqualTo(Vector2.one));
        }
        [Test] public void BreathingVariesSmoothlyWithoutFlashingOffAndReducedDepthIsQuieter()
        {
            float min = float.MaxValue, max = float.MinValue, previous = LocalLight2DSource.SampleBreathing(0,3,.45f).x;
            for(int i=1;i<=600;i++)
            {
                float t = i/60f; var full = LocalLight2DSource.SampleBreathing(t,3,.45f);
                var reduced = LocalLight2DSource.SampleBreathing(t,3,.225f);
                Assert.That(full.x, Is.InRange(.55f,1.45f)); Assert.That(Mathf.Abs(full.x-previous), Is.LessThan(.024f));
                Assert.That(Mathf.Abs(reduced.x-1), Is.LessThanOrEqualTo(Mathf.Abs(full.x-1)+.000001f));
                Assert.That(full.y, Is.InRange(.93f,1.07f)); previous = full.x; min = Mathf.Min(min,full.x); max = Mathf.Max(max,full.x);
            }
            Assert.That(max-min, Is.GreaterThan(.5f), "Ignite's breath must be visibly stronger than subtle torch flutter.");
        }
    }
}
