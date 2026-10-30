using System.Reflection;
using NUnit.Framework;
using Scripts.StatusEffects;
using UnityEngine;

namespace RelicKeeper.Tests.EditMode
{
    public sealed class ShockPresentationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _host;
        private SpriteRenderer _body;
        private Sprite _sprite;
        private ShockVisualController _shock;
        private bool _hadReduced; private int _reduced;
        private SpriteRenderer Arc => _host.transform.Find("ShockArcs").GetComponent<SpriteRenderer>();
        private void Tick() => typeof(ShockVisualController).GetMethod("LateUpdate",Private).Invoke(_shock,null);
        private object Get(string field) => typeof(ShockVisualController).GetField(field,Private).GetValue(_shock);
        [SetUp] public void Setup()
        {
            _hadReduced = PlayerPrefs.HasKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
            _reduced = PlayerPrefs.GetInt(GameplayPresentationSettings.ReducedCombatEffectsKey);
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey,0);
            _host = new GameObject("Shock presentation"); _body = _host.AddComponent<SpriteRenderer>();
            _sprite = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,0),24);
            _body.sprite = _sprite; _shock = _host.AddComponent<ShockVisualController>();
        }
        [TearDown] public void Cleanup()
        {
            _shock.Stop(); Object.DestroyImmediate(_host); Object.DestroyImmediate(_sprite);
            if (_hadReduced) PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey,_reduced);
            else PlayerPrefs.DeleteKey(GameplayPresentationSettings.ReducedCombatEffectsKey);
        }
        [Test] public void BuildupIsDistinctFromActiveAndOnlyFullShockStartsTheActivationAccent()
        {
            _shock.SetBuildup(.4f); Assert.That(_shock.IsActive,Is.False); Assert.That(_shock.BuildupNormalized,Is.EqualTo(.4f));
            Assert.That((float)Get("_activationUntil"),Is.EqualTo(float.NegativeInfinity));
            _shock.Play(2); Assert.That(_shock.IsActive,Is.True); Assert.That(_shock.BuildupNormalized,Is.Zero);
            Assert.That((float)Get("_activationUntil"),Is.GreaterThan(Time.time));
        }
        [Test] public void RefreshKeepsOneArcLayerAndDoesNotReplayTheActivationAccent()
        {
            _shock.Play(2); var arc = Arc; float until = (float)Get("_activationUntil");
            for(int i=0;i<20;i++) _shock.Play(1);
            Assert.That(Arc,Is.SameAs(arc)); Assert.That((float)Get("_activationUntil"),Is.EqualTo(until));
            Assert.That((float)Get("_remainingSeconds"),Is.EqualTo(2)); Assert.That(_host.GetComponentsInChildren<Collider2D>(),Is.Empty);
        }
        [Test] public void ArcNeverWritesTheBodyTintMaterialOrHitFlash()
        {
            var tint = new Color(.2f,.5f,.9f,.6f); _body.color = tint; var material = _body.sharedMaterial;
            var block = new MaterialPropertyBlock(); block.SetFloat("_FxHitFlash",.8f); _body.SetPropertyBlock(block);
            _shock.SetBuildup(.5f); _shock.Play(2); Tick(); _shock.Stop();
            Assert.That(_body.color,Is.EqualTo(tint)); Assert.That(_body.sharedMaterial,Is.SameAs(material));
            _body.GetPropertyBlock(block); Assert.That(block.GetFloat("_FxHitFlash"),Is.EqualTo(.8f));
        }
        [Test] public void SpriteFlipSortingAndVisibilityTrackTheActualBody()
        {
            _shock.Play(2); _body.flipX = true; _body.sortingOrder = 25; Tick();
            Assert.That(Arc.sprite,Is.SameAs(_body.sprite)); Assert.That(Arc.flipX,Is.True); Assert.That(Arc.sortingOrder,Is.EqualTo(28));
            _body.enabled = false; Tick(); Assert.That(Arc.enabled,Is.False);
            _body.enabled = true; Tick(); Assert.That(Arc.enabled,Is.True);
        }
        [Test] public void StopDisableAndDeadCorpseClearBothPresentationStates()
        {
            _shock.SetBuildup(.5f); _shock.Stop(); Assert.That(_shock.BuildupNormalized,Is.Zero); Assert.That(Arc.enabled,Is.False);
            _shock.Play(2); typeof(ShockVisualController).GetMethod("OnDisable",Private).Invoke(_shock,null);
            Assert.That(_shock.IsActive,Is.False); Assert.That(Arc.enabled,Is.False);
            var health = _host.AddComponent<Scripts.Enemies.EnemyHealth>();
            typeof(Scripts.Enemies.EnemyHealth).GetField("_isDead",Private).SetValue(health,true);
            _shock.SetBuildup(.3f); Assert.That(_shock.BuildupNormalized,Is.Zero); Assert.That(Arc.enabled,Is.False);
        }
        [Test] public void ExpirationRemovesActiveArcsAndReducedModeUsesLessStrength()
        {
            _shock.Play(2); var block = new MaterialPropertyBlock(); Arc.GetPropertyBlock(block); float strength = block.GetFloat("_ArcStrength");
            PlayerPrefs.SetInt(GameplayPresentationSettings.ReducedCombatEffectsKey,1); Tick(); Arc.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_ArcStrength"),Is.LessThan(strength)); Assert.That(block.GetFloat("_ArcReduced"),Is.EqualTo(1));
            typeof(ShockVisualController).GetField("_remainingSeconds",Private).SetValue(_shock,0);
            typeof(ShockVisualController).GetMethod("Update",Private).Invoke(_shock,null);
            Assert.That(_shock.IsActive,Is.False); Assert.That(Arc.enabled,Is.False);
        }
        [Test] public void MaterialIsIncludedThroughResourcesAndShaderIsSupported()
        {
            var material = Resources.Load<Material>("Visuals/ShockArc"); Assert.That(material,Is.Not.Null);
            Assert.That(material.shader.name,Is.EqualTo("RelicKeeper/Status/Shock Arc")); Assert.That(material.shader.isSupported,Is.True);
        }
        [Test] public void LightningHasShortBurstsQuietGapsAndHeldShapesInsteadOfFlow()
        {
            int lit = 0, quiet = 0, shapeChanges = 0; float previous = float.NaN;
            for(int i=0;i<2000;i++)
            {
                var sample = ShockVisualController.SampleDischarge(i*.001f,false);
                if(sample.y>.05f) { lit++; if(sample.x!=previous) shapeChanges++; previous=sample.x; }
                else quiet++;
                Assert.That(sample.y,Is.InRange(0,1));
            }
            Assert.That(lit,Is.InRange(200,900)); Assert.That(quiet,Is.GreaterThan(lit)); Assert.That(shapeChanges,Is.GreaterThan(8));
            Assert.That(ShockVisualController.SampleDischarge(.012f,false).x,
                Is.EqualTo(ShockVisualController.SampleDischarge(.02f,false).x),"A snap holds its topology instead of scrolling across the body.");
        }
        [Test] public void ReducedLightningHasFewerWiderSnaps()
        {
            int normalStarts = 0, reducedStarts = 0; bool wasNormal = false, wasReduced = false;
            for(int i=0;i<3000;i++)
            {
                bool normal = ShockVisualController.SampleDischarge(i*.001f,false).y>.05f;
                bool reduced = ShockVisualController.SampleDischarge(i*.001f,true).y>.05f;
                if(normal && !wasNormal) normalStarts++; if(reduced && !wasReduced) reducedStarts++;
                wasNormal=normal; wasReduced=reduced;
            }
            Assert.That(reducedStarts,Is.LessThan(normalStarts/2));
        }
    }
}
